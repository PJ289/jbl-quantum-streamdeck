using QECommon;

namespace JblQuantumBridge;

/// <summary>
/// Resolves the live QuantumServer IPC UDP port from shared memory (QE_CONFIG_MEMORY).
/// Hardcoding 20502 is wrong — that socket exists but does not complete ConnectIPC handshakes.
/// </summary>
internal sealed class ServiceEndpoint : IConfigListener, IDisposable
{
    private readonly QEConfig.Client _config = new();
    private bool _started;

    public ushort Port { get; private set; }

    public bool TryStart(out string? error)
    {
        error = null;
        if (!_config.Start(this))
        {
            error = "QE_CONFIG_MEMORY attach failed (is QuantumService running?).";
            return false;
        }

        _started = true;
        Port = _config.IPCPort;
        if (Port == 0)
        {
            error = "QEConfig reported IPC port 0.";
            return false;
        }

        return true;
    }

    public void OnIPCPortChanged(ushort ipcPort)
    {
        Port = ipcPort;
        try
        {
            QEDebug.Log.Info($"IPC port changed to {ipcPort}", nameof(ServiceEndpoint), 0);
        }
        catch
        {
            // Ignore logging failures.
        }
    }

    public void Dispose()
    {
        if (_started)
        {
            _config.Stop();
            _started = false;
        }
    }
}
