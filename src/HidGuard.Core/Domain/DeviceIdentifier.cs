namespace HidGuard.Core.Domain;

public sealed record DeviceIdentifier
{
    public string Path { get; }
    public ushort VendorId { get; }
    public ushort ProductId { get; }

    public DeviceIdentifier(string path, ushort vendorId, ushort productId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = path.Trim();
        VendorId = vendorId;
        ProductId = productId;
    }

    public string FormattedHardwareId => $"VID_{VendorId:X4}&PID_{ProductId:X4}";

    public static DeviceIdentifier Synthetic { get; } = new("SOFTWARE_INJECTED_INPUT", 0, 0);

    public override string ToString() => FormattedHardwareId;

    public static bool TryParseIds(string path, out ushort vendorId, out ushort productId)
    {
        vendorId = 0;
        productId = 0;

        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var vidIndex = path.IndexOf("VID_", StringComparison.OrdinalIgnoreCase);
        var pidIndex = path.IndexOf("PID_", StringComparison.OrdinalIgnoreCase);

        if (vidIndex < 0 || pidIndex < 0 || path.Length < vidIndex + 8 || path.Length < pidIndex + 8)
        {
            return false;
        }

        var vidHex = path.Substring(vidIndex + 4, 4);
        var pidHex = path.Substring(pidIndex + 4, 4);

        if (ushort.TryParse(vidHex, System.Globalization.NumberStyles.HexNumber, null, out vendorId) &&
            ushort.TryParse(pidHex, System.Globalization.NumberStyles.HexNumber, null, out productId))
        {
            return true;
        }

        return false;
    }
}
