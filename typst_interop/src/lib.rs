use chrono::{Datelike, Timelike};
use ecow::EcoString;
use once_cell::sync::Lazy;
use std::collections::HashMap;
use typst::diag::{FileError, FileResult};
use typst::foundations::{Bytes, Datetime, Dict, Value};
use typst::syntax::{FileId, Source, VirtualPath};
use typst::text::{Font, FontBook};
use typst::utils::LazyHash;
use typst::{Library, LibraryExt, World};
use typst_kit::download::{DownloadState, Downloader, Progress};
use typst_kit::fonts::FontSearcher;
use typst_kit::package::PackageStorage;
use typst_syntax::package::PackageSpec;

static GLOBAL_FONTS: Lazy<(Vec<Font>, LazyHash<FontBook>)> = Lazy::new(|| {
    let mut searcher = FontSearcher::new();
    searcher.include_system_fonts(true);
    searcher.include_embedded_fonts(true);
    let fonts: Vec<Font> = searcher
        .search()
        .fonts
        .into_iter()
        .flat_map(|slot| slot.get())
        .collect();
    let book = FontBook::from_fonts(&fonts);
    (fonts, LazyHash::new(book))
});

static EMBEDDED_FONTS: Lazy<(Vec<Font>, LazyHash<FontBook>)> = Lazy::new(|| {
    let mut searcher = FontSearcher::new();
    searcher.include_system_fonts(false);
    searcher.include_embedded_fonts(true);
    let fonts: Vec<Font> = searcher
        .search()
        .fonts
        .into_iter()
        .flat_map(|slot| slot.get())
        .collect();
    let book = FontBook::from_fonts(&fonts);
    (fonts, LazyHash::new(book))
});

static SYSTEM_ONLY_FONTS: Lazy<(Vec<Font>, LazyHash<FontBook>)> = Lazy::new(|| {
    let mut searcher = FontSearcher::new();
    searcher.include_system_fonts(true);
    searcher.include_embedded_fonts(false);
    let fonts: Vec<Font> = searcher
        .search()
        .fonts
        .into_iter()
        .flat_map(|slot| slot.get())
        .collect();
    let book = FontBook::from_fonts(&fonts);
    (fonts, LazyHash::new(book))
});

struct Silent;
impl Progress for Silent {
    fn print_start(&mut self) {}
    fn print_progress(&mut self, _: &DownloadState) {}
    fn print_finish(&mut self, _: &DownloadState) {}
}

pub struct VfsWorld {
    library: LazyHash<Library>,
    book: LazyHash<FontBook>,
    fonts: Vec<Font>,
    sources: HashMap<FileId, Source>,
    files: HashMap<FileId, Bytes>,
    main_id: FileId,
    inputs: Dict,
    package_storage: PackageStorage,
    cache_path: Option<std::path::PathBuf>,
    data_path: Option<std::path::PathBuf>,
    packages_source: u8,
    fonts_source: u8,
}

impl VfsWorld {
    pub fn new(
        main_path: &str,
        packages_source: u8,
        fonts_source: u8,
        cache_path: Option<std::path::PathBuf>,
        data_path: Option<std::path::PathBuf>,
    ) -> Self {
        let main_id = FileId::new(None, VirtualPath::new(main_path));

        let (fonts, book) = match fonts_source {
            0 => (&*GLOBAL_FONTS).clone(),                         // All
            1 => (&*EMBEDDED_FONTS).clone(),                       // DefaultOnly
            2 => (&*SYSTEM_ONLY_FONTS).clone(),                    // SystemOnly
            3 | 4 => (Vec::new(), LazyHash::new(FontBook::new())), // ProvidedOnly or None
            _ => (&*GLOBAL_FONTS).clone(),
        };

        let inputs = Dict::new();
        let library = Library::builder().with_inputs(inputs.clone()).build();

        let downloader = Downloader::new("typst-interop");
        let package_storage =
            PackageStorage::new(cache_path.clone(), data_path.clone(), downloader);

        Self {
            library: LazyHash::new(library),
            book,
            fonts,
            sources: HashMap::new(),
            files: HashMap::new(),
            main_id,
            inputs,
            package_storage,
            cache_path,
            data_path,
            packages_source,
            fonts_source,
        }
    }

    pub fn reset(&mut self) {
        self.sources.clear();
        self.files.clear();
        self.inputs = Dict::new();
        let library = Library::builder().with_inputs(self.inputs.clone()).build();
        self.library = LazyHash::new(library);

        let (fonts, book) = match self.fonts_source {
            0 => (&*GLOBAL_FONTS).clone(),                         // All
            1 => (&*EMBEDDED_FONTS).clone(),                       // DefaultOnly
            2 => (&*SYSTEM_ONLY_FONTS).clone(),                    // SystemOnly
            3 | 4 => (Vec::new(), LazyHash::new(FontBook::new())), // ProvidedOnly or None
            _ => (&*GLOBAL_FONTS).clone(),
        };
        self.fonts = fonts;
        self.book = book;
    }

    pub fn set_source(&mut self, path: &str, text: &str) {
        let id = FileId::new(None, VirtualPath::new(path));
        let source = Source::new(id, text.into());
        self.sources.insert(id, source);
    }

    pub fn set_file(&mut self, path: &str, data: Vec<u8>) {
        let id = FileId::new(None, VirtualPath::new(path));
        self.files.insert(id, Bytes::new(data));
    }

    pub fn set_package_source(&mut self, spec: PackageSpec, path: &str, text: &str) {
        let id = FileId::new(Some(spec), VirtualPath::new(path));
        let source = Source::new(id, text.into());
        self.sources.insert(id, source);
    }

    pub fn set_package_file(&mut self, spec: PackageSpec, path: &str, data: Vec<u8>) {
        let id = FileId::new(Some(spec), VirtualPath::new(path));
        self.files.insert(id, Bytes::new(data));
    }

    pub fn set_package_paths(
        &mut self,
        cache: Option<std::path::PathBuf>,
        data: Option<std::path::PathBuf>,
    ) {
        self.cache_path = cache;
        self.data_path = data;
        let downloader = Downloader::new("typst-interop");
        self.package_storage =
            PackageStorage::new(self.cache_path.clone(), self.data_path.clone(), downloader);
    }

    pub fn add_font(&mut self, data: Vec<u8>) {
        if self.fonts_source == 4 {
            return;
        }
        let new_fonts = Font::iter(Bytes::new(data));
        for font in new_fonts {
            self.fonts.push(font);
        }
        self.book = LazyHash::new(FontBook::from_fonts(&self.fonts));
    }

    pub fn set_input(&mut self, key: &str, value: &str) {
        self.inputs.insert(
            EcoString::from(key).into(),
            Value::Str(EcoString::from(value).into()),
        );
        let library = Library::builder().with_inputs(self.inputs.clone()).build();
        self.library = LazyHash::new(library);
    }

    fn resolve_real_path(&self, id: FileId) -> FileResult<std::path::PathBuf> {
        match id.package() {
            Some(spec) => {
                if self.packages_source == 1 || self.packages_source == 3 {
                    return Err(FileError::NotFound(
                        id.vpath().as_rootless_path().to_path_buf(),
                    ));
                }

                let mut silent = Silent;
                let pkg_path = self
                    .package_storage
                    .prepare_package(spec, &mut silent)
                    .map_err(|e| {
                        FileError::Other(Some(EcoString::from(format!(
                            "Failed to prepare package: {}",
                            e
                        ))))
                    })?;
                Ok(id.vpath().resolve(&pkg_path).ok_or(FileError::NotFound(
                    id.vpath().as_rootless_path().to_path_buf(),
                ))?)
            }
            None => Err(FileError::NotFound(
                id.vpath().as_rootless_path().to_path_buf(),
            )),
        }
    }
}

impl World for VfsWorld {
    fn library(&self) -> &LazyHash<Library> {
        &self.library
    }

    fn book(&self) -> &LazyHash<FontBook> {
        &self.book
    }

    fn main(&self) -> FileId {
        self.main_id
    }

    fn source(&self, id: FileId) -> FileResult<Source> {
        if let Some(source) = self.sources.get(&id) {
            if let Some(_spec) = id.package() {
                // If it's a package source and we are in InternetOnly or None mode, block it
                if self.packages_source == 2 || self.packages_source == 3 {
                    return Err(FileError::NotFound(
                        id.vpath().as_rootless_path().to_path_buf(),
                    ));
                }
            }
            return Ok(source.clone());
        }

        let path = self.resolve_real_path(id)?;
        let text = std::fs::read_to_string(&path).map_err(|e| FileError::from_io(e, &path))?;
        Ok(Source::new(id, text.into()))
    }

    fn file(&self, id: FileId) -> FileResult<Bytes> {
        if let Some(bytes) = self.files.get(&id) {
            if let Some(_spec) = id.package() {
                // If it's a package file and we are in InternetOnly or None mode, block it
                if self.packages_source == 2 || self.packages_source == 3 {
                    return Err(FileError::NotFound(
                        id.vpath().as_rootless_path().to_path_buf(),
                    ));
                }
            }
            return Ok(bytes.clone());
        }

        let path = self.resolve_real_path(id)?;
        let data = std::fs::read(&path).map_err(|e| FileError::from_io(e, &path))?;
        Ok(Bytes::new(data))
    }

    fn font(&self, index: usize) -> Option<Font> {
        self.fonts.get(index).cloned()
    }

    fn today(&self, _offset: Option<i64>) -> Option<Datetime> {
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

#[unsafe(no_mangle)]
pub extern "C" fn typst_version() -> *mut c_char {
    let version = "0.14.2";
    CString::new(version).unwrap().into_raw()
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_context_new(
    main_path: *const c_char,
    packages_source: u8,
    fonts_source: u8,
    cache_path: *const c_char,
    data_path: *const c_char,
) -> *mut VfsWorld {
    let main_path = unsafe { CStr::from_ptr(main_path).to_str().unwrap_or("main.typ") };
    let cache_path = if cache_path.is_null() {
        None
    } else {
        Some(std::path::PathBuf::from(unsafe {
            CStr::from_ptr(cache_path).to_str().unwrap()
        }))
    };
    let data_path = if data_path.is_null() {
        None
    } else {
        Some(std::path::PathBuf::from(unsafe {
            CStr::from_ptr(data_path).to_str().unwrap()
        }))
    };

    let world = VfsWorld::new(
        main_path,
        packages_source,
        fonts_source,
        cache_path,
        data_path,
    );
    Box::into_raw(Box::new(world))
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_context_free(world: *mut VfsWorld) {
    if !world.is_null() {
        unsafe { drop(Box::from_raw(world)) };
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_context_reset(world: *mut VfsWorld) {
    let world = unsafe { &mut *world };
    world.reset();
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_set_source(world: *mut VfsWorld, path: *const c_char, text: *const c_char) {
    let world = unsafe { &mut *world };
    let path = unsafe { CStr::from_ptr(path).to_str().unwrap() };
    let text = unsafe { CStr::from_ptr(text).to_str().unwrap() };
    world.set_source(path, text);
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_set_file(
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

#[unsafe(no_mangle)]
pub extern "C" fn typst_add_font(world: *mut VfsWorld, data: *const c_uchar, data_len: size_t) {
    let world = unsafe { &mut *world };
    let data = unsafe { std::slice::from_raw_parts(data, data_len).to_vec() };
    world.add_font(data);
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_set_input(world: *mut VfsWorld, key: *const c_char, value: *const c_char) {
    let world = unsafe { &mut *world };
    let key = unsafe { CStr::from_ptr(key).to_str().unwrap() };
    let value = unsafe { CStr::from_ptr(value).to_str().unwrap() };
    world.set_input(key, value);
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_set_package_paths(
    world: *mut VfsWorld,
    cache: *const c_char,
    data: *const c_char,
) {
    let world = unsafe { &mut *world };
    let cache = if cache.is_null() {
        None
    } else {
        Some(std::path::PathBuf::from(unsafe {
            CStr::from_ptr(cache).to_str().unwrap()
        }))
    };
    let data = if data.is_null() {
        None
    } else {
        Some(std::path::PathBuf::from(unsafe {
            CStr::from_ptr(data).to_str().unwrap()
        }))
    };
    world.set_package_paths(cache, data);
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_set_package_source(
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

#[unsafe(no_mangle)]
pub extern "C" fn typst_set_package_file(
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

#[repr(C)]
pub struct CompilationResult {
    pub success: u8,
    pub pdf_data: *mut c_uchar,
    pub pdf_len: size_t,
    pub error_message: *mut c_char,
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_compile_pdf(world: *mut VfsWorld) -> CompilationResult {
    let world = unsafe { &mut *world };

    match typst::compile(world).output {
        Ok(document) => match typst_pdf::pdf(&document, &typst_pdf::PdfOptions::default()) {
            Ok(pdf) => {
                let mut pdf = pdf;
                pdf.shrink_to_fit();
                let len = pdf.len();
                let ptr = pdf.as_mut_ptr();
                std::mem::forget(pdf);
                CompilationResult {
                    success: 1,
                    pdf_data: ptr,
                    pdf_len: len,
                    error_message: std::ptr::null_mut(),
                }
            }
            Err(err) => {
                let msg = CString::new(format!("PDF export failed: {:?}", err)).unwrap();
                CompilationResult {
                    success: 0,
                    pdf_data: std::ptr::null_mut(),
                    pdf_len: 0,
                    error_message: msg.into_raw(),
                }
            }
        },
        Err(errors) => {
            let mut msg = String::new();
            for err in errors {
                msg.push_str(&format!("{}\n", err.message));
            }
            let c_msg = CString::new(msg).unwrap();
            CompilationResult {
                success: 0,
                pdf_data: std::ptr::null_mut(),
                pdf_len: 0,
                error_message: c_msg.into_raw(),
            }
        }
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_free_pdf(ptr: *mut c_uchar, len: size_t) {
    if !ptr.is_null() {
        unsafe { Vec::from_raw_parts(ptr, len, len) };
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_free_string(ptr: *mut c_char) {
    if !ptr.is_null() {
        unsafe {
            let _ = CString::from_raw(ptr);
        };
    }
}
