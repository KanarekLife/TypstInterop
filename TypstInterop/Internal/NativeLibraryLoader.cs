using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace TypstInterop.Internal;

internal static class NativeLibraryLoader
{
    private static bool _initialized = false;
    private static readonly object Lock = new();

    public static void Initialize()
    {
        if (_initialized)
            return;
        lock (Lock)
        {
            if (_initialized)
                return;

#if NETCOREAPP3_0_OR_GREATER || NET5_0_OR_GREATER || NET8_0_OR_GREATER || NET9_0_OR_GREATER || NET10_0_OR_GREATER
            NativeLibrary.SetDllImportResolver(
                typeof(NativeLibraryLoader).Assembly,
                ResolveDllImport
            );
#else
            LoadForNetFramework();
#endif
            _initialized = true;
        }
    }

#if NETCOREAPP3_0_OR_GREATER || NET5_0_OR_GREATER || NET8_0_OR_GREATER || NET9_0_OR_GREATER || NET10_0_OR_GREATER
    private static IntPtr ResolveDllImport(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath
    )
    {
        if (libraryName != "typst_interop")
            return IntPtr.Zero;
        var rid = GetCurrentRid();
        var prefix = GetNativePrefix();
        var extension = GetNativeExtension();
        var libFileName = $"{prefix}typst_interop{extension}";

        var assemblyDir = GetAssemblyDirectory(assembly);
        if (assemblyDir != null)
        {
            var runtimePath = Path.Combine(assemblyDir, "runtimes", rid, "native", libFileName);
            if (File.Exists(runtimePath))
            {
                return NativeLibrary.Load(runtimePath);
            }
        }

        if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out var handle))
        {
            return handle;
        }
        return IntPtr.Zero;
    }
#endif

    private static void LoadForNetFramework()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var rid = GetCurrentRid();
        var extension = GetNativeExtension();
        var assemblyDir = GetAssemblyDirectory(Assembly.GetExecutingAssembly());
        if (assemblyDir == null)
            return;

        var libFileName = $"typst_interop{extension}";

        // Try several paths
        var pathsToTry = new List<string>
        {
            Path.Combine(assemblyDir, "runtimes", rid, "native", libFileName),
            Path.Combine(assemblyDir, libFileName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, libFileName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", libFileName),
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "runtimes",
                rid,
                "native",
                libFileName
            ),
        };

        foreach (var dllPath in pathsToTry)
        {
            if (!File.Exists(dllPath))
                continue;
            // Add the directory to the search path for dependencies
            var dir = Path.GetDirectoryName(dllPath);
            if (dir != null)
            {
                SetDllDirectory(dir);
            }

            var handle = LoadLibrary(dllPath);
            if (handle != IntPtr.Zero)
            {
                return;
            }
        }
    }

    private static string? GetAssemblyDirectory(Assembly assembly)
    {
        try
        {
            var location = assembly.Location;
            if (!string.IsNullOrEmpty(location))
            {
                return Path.GetDirectoryName(location);
            }
        }
        catch
        {
            // ignored
        }

        try
        {
#pragma warning disable SYSLIB0012
            var codeBase = assembly.CodeBase;
#pragma warning restore SYSLIB0012
            if (!string.IsNullOrEmpty(codeBase))
            {
                var uri = new Uri(codeBase);
                return Path.GetDirectoryName(uri.LocalPath);
            }
        }
        catch
        {
            // ignored
        }

        return AppDomain.CurrentDomain.BaseDirectory;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetDllDirectory(string lpPathName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    private static string GetCurrentRid()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? "win-arm64"
                : "win-x64";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? "linux-arm64"
                : "linux-x64";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? "osx-arm64"
                : "osx-x64";
        return "unknown";
    }

    private static string GetNativeExtension()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return ".dll";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return ".so";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return ".dylib";
        return "";
    }

    private static string GetNativePrefix()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "";
        return "lib";
    }
}
