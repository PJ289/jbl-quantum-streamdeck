namespace JblQuantumBridge;

/// <summary>
/// Process entry. Bootstraps Quantum Engine assemblies before touching Program (which references them).
/// </summary>
internal static class EntryPoint
{
    public static async Task<int> Main()
    {
        try
        {
            var enginePath = RuntimeBootstrap.ResolveEnginePath();
            RuntimeBootstrap.Initialize(enginePath);
            return await Program.RunAsync(enginePath);
        }
        catch (Exception ex)
        {
            await Console.Out.WriteLineAsync(
                System.Text.Json.JsonSerializer.Serialize(new { ok = false, error = ex.Message }));
            return 1;
        }
    }
}
