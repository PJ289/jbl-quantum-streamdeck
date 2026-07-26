// Compile-time stubs for QuantumServer public surface used by QuantumBridge.
// At runtime RuntimeBootstrap loads the real QuantumServer.dll from Quantum Engine.
// These stubs exist so CI can compile without Quantum Engine installed.

using System.Runtime.InteropServices;

namespace QEDebug
{
    public static class Log
    {
        public enum OutputType
        {
            ONLY_FILE = 0,
        }

        public static bool Initialize(string path, string name, OutputType type, ulong maxSize) => false;

        public static void Uninitialize() { }

        public static void Info(string message, string source, int code) { }
    }
}

namespace QESHM
{
    public static class Global
    {
        public static bool Initialize(string logPath) => false;

        public static void Uninitialize() { }
    }
}

namespace QEIPC
{
    public static class Global
    {
        public static bool Initialize(string logPath) => false;

        public static void Uninitialize() { }
    }

    public class Client
    {
        public bool Create(ushort port, string clientId, string clientInfo, bool bWatchServiceConnectionState) => false;

        public void Destroy() { }
    }

    public class ClientIPC
    {
        public virtual bool Start(Client client, string path) => false;

        public virtual void Stop() { }

        public virtual bool IsStarted() => false;

        public virtual IntPtr SendMessage(IPC_MSG msg, uint responseMsgId, uint timeoutMs) => IntPtr.Zero;

        public virtual void ReleaseMsg(IntPtr msg) { }

        protected virtual void OnMessage(IntPtr msg) { }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct IPC_MSG
    {
        public uint MsgId;

        public IPC_MSG(uint msgId) => MsgId = msgId;
    }
}

namespace QECommon
{
    public class ClientIPC : QEIPC.ClientIPC
    {
    }

    public interface IConfigListener
    {
        void OnIPCPortChanged(ushort ipcPort);
    }

    public struct STRING
    {
        public string Name;

        public STRING(string name) => Name = name;
    }

    public enum ANCState : byte
    {
        OFF = 0,
        ANC = 1,
        TALK_THROUGH = 2,
        AMBIENT_AWARE = 3,
    }

    public struct ANC_STATE
    {
        public ANCState eValue;
    }

    public enum SidetoneLevel : byte
    {
    }

    public struct SIDETONE_LEVEL
    {
        public SidetoneLevel eValue;
    }

    public struct HIDV3_STATUS_LEVEL
    {
        public byte Info;
    }

    public struct BATTERY_STATUS
    {
        public byte BatteryPercentage;
        public byte IsCharging;
    }

    public struct HIDV3_DEVICE_BATTERY_STATUS
    {
        public byte Info;
    }

    public enum DeviceStatus : int
    {
        ONLINE = 0,
        READY = 1,
    }

    public struct PRODUCT_INFO
    {
        public string ProductType;
        public string DeviceID;
        public DeviceStatus eStatus;
    }

    public struct PROFILE_PRESET_DATA
    {
        public string Name;
        public uint Color;
        public string Id;
        public bool IsSystem;
        public bool IsModify;
        public bool IsDisable;
    }

    public static class ProductManagement
    {
        public abstract class BaseListener
        {
            protected bool Subscribe(ClientIPC ipc) => false;

            protected void Unsubscribe(ClientIPC ipc) { }

            protected virtual void OnProductOnline(in PRODUCT_INFO device) { }

            protected virtual void OnProductReady(in PRODUCT_INFO device) { }

            protected virtual void OnProductOffline(in PRODUCT_INFO device) { }

            protected virtual void OnProductNotReady(in PRODUCT_INFO device) { }

            protected virtual void OnProductList(PRODUCT_INFO[] deviceList, uint validCount) { }
        }
    }

    public static class ANCStateProp
    {
        public class Client
        {
            public bool GetSync(ClientIPC ipc, ref ANC_STATE value, uint timeoutMs) => false;

            public void Set(ClientIPC ipc, ref ANC_STATE value) { }
        }
    }

    public static class BatteryLevelProp
    {
        public class Client
        {
            public bool GetSync(ClientIPC ipc, ref byte value, uint timeoutMs) => false;
        }
    }

    public static class OverlayBatteryProp
    {
        public class Client
        {
            public bool GetSync(ClientIPC ipc, ref BATTERY_STATUS value, uint timeoutMs) => false;
        }
    }

    public static class SoftwareSidetoneLevelProp
    {
        public class Client
        {
            public bool GetSync(ClientIPC ipc, ref SIDETONE_LEVEL value, uint timeoutMs) => false;

            public void Set(ClientIPC ipc, ref SIDETONE_LEVEL value) { }
        }
    }

    public static class SidetoneLevelProp
    {
        public class Client
        {
            public bool GetSync(ClientIPC ipc, ref SIDETONE_LEVEL value, uint timeoutMs) => false;
        }
    }

    public static class HIDV3SidetoneProp
    {
        public class Client
        {
            public bool GetSync(ClientIPC ipc, ref HIDV3_STATUS_LEVEL value, uint timeoutMs) => false;
        }
    }

    public static class HIDV3MicVolumeProp
    {
        public class Client
        {
            public void Set(ClientIPC ipc, ref byte value) { }
        }
    }

    public static class GameChatBalanceProp
    {
        public class Client
        {
            public void Set(ClientIPC ipc, ref byte value) { }
        }
    }

    public static class HIDV3LeftDeviceBatteryProp
    {
        public class Client
        {
            public bool GetSync(ClientIPC ipc, ref HIDV3_DEVICE_BATTERY_STATUS value, uint timeoutMs) => false;
        }
    }

    public static class ProfileSwitchPresetProp
    {
        public class Client
        {
            public void Set(ClientIPC ipc, ref STRING value) { }
        }
    }

    public static class ProfileCurrentPresetProp
    {
        public class Client
        {
            public bool GetSync(ClientIPC ipc, ref STRING value, uint timeoutMs) => false;
        }
    }

    public static class ProfilePresetsProp
    {
        public class Client
        {
            public bool GetSync(ClientIPC ipc, ref PROFILE_PRESET_DATA[] value, uint timeoutMs) => false;
        }
    }
}

namespace QEConfig
{
    public class Client
    {
        public ushort IPCPort { get; set; }

        public bool Start(QECommon.IConfigListener listener) => false;

        public void Stop() { }
    }
}
