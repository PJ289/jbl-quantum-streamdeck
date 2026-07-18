namespace JblQuantumBridge;

internal static class IpcBootstrap
{
    private static bool _shmInitialized;
    private static bool _ipcInitialized;
    private static bool _debugInitialized;

    public static bool Initialize(string shmLogPath, string ipcLogPath, string debugLogPath)
    {
        if (!QEDebug.Log.Initialize(debugLogPath, "QuantumBridge", QEDebug.Log.OutputType.ONLY_FILE, 5_000_000ul))
        {
            return false;
        }

        _debugInitialized = true;

        if (!QESHM.Global.Initialize(shmLogPath))
        {
            return false;
        }

        _shmInitialized = true;

        if (!QEIPC.Global.Initialize(ipcLogPath))
        {
            return false;
        }

        _ipcInitialized = true;
        return true;
    }

    public static void Uninitialize()
    {
        if (_ipcInitialized)
        {
            QEIPC.Global.Uninitialize();
            _ipcInitialized = false;
        }

        if (_shmInitialized)
        {
            QESHM.Global.Uninitialize();
            _shmInitialized = false;
        }

        if (_debugInitialized)
        {
            try
            {
                QEDebug.Log.Uninitialize();
            }
            catch
            {
                // Best-effort shutdown.
            }

            _debugInitialized = false;
        }
    }
}
