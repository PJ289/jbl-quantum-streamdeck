using System.Text.Json;
using System.Text.Json.Serialization;
using QECommon;
using QEIPC;

namespace JblQuantumBridge;

internal static class Program
{
    private const uint SyncTimeoutMs = 5000;
    private const string ClientInfo = "QE_APPLICATION";
    private const string ServiceIpcPath = "QE_SERVICE_APP";

    private static ServiceEndpoint? _endpoint;
    private static QEIPC.Client? _client;
    private static QECommon.ClientIPC? _serviceIpc;
    private static DeviceSession? _device;
    private static ProfileSession? _profiles;

    public static async Task<int> RunAsync(string enginePath)
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QuantumENGINE",
            "StreamDeckBridge"
        );
        Directory.CreateDirectory(logDir);
        var shmLogPath = Path.Combine(logDir, "QuantumClientSHM.log");
        var ipcLogPath = Path.Combine(logDir, "QuantumClientIPC.log");
        var debugLogPath = Path.Combine(logDir, "QuantumBridge.log");

        if (!IpcBootstrap.Initialize(shmLogPath, ipcLogPath, debugLogPath))
        {
            await WriteErrorAsync("QEIPC/QESHM initialize failed.");
            return 1;
        }

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            Disconnect();
            IpcBootstrap.Uninitialize();
        };

        if (!Connect(out var connectError))
        {
            await WriteErrorAsync(connectError ?? "Could not connect to QuantumServer.");
            return 1;
        }

        await WriteJsonAsync(new
        {
            ok = true,
            @event = "ready",
            port = _endpoint?.Port,
            deviceId = _device?.DeviceId,
            productType = _device?.ProductType,
            profiles = _profiles?.IsStarted == true,
        });

        string? line;
        while ((line = await Console.In.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var request = JsonSerializer.Deserialize<BridgeRequest>(line);
                if (request?.Cmd is null)
                {
                    await WriteErrorAsync("Missing cmd field.");
                    continue;
                }

                var response = HandleCommand(request);
                await WriteJsonAsync(response);
            }
            catch (Exception ex)
            {
                await WriteErrorAsync(ex.Message);
            }
        }

        Disconnect();
        return 0;
    }

    private static bool Connect(out string? error)
    {
        error = null;

        _endpoint = new ServiceEndpoint();
        if (!_endpoint.TryStart(out error))
        {
            return false;
        }

        var clientId = $"{Environment.ProcessId:X16}";
        _client = new QEIPC.Client();
        if (!_client.Create(_endpoint.Port, clientId, ClientInfo, bWatchServiceConnectionState: true))
        {
            error = $"QEIRegisterAsClient failed on port {_endpoint.Port}.";
            return false;
        }

        _serviceIpc = new QECommon.ClientIPC();
        if (!((QEIPC.ClientIPC)_serviceIpc).Start(_client, ServiceIpcPath))
        {
            error = "Could not start QE_SERVICE_APP IPC channel.";
            return false;
        }

        // ConnectIPC completes asynchronously ("Start request" → "Finish request").
        Thread.Sleep(200);

        _device = new DeviceSession();
        if (!_device.Connect(_client))
        {
            var detail = string.IsNullOrWhiteSpace(_device.DiscoveryLog)
                ? "no discovery log"
                : _device.DiscoveryLog.Trim();
            error =
                "Could not discover/connect to headset. Ensure Quantum Engine is running with Q810 connected. "
                + "Optional: set QUANTUM_DEVICE_ID. Details: "
                + detail;
            return false;
        }

        _profiles = new ProfileSession();
        if (!_profiles.Connect(_client))
        {
            // Profiles are optional for ANC/battery; keep device session usable.
            _profiles.Disconnect();
            _profiles = null;
        }

        return true;
    }

    private static void Disconnect()
    {
        _profiles?.Disconnect();
        _device?.Disconnect();
        _serviceIpc?.Stop();
        _client?.Destroy();
        _endpoint?.Dispose();
        _profiles = null;
        _device = null;
        _serviceIpc = null;
        _client = null;
        _endpoint = null;
    }

    private static QECommon.ClientIPC RequireDeviceIpc()
    {
        if (_device?.DeviceIpc.IsStarted() != true)
        {
            throw new InvalidOperationException("Device IPC not connected.");
        }

        return _device.DeviceIpc;
    }

    private static object HandleCommand(BridgeRequest request)
    {
        var cmd = request.Cmd.ToLowerInvariant();
        if (cmd is "list-profiles" or "get-profile" or "set-profile" or "cycle-profile")
        {
            var profiles = RequireProfiles();
            return cmd switch
            {
                "list-profiles" => profiles.ListProfiles(),
                "get-profile" => profiles.GetCurrentProfile(),
                "set-profile" => profiles.SetProfile(request.RequireString("value")),
                "cycle-profile" => profiles.CycleProfile(),
                _ => throw new InvalidOperationException($"Unknown cmd: {request.Cmd}"),
            };
        }

        var ipc = RequireDeviceIpc();
        return cmd switch
        {
            "ping" => new { ok = true, pong = true },
            "get-status" => GetStatus(ipc),
            "get-anc" => GetAnc(ipc),
            "set-anc" => SetAnc(ipc, request.RequireInt("value", 0, 3)),
            "cycle-anc" => CycleAnc(ipc),
            "toggle-anc" => ToggleAnc(ipc),
            "get-battery" => GetBattery(ipc),
            "set-sidetone" => SetSidetone(ipc, request.RequireInt("value", 0, 4)),
            "get-sidetone" => GetSidetone(ipc),
            "set-mic-volume" => SetMicVolume(ipc, request.RequireInt("value", 0, 100)),
            "set-game-chat-balance" => SetGameChatBalance(ipc, request.RequireInt("value", 0, 100)),
            _ => throw new InvalidOperationException($"Unknown cmd: {request.Cmd}"),
        };
    }

    private static ProfileSession RequireProfiles()
    {
        if (_profiles?.IsStarted != true)
        {
            throw new InvalidOperationException(
                "Profile IPC (QE_PROFILE) not connected. Is Quantum Engine / QuantumService running?");
        }

        return _profiles;
    }

    private static object GetStatus(QECommon.ClientIPC ipc)
    {
        var anc = ReadAnc(ipc);
        var battery = ReadBattery(ipc);
        return new
        {
            ok = true,
            anc = (int)anc.eValue,
            ancName = anc.eValue.ToString(),
            batteryPercent = battery.Percent,
            batteryCharging = battery.Charging,
            batterySource = battery.Source,
        };
    }

    private static object GetAnc(QECommon.ClientIPC ipc)
    {
        var state = ReadAnc(ipc);
        return new { ok = true, anc = (int)state.eValue, name = state.eValue.ToString() };
    }

    private static object SetAnc(QECommon.ClientIPC ipc, int value)
    {
        var ancClient = new ANCStateProp.Client();
        var state = new ANC_STATE { eValue = (ANCState)value };
        ancClient.Set(ipc, ref state);
        return new { ok = true, anc = value, name = state.eValue.ToString() };
    }

    private static object CycleAnc(QECommon.ClientIPC ipc)
    {
        var current = ReadAnc(ipc);
        var next = (ANCState)(((int)current.eValue + 1) % 4);
        var state = new ANC_STATE { eValue = next };
        new ANCStateProp.Client().Set(ipc, ref state);
        return new { ok = true, anc = (int)next, name = next.ToString() };
    }

    /// <summary>OFF ↔ ANC only (skips Talk Through / Ambient Aware).</summary>
    private static object ToggleAnc(QECommon.ClientIPC ipc)
    {
        var current = ReadAnc(ipc);
        var next = (int)current.eValue == 1 ? ANCState.OFF : ANCState.ANC;
        var state = new ANC_STATE { eValue = next };
        new ANCStateProp.Client().Set(ipc, ref state);
        return new { ok = true, anc = (int)next, name = next.ToString() };
    }

    private static object GetBattery(QECommon.ClientIPC ipc)
    {
        var battery = ReadBattery(ipc);
        return new
        {
            ok = true,
            percent = battery.Percent,
            charging = battery.Charging,
            source = battery.Source,
        };
    }

    private static object GetSidetone(QECommon.ClientIPC ipc)
    {
        var software = new SIDETONE_LEVEL();
        if (new SoftwareSidetoneLevelProp.Client().GetSync(ipc, ref software, SyncTimeoutMs))
        {
            return new { ok = true, sidetone = (int)software.eValue, source = "SoftwareSidetoneLevelProp" };
        }

        var device = new SIDETONE_LEVEL();
        if (new SidetoneLevelProp.Client().GetSync(ipc, ref device, SyncTimeoutMs))
        {
            return new { ok = true, sidetone = (int)device.eValue, source = "SidetoneLevelProp" };
        }

        var hid = new HIDV3_STATUS_LEVEL();
        if (new HIDV3SidetoneProp.Client().GetSync(ipc, ref hid, SyncTimeoutMs))
        {
            return new { ok = true, sidetone = (int)hid.Info, source = "HIDV3SidetoneProp" };
        }

        throw new InvalidOperationException("Could not read sidetone (device connected?).");
    }

    private static object SetSidetone(QECommon.ClientIPC ipc, int value)
    {
        var software = new SIDETONE_LEVEL { eValue = (SidetoneLevel)value };
        new SoftwareSidetoneLevelProp.Client().Set(ipc, ref software);
        return new { ok = true, sidetone = value };
    }

    private static object SetMicVolume(QECommon.ClientIPC ipc, int value)
    {
        var micClient = new HIDV3MicVolumeProp.Client();
        byte volume = (byte)value;
        micClient.Set(ipc, ref volume);
        return new { ok = true, micVolume = value };
    }

    private static object SetGameChatBalance(QECommon.ClientIPC ipc, int value)
    {
        var balanceClient = new GameChatBalanceProp.Client();
        byte balance = (byte)value;
        balanceClient.Set(ipc, ref balance);
        return new { ok = true, balance = value };
    }

    private static ANC_STATE ReadAnc(QECommon.ClientIPC ipc)
    {
        var ancClient = new ANCStateProp.Client();
        var state = new ANC_STATE();
        if (!ancClient.GetSync(ipc, ref state, SyncTimeoutMs))
        {
            throw new InvalidOperationException("Could not read ANC state (is Quantum Engine running with Q810 connected?).");
        }

        return state;
    }

    private static BatteryReading ReadBattery(QECommon.ClientIPC ipc)
    {
        var overlay = new OverlayBatteryProp.Client();
        var overlayStatus = new BATTERY_STATUS();
        if (overlay.GetSync(ipc, ref overlayStatus, SyncTimeoutMs))
        {
            return new BatteryReading(
                overlayStatus.BatteryPercentage,
                overlayStatus.IsCharging != 0,
                "OverlayBatteryProp"
            );
        }

        var levelClient = new BatteryLevelProp.Client();
        byte level = 0;
        if (levelClient.GetSync(ipc, ref level, SyncTimeoutMs))
        {
            return new BatteryReading(level, false, "BatteryLevelProp");
        }

        var hidClient = new HIDV3LeftDeviceBatteryProp.Client();
        var hidStatus = new HIDV3_DEVICE_BATTERY_STATUS();
        if (hidClient.GetSync(ipc, ref hidStatus, SyncTimeoutMs))
        {
            return new BatteryReading(hidStatus.Info, false, "HIDV3LeftDeviceBatteryProp");
        }

        throw new InvalidOperationException("Could not read battery from any known property.");
    }

    private static Task WriteJsonAsync(object payload) =>
        Console.Out.WriteLineAsync(JsonSerializer.Serialize(payload));

    private static Task WriteErrorAsync(string message) =>
        WriteJsonAsync(new { ok = false, error = message });

    private readonly record struct BatteryReading(byte Percent, bool Charging, string Source);
}

internal sealed class BridgeRequest
{
    [JsonPropertyName("cmd")]
    public string? Cmd { get; set; }

    [JsonPropertyName("value")]
    public JsonElement? Value { get; set; }

    public int RequireInt(string fieldName, int min, int max)
    {
        if (Value is not { } element || element.ValueKind != JsonValueKind.Number)
        {
            throw new ArgumentException($"Missing numeric {fieldName}.");
        }

        var value = element.GetInt32();
        if (value < min || value > max)
        {
            throw new ArgumentException($"{fieldName} must be between {min} and {max}.");
        }

        return value;
    }

    public string RequireString(string fieldName)
    {
        if (Value is not { } element || element.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException($"Missing string {fieldName}.");
        }

        var value = element.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} cannot be empty.");
        }

        return value;
    }
}
