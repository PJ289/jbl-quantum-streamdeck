using QECommon;
using QEIPC;

namespace JblQuantumBridge;

/// <summary>
/// Quantum Engine user profiles (presets) live on the QE_PROFILE IPC channel,
/// not on the device channel. Switching applies EQ, spatial, etc. as saved in Engine.
/// </summary>
internal sealed class ProfileSession
{
    private const string ProfileIpcPath = "QE_PROFILE";
    private const uint SyncTimeoutMs = 5000;

    private readonly QECommon.ClientIPC _ipc = new();

    public bool IsStarted => _ipc.IsStarted();

    public bool Connect(QEIPC.Client client)
    {
        if (_ipc.IsStarted())
        {
            return true;
        }

        if (!((QEIPC.ClientIPC)_ipc).Start(client, ProfileIpcPath))
        {
            return false;
        }

        Thread.Sleep(200);
        return true;
    }

    public void Disconnect()
    {
        if (_ipc.IsStarted())
        {
            _ipc.Stop();
        }
    }

    public object ListProfiles()
    {
        var presets = ReadUniquePresets();
        var current = ReadCurrentName();
        return new
        {
            ok = true,
            current,
            profiles = presets.Select(DescribePreset).ToArray(),
        };
    }

    public object GetCurrentProfile()
    {
        var current = ReadCurrentName();
        if (string.IsNullOrWhiteSpace(current))
        {
            throw new InvalidOperationException("Could not read current profile.");
        }

        var match = ReadUniquePresets().FirstOrDefault(p =>
            string.Equals(p.Name, current, StringComparison.OrdinalIgnoreCase));
        return new
        {
            ok = true,
            name = current,
            color = FormatColor(match.Color),
            id = match.Id,
        };
    }

    public object SetProfile(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Profile name is required.");
        }

        name = name.Trim();
        var presets = ReadUniquePresets();
        var match = presets.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.Id, name, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(match.Name) && string.IsNullOrEmpty(match.Id))
        {
            throw new InvalidOperationException(
                $"Profile not found: {name}. Use list-profiles to see available names.");
        }

        if (match.IsDisable)
        {
            throw new InvalidOperationException($"Profile is disabled: {match.Name}");
        }

        SwitchTo(match.Name);
        return new { ok = true, name = match.Name, id = match.Id, color = FormatColor(match.Color) };
    }

    public object CycleProfile()
    {
        var presets = ReadUniquePresets()
            .Where(p => !p.IsDisable && !string.IsNullOrWhiteSpace(p.Name))
            .ToArray();

        if (presets.Length == 0)
        {
            throw new InvalidOperationException("No profiles available.");
        }

        var current = ReadCurrentName();
        var index = Array.FindIndex(
            presets,
            p => string.Equals(p.Name, current, StringComparison.OrdinalIgnoreCase));

        var next = presets[(index + 1) % presets.Length];
        SwitchTo(next.Name);
        return new
        {
            ok = true,
            name = next.Name,
            id = next.Id,
            color = FormatColor(next.Color),
            previous = current,
        };
    }

    private static object DescribePreset(PROFILE_PRESET_DATA p) => new
    {
        name = p.Name,
        id = p.Id,
        color = FormatColor(p.Color),
        isSystem = p.IsSystem,
        isDisable = p.IsDisable,
        isModify = p.IsModify,
    };

    /// <summary>Quantum stores Color as .NET ARGB (AARRGGBB).</summary>
    private static string FormatColor(uint color)
    {
        if (color == 0 || color == uint.MaxValue)
        {
            return "#FC3F2A";
        }

        var r = (color >> 16) & 0xFF;
        var g = (color >> 8) & 0xFF;
        var b = color & 0xFF;
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private void SwitchTo(string name)
    {
        var value = new STRING(name);
        new ProfileSwitchPresetProp.Client().Set(_ipc, ref value);
        // Engine applies EQ/spatial asynchronously; give the service a moment.
        Thread.Sleep(150);
    }

    private string ReadCurrentName()
    {
        var current = new STRING();
        if (!new ProfileCurrentPresetProp.Client().GetSync(_ipc, ref current, SyncTimeoutMs))
        {
            return string.Empty;
        }

        return current.Name?.Trim() ?? string.Empty;
    }

    private PROFILE_PRESET_DATA[] ReadUniquePresets()
    {
        PROFILE_PRESET_DATA[] presets = [];
        if (!new ProfilePresetsProp.Client().GetSync(_ipc, ref presets, SyncTimeoutMs) || presets is null)
        {
            throw new InvalidOperationException("Could not list profiles (is Quantum Engine running?).");
        }

        // Engine sometimes returns recent/history duplicates; keep first occurrence per Id/Name.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unique = new List<PROFILE_PRESET_DATA>();
        foreach (var preset in presets)
        {
            if (string.IsNullOrWhiteSpace(preset.Name))
            {
                continue;
            }

            var key = !string.IsNullOrWhiteSpace(preset.Id) ? preset.Id : preset.Name;
            if (!seen.Add(key))
            {
                continue;
            }

            unique.Add(preset);
        }

        return unique.ToArray();
    }
}
