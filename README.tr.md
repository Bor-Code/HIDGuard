# HIDGuard: Teknik Mimari ve Gelistirme Dokumantasyonu

[English](README.md) | [Türkçe](README.tr.md)

Bu dokuman, HIDGuard projesinin sifirdan Asama 9 dahil olmak uzere gelistirilen tum katmanlarini, algoritmalarini, Windows Win32 API entegrasyonlarini ve guvenlik ilkelerini teknik derinligiyle aciklamaktadir.

---

## 1. Proje Amaci ve Tehdit Modeli

### 1.1 Saldiri Mekanizmasi (Keystroke Injection / BadUSB)
Geleneksel isletim sistemleri ve antivirus / EDR yazilimlari, USB baglanti noktasina takilan donanimlarin kimlik bildirimlerine varsayilan olarak guvenir. Rubber Ducky, BadUSB, Digispark, Teensy ve Flipper Zero gibi mikrokontrolcu tabanli cihazlar, bilgisayara takildiklarinda kendilerini standart bir USB Insan Arayuz Aygiti (HID - Human Interface Device) klavye olarak tanitirlar.

Isletim sistemi bu cihazi mesru bir klavye kabul ettigi anda saldirgan cihaz, saniyede yuzlerce veya binlerce tus vurusunu sisteme enjekte eder:
- Win + R kombinasyonu acilir.
- powershell -WindowStyle Hidden -Enc ... komutu gonderilir.
- Bellekte zararli kod (payload) calistirilir veya yetki yukseltme gerceklestirilir.
- Tum bu surec insan gozunun fark edemeyecegi bir hizda (100 ila 500 milisaniye icinde) tamamlanir.

### 1.2 HIDGuard Cozum Yaklasimi
HIDGuard, saldiriyi imza veya dosya taramasiyla degil, tus vuruslarinin fiziksel ve biyomekanik dinamiklerini (inter-keystroke timing analysis) analiz ederek engeller:
1. Insan parmaklarinin fizyolojik hareket kabiliyetinin bir alt siniri vardir (fizyolojik esik).
2. Insanlar yazarken tuslar arasi surelerde dogal bir duzensizlik (jitter / standart sapma) sergiler.
3. Donanimsal enjeksiyon araclari ise mikrosaniye hassasiyetinde esbicimli (uniform / sifira yakin standart sapma) ve olaganustu yuksek hizlarda (> 180-300 WPM) veri gonderir.
4. HIDGuard, bu anormalligi tespit ettigi anda saldiriyi havada yutar (WH_KEYBOARD_LL), isletim sistemini kilitler (LockWorkStation) ve gerekirse aygiti surucu duzeyinde devre disi birakir (CM_Disable_DevNode).

---

## 2. Mimari Genel Bakis

Proje, bagimliliklarin yonunu domain merkezine dogru tutan temiz mimari (Clean Architecture) ve katmanli yapi uzerinde insa edilmistir:

```
[ Donanim / Klavye Girisleri ]
              |
              v
+-------------------------------------------------------------+
| HidGuard.Platform.Windows (Win32 Raw Input)                 |
| - Message-only Window (HWND)                                |
| - RegisterRawInputDevices (RIDEV_INPUTSINK)                 |
| - Raw Input Parsing & Handle Resolution                     |
+-----------------------------+-------------------------------+
                              | KeystrokeEvent
                              v
+-------------------------------------------------------------+
| HidGuard.Core (Boru Hatti & Degerlendirme)                  |
| - System.Threading.Channels (Bounded, Non-blocking)         |
| - TimingAnalysisEngine (Sliding Window, Mean, StdDev)       |
| - AnomalyScorer (Confidence Calculation)                    |
| - TrustStore (Hardware Trust Management)                    |
| - PolicyEngine (Decision Matrix & Anti-Lockout)             |
| - PipelineEventAggregator (Event Publishing)                |
+-----------------------------+-------------------------------+
                              | PolicyDecision
                              v
+-------------------------------------------------------------+
| HidGuard.Platform.Windows (Karsi Onlemler)                  |
| - WindowsLowLevelKeyboardHook (WH_KEYBOARD_LL)              |
| - WindowsWorkstationLocker (LockWorkStation)                |
| - WindowsDeviceDisabler (CfgMgr32 CM_Disable_DevNode)       |
+-----------------------------+-------------------------------+
                              | Telemetri & Olaylar
                              v
+-------------------------------------------------------------+
| HidGuard.App (Kullanici Arayuzu & Sistem Tepsisi)           |
| - WPF Modern Dark Mode Dashboard                            |
| - System.Windows.Forms.NotifyIcon Entegrasyonu              |
| - Tekil Ornek (Single-Instance Mutex) Kontrolu              |
| - MVVM Katmani (DispatchToUi Senkronizasyonu)               |
+-------------------------------------------------------------+
```

---

## 3. Katmanlar ve Detayli Teknik Inceleme

### Asama 0: Proje Temeli ve Kod Kalitesi Standartlari
- Teknoloji: .NET 8 (C# 12).
- Derleme Kurallari: Directory.Build.props uzerinden TreatWarningsAsErrors = true, Nullable = enable, AnalysisMode = AllEnabledByDefault.
- Kod Analizcileri: Microsoft .editorconfig analiz kurallari en siki seviyeye (error) cekilmistir (CA1062 null kontrolu, CA1848 yuksek performansli loglama, CA2007 ConfigureAwait, CA1305 InvariantCulture kullanimi).
- Yorum ve Emoji Yasagi: Kodun kendini belgelemesi ilkesi geregi repo genelinde sifir yorum satiri ve sifir emoji kurali uygulanmaktadir.

---

### Asama 1: Cekirdek Domain ve Zamanlama Analiz Motoru (HidGuard.Core.Timing)

Zamanlama analizi, her klavye aygiti icin ayri tutulan sinirli bir kayan pencere (SlidingWindow) uzerinde isletilir:

#### Matematiksel Hesaplamalar ve Istatistiksel Analiz
1. Tuslar Arasi Aralik (Inter-Keystroke Interval - Delta t):
   Ardilik iki tusa basilma ani arasindaki milisaniye farki: Delta t = t_i - t_{i-1}
2. Ortalama Aralik (Mean):
   Tum araliklarin aritmetik ortalamasi.
3. Standart Sapma (Standard Deviation):
   Araliklarin ortalamadan sapma derecesi.
4. Varyasyon Katsayisi (Coefficient of Variation - CV):
   CV = Standart Sapma / Ortalama
5. Yazma Hizi Tahmini (WPM - Words Per Minute):
   Standart tipografik kural geregi 1 kelime ortalama 5 karaktere esittir:
   WPM = (60,000 / Ortalama) / 5

#### Karar Esikleri
- Fizyolojik Alt Sinir (HumanFeasibilityFloorMs = 25 ms): Insan parmaginin motor kontrol sinirlari dahilinde ardisik iki tusa 25 ms'den daha kisa surede basmasi imkansiza yakindir. Pencere icindeki araliklarin yuzde 35'inden fazlasi bu esigin altindaysa dogrudan saldiri sinyali uretilir.
- Robotik Esbicimlilik (CoefficientOfVariationThreshold = 0.18): BadUSB cihazlari genelde sabit delay komutlari kullanir. CV < 0.18 olmasi, insan yazimindaki dogal gecikme dagiliminin (jitter) bulunmadigini, yazimin bir cip tarafindan yapildigini kanitlar.
- Hiz Asimi (MaxAllowedWpm = 180 WPM): Profesyonel daktilolar dahi nadiren 150-180 WPM uzerine cikabilir. 250+ WPM hizlar dogrudan yapay enjeksiyon olarak siniflandirilir.

---

### Asama 2: Windows Ham Giris Yakalama Katmani (HidGuard.Platform.Windows)

Klasik Windows klavye kancalari (SetWindowsHookEx), tusa basildigini bildirir ancak tusun hangi fiziksel USB cihazindan geldigini soyleyemez. Bir BadUSB saldirgani sisteme sizdiginda, orijinal klavye ile sahte klavye ayirt edilemezse sistem savunmasiz kalir.

#### Win32 Raw Input API Kullanimi
1. Pencere Olusturma: Arka planda yalnizca mesaj alan bir HWND (HWND_MESSAGE) olusturulur.
2. Kayit: RegisterRawInputDevices fonksiyonuna RAWINPUTDEVICE yapisi gonderilir:
   - usUsagePage = 0x01 (Generic Desktop Controls)
   - usUsage = 0x06 (Keyboard)
   - dwFlags = RIDEV_INPUTSINK (Pencere odakta olmasa dahi tum sistem genelindeki klavye ham verilerini toplar)
3. Aygit Tanimlama: WM_INPUT mesaji geldiginde GetRawInputDeviceInfoW cagrilaraq aygitin donanim yolu alinir:
   \\?\HID#VID_16C0&PID_05DF&MI_00#7&...
4. Kimlik Cozumleme: DeviceIdentifier sinifi, bu yoldan Vendor ID (VID) ve Product ID (PID) ikilisini ayristirir.

---

### Asama 3: Donanim Guven Deposu ve Politika Motoru (HidGuard.Core.Trust & Policy)

Sistemde guvenilen aygitlarin beyaz listesi (whitelist) ve engellenen aygitlarin kara listesi (blacklist) yonetilir.

#### Atomik Dosya Yazimi ve Yedekleme (Atomic Persistence)
Konfigurasyon ve guven deposu dosyalari yazilirken elektrik kesintisi veya cokme durumunda bozulmayi onlemek icin 3 adimli atomik yazma deseni uygulanir:
1. Veri gecici dosyaya yazilir (truststore.json.tmp).
2. Var olan dosya yedeklenir (truststore.json.bak).
3. Gecici dosya asil dosyanin uzerine tasinir (File.Move(..., overwrite: true)).

#### Anti-Lockout (Kendi Kendini Kilitlemeyi Onleme) Guvencesi
Eger bir saldiri tespit edilirse ve politika motoru klavyeyi devre disi birakmak isterse, once sistemdeki aktif klavye sayisi kontrol edilir:
- Sistemde takili tek bir klavye varsa, o klavye asla tamamen devre disi birakilmaz.
- Cunku tek klavye devre disi birakilirsa kullanici sistem yonetimini kaybeder.
- Bu durumda motor guvenlik derecesini dusurerek aygiti kapatmak yerine istasyonu kilitler (LockWorkStation) veya gecici tus yutma uygular.

---

### Asama 4: Aktif Engelleme ve Mudahale Motoru (HidGuard.Core.Countermeasures)

Saldiri tespit edildigi anda isletim sistemi ve donanim duzeyinde 3 kademeli savunma tetiklenir:

1. Tus Vurusu Yutma (WindowsLowLevelKeyboardHook):
   - Win32 WH_KEYBOARD_LL kancasi, ozel bir is parcaciginda mesaj dongusuyle (GetMessage, DispatchMessage) calisir.
   - Kanca geri cagrimi (hook callback) 1 milisaniyenin altinda yanit verir.
   - Anomali bayragi aktif oldugu surece CallNextHookEx cagrilmaz; dogrudan 1 dondurulerek tus vurusu isletim sistemi mesaj kuyrugundan silinir (tus ekrana veya komut satirina ulasamaz).
2. Istasyon Kilitleme (WindowsWorkstationLocker):
   - user32.dll icindeki LockWorkStation() API'si cagrilaraq Windows aninda kilit ekranina alinir. Bu islem, acilmis olabilecek Run veya PowerShell pencerelerine komut yazilmasini durdurur.
3. PnP Duzeyinde Aygit Devre Disi Birakma (WindowsDeviceDisabler):
   - cfgmgr32.dll API'leri (CM_Locate_DevNodeW, CM_Disable_DevNode) kullanilarak saldirgan USB aygitinin aygit yoneticisi dugumu surucu seviyesinde kapatilir.

---

### Asama 5: Uctan Uca Boru Hatti Entegrasyonu (HidGuard.Core.Pipeline)

Girdi yakalama ile analiz ve mudahale motorlari System.Threading.Channels (BoundedChannel) ile birbirinden tamamen ayrilmistir (producer-consumer deseni).

- Girdi Ureticisi (Producer): Win32 Raw Input dinleyicisi tusu yakalar ve kanala yazar. Kanal kapasitesi 10,000 ogredir. Kanal dolarsa en eski oge atilir; UI veya donanim dinleyicisi asla kilitlenmez.
- Tuketici (Consumer): Arka plan is parcacigi kanaldan tuslari okur, zamanlama analiz motoruna verir, anomali skorunu hesaplar ve gerekirse karsi onlemleri tetikler.
- Performans: Boru hatti saniyede 10,000'den fazla tus vurusunu milisaniyenin altindaki gecikmelerle isleme kapasitesine sahiptir.

---

### Asama 6: Yapilandirma, Gunlukleme ve Denetim Izi (Configuration, Audit, Logging)

#### Gizlilik Ilkesi (Zero-Keylogger Guarantee)
HIDGuard bir guvenlik aracidir ve kesinlikle bir casus yazilima (keylogger) donusmemelidir.
- Normal calisma dongusunde AuditEvent ve log modellerinde basilan karakteri veya sanal tus kodunu (VirtualKey / VKCode) tutan hicbir alan yoktur.
- Yalnizca aygit kimligi, zaman damgasi, uygulanan karsi onlem ve anomali yuzdesi kaydedilir.
- Bu kural, AuditTrailTests icinde Reflection ile denetlenmekte; model uzerinde tus verisi tasiyabilecek bir ozellik eklendiginde testler derleme aninda basarisiz olmaktadir.

#### Sifrelenmis Yerel Olay Deposu (EncryptedIncidentStore & DPAPI)
Adli bilisim ve saldiri analizi icin gelistirilen olay depolama katmani en yuksek gizlilik standartlarini saglar:
1. Opt-In Icerik Yakalama (EnableOptInPayloadCapture):
   - Varsayilan olarak kesinlikle kapalidir (false).
   - Yalnizca sistem yoneticisi acikca izin verirse (opt-in) ve yalnizca tespit edilen aktif bir saldiri anomalisinde tetikleyici tus dizisi yakalanir (arka plan yazimi asla yakalanmaz).
2. DPAPI Sifreleme (ProtectedData.Protect):
   - Yakalanan anomali icerigi disk yazilmadan once Windows DPAPI (DataProtectionScope.CurrentUser) ile aninda sifrelenir. Dosyalar harici ortama aktarilsa dahi yalnizca ayni Windows kullanici oturumunda cozumlenebilir.
3. Boyut ve Saklama Sinirlari:
   - MaxPayloadBytes (varsayilan: 4 KB) ile olay basina boyut sinirlandirilir.
   - RetentionDays (varsayilan: 30 gun) suresi dolan olaylar PurgeExpiredIncidentsAsync ile otomatik silinir.
   - MaxIncidentCount (varsayilan: 1000) kapasitesi asildiginda en eski olaylar otomatik temizlenir.
4. Yalnizca Metadata Iceren Rapor Disari Aktarimi (ExportMetadataReportAsync):
   - Guvenlik analistleri veya SOC ekipleriyle paylasilmak uzere uretilen JSON veya CSV raporlarinda ham veya sifreli tus icerigi kesinlikle yer almaz; yalnizca metadata (DeviceId, AnomalyScore, Rule, Action, Timestamp vb.) aktarilir.

#### Denetim Izi (Audit Trail) ve Gunlukleme (Logging)
- FileAuditTrailService: Gunluk olarak bolunen JSON Lines formatinda (audit-yyyyMMdd.jsonl) append-only denetim kaydi tutar.
- FileConfigurationManager: FileSystemWatcher ile diskteki config.json dosyasini izler; dosya harici olarak duzenlendiginde uygulama kapatilmadan ayarlar dinamik olarak yenilenir (hot-reload).
- HidGuardLoggingFactory: Serilog ile 50 MB dosya siniri, 30 gunluk saklama ve InvariantCulture ile bicimlendirilmis yapilandirilmis log uretir.

---

### Asama 7: WPF Arayuzu ve Sistem Tepsisi Paneli (HidGuard.App)

#### Mimari Ozellikler
1. Sistem Tepsisi Yasam Dongusu (TrayIconService):
   - Windows tepsi alaninda kalkan simgesiyle yer alir.
   - Kullanici ana pencereyi kapattiginda, MainWindow.OnClosing metodu iptal edilir (e.Cancel = true) ve pencere gizlenir (Hide()). Uygulama arka planda korumaya devam eder.
   - Uygulama yalnizca tepsi menusundeki Exit secenegiyle sonlandirilir.
2. Tekil Ornek Denetimi (SingleInstanceService):
   - Local\HIDGuard_Application_SingleInstance_Mutex adlandirilmis Mutex'i ile sistemde ayni anda yalnizca tek bir HIDGuard orneginin calismasi garanti edilir. Ikinci bir kopya acilirsa hemen sonlanir.
3. UAC Ayricalik Farkindaligi (UacElevationService):
   - Donanim kapatma islemleri yonetici yetkisi gerektirdiginden, uygulamanin yonetici haklariyla calisip calismadigi (WindowsBuiltInRole.Administrator) denetlenir ve arayuzde rozet olarak gosterilir.
4. Dispatcher Senkronizasyonu (DispatchToUi):
   - Arka plan olaylari UI is parcacigina tasinirken Application.Current?.Dispatcher kullanilir; birim test ortamlarinda ise dogrudan senkron cagri yapilarak testlerin kilitlenmesi onlenir.

#### Dashboard Bolumleri
- Overview: Canli koruma anahtari, anlik toplam tus vurusu, engellenen tuslar, tespit edilen saldirilar, notrlestirilen tehditler, anlik WPM ve anomali cubugu.
- Devices: Entegre klavyelerin donanim kimlikleri (VID/PID), guven seviyeleri (Trusted, Unknown, Blocked) ve Trust / Block / Reset butonlari.
- Live Metrics: Ortalama vurus araligi, standart sapma, hiz ve son vuruslarin zamanlama grafigi.
- Audit Trail: Filtrelenebilir ve aranabilir denetim izi tablosu.
- Settings: Kayan pencere boyutu, standart sapma esigi, maksimum WPM ve karsi onlem anahtarlari.

---

### Asama 8: Saldiri Simulatoru ve Uctan Uca Dogrulama Araclari (HidGuard.Simulator)

#### Mimari Ozellikler
1. Sentetik Enjeksiyon Soyutlamasi (IKeystrokeInjector):
   - Win32KeystrokeInjector: PInvoke.SendInput uzerinden aktif Windows masaustu pencerelerine dogrudan klavye olaylari (tus basma, kombinasyon, bekleme) gonderir.
   - SimulatedKeystrokeInjector: Masaustune temas etmeksizin bellek uzerinde calisir; birim testlerde ve CI ortamlinda tam guvenli, deterministik dogrulama saglar.
2. DuckyScript Cozumleyici (DuckyScriptParser):
   - Hak5 USB Rubber Ducky 1.0 / 2.0 syntax destegi (STRING, STRINGLN, DELAY, DEFAULT_DELAY, REPEAT, ENTER, GUI/WINDOWS, CTRL, ALT, SHIFT kombinasyonlari, F1-F24).
   - Sanal tus kodlarini (VirtualKeyMap) eksiksiz sekilde Win32 sanal tus kodlarina esler.
3. Saldiri Profilleri (Attack Profiles):
   - BurstAttackProfile: Donanimsal USB saat hizinda (< 10ms, ornegin 3-4ms araliklarla) yuksek hizli tus enjeksiyonu.
   - JitterAttackProfile: Mikrokontrolcu dongulerini modelleyen sabit aralikli ve sifira yakin varyansli (15ms +/- 0.1ms) esbicimli saldiri.
   - HumanTypingProfile: Box-Muller Gauss dagilimiyla insan biyomekanik parmak hareketlerini (ortalama 130ms, standart sapma 35ms, kelime arasi dogal duraklamalar) modelleyen insan klavye emulasyonu.
   - DuckyScriptAttackProfile: Cozumlenen DuckyScript talimatlarini gercek zamanli veya sanal saatle uygulayan profil.
4. Uctan Uca Dogrulama Kosucusu (VerificationRunner):
   - 5 kritik guvenlik senaryosunu canli boru hatti uzerinde otomatik olarak dogrular:
     1. Burst Attack Tespiti ve Karsi Onlem (Anomali skoru yukselir, tuslar yutulur, aygit engellenir).
     2. Dusuk Jitter / Esbicimlilik Tespiti (Mikrokontrolcu dongusu standart sapma anomalisiyle yakalanir).
     3. Mesru Insan Yazimi Gecisi (Sifir yanlis alarm, sifir engelleme, kesintisiz gecis).
     4. DuckyScript Zararli Payload Notrlestirme (PowerShell / cmd calistirma girisimi basariyla yutulur).
     5. Gizlilik Ilkesi Dogrulamasi (Denetim izi ve veri modellerinde asla sanal tus kodu veya metin saklanmaz).

---

## 4. Test Kapsami ve Kalite Metrikleri

Tum katmanlar xUnit ve Shouldly kutuphaneleriyle kapsamli sekilde test edilmistir.

| Proje | Test Sayisi | Durum | Kapsanan Alanlar |
| :--- | :--- | :--- | :--- |
| HidGuard.Core.Tests | 105 | Basarili | Zamanlama motoru, anomali puanlama, guven deposu, politika motoru, uctan uca boru hatti, atomik konfigurasyon, gizlilik dogrulamasi |
| HidGuard.Platform.Windows.Tests | 23 | Basarili | Raw Input cozumleyici, donanim kapatici, calisma istasyonu kilitleyici, dusuk seviyeli klavye kancasi, Serilog fabrikasi |
| HidGuard.App.Tests | 17 | Basarili | MVVM komutlari, tekil ornek Mutex'i, aygit yonetimi, metrikler, ayarlar ve ana pencere gezinmesi |
| HidGuard.Simulator.Tests | 28 | Basarili | DuckyScript parser, token/kombinasyon cozumleme, burst/jitter/human profilleri, uctan uca 5 dogrulama senaryosu |
| Toplam | 173 | %100 Yesil | Sifir uyari, sifir hata (TreatWarningsAsErrors) |

---

## 5. Projeyi Derleme ve Calistirma

### Gereksinimler
- Windows 10 / 11 (x64)
- .NET 8 SDK (v8.0.x veya v10.0.x)

### Derleme
```powershell
dotnet build HidGuard.sln --configuration Release
```

### Testleri Calistirma
```powershell
dotnet test HidGuard.sln --configuration Release
```

### Arayuz Uygulamasini Baslatma
```powershell
dotnet run --project src/HidGuard.App/HidGuard.App.csproj --configuration Release
```

### Saldiri Simulatorunu Calistirma
```powershell
# Otomatik uctan uca guvenlik dogrulama raporunu calistirma
dotnet run --project tools/HidGuard.Simulator/HidGuard.Simulator.csproj -- --verify

# Komut satiri saldiri profilleri (Dry-run / Guvenli Kip)
dotnet run --project tools/HidGuard.Simulator/HidGuard.Simulator.csproj -- --mode burst --dry-run
dotnet run --project tools/HidGuard.Simulator/HidGuard.Simulator.csproj -- --mode jitter --dry-run
dotnet run --project tools/HidGuard.Simulator/HidGuard.Simulator.csproj -- --mode human --dry-run

# Etkilesimli konsol menusunu baslatma
dotnet run --project tools/HidGuard.Simulator/HidGuard.Simulator.csproj
```

### Dagitim Paketini Olusturma
```powershell
powershell -ExecutionPolicy Bypass -File scripts/package.ps1 -Version 1.0.0
```

---

## 6. Gelecek Asamalar

- [x] Asama 8 (Attack Simulator & Verification Tooling): tools/HidGuard.Simulator projesi tamamlandi; DuckyScript motoru, burst, jitter ve insan profilleri olusturuldu, uctan uca 5 otomatik dogrulama senaryosu tamamlandi.
- [x] Asama 9 (Final Documentation, CI/CD Packaging & Release): Projenin nihai dokumantasyonu, mimari diyagramlar, CONTRIBUTING.md, SECURITY.md, GitHub Actions CI/CD hatti ve otomatik dagitim paketleme betigi tamamlandi.

