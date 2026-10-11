using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input;
using HidGuard.Core.Domain;

namespace HidGuard.Platform.Windows;

internal sealed class RawInputDeviceResolver
{
    private readonly ConcurrentDictionary<nint, DeviceIdentifier> cache = new();

    public unsafe DeviceIdentifier Resolve(HANDLE hDevice)
    {
        var rawHandle = (nint)hDevice.Value;

        if (rawHandle == 0)
        {
            return DeviceIdentifier.Synthetic;
        }

        if (cache.TryGetValue(rawHandle, out var cached))
        {
            return cached;
        }

        var path = QueryDevicePath(hDevice);

        if (string.IsNullOrWhiteSpace(path))
        {
            var fallback = new DeviceIdentifier($"UNKNOWN_DEVICE_{rawHandle:X}", 0, 0);
            cache[rawHandle] = fallback;
            return fallback;
        }

        DeviceIdentifier resolved;
        if (DeviceIdentifier.TryParseIds(path, out var vid, out var pid))
        {
            resolved = new DeviceIdentifier(path, vid, pid);
        }
        else
        {
            resolved = new DeviceIdentifier(path, 0, 0);
        }

        cache[rawHandle] = resolved;
        return resolved;
    }

    public void Invalidate(HANDLE hDevice)
    {
        cache.TryRemove((nint)hDevice.Value, out _);
    }

    public unsafe IReadOnlyList<DeviceIdentifier> EnumerateKeyboards()
    {
        var deviceCount = 0u;
        var structSize = (uint)sizeof(RAWINPUTDEVICELIST);

        _ = PInvoke.GetRawInputDeviceList(null, &deviceCount, structSize);

        if (deviceCount == 0)
        {
            return Array.Empty<DeviceIdentifier>();
        }

        var rawList = new RAWINPUTDEVICELIST[deviceCount];
        fixed (RAWINPUTDEVICELIST* pRawList = rawList)
        {
            var result = PInvoke.GetRawInputDeviceList(pRawList, &deviceCount, structSize);
            if (result == uint.MaxValue)
            {
                return Array.Empty<DeviceIdentifier>();
            }
        }

        var keyboards = new List<DeviceIdentifier>();

        for (var i = 0; i < deviceCount; i++)
        {
            var item = rawList[i];
            if (item.dwType == RID_DEVICE_INFO_TYPE.RIM_TYPEKEYBOARD)
            {
                var identifier = Resolve(item.hDevice);
                if (identifier != DeviceIdentifier.Synthetic)
                {
                    keyboards.Add(identifier);
                }
            }
        }

        return keyboards;
    }

    private static unsafe string? QueryDevicePath(HANDLE hDevice)
    {
        var charCount = 0u;
        _ = PInvoke.GetRawInputDeviceInfo(
            hDevice,
            RAW_INPUT_DEVICE_INFO_COMMAND.RIDI_DEVICENAME,
            null,
            &charCount);

        if (charCount == 0)
        {
            return null;
        }

        var buffer = new char[charCount];
        fixed (char* pBuffer = buffer)
        {
            var result = PInvoke.GetRawInputDeviceInfo(
                hDevice,
                RAW_INPUT_DEVICE_INFO_COMMAND.RIDI_DEVICENAME,
                pBuffer,
                &charCount);

            if (result == uint.MaxValue || result == 0)
            {
                return null;
            }

            return new string(buffer, 0, (int)result).TrimEnd('\0');
        }
    }
}
