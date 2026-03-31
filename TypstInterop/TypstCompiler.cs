using System.Runtime.InteropServices;
using System.Text;
using TypstInterop.Abstractions;
using TypstInterop.Internal;
using TypstInterop.Models;

namespace TypstInterop;

/// <summary>
/// Unified Typst compiler.
/// This class provides a managed wrapper around the Typst engine.
/// Each instance maintains its own world (fonts and package cache) and can be reused.
/// </summary>
public sealed unsafe class TypstCompiler : ITypstCompiler, ITypstConfigurator
{
    private const string MainPath = "main.typ";

    private readonly VfsWorld* _world;
    private readonly ReaderWriterLockSlim _lock = new();
    private bool _isDisposed;

    /// <summary>
    /// Gets the version of the underlying Typst compiler.
    /// </summary>
    public static string TypstVersion
    {
        get
        {
            NativeLibraryLoader.Initialize();
            var pVersion = NativeMethods.typst_version();
            var version = PtrToStringUtf8(pVersion);
            NativeMethods.typst_free_string(pVersion);
            return version;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TypstCompiler"/> class with default options.
    /// </summary>
    public TypstCompiler() : this(new TypstCompilerOptions()) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TypstCompiler"/> class with the specified options.
    /// </summary>
    /// <param name="options">The compiler options.</param>
    public TypstCompiler(TypstCompilerOptions options)
    {
        NativeLibraryLoader.Initialize();
        fixed (byte* pMainPath = ToUtf8NullTerminated(MainPath))
        fixed (byte* pCachePath = options.CachePath != null ? ToUtf8NullTerminated(options.CachePath) : null)
        fixed (byte* pDataPath = options.DataPath != null ? ToUtf8NullTerminated(options.DataPath) : null)
        {
            _world = NativeMethods.typst_context_new(
                pMainPath,
                (byte)options.PackagesSource,
                (byte)options.FontsSource,
                pCachePath,
                pDataPath
            );
        }
    }

    private void CheckDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(TypstCompiler));
    }

    /// <inheritdoc />
    public TypstCompilationResult Compile(Action<ITypstConfigurator> configure)
    {
        CheckDisposed();

        _lock.EnterWriteLock();
        try
        {
            NativeMethods.typst_context_reset(_world);
            configure(this);

            var result = NativeMethods.typst_compile_pdf(_world);
            if (result.success != 0)
            {
                var pdf = new byte[(int)result.pdf_len.Value];
                Marshal.Copy((IntPtr)result.pdf_data, pdf, 0, pdf.Length);
                NativeMethods.typst_free_pdf(result.pdf_data, result.pdf_len);
                return TypstCompilationResult.Success(pdf);
            }
            else
            {
                var error = PtrToStringUtf8(result.error_message);
                NativeMethods.typst_free_string(result.error_message);
                return TypstCompilationResult.Failure(error);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    #region ITypstConfigurator Implementation

    ITypstConfigurator ITypstConfigurator.WithSource(ReadOnlySpan<char> content)
    {
        return WithSourceInternal(MainPath.AsSpan(), content);
    }

    private ITypstConfigurator WithSourceInternal(ReadOnlySpan<char> path, ReadOnlySpan<char> text)
    {
        fixed (byte* pPath = ToUtf8NullTerminated(path))
        fixed (byte* pText = ToUtf8NullTerminated(text))
        {
            NativeMethods.typst_set_source(_world, pPath, pText);
        }
        return this;
    }

    ITypstConfigurator ITypstConfigurator.WithFile(ReadOnlySpan<char> path, ReadOnlySpan<byte> data)
    {
        fixed (byte* pPath = ToUtf8NullTerminated(path))
        fixed (byte* pData = data)
        {
            NativeMethods.typst_set_file(_world, pPath, pData, (nuint)data.Length);
        }
        return this;
    }

    ITypstConfigurator ITypstConfigurator.WithFile(ReadOnlySpan<char> path, Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ((ITypstConfigurator)this).WithFile(path, ms.ToArray().AsSpan());
    }

    ITypstConfigurator ITypstConfigurator.WithFile(
        ReadOnlySpan<char> path,
        ReadOnlySpan<char> text,
        Encoding? encoding)
    {
        if (path.EndsWith(".typ".AsSpan(), StringComparison.OrdinalIgnoreCase)) 
            return WithSourceInternal(path, text);
        var bytes = (encoding ?? Encoding.UTF8).GetBytes(text.ToArray());
        return ((ITypstConfigurator)this).WithFile(path, bytes.AsSpan());
    }

    ITypstConfigurator ITypstConfigurator.WithInput(ReadOnlySpan<char> key, ReadOnlySpan<char> value)
    {
        fixed (byte* pKey = ToUtf8NullTerminated(key))
        fixed (byte* pValue = ToUtf8NullTerminated(value))
        {
            NativeMethods.typst_set_input(_world, pKey, pValue);
        }
        return this;
    }

    ITypstConfigurator ITypstConfigurator.WithFont(ReadOnlySpan<byte> data)
    {
        fixed (byte* pData = data)
        {
            NativeMethods.typst_add_font(_world, pData, (nuint)data.Length);
        }
        return this;
    }

    ITypstConfigurator ITypstConfigurator.WithFont(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ((ITypstConfigurator)this).WithFont(ms.ToArray().AsSpan());
    }

    ITypstConfigurator ITypstConfigurator.WithPackage(string packageSpec, Action<ITypstPackageConfigurator> configure)
    {
        var configurator = new TypstPackageConfigurator(this, packageSpec);
        configure(configurator);
        return this;
    }

    #endregion

    #region Package Implementation Helpers

    private void WithPackageSourceInternal(string packageSpec, ReadOnlySpan<char> path, ReadOnlySpan<char> text)
    {
        fixed (byte* pSpec = ToUtf8NullTerminated(packageSpec.AsSpan()))
        fixed (byte* pPath = ToUtf8NullTerminated(path))
        fixed (byte* pText = ToUtf8NullTerminated(text))
        {
            if (NativeMethods.typst_set_package_source(_world, pSpec, pPath, pText) == 0)
                throw new ArgumentException($"Invalid package specification: {packageSpec}", nameof(packageSpec));
        }
    }

    private void WithPackageFileInternal(string packageSpec, ReadOnlySpan<char> path, ReadOnlySpan<byte> data)
    {
        fixed (byte* pSpec = ToUtf8NullTerminated(packageSpec.AsSpan()))
        fixed (byte* pPath = ToUtf8NullTerminated(path))
        fixed (byte* pData = data)
        {
            if (NativeMethods.typst_set_package_file(_world, pSpec, pPath, pData, (nuint)data.Length) == 0)
                throw new ArgumentException($"Invalid package specification: {packageSpec}", nameof(packageSpec));
        }
    }

    private sealed class TypstPackageConfigurator : ITypstPackageConfigurator
    {
        private readonly TypstCompiler _compiler;
        private readonly string _packageSpec;

        public TypstPackageConfigurator(TypstCompiler compiler, string packageSpec)
        {
            _compiler = compiler;
            _packageSpec = packageSpec;
        }

        public ITypstPackageConfigurator WithSource(ReadOnlySpan<char> path, ReadOnlySpan<char> text)
        {
            _compiler.WithPackageSourceInternal(_packageSpec, path, text);
            return this;
        }

        public ITypstPackageConfigurator WithFile(ReadOnlySpan<char> path, ReadOnlySpan<byte> data)
        {
            _compiler.WithPackageFileInternal(_packageSpec, path, data);
            return this;
        }

        public ITypstPackageConfigurator WithFile(ReadOnlySpan<char> path, Stream stream)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return WithFile(path, ms.ToArray().AsSpan());
        }

        public ITypstPackageConfigurator WithDirectory(string directoryPath, bool recursive = true)
        {
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var fullRoot = Path.GetFullPath(directoryPath);
            if (!fullRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
                fullRoot += Path.DirectorySeparatorChar;

            foreach (var file in Directory.EnumerateFiles(directoryPath, "*", searchOption))
            {
                var fullFile = Path.GetFullPath(file);
                string relativePath;
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
                relativePath = Path.GetRelativePath(fullRoot, fullFile);
#else
                relativePath = fullFile.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) ? fullFile.Substring(fullRoot.Length) : Path.GetFileName(fullFile);
#endif
                if (file.EndsWith(".typ", StringComparison.OrdinalIgnoreCase))
                {
                    WithSource(relativePath, File.ReadAllText(file));
                }
                else
                {
                    WithFile(relativePath, File.ReadAllBytes(file));
                }
            }
            return this;
        }
    }

    #endregion

    private static string PtrToStringUtf8(byte* ptr)
    {
        if (ptr == null)
            return string.Empty;
        var len = 0;
        while (ptr[len] != 0)
            len++;
        return Encoding.UTF8.GetString(ptr, len);
    }

    private static byte[] ToUtf8NullTerminated(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty)
            return [0];
        fixed (char* pSpan = span)
        {
            var byteCount = Encoding.UTF8.GetByteCount(pSpan, span.Length);
            var bytes = new byte[byteCount + 1];
            fixed (byte* pBytes = bytes)
            {
                Encoding.UTF8.GetBytes(pSpan, span.Length, pBytes, byteCount);
            }
            bytes[byteCount] = 0;
            return bytes;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        if (_world != null)
        {
            NativeMethods.typst_context_free(_world);
        }
        _lock.Dispose();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    ~TypstCompiler()
    {
        Dispose();
    }
}
