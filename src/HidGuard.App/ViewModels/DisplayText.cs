using HidGuard.Core.Audit;

namespace HidGuard.App.ViewModels;

public static class StatusPalette
{
    public const string Success = "#2E7D32";
    public const string Warning = "#B26A00";
    public const string Danger = "#C62828";
    public const string Info = "#1F4E8C";
    public const string Neutral = "#6B7280";
    public const string Calm = "#4F7CAC";
}

public static class DisplayText
{
    public static string ForEventType(AuditEventType type) => type switch
    {
        AuditEventType.DeviceConnected => "Cihaz bağlandı",
        AuditEventType.DeviceDisconnected => "Cihaz çıkarıldı",
        AuditEventType.DeviceTrustModified => "Güven durumu değişti",
        AuditEventType.AnomalyDetected => "Anomali tespit edildi",
        AuditEventType.CountermeasureExecuted => "Karşı önlem uygulandı",
        AuditEventType.ConfigurationChanged => "Yapılandırma değişti",
        _ => type.ToString()
    };
}
