# HIDGuard Kullanım Kılavuzu

Bu kılavuz, HIDGuard'ın ne işe yaradığını, sizi hangi tehditlere karşı nasıl koruduğunu ve arayüzdeki özellikleri nasıl kullanacağınızı sade bir dille açıklamaktadır.

---

## 1. HIDGuard Nedir ve Neden Gereklidir?

### Problem: USB Tabanlı Klavye Saldırıları (BadUSB / Rubber Ducky)
Bilgisayarınıza bir USB bellek taktığınızda, Windows bunu bir depolama aygıtı olarak görür ve antivirüs programınız dosyaları tarar. Ancak saldırganlar **USB Rubber Ducky**, **Flipper Zero**, **Digispark** veya **Raspberry Pi Pico** gibi cihazlar kullandığında durum tamamen değişir.

Bu cihazlar bilgisayara takıldığında Windows'a şöyle der:
> "Ben bir USB bellek değilim; ben standart bir USB klavyeyim."

Windows, USB standardı gereği takılan her klavyeye koşulsuz güvenir. Aygıt tanındığı saniyede, önceden programlanmış zararlı komutları insanüstü bir hızla (saniyede yüzlerce tuş) yazmaya başlar:
- Çalıştır penceresini açar (`Win + R`),
- Arka planda PowerShell veya komut satırını çalıştırır,
- Zararlı yazılımı indirir veya sistem parolalarını sızdırır.
Tüm bu işlem **yarım saniye (500 milisaniye)** içinde biter. Bir antivirüs veya insan bunu fark edip engelleyemez.

### Çözüm: HIDGuard Nasıl Korur?
HIDGuard, dosya taraması yapmaz; klavyeden gelen **tuş vuruşlarının fiziksel ritmini ve hızını** inceler:
1. **İnsan Sınırı**: Hiçbir insan iki tuşa 20-25 milisaniyeden daha kısa aralıklarla aralıksız basamaz (biyolojik parmak refleksi sınırı).
2. **Doğal Ritim (Jitter)**: İnsanlar yazı yazarken harfler arasında doğal olarak düzensiz duraklar (bazen 50 ms, bazen 120 ms).
3. **Robotik Saldırı Tespiti**: Saldırgan USB donanımları ise mikrosaniye hassasiyetinde dümdüz ve mekanik bir hızla tuş gönderir.

HIDGuard bu insan-dışı hızı ve mekanik düzeni algıladığı mikrosaniyede aktif savunmaya geçer:
- **Tuşları Yutar**: Tuşların Windows'a ve uygulamalara ulaşmasını engeller (`WH_KEYBOARD_LL`).
- **Ekranı Kilitler**: Windows oturumunu anında kilitler (`LockWorkStation`).
- **Donanımı Kapatır**: Eğer sistemde birden fazla klavye varsa, saldırgan USB cihazı Windows aygıt yöneticisi düzeyinde devre dışı bırakır.

---

## 2. Arayüz ve Sekmeler Rehberi

Programı açtığınızda sol menüde 6 temel sekme ve üst çubukta hızlı kontroller yer alır:

### Üst Çubuk Kontrolleri
- **Tema Seçici (Kırmızı / Yeşil / Mavi)**: Arayüz temasını tek tıkla Kırmızı-Siyah (Varsayılan), Yeşil-Siyah veya Mavi-Siyah yapar.
- **KORUMA AKTİF / DURAKLATILDI Göstergesi**: Canlı savunma motorunun devrede olup olmadığını gösterir.
- **Korumayı Duraklat / Etkinleştir Butonu**: Test yaparken veya geçici olarak korumayı kapatmak istediğinizde kullanılır.

---

### Sekme 1: Genel Bakış (Overview)
Uygulamanın ana kumanda merkezidir:
- **Koruma Durumu Kartı**: Savunma motorunun çalışıp çalışmadığını gösterir.
- **Canlı İstatistikler**:
  - *İşlenen Tuş Sayısı*: Sisteme gelen toplam tuş vuruşu.
  - *Engellenen Tuş Sayısı*: Saldırı anında yutulan zararlı tuşlar.
  - *Tespit Edilen Olaylar*: Başlatılan savunma müdahaleleri.
  - *Güvenilen Cihazlar*: Sistemde kayıtlı onaylı klavyeleriniz.
- **Son Savunma Eylemleri**: Yapılan son engellemelerin zamanı, nedeni ve sonucu.

---

### Sekme 2: Bağlı Donanımlar (Devices)
Bilgisayarınıza bağlı tüm giriş aygıtlarını listeler:
- **Yetkilendirme Durumu**:
  - *Güvenilen*: Sizin onayladığınız meşru klavyeniz.
  - *Doğrulanmamış*: Yeni takılan ve henüz onaylanmamış donanım.
  - *Engellenen*: Saldırı tespit edilip devre dışı bırakılmış aygıt.
- **Aygıt Bilgileri**: Donanım Kimliği (VID/PID) ve aygıt yolu.

---

### Sekme 3: Canlı Metrikler (Metrics)
Yazı yazarken arka planda çalışan matematiksel analiz motorunu görselleştirir:
- **Son Tuşlar Arası Aralık**: Bastığınız son iki tuş arasındaki milisaniye (ms).
- **Yazma Hızı (WPM)**: Dakika başına kelime hızınız.
- **Varyasyon Katsayısı (Jitter)**: Yazma ritminizin doğallığı. İnsanlarda bu değer dalgalıdır; saldırı cihazlarında ise sıfıra yakındır.
- **Gecikme Dağılımı Histogramı**: Tuş aralıklarınızın süre dağılım grafiği.

---

### Sekme 4: Saldırı Simülatörü ve Test Stüdyosu (Simulator)
Pahalı bir Rubber Ducky veya saldırı cihazı satın almadan HIDGuard'ın gerçekten çalışıp çalışmadığını test edebileceğiniz laboratuvar ortamıdır:
- **Saldırı Profilleri**: 8 farklı hazır saldırı senaryosu seçebilirsiniz (PowerShell İndirici, Mimikatz Bellek Dökümü, UAC Atlatma, Fidye Yazılımı Simülasyonu, vb.).
- **Güvenli Kuru Çalıştırma (Dry-Run)**: Testleri sisteminize gerçek zarar vermeden sanal ortamda güvenle koşturur.
- **Hız Kontrolleri (WPM / CPM / Jitter)**: Saldırının ne kadar hızlı enjekte edileceğini ayarlayabilirsiniz.
- **Çalıştır (Oynat) Butonu**: Testi başlatır. HIDGuard'ın saldırıyı nasıl yakaladığını konsol çıktısında ve doğrulama tablosunda anında görürsünüz.

---

### Sekme 5: Denetim Günlüğü (Audit Log)
Tüm güvenlik olaylarının adli bilişim standartlarında kaydıdır:
- **Değiştirilemez Olay Kaydı**: Hangi donanımın saat kaçta ne yaptığı, ne kadar sürede ne karar verildiği şifreli ve tutarlı olarak saklanır.
- **Rapor Dışa Aktar**: Olayları kurumsal raporlama için JSON veya CSV formatında dışa aktarabilirsiniz.

---

### Sekme 6: Yapılandırma ve Ayarlar (Settings)
Gelişmiş kullanıcılar için motor hassasiyeti ayarları:
- **İnsan-Altı Hız Tabanı (ms)**: Varsayılan 25 ms. Bu sürenin altındaki vuruşlar robotik kabul edilir.
- **Şüphe Skoru Eşiği**: Alarm üretmek için gereken güven skoru (Varsayılan 65).
- **Karşı Önlemler**: Ekran kilitleme, tuş yutma ve aygıt devre dışı bırakma seçenekleri.
- **Görsel Tema**: Kırmızı-Siyah, Yeşil-Siyah veya Mavi-Siyah tema seçimi.

---

## 3. Gerçek Hayat Senaryoları

### Senaryo A: Biri Masanıza Gelip Kötü Amaçlı Bir USB Taktı
1. Bilgisayarınız açıkken saldırgan USB cihazı takar.
2. Cihaz milisaniyeler içinde komut yazmaya başlar.
3. HIDGuard ilk 10-15 tuş vuruşundaki imkansız hızı algılar:
   - Tuşları anında havada yutar (komut istemcisi açılmaz).
   - Windows oturumunuzu kilitler (`Kilitlendi`).
   - Cihazı devre dışı bırakır.
4. Saldırgan hiçbir şey yapamadan başarısız olur.

### Senaryo B: Yeni Bir Normal Klavye Satın Aldınız ve Taktınız
1. Yeni klavyeyi USB portuna taktığınızda ekranın ortasında şık bir onay penceresi açılır: *"Yeni USB Klavye Algılandı"*.
2. Pencerede cihazın Donanım Kimliği gösterilir.
3. Klavyeyi bizzat siz bağladığınız için **"Donanıma Güven"** butonuna basarsınız.
4. Klavye güvenilen listesine eklenir ve sorunsuz çalışmaya devam eder.

### Senaryo C: Hızlı Yazı Yazıyorum veya Kod Yapıştırıyorum (Ctrl+V)
- **Ctrl+V ile Yapıştırma**: Windows panosu bellek transferi ile çalışır; ardışık yüzlerce klavye tuşu göndermez. HIDGuard bunu bilir ve kesinlikle alarm üretmez.
- **Hızlı Daktilocular**: Çok hızlı (150+ WPM) yazsanız bile parmaklarınızın doğal ritim değişkenliği (jitter) korunur. HIDGuard çok parametreli analiz yaptığı için hızlı yazan insanları asla engellemez.
- **Parola Yöneticileri (1Password, Bitwarden, KeePass)**: Bu yazılımların gönderdiği girdiler işletim sistemi tarafından özel bayrakla (`LLKHF_INJECTED`) işaretlenir. HIDGuard yazılım girdilerini fiziksel USB cihazı saldırısıyla karıştırmaz.

---

## 4. Sıkça Sorulan Sorular (SSS)

**Soru: HIDGuard şifrelerimi veya yazdığım özel mesajları kaydediyor mu?**  
**Cevap**: Kesinlikle hayır. HIDGuard mimarisinde **Sıfır Kayıt (Zero-Keylogging)** ilkesi katı bir şekilde uygulanır. Normal kullanımda bastığınız harfler, kelimeler veya parolalar bellekte dahi tutulmaz; yalnızca tuşlar arasındaki milisaniye süreleri matematiksel olarak işlenir.

**Soru: Bilgisayarım kilitlenir ve klavyem çalışmazsa ne yaparım?**  
**Cevap**: HIDGuard bünyesinde **Kilitlenme Önleme (Anti-Lockout)** koruması vardır. Sistemde takılı tek bir klavye varsa, o klavye asla donanım düzeyinde devre dışı bırakılmaz. Böylece kullanıcının sistem dışı kalması engellenir.

**Soru: İnternet bağlantısı gerekiyor mu?**  
**Cevap**: Hayır. HIDGuard %100 yerel ve çevrimdışı (offline) çalışır. Dışarıya hiçbir telemetri, veri veya rapor gönderilmez.

**Soru: Uygulamayı nasıl çalıştırırım?**  
**Cevap**: Proje ana klasöründeki `run-hidguard.bat` dosyasına çift tıklayarak veya `publish\HidGuard.App.exe` dosyasını açarak hemen kullanmaya başlayabilirsiniz.
