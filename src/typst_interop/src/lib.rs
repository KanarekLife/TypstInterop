use chrono::{Datelike, Timelike};
use std::collections::HashMap;
use std::sync::LazyLock;
use typst::diag::{FileError, FileResult, Severity, SourceDiagnostic, Warned};
use typst::ecow::{EcoString, EcoVec};
use typst::foundations::{Bytes, Datetime, Dict, Value};
use typst::syntax::package::PackageSpec;
use typst::syntax::{FileId, RootedPath, Source, VirtualPath, VirtualRoot};
use typst::text::{Font, FontBook, FontInfo};
use typst::utils::{LazyHash, Scalar};
use typst::{Feature, Features, Library, LibraryExt, World, WorldExt};
use typst_html::{HtmlDocument, HtmlOptions};
use typst_kit::downloader::SystemDownloader;
use typst_kit::fonts::{self, FontPath, FontStore};
use typst_kit::packages::{FsPackages, SystemPackages, UniversePackages};
use typst_layout::PagedDocument;
use typst_pdf::{PdfOptions, PdfStandard, PdfStandards, Timestamp};
use typst_render::RenderOptions;

/// Maximum age (in compilations) a memoized entry may survive without being
/// touched before comemo evicts it. Mirrors the value used by the Typst CLI.
const MAX_COMEMO_AGE: usize = 5;

/// The embedded fonts shipped with Typst, loaded and cached once.
///
/// Embedded fonts are already in memory (compiled into the binary), so caching
/// the decoded [`Font`] objects is cheap.
static EMBEDDED_FONTS: LazyLock<Vec<(Font, FontInfo)>> =
    LazyLock::new(|| fonts::embedded().collect());

/// Metadata for all discoverable system fonts, scanned and cached once.
///
/// Only the font *metadata* (path, collection index, [`FontInfo`]) is cached;
/// the actual font *data* is loaded lazily by the [`FontStore`] on first glyph
/// access. This avoids reading/decoding every system font file up front (which
/// can be hundreds of files and a large amount of memory) just to compile a
/// document that uses one or two fonts.
///
/// `FontPath` is not `Clone`, so we cache the cloneable parts and rebuild the
/// `FontPath` on demand in [`extend_with_system_fonts`].
static SYSTEM_FONTS: LazyLock<Vec<(std::path::PathBuf, u32, FontInfo)>> = LazyLock::new(|| {
    fonts::system()
        .map(|(path, info)| (path.path, path.index, info))
        .collect()
});

/// Extends `store` with lazily-loaded system fonts from the [`SYSTEM_FONTS`]
/// metadata cache.
fn extend_with_system_fonts(store: &mut FontStore) {
    store.extend(SYSTEM_FONTS.iter().map(|(path, index, info)| {
        (
            FontPath {
                path: path.clone(),
                index: *index,
            },
            info.clone(),
        )
    }));
}

/// Builds a fresh [`FontStore`] for the requested font source selection.
///
/// `fonts_source`: 0 = All, 1 = DefaultOnly (embedded), 2 = SystemOnly,
/// 3 = ProvidedOnly, 4 = None.
fn build_font_store(fonts_source: u8) -> FontStore {
    let mut store = FontStore::new();
    match fonts_source {
        1 => store.extend(EMBEDDED_FONTS.iter().cloned()),
        2 => extend_with_system_fonts(&mut store),
        3 | 4 => {}
        _ => {
            // All (0) and any unknown value fall back to embedded + system.
            store.extend(EMBEDDED_FONTS.iter().cloned());
            extend_with_system_fonts(&mut store);
        }
    }
    store
}

/// Builds the standard library, enabling the HTML feature so that HTML export
/// works without bailing out during compilation.
fn build_library(inputs: Dict) -> Library {
    Library::builder()
        .with_inputs(inputs)
        .with_features(Features::from_iter([Feature::Html]))
        .build()
}

/// Builds a [`SystemPackages`] handle, defaulting to the standard system
/// directories when no explicit cache/data paths are provided.
fn build_package_storage(
    cache: Option<std::path::PathBuf>,
    data: Option<std::path::PathBuf>,
) -> SystemPackages {
    let downloader = SystemDownloader::new("typst-interop");
    let universe = UniversePackages::new(downloader);
    let data = data.map(FsPackages::new).or_else(FsPackages::system_data);
    let cache = cache.map(FsPackages::new).or_else(FsPackages::system_cache);
    SystemPackages::from_parts(data, cache, universe)
}

/// Interns a [`FileId`] for the given root and virtual path.
///
/// Returns `None` if the path is not a valid virtual path, allowing callers at
/// the FFI boundary to skip invalid input instead of panicking.
fn intern_path(root: VirtualRoot, path: &str) -> Option<FileId> {
    VirtualPath::new(path)
        .ok()
        .map(|vpath| FileId::new(RootedPath::new(root, vpath)))
}

pub struct VfsWorld {
    library: LazyHash<Library>,
    /// Set when `inputs` change so the `Library` is rebuilt lazily (once)
    /// before the next compile instead of on every `set_input` call.
    ///
    /// Rebuilding the whole standard `Library` on every input insertion is
    /// expensive (full std-scope construction plus a clone of the growing
    /// inputs `Dict`), which is O(n^2) for n inputs. Deferring the rebuild to
    /// compile time collapses that to a single build per compile.
    library_dirty: bool,
    font_store: FontStore,
    /// Whether `font_store` currently reflects exactly `fonts_source` with no
    /// user-added fonts. When true, `reset()` can keep the existing store
    /// instead of rebuilding it (which re-clones every system font and rebuilds
    /// the `FontBook` on every compile).
    font_store_pristine: bool,
    sources: HashMap<FileId, Source>,
    files: HashMap<FileId, Bytes>,
    main_id: FileId,
    inputs: Dict,
    package_storage: SystemPackages,
    cache_path: Option<std::path::PathBuf>,
    data_path: Option<std::path::PathBuf>,
    packages_source: u8,
    fonts_source: u8,
    /// Optional real filesystem root for the project namespace. When set,
    /// project files not provided in-memory are read from disk relative to it.
    root_path: Option<std::path::PathBuf>,
}

impl VfsWorld {
    pub fn new(
        main_path: &str,
        packages_source: u8,
        fonts_source: u8,
        cache_path: Option<std::path::PathBuf>,
        data_path: Option<std::path::PathBuf>,
        root_path: Option<std::path::PathBuf>,
    ) -> Self {
        let main_id = intern_path(VirtualRoot::Project, main_path)
            .unwrap_or_else(|| intern_path(VirtualRoot::Project, "main.typ").unwrap());

        let font_store = build_font_store(fonts_source);

        let inputs = Dict::new();
        let library = build_library(inputs.clone());

        let package_storage = build_package_storage(cache_path.clone(), data_path.clone());

        Self {
            library: LazyHash::new(library),
            library_dirty: false,
            font_store,
            font_store_pristine: true,
            sources: HashMap::new(),
            files: HashMap::new(),
            main_id,
            inputs,
            package_storage,
            cache_path,
            data_path,
            packages_source,
            fonts_source,
            root_path,
        }
    }

    pub fn reset(&mut self) {
        self.sources.clear();
        self.files.clear();

        // Only mark the library for rebuild if inputs were actually set; an
        // already-empty inputs dict means the current library is still valid.
        if !self.inputs.is_empty() {
            self.inputs = Dict::new();
            self.library_dirty = true;
        }

        // Rebuilding the font store re-clones every system font and rebuilds
        // the FontBook, so only do it if the store was mutated (e.g. by
        // add_font). When it still matches the configured source, keep it.
        if !self.font_store_pristine {
            self.font_store = build_font_store(self.fonts_source);
            self.font_store_pristine = true;
        }
    }

    /// Rebuilds the standard library if inputs changed since the last build.
    ///
    /// Must be called before reading [`World::library`] (i.e. before compiling)
    /// so that deferred input changes are reflected exactly once.
    fn prepare(&mut self) {
        if self.library_dirty {
            self.library = LazyHash::new(build_library(self.inputs.clone()));
            self.library_dirty = false;
        }
    }

    pub fn set_root(&mut self, root: Option<std::path::PathBuf>) {
        self.root_path = root;
    }

    pub fn set_source(&mut self, path: &str, text: &str) {
        if let Some(id) = intern_path(VirtualRoot::Project, path) {
            let source = Source::new(id, text.into());
            self.sources.insert(id, source);
        }
    }

    pub fn set_file(&mut self, path: &str, data: Vec<u8>) {
        if let Some(id) = intern_path(VirtualRoot::Project, path) {
            self.files.insert(id, Bytes::new(data));
        }
    }

    pub fn set_package_source(&mut self, spec: PackageSpec, path: &str, text: &str) {
        if let Some(id) = intern_path(VirtualRoot::Package(spec), path) {
            let source = Source::new(id, text.into());
            self.sources.insert(id, source);
        }
    }

    pub fn set_package_file(&mut self, spec: PackageSpec, path: &str, data: Vec<u8>) {
        if let Some(id) = intern_path(VirtualRoot::Package(spec), path) {
            self.files.insert(id, Bytes::new(data));
        }
    }

    pub fn set_package_paths(
        &mut self,
        cache: Option<std::path::PathBuf>,
        data: Option<std::path::PathBuf>,
    ) {
        self.cache_path = cache;
        self.data_path = data;
        self.package_storage =
            build_package_storage(self.cache_path.clone(), self.data_path.clone());
    }

    pub fn add_font(&mut self, data: Vec<u8>) {
        if self.fonts_source == 4 {
            return;
        }
        for font in Font::iter(Bytes::new(data)) {
            let info = font.info().clone();
            self.font_store.push((font, info));
            self.font_store_pristine = false;
        }
    }

    pub fn set_input(&mut self, key: &str, value: &str) {
        self.inputs.insert(
            EcoString::from(key).into(),
            Value::Str(EcoString::from(value).into()),
        );
        // Defer the (expensive) Library rebuild until compile time so that
        // setting N inputs costs one rebuild instead of N.
        self.library_dirty = true;
    }

    /// Lists the available font family names, sorted and deduplicated.
    pub fn font_families(&self) -> Vec<String> {
        let mut names: Vec<String> = self
            .font_store
            .book()
            .families()
            .map(|(name, _)| name.to_string())
            .collect();
        names.sort();
        names.dedup();
        names
    }

    fn resolve_real_path(&self, id: FileId) -> FileResult<std::path::PathBuf> {
        match id.root() {
            VirtualRoot::Package(spec) => {
                if self.packages_source == 1 || self.packages_source == 3 {
                    return Err(FileError::NotFound(vpath_buf(id)));
                }

                let root = self.package_storage.obtain(spec).map_err(|e| {
                    FileError::Other(Some(EcoString::from(format!(
                        "Failed to prepare package: {}",
                        e
                    ))))
                })?;
                root.resolve(id.vpath())
            }
            VirtualRoot::Project => {
                // Resolve project files against a real on-disk root, if one was
                // configured. Otherwise nothing is available on disk.
                match &self.root_path {
                    Some(root) => id
                        .vpath()
                        .realize(root)
                        .map_err(|_| FileError::NotFound(vpath_buf(id))),
                    None => Err(FileError::NotFound(vpath_buf(id))),
                }
            }
        }
    }
}

/// Returns the rootless filesystem path for a file id, used for error reporting.
fn vpath_buf(id: FileId) -> std::path::PathBuf {
    std::path::PathBuf::from(id.vpath().get_without_slash())
}

impl World for VfsWorld {
    fn library(&self) -> &LazyHash<Library> {
        &self.library
    }

    fn book(&self) -> &LazyHash<FontBook> {
        self.font_store.book()
    }

    fn main(&self) -> FileId {
        self.main_id
    }

    fn source(&self, id: FileId) -> FileResult<Source> {
        if let Some(source) = self.sources.get(&id) {
            if matches!(id.root(), VirtualRoot::Package(_)) {
                // If it's a package source and we are in InternetOnly or None mode, block it
                if self.packages_source == 2 || self.packages_source == 3 {
                    return Err(FileError::NotFound(vpath_buf(id)));
                }
            }
            return Ok(source.clone());
        }

        let path = self.resolve_real_path(id)?;
        let text = std::fs::read_to_string(&path).map_err(|e| FileError::from_io(e, &path))?;
        Ok(Source::new(id, text))
    }

    fn file(&self, id: FileId) -> FileResult<Bytes> {
        if let Some(bytes) = self.files.get(&id) {
            if matches!(id.root(), VirtualRoot::Package(_)) {
                // If it's a package file and we are in InternetOnly or None mode, block it
                if self.packages_source == 2 || self.packages_source == 3 {
                    return Err(FileError::NotFound(vpath_buf(id)));
                }
            }
            return Ok(bytes.clone());
        }

        let path = self.resolve_real_path(id)?;
        let data = std::fs::read(&path).map_err(|e| FileError::from_io(e, &path))?;
        Ok(Bytes::new(data))
    }

    fn font(&self, index: usize) -> Option<Font> {
        self.font_store.font(index)
    }

    fn today(&self, _offset: Option<typst::foundations::Duration>) -> Option<Datetime> {
        let now = chrono::Local::now();
        Datetime::from_ymd_hms(
            now.year(),
            now.month() as u8,
            now.day() as u8,
            now.hour() as u8,
            now.minute() as u8,
            now.second() as u8,
        )
    }
}

// C API
use libc::{c_char, c_uchar, size_t};
use std::ffi::{CStr, CString};

/// Output formats supported by the unified compile entry point.
/// `FORMAT_PDF` (0) is the implicit default handled by the catch-all arm.
#[allow(dead_code)]
const FORMAT_PDF: u8 = 0;
const FORMAT_PNG: u8 = 1;
const FORMAT_SVG: u8 = 2;
const FORMAT_HTML: u8 = 3;

/// Options controlling a single compilation, passed across the FFI boundary.
#[repr(C)]
pub struct CompileOptions {
    /// Output format: 0 = PDF, 1 = PNG, 2 = SVG, 3 = HTML.
    pub format: u8,
    /// Pixels per inch for PNG rendering (defaults to 144 if <= 0).
    pub ppi: f32,
    /// PDF standard selector (see `pdf_standards_from_code`). 0 = default (PDF 1.7).
    pub pdf_standard: u8,
    /// Whether to embed a fixed timestamp (`use_timestamp` != 0) in the PDF.
    pub use_timestamp: u8,
    /// Unix timestamp (seconds, UTC) to embed when `use_timestamp` is set.
    pub timestamp_unix: i64,
    /// Whether to pretty-print SVG/HTML output (!= 0).
    pub pretty: u8,
    /// Document title (UTF-8 C string, may be null).
    pub title: *const c_char,
    /// Document author (UTF-8 C string, may be null). Single author only.
    pub author: *const c_char,
}

/// Maps a PDF standard code to a [`PdfStandards`] value. Unknown codes fall back
/// to the default (PDF 1.7).
fn pdf_standards_from_code(code: u8) -> PdfStandards {
    let standard = match code {
        1 => PdfStandard::A_2b,
        2 => PdfStandard::A_3b,
        3 => PdfStandard::V_1_7,
        4 => PdfStandard::A_1b,
        5 => PdfStandard::A_2u,
        6 => PdfStandard::A_3u,
        7 => PdfStandard::V_2_0,
        8 => PdfStandard::Ua_1,
        _ => PdfStandard::V_1_7,
    };
    PdfStandards::new(&[standard]).unwrap_or_default()
}

/// A compilation result: the produced outputs plus structured diagnostics.
#[repr(C)]
pub struct CompilationResult {
    pub success: u8,
    /// Pointer to an array of `output_count` output buffers.
    pub outputs: *mut OutputBuffer,
    pub output_count: size_t,
    /// Pointer to an array of `diagnostic_count` diagnostics (errors + warnings).
    pub diagnostics: *mut Diagnostic,
    pub diagnostic_count: size_t,
}

/// A single output buffer (a PDF blob, a PNG page, or an SVG/HTML string).
#[repr(C)]
pub struct OutputBuffer {
    pub data: *mut c_uchar,
    pub len: size_t,
}

/// A structured diagnostic (error or warning) with position information.
#[repr(C)]
pub struct Diagnostic {
    /// 0 = error, 1 = warning.
    pub severity: u8,
    pub message: *mut c_char,
    /// File path the diagnostic points into, or null when detached.
    pub file_path: *mut c_char,
    /// 1-based line number, or 0 when unavailable.
    pub line: size_t,
    /// 1-based column number, or 0 when unavailable.
    pub column: size_t,
    /// Newline-separated hint text, or null when there are no hints.
    pub hints: *mut c_char,
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_version() -> *mut c_char {
    let version = "0.15.0";
    CString::new(version).unwrap().into_raw()
}

/// Create a new VfsWorld.
///
/// # Safety
///
/// The `main_path`, `cache_path`, `data_path`, and `root_path` must be valid C
/// strings if not null.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn typst_context_new(
    main_path: *const c_char,
    packages_source: u8,
    fonts_source: u8,
    cache_path: *const c_char,
    data_path: *const c_char,
    root_path: *const c_char,
) -> *mut VfsWorld {
    let main_path = unsafe { cstr_to_str(main_path) }.unwrap_or("main.typ");
    let cache_path = unsafe { cstr_to_pathbuf(cache_path) };
    let data_path = unsafe { cstr_to_pathbuf(data_path) };
    let root_path = unsafe { cstr_to_pathbuf(root_path) };

    let world = VfsWorld::new(
        main_path,
        packages_source,
        fonts_source,
        cache_path,
        data_path,
        root_path,
    );
    Box::into_raw(Box::new(world))
}

/// Free a VfsWorld.
///
/// # Safety
///
/// The `world` pointer must be a valid pointer created by `typst_context_new`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn typst_context_free(world: *mut VfsWorld) {
    if !world.is_null() {
        unsafe { drop(Box::from_raw(world)) };
    }
}

/// Reset a VfsWorld.
///
/// # Safety
///
/// The `world` pointer must be a valid pointer to a `VfsWorld`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn typst_context_reset(world: *mut VfsWorld) {
    let world = unsafe { &mut *world };
    world.reset();
}

/// Set (or clear, when null) the real on-disk project root.
///
/// # Safety
///
/// The `world` pointer must be valid. `root_path` must be a valid C string if
/// not null.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn typst_set_root(world: *mut VfsWorld, root_path: *const c_char) {
    let world = unsafe { &mut *world };
    let root = unsafe { cstr_to_pathbuf(root_path) };
    world.set_root(root);
}

/// Set a source file in the VfsWorld.
///
/// # Safety
///
/// The `world` pointer must be valid. `path` and `text` must be valid C strings.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn typst_set_source(
    world: *mut VfsWorld,
    path: *const c_char,
    text: *const c_char,
) {
    let world = unsafe { &mut *world };
    let path = unsafe { CStr::from_ptr(path).to_str().unwrap() };
    let text = unsafe { CStr::from_ptr(text).to_str().unwrap() };
    world.set_source(path, text);
}

/// Set a binary file in the VfsWorld.
///
/// # Safety
///
/// The `world` pointer must be valid. `path` must be a valid C string. `data` must be a valid buffer of `data_len` bytes.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn typst_set_file(
    world: *mut VfsWorld,
    path: *const c_char,
    data: *const c_uchar,
    data_len: size_t,
) {
    let world = unsafe { &mut *world };
    let path = unsafe { CStr::from_ptr(path).to_str().unwrap() };
    let data = unsafe { std::slice::from_raw_parts(data, data_len).to_vec() };
    world.set_file(path, data);
}

/// Add a font to the VfsWorld.
///
/// # Safety
///
/// The `world` pointer must be valid. `data` must be a valid buffer of `data_len` bytes.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn typst_add_font(
    world: *mut VfsWorld,
    data: *const c_uchar,
    data_len: size_t,
) {
    let world = unsafe { &mut *world };
    let data = unsafe { std::slice::from_raw_parts(data, data_len).to_vec() };
    world.add_font(data);
}

/// Set an input variable in the VfsWorld.
///
/// # Safety
///
/// The `world` pointer must be valid. `key` and `value` must be valid C strings.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn typst_set_input(
    world: *mut VfsWorld,
    key: *const c_char,
    value: *const c_char,
) {
    let world = unsafe { &mut *world };
    let key = unsafe { CStr::from_ptr(key).to_str().unwrap() };
    let value = unsafe { CStr::from_ptr(value).to_str().unwrap() };
    world.set_input(key, value);
}

/// Set package paths in the VfsWorld.
///
/// # Safety
///
/// The `world` pointer must be valid. `cache` and `data` must be valid C strings if not null.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn typst_set_package_paths(
    world: *mut VfsWorld,
    cache: *const c_char,
    data: *const c_char,
) {
    let world = unsafe { &mut *world };
    let cache = unsafe { cstr_to_pathbuf(cache) };
    let data = unsafe { cstr_to_pathbuf(data) };
    world.set_package_paths(cache, data);
}

/// Set a package source file in the VfsWorld.
///
/// # Safety
///
/// The `world` pointer must be valid. `package_spec`, `path`, and `text` must be valid C strings.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn typst_set_package_source(
    world: *mut VfsWorld,
    package_spec: *const c_char,
    path: *const c_char,
    text: *const c_char,
) -> i32 {
    let world = unsafe { &mut *world };
    let spec_str = unsafe { CStr::from_ptr(package_spec).to_str().unwrap() };
    let path = unsafe { CStr::from_ptr(path).to_str().unwrap() };
    let text = unsafe { CStr::from_ptr(text).to_str().unwrap() };

    match spec_str.parse::<PackageSpec>() {
        Ok(spec) => {
            world.set_package_source(spec, path, text);
            1
        }
        Err(_) => 0,
    }
}

/// Set a package binary file in the VfsWorld.
///
/// # Safety
///
/// The `world` pointer must be valid. `package_spec` and `path` must be valid C strings. `data` must be a valid buffer of `data_len` bytes.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn typst_set_package_file(
    world: *mut VfsWorld,
    package_spec: *const c_char,
    path: *const c_char,
    data: *const c_uchar,
    data_len: size_t,
) -> i32 {
    let world = unsafe { &mut *world };
    let spec_str = unsafe { CStr::from_ptr(package_spec).to_str().unwrap() };
    let path = unsafe { CStr::from_ptr(path).to_str().unwrap() };
    let data = unsafe { std::slice::from_raw_parts(data, data_len).to_vec() };

    match spec_str.parse::<PackageSpec>() {
        Ok(spec) => {
            world.set_package_file(spec, path, data);
            1
        }
        Err(_) => 0,
    }
}

/// Lists the available font family names, newline-separated. Returns null when
/// there are no fonts. The returned string must be freed with `typst_free_string`.
///
/// # Safety
///
/// The `world` pointer must be valid.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn typst_list_fonts(world: *mut VfsWorld) -> *mut c_char {
    let world = unsafe { &*world };
    let names = world.font_families();
    if names.is_empty() {
        return std::ptr::null_mut();
    }
    CString::new(names.join("\n"))
        .map(CString::into_raw)
        .unwrap_or(std::ptr::null_mut())
}

/// Converts a [`SourceDiagnostic`] into the FFI-friendly [`Diagnostic`] form,
/// resolving the span to a file path and line/column where possible.
fn build_diagnostic(world: &VfsWorld, diag: &SourceDiagnostic) -> Diagnostic {
    let severity = match diag.severity {
        Severity::Error => 0,
        Severity::Warning => 1,
    };

    let message = string_to_cstr(diag.message.as_str());

    let mut file_path = std::ptr::null_mut();
    let mut line: size_t = 0;
    let mut column: size_t = 0;

    if let Some(id) = diag.span.id() {
        file_path = string_to_cstr(id.vpath().get_without_slash());

        if let (Some(range), Ok(source)) = (world.range(diag.span), world.source(id))
            && let Some((l, c)) = source.lines().byte_to_line_column(range.start)
        {
            line = l + 1;
            column = c + 1;
        }
    }

    let hints = if diag.hints.is_empty() {
        std::ptr::null_mut()
    } else {
        let joined = diag
            .hints
            .iter()
            .map(|h| h.v.as_str())
            .collect::<Vec<_>>()
            .join("\n");
        string_to_cstr(&joined)
    };

    Diagnostic {
        severity,
        message,
        file_path,
        line,
        column,
        hints,
    }
}

/// Builds a heap-allocated diagnostics array from errors followed by warnings.
fn build_diagnostics(
    world: &VfsWorld,
    errors: &[SourceDiagnostic],
    warnings: &[SourceDiagnostic],
) -> (*mut Diagnostic, size_t) {
    if errors.is_empty() && warnings.is_empty() {
        return (std::ptr::null_mut(), 0);
    }
    let mut all: Vec<Diagnostic> = Vec::with_capacity(errors.len() + warnings.len());
    for err in errors {
        all.push(build_diagnostic(world, err));
    }
    for warn in warnings {
        all.push(build_diagnostic(world, warn));
    }
    let len = all.len();
    let ptr = all.as_mut_ptr();
    std::mem::forget(all);
    (ptr, len)
}

/// Builds a heap-allocated outputs array from a list of byte vectors.
fn build_outputs(buffers: Vec<Vec<u8>>) -> (*mut OutputBuffer, size_t) {
    if buffers.is_empty() {
        return (std::ptr::null_mut(), 0);
    }
    let mut outputs: Vec<OutputBuffer> = buffers
        .into_iter()
        .map(|mut data| {
            // Capacity must equal length: `typst_free_result` reconstructs the
            // Vec with `from_raw_parts(ptr, len, len)`.
            data.shrink_to_fit();
            let len = data.len();
            let ptr = data.as_mut_ptr();
            std::mem::forget(data);
            OutputBuffer { data: ptr, len }
        })
        .collect();
    let len = outputs.len();
    let ptr = outputs.as_mut_ptr();
    std::mem::forget(outputs);
    (ptr, len)
}

/// Builds a successful result from output buffers and (warning) diagnostics.
fn success_result(
    outputs: Vec<Vec<u8>>,
    diagnostics: (*mut Diagnostic, size_t),
) -> CompilationResult {
    let (outputs, output_count) = build_outputs(outputs);
    CompilationResult {
        success: 1,
        outputs,
        output_count,
        diagnostics: diagnostics.0,
        diagnostic_count: diagnostics.1,
    }
}

/// Builds a failure result carrying only diagnostics.
fn failure_result(diagnostics: (*mut Diagnostic, size_t)) -> CompilationResult {
    CompilationResult {
        success: 0,
        outputs: std::ptr::null_mut(),
        output_count: 0,
        diagnostics: diagnostics.0,
        diagnostic_count: diagnostics.1,
    }
}

/// Builds the PDF export options from the FFI options struct.
fn build_pdf_options(opts: &CompileOptions) -> PdfOptions {
    let mut pdf_options = PdfOptions {
        standards: pdf_standards_from_code(opts.pdf_standard),
        ..Default::default()
    };

    if opts.use_timestamp != 0
        && let Some(dt) = chrono::DateTime::from_timestamp(opts.timestamp_unix, 0)
        && let Some(datetime) = Datetime::from_ymd_hms(
            dt.year(),
            dt.month() as u8,
            dt.day() as u8,
            dt.hour() as u8,
            dt.minute() as u8,
            dt.second() as u8,
        )
    {
        pdf_options.timestamp = Some(Timestamp::new_utc(datetime));
    }

    pdf_options
}

/// Applies title/author metadata onto a paged document before export.
unsafe fn apply_metadata(document: &mut PagedDocument, opts: &CompileOptions) {
    if let Some(title) = unsafe { cstr_to_str(opts.title) }
        && !title.is_empty()
    {
        document.info_mut().title = Some(EcoString::from(title));
    }
    if let Some(author) = unsafe { cstr_to_str(opts.author) }
        && !author.is_empty()
    {
        document.info_mut().author = vec![EcoString::from(author)];
    }
}

/// Compile a document to the format described by `options`.
///
/// # Safety
///
/// The `world` pointer must be valid. `options` must point to a valid
/// [`CompileOptions`], whose string fields must be valid C strings if not null.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn typst_compile(
    world: *mut VfsWorld,
    options: *const CompileOptions,
) -> CompilationResult {
    let world = unsafe { &mut *world };
    let opts = unsafe { &*options };

    // Rebuild the standard library if inputs changed since the last compile.
    world.prepare();

    let result = if opts.format == FORMAT_HTML {
        unsafe { compile_html(world) }
    } else {
        unsafe { compile_paged(world, opts) }
    };

    // Bound the global comemo memoization cache. Without this, the cache grows
    // without limit across compiles on a reused compiler. Matches the CLI.
    comemo::evict(MAX_COMEMO_AGE);

    result
}

/// Compiles to a paged document and exports it as PDF, PNG, or SVG.
unsafe fn compile_paged(world: &VfsWorld, opts: &CompileOptions) -> CompilationResult {
    let Warned { output, warnings } = typst::compile::<PagedDocument>(world);
    match output {
        Ok(mut document) => {
            let export: Result<Vec<Vec<u8>>, EcoVec<SourceDiagnostic>> = match opts.format {
                FORMAT_PNG => Ok(render_png(&document, opts.ppi)),
                FORMAT_SVG => Ok(render_svg(&document, opts.pretty != 0)),
                _ => {
                    unsafe { apply_metadata(&mut document, opts) };
                    let pdf_options = build_pdf_options(opts);
                    typst_pdf::pdf(&document, &pdf_options).map(|pdf| vec![pdf])
                }
            };

            match export {
                Ok(buffers) => success_result(buffers, build_diagnostics(world, &[], &warnings)),
                Err(errors) => failure_result(build_diagnostics(world, &errors, &warnings)),
            }
        }
        Err(errors) => failure_result(build_diagnostics(world, &errors, &warnings)),
    }
}

/// Compiles the document to a single HTML string.
unsafe fn compile_html(world: &VfsWorld) -> CompilationResult {
    let Warned { output, warnings } = typst::compile::<HtmlDocument>(world);
    match output {
        Ok(document) => match typst_html::html(&document, &HtmlOptions { pretty: true }) {
            Ok(html) => {
                let outputs: Vec<Vec<u8>> = vec![html.into_bytes()];
                success_result(outputs, build_diagnostics(world, &[], &warnings))
            }
            Err(errors) => failure_result(build_diagnostics(world, &errors, &warnings)),
        },
        Err(errors) => failure_result(build_diagnostics(world, &errors, &warnings)),
    }
}

/// Renders every page of the document to a PNG, returning one buffer per page.
fn render_png(document: &PagedDocument, ppi: f32) -> Vec<Vec<u8>> {
    let ppi = if ppi <= 0.0 { 144.0 } else { ppi };
    let options = RenderOptions {
        // `render` works in pixels per point; 1 inch = 72 points.
        pixel_per_pt: Scalar::new((ppi / 72.0) as f64),
        ..Default::default()
    };
    let mut buffers = Vec::with_capacity(document.pages().len());
    for page in document.pages() {
        let pixmap = typst_render::render(page, &options);
        if let Ok(data) = pixmap.encode_png() {
            buffers.push(data);
        }
    }
    buffers
}

/// Renders every page of the document to an SVG, returning one buffer per page.
fn render_svg(document: &PagedDocument, pretty: bool) -> Vec<Vec<u8>> {
    let options = typst_svg::SvgOptions {
        pretty,
        ..Default::default()
    };
    document
        .pages()
        .iter()
        .map(|page| typst_svg::svg(page, &options).into_bytes())
        .collect()
}

/// Free a compilation result and all owned buffers/diagnostics.
///
/// # Safety
///
/// `result` must have been produced by `typst_compile` and not freed before.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn typst_free_result(result: CompilationResult) {
    if !result.outputs.is_null() {
        let outputs = unsafe {
            Vec::from_raw_parts(result.outputs, result.output_count, result.output_count)
        };
        for output in outputs {
            if !output.data.is_null() {
                unsafe { drop(Vec::from_raw_parts(output.data, output.len, output.len)) };
            }
        }
    }
    if !result.diagnostics.is_null() {
        let diagnostics = unsafe {
            Vec::from_raw_parts(
                result.diagnostics,
                result.diagnostic_count,
                result.diagnostic_count,
            )
        };
        for diag in diagnostics {
            unsafe { free_cstring(diag.message) };
            unsafe { free_cstring(diag.file_path) };
            unsafe { free_cstring(diag.hints) };
        }
    }
}

/// Free a string.
///
/// # Safety
///
/// `ptr` must be a pointer to a string returned by an FFI function.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn typst_free_string(ptr: *mut c_char) {
    unsafe { free_cstring(ptr) };
}

/// Frees a C string allocated by Rust, ignoring null.
unsafe fn free_cstring(ptr: *mut c_char) {
    if !ptr.is_null() {
        unsafe {
            let _ = CString::from_raw(ptr);
        };
    }
}

/// Allocates a C string from a Rust string, replacing interior NULs so the
/// conversion cannot fail. Returns null on allocation failure.
fn string_to_cstr(s: &str) -> *mut c_char {
    let sanitized = s.replace('\0', " ");
    CString::new(sanitized)
        .map(CString::into_raw)
        .unwrap_or(std::ptr::null_mut())
}

/// Converts a possibly-null C string to a `&str`.
unsafe fn cstr_to_str<'a>(ptr: *const c_char) -> Option<&'a str> {
    if ptr.is_null() {
        None
    } else {
        unsafe { CStr::from_ptr(ptr).to_str().ok() }
    }
}

/// Converts a possibly-null C string to an owned `PathBuf`.
unsafe fn cstr_to_pathbuf(ptr: *const c_char) -> Option<std::path::PathBuf> {
    unsafe { cstr_to_str(ptr) }.map(std::path::PathBuf::from)
}
