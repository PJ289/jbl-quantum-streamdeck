using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using QECommon;
using QEIPC;

namespace JblQuantumBridge;

internal sealed class DeviceSession : ProductManagement.BaseListener
{
    private const string ProductManagementPath = "QE_PRODUCT_MANAGEMENT";
    private const uint SubscribeMsgId = 6;
    private const uint DeviceListMsgId = 8;
    private const uint SyncTimeoutMs = 3000;

    private readonly LoggingClientIpc _productIpc = new();
    private readonly QECommon.ClientIPC _deviceIpc = new();
    private readonly StringBuilder _discoveryLog = new();
    private PRODUCT_INFO? _selected;

    public QECommon.ClientIPC DeviceIpc => _deviceIpc;

    public string? DeviceId { get; private set; }

    public string? ProductType { get; private set; }

    public string DiscoveryLog => _discoveryLog.ToString();

    public bool Connect(QEIPC.Client client)
    {
        _productIpc.OnRawMessage = msgId => Log($"RAW product IPC msgId={msgId}");

        var overrideDeviceId = Environment.GetEnvironmentVariable("QUANTUM_DEVICE_ID");
        if (!string.IsNullOrWhiteSpace(overrideDeviceId))
        {
            Log($"Using QUANTUM_DEVICE_ID override: {overrideDeviceId}");
            return ConnectToDevice(client, overrideDeviceId.Trim(), requireHealthy: false);
        }

        if (TryDiscoverViaProductManagement(client))
        {
            return true;
        }

        if (TryDiscoverFromQuantumClientLog(client))
        {
            return true;
        }

        return TryHeuristicDeviceIds(client);
    }

    public void Disconnect()
    {
        _deviceIpc.Stop();
        DeviceId = null;
        ProductType = null;
    }

    private bool TryDiscoverViaProductManagement(QEIPC.Client client)
    {
        if (!_productIpc.Start(client, ProductManagementPath))
        {
            Log("Failed to start QE_PRODUCT_MANAGEMENT IPC.");
            return false;
        }

        Log("QE_PRODUCT_MANAGEMENT connected.");

        try
        {
            if (!Subscribe(_productIpc))
            {
                Log("ProductManagement.Subscribe failed.");
                return false;
            }

            Log("Subscribed; waiting briefly for async list...");
            Thread.Sleep(500);

            if (_selected is { } asyncDevice)
            {
                ProductType = asyncDevice.ProductType;
                Log($"Selected async {asyncDevice.ProductType} id={asyncDevice.DeviceID}");
                return ConnectToDevice(client, asyncDevice.DeviceID);
            }

            Log("No async list; trying sync SUBSCRIBE...");
            var device = RequestDeviceListSync();
            if (device is not null)
            {
                ProductType = device.Value.ProductType;
                Log($"Selected sync {device.Value.ProductType} id={device.Value.DeviceID}");
                return ConnectToDevice(client, device.Value.DeviceID);
            }

            Log("Product management returned no devices.");
            return false;
        }
        finally
        {
            try
            {
                if (_productIpc.IsStarted())
                {
                    Unsubscribe(_productIpc);
                }
            }
            catch
            {
                // Ignore cleanup errors.
            }

            _productIpc.Stop();
        }
    }

    private PRODUCT_INFO? RequestDeviceListSync()
    {
        var response = ((QEIPC.ClientIPC)_productIpc).SendMessage(
            new IPC_MSG(SubscribeMsgId),
            DeviceListMsgId,
            SyncTimeoutMs);

        if (response == IntPtr.Zero)
        {
            Log("Sync SUBSCRIBE timed out or returned null.");
            return null;
        }

        try
        {
            return ParseDeviceList(response);
        }
        finally
        {
            _productIpc.ReleaseMsg(response);
        }
    }

    private PRODUCT_INFO? ParseDeviceList(IntPtr msgBuf)
    {
        var count = (uint)Marshal.ReadInt32(msgBuf, 8);
        Log($"Device list count={count}");

        var offset = Marshal.SizeOf<IPC_MSG>() + 4;
        PRODUCT_INFO? preferred = null;
        PRODUCT_INFO? fallback = null;

        for (var i = 0; i < count; i++)
        {
            var device = Marshal.PtrToStructure<PRODUCT_INFO>(msgBuf + offset);
            offset += Marshal.SizeOf<PRODUCT_INFO>();
            Log($"  [{i}] type={device.ProductType} status={device.eStatus} id={device.DeviceID}");

            if (device.eStatus != DeviceStatus.READY && device.eStatus != DeviceStatus.ONLINE)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(device.DeviceID))
            {
                continue;
            }

            fallback ??= device;
            if (IsPreferredProduct(device.ProductType))
            {
                preferred = device;
            }
        }

        return preferred ?? fallback;
    }

    private bool ConnectToDevice(QEIPC.Client client, string deviceId, bool requireHealthy = true)
    {
        if (!_deviceIpc.Start(client, deviceId))
        {
            Log($"Failed to start device IPC channel: {deviceId}");
            return false;
        }

        // ConnectIPC is asynchronous at the native layer ("Start request"); give it a moment.
        Thread.Sleep(750);

        if (requireHealthy && !DeviceChannelLooksHealthy())
        {
            Log($"Channel opened but properties unavailable: {deviceId}");
            _deviceIpc.Stop();
            return false;
        }

        DeviceId = deviceId;
        Log($"Device IPC started: {deviceId}");
        return true;
    }

    private bool DeviceChannelLooksHealthy()
    {
        try
        {
            var ancClient = new ANCStateProp.Client();
            var anc = new ANC_STATE();
            if (ancClient.GetSync(_deviceIpc, ref anc, 2500))
            {
                Log($"Health OK via ANCStateProp (anc={(int)anc.eValue}).");
                return true;
            }
        }
        catch
        {
            // Ignore and try other props.
        }

        try
        {
            var batteryClient = new BatteryLevelProp.Client();
            byte level = 0;
            if (batteryClient.GetSync(_deviceIpc, ref level, 2500))
            {
                Log($"Health OK via BatteryLevelProp (level={level}).");
                return true;
            }
        }
        catch
        {
            // Ignore and try other props.
        }

        try
        {
            var overlay = new OverlayBatteryProp.Client();
            var status = new BATTERY_STATUS();
            if (overlay.GetSync(_deviceIpc, ref status, 2500))
            {
                Log($"Health OK via OverlayBatteryProp (level={status.BatteryPercentage}).");
                return true;
            }
        }
        catch
        {
            // Ignore.
        }

        return false;
    }

    private bool TryDiscoverFromQuantumClientLog(QEIPC.Client client)
    {
        // Quantum Engine logs the live DeviceID as: "Product device online. Q810: Q810/USB\..."
        var logPath = Path.Combine(Path.GetTempPath(), "QuantumENGINE", "QuantumClient.log");
        if (!File.Exists(logPath))
        {
            Log($"QuantumClient.log not found at {logPath}");
            return false;
        }

        Log($"Scanning {logPath} for DeviceID...");
        string? latest = null;
        try
        {
            using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } line)
            {
                const string marker = "Product device online. Q810: ";
                var idx = line.IndexOf(marker, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    latest = line[(idx + marker.Length)..].Trim();
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Failed reading QuantumClient.log: {ex.Message}");
            return false;
        }

        if (string.IsNullOrWhiteSpace(latest))
        {
            Log("No Q810 DeviceID found in QuantumClient.log.");
            return false;
        }

        Log($"Found DeviceID in log: {latest}");
        ProductType = "Q810";
        return ConnectToDevice(client, latest, requireHealthy: false);
    }

    private bool TryHeuristicDeviceIds(QEIPC.Client client)
    {
        Log("Trying heuristic device IPC names (validated)...");
        foreach (var candidate in EnumerateCandidateDeviceIds())
        {
            // Only probe the most likely Q810/USB\... forms to keep startup fast.
            if (!candidate.StartsWith("Q810/USB\\", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Log($"Probe: {candidate}");
            if (ConnectToDevice(client, candidate, requireHealthy: true))
            {
                ProductType ??= "Q810";
                return true;
            }
        }

        Log("No healthy heuristic device IPC name worked.");
        return false;
    }

    private static IEnumerable<string> EnumerateCandidateDeviceIds()
    {
        // Prefer Q810/<id> first — that matches HIDV3 GetDeviceID() in QuantumServer.
        var preferred = new List<string>();
        var fallback = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddPreferred(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value) && seen.Add(value!))
            {
                preferred.Add(value!);
            }
        }

        void AddFallback(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value) && seen.Add(value!))
            {
                fallback.Add(value!);
            }
        }

        foreach (var instanceId in QueryConnectedQuantumInstanceIds())
        {
            var slashId = instanceId.Replace('\\', '/');
            var leaf = instanceId.Contains('\\') ? instanceId[(instanceId.LastIndexOf('\\') + 1)..] : instanceId;

            AddPreferred("Q810/" + instanceId);
            AddPreferred("Q810/" + slashId);
            AddPreferred("Q810/" + leaf);
            AddFallback(instanceId);
            AddFallback(slashId);
            AddFallback(leaf);
        }

        AddFallback("QE_OSC_PRODUCTS");

        foreach (var id in preferred)
        {
            yield return id;
        }

        foreach (var id in fallback)
        {
            yield return id;
        }
    }

    private static IEnumerable<string> QueryConnectedQuantumInstanceIds()
    {
        const string usbRoot = @"SYSTEM\CurrentControlSet\Enum\USB";
        const string hidRoot = @"SYSTEM\CurrentControlSet\Enum\HID";

        foreach (var id in EnumerateEnumIds(usbRoot, "VID_0ECB&PID_2069"))
        {
            yield return id;
        }

        foreach (var id in EnumerateEnumIds(hidRoot, "VID_0ECB&PID_2069"))
        {
            yield return id;
        }
    }

    private static IEnumerable<string> EnumerateEnumIds(string root, string vidPidToken)
    {
        using var rootKey = Registry.LocalMachine.OpenSubKey(root);
        if (rootKey is null)
        {
            yield break;
        }

        var bus = root.EndsWith(@"\USB", StringComparison.OrdinalIgnoreCase) ? "USB" : "HID";
        foreach (var deviceClass in rootKey.GetSubKeyNames())
        {
            if (!deviceClass.Contains(vidPidToken, StringComparison.OrdinalIgnoreCase)
                && !(deviceClass.Contains("VID_0ECB", StringComparison.OrdinalIgnoreCase)
                     && deviceClass.Contains("PID_2069", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            using var classKey = rootKey.OpenSubKey(deviceClass);
            if (classKey is null)
            {
                continue;
            }

            foreach (var instance in classKey.GetSubKeyNames())
            {
                yield return $"{bus}\\{deviceClass}\\{instance}";
            }
        }
    }

    protected override void OnProductOnline(in PRODUCT_INFO device)
    {
        Log($"ONLINE {device.ProductType} {device.DeviceID}");
        SelectIfBetter(device);
    }

    protected override void OnProductReady(in PRODUCT_INFO device)
    {
        Log($"READY {device.ProductType} {device.DeviceID}");
        SelectIfBetter(device);
    }

    protected override void OnProductOffline(in PRODUCT_INFO device) =>
        Log($"OFFLINE {device.ProductType} {device.DeviceID}");

    protected override void OnProductNotReady(in PRODUCT_INFO device) =>
        Log($"NOT_READY {device.ProductType} {device.DeviceID}");

    protected override void OnProductList(PRODUCT_INFO[] deviceList, uint validCount)
    {
        Log($"Async device list count={validCount}");
        for (var i = 0; i < validCount && i < deviceList.Length; i++)
        {
            Log($"  [{i}] type={deviceList[i].ProductType} status={deviceList[i].eStatus} id={deviceList[i].DeviceID}");
            SelectIfBetter(deviceList[i]);
        }
    }

    private void SelectIfBetter(in PRODUCT_INFO device)
    {
        if (device.eStatus != DeviceStatus.READY && device.eStatus != DeviceStatus.ONLINE)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(device.DeviceID))
        {
            return;
        }

        if (_selected is { } current && IsPreferredProduct(current.ProductType))
        {
            return;
        }

        if (_selected is null || IsPreferredProduct(device.ProductType))
        {
            _selected = device;
        }
    }

    private static bool IsPreferredProduct(string productType) =>
        !string.IsNullOrEmpty(productType)
        && (productType.Equals("Q810", StringComparison.OrdinalIgnoreCase)
            || productType.Contains("810", StringComparison.OrdinalIgnoreCase));

    private void Log(string message)
    {
        _discoveryLog.AppendLine(message);
        try
        {
            QEDebug.Log.Info(message, nameof(DeviceSession), 0);
        }
        catch
        {
            // Logging must never break discovery.
        }
    }

    private sealed class LoggingClientIpc : QECommon.ClientIPC
    {
        public Action<uint>? OnRawMessage { get; set; }

        protected override void OnMessage(IntPtr msg)
        {
            try
            {
                OnRawMessage?.Invoke((uint)Marshal.ReadInt32(msg));
            }
            catch
            {
                // Ignore logging failures.
            }

            base.OnMessage(msg);
        }
    }
}
