using System.Reflection;
using System.Runtime.InteropServices;

namespace JblQuantumBridge;

/// <summary>
/// Loads Quantum Engine managed/native libraries from the installed product folder.
/// Must run before any type from QuantumServer.dll is referenced by executing code.
/// </summary>
internal static class RuntimeBootstrap
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    public static string ResolveEnginePath()
    {
        var fromEnv = Environment.GetEnvironmentVariable("QUANTUM_ENGINE_PATH")?.Trim();
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv.TrimEnd('\\', '/');
        }

        return @"C:\Program Files\JBL\QuantumENGINE";
    }

    public static void Initialize(string enginePath)
    {
        if (!Directory.Exists(enginePath))
        {
            throw new DirectoryNotFoundException($"Quantum Engine path not found: {enginePath}");
        }

        var quantumServerPath = Path.Combine(enginePath, "QuantumServer.dll");
        if (!File.Exists(quantumServerPath))
        {
            throw new FileNotFoundException(
                "QuantumServer.dll not found. Install JBL Quantum Engine or set QUANTUM_ENGINE_PATH.",
                quantumServerPath);
        }

        // Native IPC / shared-memory DLLs live next to QuantumServer in the install dir.
        SetDllDirectory(enginePath);
        Directory.SetCurrentDirectory(enginePath);

        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            var name = new AssemblyName(args.Name).Name;
            if (name is null)
            {
                return null;
            }

            var candidate = Path.Combine(enginePath, $"{name}.dll");
            return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
        };

        Assembly.LoadFrom(quantumServerPath);
    }
}
