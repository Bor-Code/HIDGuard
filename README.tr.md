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

### 1.3 Mesru Insan Disi Girdilerin Ele Alinmasi
Guvenlik aracinin uretim ortaminda yanlis pozitif uretmemesi icin mesru yazilim ve otomasyonlar ozenle ele alinir:
- Pano Yapistirma (Ctrl+V, Shift+Insert): Isletim sistemi pano transferlerini OLE / WM_PASTE ile bellek duzeyinde gerceklestirir, ardil tus vuruslari olusmaz ve anomali tetiklenmez.
- Parola Yoneticileri: KeePass, 1Password ve Bitwarden gibi yazilimlar SendInput API'sini kullanir. Windows cekirdegi bu girdilere hDevice == IntPtr.Zero ve LLKHF_INJECTED bayragi atar. HIDGuard yazilim girdilerini fiziksel USB cihaz kuyruklarindan ayirir.
- Erisilebilirlik ve Ekran Klavyesi (osk.exe): Windows erisilebilirlik alt sisteminin urettigi sanal girdiler tespit edilerek fiziksel aygit anomali puanlamasindan muaf tutulur.
- Uzak Masaustu (RDP, Citrix, VNC): Uzak oturum girdileri Terminal Services sanal surucusu (TS_KBD) uzerinden gelir; fiziksel yerel USB kok dugumleriyle karistirilmaz.
- Hizli Insan Daktilolari: Hiz 150+ WPM seviyesine ulassa dahi insan motor kontrolu dogal varyansini (degisim katsayisi > 0.20) korur ve ardisik vuruslarda 25 ms fizyolojik tabaninin altina inmez. Cok parametreli skorlama sayesinde yanlis engelleme olusmaz.

### 1.4 Durust Sinirlamalar ve Bilinen Atlatma Yontemleri
Guvenlik muhendisligi seffafligi geregi bilinen tasarim sinirliliklari acikca belgelenmistir:
- Rastgele Gecikmeli Jitter / Insan Emulasyonu: Mikrokontrolcuye insan yazim ritmini taklit eden (ortalama 120 ms, standart sapma 35 ms) gecikmeler eklenirse zamanlama analizi atlatilabilir. Ancak bu durum 350 milisaniyelik saldiriyi 15 ila 45 saniyeye uzatarak kullanicinin ekrandaki hareketliligi gorup cihazi fiziken cikarmasina veya pencereyi kapatmasina genis bir mudahale penceresi tanir.
- Donanim Kimlik Klonlama (VID/PID Spoofing): USB standartlari donanimsal kriptografik dogrulamayi zorunlu kilmadigi icin yetkili bir klavyenin VID ve PID degerleri kotu niyetli cihaza kopyalanabilir. HIDGuard ham aygit yolu topolojisiyle bunu zorlastirir, ancak protokol duzeyinde tam guvence yalnizca kriptografik USB standartlariyla mumkundur.
- Cekirdek Seviyesi Girdi Enjeksiyonu (Ring 0): Ring 0'da calisan kok kullanici yazilimlari (rootkit) Win32 kullanici kipi hook ve Raw Input katmanini atlatir. HIDGuard isletim sistemi cekirdegini degil, harici fiziksel BadUSB donanimlarini engeller.
- Tek Klavye Kilitleme Korumasi: Sistemde tek klavye varsa kullanicinin yonetim erisimini kaybetmemesi icin aygit dugumu kapatilmaz; yalnizca tus yutma ve ekran kilitleme uygulanir.

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
4. Windows Olay Gunlugu Entegrasyonu (WindowsEventLogService):
   - Saldiri anomalisinin tespiti (Event ID 1001), aygitin surucu duzeyinde kapatilmasi (Event ID 1002) ve tek klavye kilitleme korumasi (Event ID 1003) dogrudan Windows Application olay gunlugune HIDGuard kaynagiyla yapilandirilmis guvenlik kayitlari olarak yazilir.

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
2. Bellek Ici Dairesel Arabellek (IncidentPayloadBuffer):
   - Yalnizca opt-in yakalama etkinken calisir; kapaliyken tum tus vuruslari aninda bellekten dusurulur.
   - Aygit basina bagimsiz kuyrukta yalnizca son CaptureWindowKeystrokes (varsayilan: 64, azami: 1024) adet KeyDown sanal tus kodunu tutar.
   - Saldiri tespit edildigi anda TakeSnapshot ile anlik goruntu alinip arabellek aninda temizlenir.
3. DPAPI Sifreleme ve Bellek Temizligi:
   - Yakalanan anomali icerigi disk yazilmadan once Windows DPAPI (DataProtectionScope.CurrentUser) ile aninda sifrelenir.
   - Duz metin tus dizisi sifrelenir sifrelenmez Array.Clear ile yonetilen bellekten aninda sifirlanir; boylece bellek dokumlerinde (memory dump) tus izi birakilmaz.
4. Boyut, Saklama Sinirlari ve Otomatik Bakim (IncidentMaintenanceService):
   - MaxPayloadBytes (varsayilan: 4 KB) ile olay basina boyut sinirlandirilir.
   - RetentionDays (varsayilan: 30 gun) suresi dolan olaylar ve MaxIncidentCount (varsayilan: 1000) siniri IncidentMaintenanceService tarafindan arka planda periyodik olarak (PurgeIntervalMinutes, varsayilan: 60 dk) otomatik temizlenir.
   - Calisma aninda IConfigurationManager.ConfigurationChanged olayini dinleyerek ayar degisikliklerini yeniden baslatma gerekmeksizin dinamik olarak uygular.
5. Yalnizca Metadata Iceren Rapor Disari Aktarimi (ExportMetadataReportAsync):
   - Guvenlik analistleri veya SOC ekipleriyle paylasilmak uzere uretilen JSON veya CSV raporlarinda ham veya sifreli tus icerigi kesinlikle yer almaz; yalnizca metadata (DeviceId, AnomalyScore, Rule, Action, Timestamp vb.) aktarilir.
   - Audit Log arayuzundeki "Export Report" butonu ile kullanici dosya kaydetme penceresi uzerinden tek tikla rapor uretebilir.

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
- Audit Trail: Filtrelenebilir ve aranabilir denetim izi tablosu ve olay raporu disari aktarma butonu (Export Report).
- Settings: Kayan pencere boyutu, standart sapma esigi, karsi onlem anahtarlari ve Sifrelenmis Olay Deposu / Adli Bilisim yapilandirma karti.

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
| HidGuard.Core.Tests | 133 | Basarili | Zamanlama motoru, anomali puanlama, guven deposu, politika motoru, boru hatti, atomik konfigurasyon, sifreli olay deposu, ring buffer, bakim servisi, olay gunlugu yonetimi |
| HidGuard.Platform.Windows.Tests | 31 | Basarili | Raw Input cozumleyici, donanim kapatici, calisma istasyonu kilitleyici, dusuk seviyeli klavye kancasi, Serilog fabrikasi, DPAPI veri koruyucu, Windows Olay Gunlugu yayincisi |
| HidGuard.App.Tests | 21 | Basarili | MVVM komutlari, tekil ornek Mutex'i, aygit yonetimi, metrikler, ayarlar paneli, rapor disari aktarma, kurulum betigi dogrulamasi ve ana pencere gezinmesi |
| HidGuard.Simulator.Tests | 28 | Basarili | DuckyScript parser, token/kombinasyon cozumleme, burst/jitter/human profilleri, uctan uca 5 dogrulama senaryosu |
| Toplam | 213 | %100 Yesil | Sifir uyari, sifir hata (TreatWarningsAsErrors) |

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

Otomatik 5 senaryolu dogrulama raporunu calistirma:
```powershell
dotnet run --project tools/HidGuard.Simulator/HidGuard.Simulator.csproj -- --verify
```

Komut satiri saldiri profillerini guvenli dry-run kipinde calistirma:
```powershell
dotnet run --project tools/HidGuard.Simulator/HidGuard.Simulator.csproj -- --mode burst --dry-run
dotnet run --project tools/HidGuard.Simulator/HidGuard.Simulator.csproj -- --mode jitter --dry-run
dotnet run --project tools/HidGuard.Simulator/HidGuard.Simulator.csproj -- --mode human --dry-run
```

Etkilesimli konsol menusunu baslatma:
```powershell
dotnet run --project tools/HidGuard.Simulator/HidGuard.Simulator.csproj
```

### Dagitim Paketini Olusturma
```powershell
powershell -ExecutionPolicy Bypass -File scripts/package.ps1 -Version 1.0.0
```

### Windows Kurulum Paketini Olusturma (Inno Setup)
```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-installer.ps1 -Version 1.0.0
```

### Yapilandirma ve Dinamik Yeniden Yukleme (Hot-Reload)
HIDGuard, ayarlarini `%LocalAppData%\HIDGuard\config.json` dosyasinda saklar. Uygulama, dosya guncellendiginde FileSystemWatcher uzerinden degisiklikleri algilayarak programi yeniden baslatmadan ayarlari dinamik olarak gunceller. Tum ayar alanlari ve kabul edilen deger araliklari docs/configuration.md dosyasinda ayrintilandirilmistir.

---

## 6. Tamamlanan Asamalar

- [x] Asama 0 (Repository Foundation): Cozum iskeleti, .editorconfig, Directory.Build.props, MIT lisansi, CI/CD ve contributing belgeleri tamamlandi.
- [x] Asama 1 (Core Domain & Timing Analysis): Kayan pencere istatistik motoru, Welford ortalama ve standart sapma algoritmalar, fizyolojik hiz esikleri tamamlandi.
- [x] Asama 2 (Raw Input Capture Layer): HWND_MESSAGE arkasi Raw Input kaydi, VID/PID ayristirma, SendInput ve donanim ayrimi tamamlandi.
- [x] Asama 3 (Trust Store & Policy Engine): Atomik JSON tabanli guven deposu, politika matrisi ve tek klavye anti-lockout guvencesi tamamlandi.
- [x] Asama 4 (Response Actions): WH_KEYBOARD_LL tus yutma, LockWorkStation istasyon kilitleme ve CM_Disable_DevNode donanim devre disi birakma tamamlandi.
- [x] Asama 5 (Encrypted Incident Store): Windows DPAPI sifrelemesi, RAM ici dairesel kuyruk, otomatik temizlik ve metadata rapor disari aktarimi tamamlandi.
- [x] Asama 6 (Tray Application & UI): Modern WPF karanlik tema, sistem tepsisi yasami, tekil ornek Mutex'i ve UAC elevation yonetimi tamamlandi.
- [x] Asama 7 (Attack Simulator & Verification Tooling): DuckyScript 1.0/2.0 cozucu, sentetik injection motoru, burst/jitter/human profilleri ve 5 uctan uca guvenlik dogrulamasi tamamlandi.
- [x] Asama 8 (Release Engineering & Packaging): Self-contained bagimsiz x64 dagitim paketi, SHA256 checksum uretimi, GitHub Actions surum yayinlama workflow'u ve Dependabot otomasyonu tamamlandi.
- [x] Asama 9 (Technical Documentation & Specifications): Kapsamli mimari belgeleri, tehdit modeli, durust sinirlamalar ve atlatma analizi, SendInput birlikte calisabilirlik raporu ve yapilandirma referansi tamamlandi.

---

## 7. Destekleyici Dokumantasyon ve Baglantilar

- [Tehdit Modeli, Sinirlamalar ve Atlatma Analizi](docs/threat-model-and-limitations.md): Saldirgan yetenekleri, teorik atlatma yontemleri ve mesru girdi ayristirma ilkeleri.
- [Win32 Raw Input ve SendInput Birlikte Calisabilirlik Analizi](docs/raw-input-and-sendinput-interop.md): Windows isletim sisteminin donanimsal ve yazilimsal tus vuruslarini raporlama farkliliklarina iliskin deneysel bulgular.
- [Yapilandirma Referansi](docs/configuration.md): config.json dosyasindaki tum alanlarin tipleri, varsayilanlari ve gecerlilik araliklari.
- [Inno Setup Kurulum Betigi](installer/HIDGuard.iss): Windows kurulum paketi, masaustu kisayollari, baslangic kayitlari ve kaldirici yapilandirmasi.
- [Katki Rehberi](.github/CONTRIBUTING.md): Dal adlandirma, Conventional Commits ve sifir-yorum/sifir-emoji kurallari.
- [Guvenlik Politikasi](SECURITY.md): Guvenlik acigi bildirim surecleri ve gizlilik garantileri.
- [Lisans](LICENSE): MIT Lisansi.

