# HIDGuard

Windows için Donanım Düzeyi Klavye ve BadUSB Savunma Sistemi

[English](README.md) | [Türkçe](README.tr.md)

---

## HIDGuard Nedir?

HIDGuard; **USB Rubber Ducky**, **Flipper Zero**, **Digispark** ve **Teensy** gibi bilgisayara takıldığında saniyeler içinde yetkisiz komutlar enjekte eden zararlı USB donanımlarına (BadUSB) karşı gerçek zamanlı koruma sağlayan bir Windows güvenlik yazılımıdır.

Geleneksel antivirüs yazılımları USB aygıtlarını doğası gereği güvenilir kabul ederken, HIDGuard tuş vuruşlarının **biyomekanik zamanlamasını** (milisaniye cinsinden yazma hızı ve ritmi) analiz ederek insan ile donanımsal enjeksiyon aygıtlarını anında ayırt eder.

---

## Öne Çıkan Özellikler

- **Gerçek Zamanlı Aktif Müdahale**: Saldırı algılandığı anda zararlı tuşlar işletim sistemine iletilmeden yutulur (`WH_KEYBOARD_LL`), oturum anında kilitlenir (`LockWorkStation`) ve gerekirse aygıt sürücü düzeyinde devre dışı bırakılır (`CM_Disable_DevNode`).
- **Sıfır Kayıt (Zero-Keylogging) Gizlilik Güvencesi**: Normal kullanımda bastığınız hiçbir harf veya parola asla kaydedilmez, diske yazılmaz veya iletilmez. Yalnızca donanım kimliği ve zamanlama analitiği işlenir.
- **Tamamen Çevrimdışı (Offline)**: İnternet bağlantısı, telemetri veya harici sunucu iletişimi yoktur.
- **3 Siber Güvenlik Teması**: Kırmızı-Siyah (Varsayılan - Crimson Obsidian), Yeşil-Siyah (Matrix) ve Mavi-Siyah (Cobalt) temaları arasında arayüzden anında canlı geçiş.
- **Entegre Saldırı Simülatörü**: Rubber Ducky / DuckyScript yüklerini gerçek sisteme zarar vermeden güvenli ortamda test edebileceğiniz yerleşik araştırma stüdyosu.

---

## Hızlı Başlangıç

### 1. Çalıştırma
Projeyi doğrudan çalıştırmak için ana dizindeki betiği kullanabilirsiniz:

```cmd
.\run-hidguard.bat
```

veya derlenmiş yürütülebilir dosyayı açabilirsiniz:

```cmd
.\publish\HidGuard.App.exe
```

### 2. Kaynak Koddan Derleme
.NET 8 SDK yüklü ortamda projeyi derlemek ve çalıştırmak için:

```cmd
dotnet run --project src/HidGuard.App/HidGuard.App.csproj -c Release
```

Tek tıkla bağımsız bir `publish\HidGuard.App.exe` üretmek için:

```cmd
.\build-exe.bat
```

---

## Kullanıcı Arayüzü Sekmeleri

1. **Genel Bakış (Overview)**: Canlı koruma durumu, son savunma eylemleri ve sistem özeti.
2. **Aygıtlar (Devices)**: Sisteme bağlı tüm giriş donanımları, güvenilirlik durumları ve yetkilendirme listesi.
3. **Canlı Metrikler (Metrics)**: Tuş vuruş frekansı, WPM, varyans ve gecikme dağılım histogramı.
4. **Saldırı Simülatörü (Simulator)**: 8 hazır saldırı profili, DuckyScript derleyici ve otomatik doğrulama testleri.
5. **Denetim Günlüğü (Audit Log)**: Değiştirilemez, kriptografik zaman damgalı olay kaydı ve dışa aktarma (JSON / CSV).
6. **Yapılandırma (Settings)**: Eşik değerleri, koruma modları ve tema seçenekleri.

---

## Detaylı Teknik Dokümantasyon

Matematiksel zamanlama algoritmaları, Win32 API çağrıları ve derinlemesine teknik mimari için:
- [Kullanıcı Rehberi ve Tehdit Modeli (İngilizce)](docs/USER_GUIDE.md)
- [Teknik Mimari ve Algoritma Detayları](docs/mimari-detaylar.tr.md)
- [Mimari Derinlemesine İnceleme (İngilizce)](docs/architecture-and-deep-dive.md)
- [Yapılandırma Rehberi](docs/configuration.md)
- [Tehdit Modeli ve Sınırlar](docs/threat-model-and-limitations.md)

---

## Lisans

Bu proje MIT lisansı ile lisanslanmıştır.
