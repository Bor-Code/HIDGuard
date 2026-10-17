using System.Globalization;
using System.Windows.Data;

namespace HidGuard.App.Converters;

public sealed class TurkishStatusConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string text)
        {
            return value;
        }

        return text switch
        {
            "Active Defense Engaged" => "Aktif Savunma Devrede",
            "Protection Paused (Monitoring Only)" => "Koruma Duraklatıldı (Yalnızca İzleme)",
            "Injection Attack Detected & Mitigated" => "Saldırı Tespit Edildi ve Engellendi",
            "Normal" => "Normal",
            "Normal Human Keystroke Dynamics" => "Normal İnsan Tuş Dinamikleri",
            "Injection Attack Detected" => "Enjeksiyon Saldırısı Tespit Edildi",
            "Suspicious Speed / Uniformity" => "Şüpheli Hız ve Düzenlilik",
            "Trusted" => "Güvenilir",
            "Blocked" => "Engellendi",
            "Unknown" => "Bilinmiyor",
            "Ready" => "Hazır",
            "Completed Successfully" => "Başarıyla Tamamlandı",
            "Cancelled" => "İptal Edildi",
            "Failed" => "Başarısız",
            "PASS" => "BAŞARILI",
            "FAIL" => "BAŞARISIZ",
            "Settings saved successfully." => "Ayarlar başarıyla kaydedildi.",
            "Settings reset to defaults. Click Save to apply." => "Ayarlar varsayılana sıfırlandı. Uygulamak için Kaydet'e tıklayın.",
            "None" => "Yok",
            "Recorded" => "Kaydedildi",
            "Success" => "Başarılı",
            "Allowed" => "İzin Verildi",
            "Decide Later" => "Daha Sonra Karar Ver",
            "Allow" => "İzin Ver",
            "Block" => "Engelle",
            "Locked Workstation" => "İstasyon Kilitlendi",
            "Disabled Device" => "Cihaz Devre Dışı Bırakıldı",
            "Swallowed Keystroke" => "Tuş Yutuldu",
            "Rapid Burst Injection" => "Hızlı Seri Enjeksiyon",
            "Low-Jitter Uniformity Loop" => "Düşük Sapmalı Sabit Hız Döngüsü",
            "Realistic Human Typing" => "Doğal İnsan Yazım Profili",
            "Stealth Mixed-Cadence Micro-Burst" => "Gizli Değişken Tempolu Mikro Seri",
            "Multi-Stage Exploit Pipeline" => "Çok Aşamalı İstismar Hattı",
            "Custom DuckyScript Payload" => "Özel DuckyScript Yükü",
            "Automated E2E Verification Suite" => "Uçtan Uca Otomatik Doğrulama Paketi",
            "High Velocity" => "Yüksek Hız",
            "Microcontroller" => "Mikrodenetleyici",
            "Benign Baseline" => "Temel İnsan Referansı",
            "Stealth Evasion" => "Gizli Atlatma",
            "Complex Exploit" => "Karmaşık İstismar",
            "Scripting" => "Betik Dili",
            "Compliance" => "Uyumluluk Testi",
            "Simulated Dry-Run" => "Güvenli Simülasyon",
            "Live OS Synthetic Injection" => "Gerçek İşletim Sistemi Enjeksiyonu",
            "Burst Attack (<10ms) Detection & Countermeasure" => "Seri Saldırı (<10ms) Tespiti ve Engelleme",
            "Low-Jitter Uniformity Detection" => "Düşük Sapmalı Tekdüzelik Tespiti",
            "Benign Human Typing Passthrough" => "Doğal İnsan Yazımı Geçiş İzni",
            "DuckyScript Malicious Payload Neutralization" => "DuckyScript Zararlı Yükünün Etkisizleştirilmesi",
            "Stealth Micro-Burst Attack Neutralization" => "Gizli Mikro Seri Saldırısının Engellenmesi",
            "Multi-Stage Exploit Execution Neutralization" => "Çok Aşamalı İstismar Yürütümünün Engellenmesi",
            "Privacy Invariant: Zero Keylogging Audit Trail" => "Gizlilik İlkesi: Sıfır Tuş Kaydı Denetimi",
            "High-velocity keystroke stream at sub-10ms intervals mimicking Hak5 Rubber Ducky and BadUSB hardware." => "Hak5 Rubber Ducky ve BadUSB donanımlarını simüle eden, 10ms altı aralıklı yüksek hızlı tuş akışı.",
            "Sub-15ms keystroke injection with ultra-low microsecond variance simulating Arduino/Digispark firmware loops." => "Arduino ve Digispark döngülerini simüle eden, mikrosaniye seviyesinde sabit aralıklı enjeksiyon.",
            "Gaussian distributed timing (mean 130ms, stddev 35ms) used as a baseline to verify benign typing passthrough." => "Doğal insan klavye kullanımını doğrulayan Gauss dağılımlı (ortalama 130ms, standart sapma 35ms) referans profil.",
            "Advanced evasion profile interleaving benign human-rate decoy typing with sudden high-velocity micro-bursts." => "Normal insan yazımı arasına ani yüksek hızlı saldırı serileri ekleyen gelişmiş atlatma profili.",
            "Simulates real-world exploit delivery: Run dialog invocation (Win+R), delay pacing, and staged command injection." => "Gerçekçi saldırı adımları: Çalıştır penceresi (Win+R), gecikmeli tempo ve kademeli komut çalıştırma.",
            "Execute custom Hak5 DuckyScript 1.0/2.0 payloads with syntax support for delays, combinations, and strings." => "Gecikme, tuş kombinasyonları ve metin dizgilerini destekleyen özel Hak5 DuckyScript 1.0/2.0 betikleri.",
            "Run the full 7-scenario automated verification suite testing timing, suppression, locking, and privacy invariants." => "Zamanlama, tuş yutma, oturum kilitleme ve gizlilik kurallarını denetleyen 7 senaryolu otomatik doğrulama paketi.",
            _ => text.StartsWith("Running", StringComparison.OrdinalIgnoreCase) ? "Çalışıyor..." : text
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}
