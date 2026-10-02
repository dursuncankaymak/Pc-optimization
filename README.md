# ⚡ Aria Boost

Oyuncular için **Windows 10 ve Windows 11** optimizasyon aracı. Tek bir küçük exe; kurulum gerektirmez.

**İndir:** [Aria-Boost.exe (en son sürüm)](https://github.com/dursuncankaymak/Pc-optimization/releases/latest/download/Aria-Boost.exe)

![Genel Bakış](https://raw.githubusercontent.com/dursuncankaymak/Pc-optimization/screenshots/overview.png)

## İlkeler

- **Placebo yok.** Yalnızca etkisi bilinen ayarlar var. İnternette dolaşan "FPS artıran 200 kayıt defteri ayarı"nın çoğu ya hiçbir şey yapmaz ya da sistemi bozar.
- **Güvenlik deliği yok.** Windows Defender, Windows Update, Spectre/Meltdown korumaları ve Bellek Bütünlüğü'ne dokunulmaz. Bellek Bütünlüğü açıksa yalnızca bilgi verilir; kararı sen verirsin.
- **Her şey geri alınabilir.** Her ayar değiştirilmeden önce orijinal değeri yedeklenir (`%LocalAppData%\AriaBoost\backup.json`). Bir anahtarı kapatmak ya da "Tümünü geri al" bilgisayarı birebir önceki hâline döndürür. İstersen işlemden önce bir Windows geri yükleme noktası da oluşturulur.
- **Test edilir.** Her sürümde GitHub'ın Windows sunucusu her optimizasyonu gerçekten uygular, doğrular, geri alır ve sistemin ilk hâline döndüğünü kontrol eder.

## Neler yapar?

### Optimizasyonlar

| Ayar | Ne işe yarar | Sürüm |
|---|---|---|
| Oyun Modu | Oyun sırasında Windows Update'in sürücü kurmasını ve yeniden başlatma bildirimlerini durdurur | 10 / 11 |
| Arka plan oyun kaydını kapat | Xbox Game Bar'ın oyunu arka planda kaydetmeye hazır beklemesini kapatır | 10 / 11 |
| Donanım hızlandırmalı GPU zamanlaması | Ekran kartı belleğini kendisi yönetir; DLSS Kare Oluşturma için gerekli | 10 (2004+) / 11 |
| Pencereli oyunlar için iyileştirmeler | Kenarlıksız pencerede gecikmeyi azaltır, Auto HDR / VRR'yi açar | 11 |
| Değişken yenileme hızı (VRR) | G-Sync / FreeSync'i desteklemeyen DX11 oyunlarda da VRR | 10 / 11 |
| Xbox tuşu Game Bar'ı açmasın | Kumandayla oynarken Game Bar'ın oyunu bölmesini engeller | 10 / 11 |
| Nihai Performans güç planı | İşlemcinin frekans düşürmesini ve çekirdek uyutmasını engeller | 10 / 11 |
| Saydamlık efektlerini kapat | Zayıf ekran kartlarında masaüstünün GPU kullanımını azaltır | 10 / 11 |
| Fare ivmesini kapat | "İşaretçi hassasiyetini artır" kapanır; nişan tutarlı olur | 10 / 11 |
| Yapışkan Tuşlar kısayolunu kapat | 5×Shift ile çıkıp oyunu masaüstüne atan pencere çıkmaz | 10 / 11 |
| Ağ kartı güç tasarrufunu kapat | Enerji Verimli Ethernet / Green Ethernet'i kapatır; uyanma gecikmesi kaynaklı ping sıçramalarını önler | 10 / 11 |
| Güncellemeleri başka PC'lere yüklemeyi kapat | Teslim İyileştirme'nin oyun sırasında yükleme hızını yemesini engeller | 10 / 11 |

Dizüstü bilgisayarlarda Nihai Performans planı önerilenlere dahil edilmez (pil süresini kısaltır), ama istenirse açılabilir.

### Ping

"Tek tuşla ping düzelten" programların çoğu ya hiçbir şey yapmaz ya da (ExitLag gibi) trafiği kendi sunucuları üzerinden yönlendiren ücretli bir hizmettir. Aria Boost önce **sorunun nerede olduğunu ölçer**: 20 saniye boyunca modeme ve internete aynı anda ping atar, grafiği çizer ve sonucu yorumlar:

| Teşhis | Anlamı | Ne yapılır |
|---|---|---|
| Sorun evdeki ağda | Modeme bile ping oynak | Wi-Fi yerine kablo, 5 GHz, Wi-Fi taramasını durdurma, ağ kartı güç tasarrufunu kapatma |
| Hat dolu | Bu bilgisayar test sırasında yoğun veri aktarıyor | İndirmeleri durdur / sınırla (Steam, torrent vb. açıksa listelenir) |
| Sorun evin dışında | Modem sabit, internet oynak | Evdeki diğer cihazlar, modemde QoS/SQM, servis sağlayıcı |

Wi-Fi'da **"Oyun sırasında Wi-Fi taramasını durdur"** ayarı, Windows'un dakikada bir yaptığı ve ping sıçratan arka plan ağ taramasını durdurur. Bağlantı koparsa Windows kendiliğinden yeniden bağlanamayacağı için yalnızca uygulama açıkken etkindir; uygulama kapanınca (ya da çökmüşse bir sonraki açılışta) otomatik geri açılır.

### Başlangıç

Açılışta başlayan programları listeler ve tek tıkla kapatır. Görev Yöneticisi'nin "Başlangıç uygulamaları" sekmesiyle aynı mekanizmayı kullanır: program silinmez, istediğin zaman geri açılır.

### Temizlik

Geçici dosyalar, Windows geçici dosyaları, hata raporları ve Geri Dönüşüm Kutusu. Kullanımdaki ve son 24 saatte oluşturulan dosyalar atlanır; bağlantı noktalarının (junction) içine girilmez. Ekran kartı gölgelendirici önbelleklerine bilerek dokunulmaz; silinirlerse oyunlar onları yeniden derlerken takılır.

## Bilerek eklenmeyenler

| Ayar | Neden yok |
|---|---|
| Defender / Windows Update / Bellek Bütünlüğü'nü kapatmak | Güvenlik deliği |
| Spectre/Meltdown korumalarını kapatmak | Güvenlik deliği |
| Hizmetleri toplu kapatmak (SysMain, Arama vb.) | Faydası tartışmalı, bir şeyleri bozma riski yüksek |
| "RAM temizleyici" | Windows belleği zaten yönetir; boşaltılan bellek hemen geri dolar, takılmaya yol açar |
| Nagle, HPET, `bcdedit` ayarları, `SystemResponsiveness` | Modern Windows'ta ölçülebilir etkisi yok ya da tersine çalışıyor |
| Gölgelendirici önbelleğini silmek | Oyunlarda takılmaya yol açar |

## Ekran görüntüleri

| Optimizasyonlar | Ping |
|---|---|
| ![](https://raw.githubusercontent.com/dursuncankaymak/Pc-optimization/screenshots/tweaks.png) | ![](https://raw.githubusercontent.com/dursuncankaymak/Pc-optimization/screenshots/ping.png) |

| Başlangıç | Temizlik |
|---|---|
| ![](https://raw.githubusercontent.com/dursuncankaymak/Pc-optimization/screenshots/startup.png) | ![](https://raw.githubusercontent.com/dursuncankaymak/Pc-optimization/screenshots/cleaner.png) |

(Ekran görüntüleri her sürümde CI tarafından otomatik çekilir.)

## Geliştirme

C# / WPF, .NET Framework 4.7.2 (Windows 10 1809+ ve Windows 11'de hazır gelir). Arayüz koddan kurulur, görsel stiller `src/AriaBoost/UI/Theme.xaml`'da; bu sayede proje Linux'ta da derlenebilir.

```bash
dotnet build src/AriaBoost/AriaBoost.csproj -c Release
dotnet build tests/AriaBoost.SelfTest/AriaBoost.SelfTest.csproj -c Release
```

```
src/AriaBoost/
├── Core/                 # Optimizasyonlar, yedekleme, başlangıç, temizlik (arayüzden bağımsız)
│   ├── TweakCatalog.cs   # Tüm optimizasyonların listesi
│   ├── RegistryTweak.cs  # Kayıt defteri tabanlı optimizasyon + yedekleme
│   ├── PowerPlans.cs     # Güç planı (powrprof API)
│   ├── NetworkDiagnostics.cs # Ping testi ve teşhis
│   ├── StartupManager.cs # Görev Yöneticisi ile uyumlu başlangıç yönetimi
│   └── Cleaner.cs        # Güvenli disk temizliği
└── UI/                   # WPF arayüzü
tests/AriaBoost.SelfTest/ # Gerçek Windows'ta uçtan uca uygula / geri al testleri
```

Yeni bir optimizasyon eklemek için `TweakCatalog.cs`'e bir giriş eklemen yeterli; CI testi onu otomatik olarak uygulayıp geri alarak dener.
