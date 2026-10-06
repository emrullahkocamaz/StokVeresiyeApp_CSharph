# Bilensis – Stok & Cari Yönetim Sistemi

Modern, şık, hızlı ve tam donanımlı Ticari Stok & Veresiye Takip Otomasyonu.

## 🚀 Yeni ve Gelişmiş Özellikler

### 1. 📊 Modern Kontrol Paneli (Dashboard)
- **Canlı KPI Kartları:** Toplam Stok Değeri, Müşteri Veresiye Alacakları, Tedarikçi Borçları, Günlük Net Kasa Girişi.
- **⚠️ Kritik Stok Uyarıları Tablosu:** Stok seviyesi belirlenen kritik limitin altına düşen ürünlerin anlık takibi.
- **💰 Borçlu Müşteri Sıralaması:** En yüksek bakiyeli müşterilerin anlık listesi.
- **🕒 Son İşlemler:** Gerçekleşen son finansal ve ticari hareketlerin özeti.
- **⚡ Hızlı İşlem Kısayolları:** Tek tıkla satış, tahsilat, ürün veya stok hareketi ekleme.

### 2. ⚡ Entegre Hızlı Satış & Fiş Modülü (POS)
- Müşteri veya perakende peşin satış seçeneği.
- Ürün seçimi ile anlık mevcut stok, KDV ve birim fiyat getirme.
- **Entegre Çalışma:** Satış yapıldığında stoktan otomatik düşer, veresiye ise cariye borç kaydeder; peşin (Nakit/Kart/Havale) ise kasa hareketine anında tahsilat ekler!

### 3. 📦 Ürün & Stok Yönetimi
- Barkod, Ürün Kodu, Kategori, Birim, Alış Fiyatı, İskonto, KDV ve Özel Satış Fiyatı desteği.
- Anlık arama (Ad, Kod, Barkod, Kategoriye göre filtreleme).
- Sadece kritik seviyedeki ürünleri filtreleme seçeneği.
- **📋 Ürün Hareket Geçmişi:** İlgili ürünün hangi tarihte kimden alınıp kime satıldığının detaylı dökümü.
- **📊 Excel'e Aktar:** Tüm ürün listesini şık formatlı Excel raporu olarak indirme.

### 4. 👥 Cari & Veresiye Takip Sistemi
- Müşteri ve Tedarikçi ayrımı, telefon, e-posta, vergi dairesi/no, adres ve kredi limiti takibi.
- **📑 Cari Hesap Ekstresi:** Yürüyen bakiye (borç/alacak farkı) ile detaylı işlem ekstresi görüntüleme ve Excel'e aktarma.
- **💵 Hızlı Tahsilat / Ödeme Alma:** Seçilen cariye tek tıkla tahsilat veya ödeme kaydı ekleme.

### 5. 🔄 Stok Hareketleri
- Gelen, Satılan, İade Giriş ve Fire/Zayi hareket türleri.
- Tarih aralığına ve hareket türüne göre filtreleme.
- Excel formatında raporlama.

### 6. 💳 Kasa & Finansal Hareketler
- Satış, Alış, Tahsilat, Ödeme işlemleri.
- Nakit, Kredi Kartı, Havale/EFT, Çek/Senet ödeme yöntemleri ve Banka/Kasa takibi.
- Tarih ve işlem türüne göre filtreleme + Excel rapor çıktısı.

### 7. ⚙️ Veritabanı Yedekleme & Kurtarma (SQLite)
- **💾 Veritabanını Yedekle:** Tek tıkla veritabanının yedeğini `.db` dosyası olarak kaydetme.
- **📂 Yedekten Geri Yükle:** Alınan yedekten verileri kurtarma.
- **📥 Excel'den Toplu İçe Aktarım:** Excel tablosundaki tüm sayfaları (Ürünler, Cariler, Stok Hareketleri, Cari Hareketleri) sisteme aktarma.

---

## 💻 Çalıştırma ve Derleme

```bash
# Projeyi derleme
dotnet build

# Uygulamayı çalıştırma
dotnet run

# Tek dosya EXE olarak yayınlama
dotnet publish -c Release -r win-x64 --self-contained true
```
