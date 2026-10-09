using Krypton.Toolkit;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Forms;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--test-pdf")
        {
            var res = InvoiceParserService.ParseInvoiceFile(args[1]);
            Console.WriteLine($"SUCCESS: {res.Success}");
            Console.WriteLine($"INVOICE NO: {res.InvoiceNumber}");
            Console.WriteLine($"GRAND TOTAL: {res.GrandTotal}");
            Console.WriteLine($"SUBTOTAL: {res.SubTotal}");
            Console.WriteLine($"VAT: {res.VatTotal}");
            Console.WriteLine($"ITEMS COUNT: {res.Items.Count}");
            double sumItems = 0;
            foreach (var it in res.Items)
            {
                Console.WriteLine($"#{it.LineNo} | Barcode: {it.Barcode} | Name: {it.ItemName} | Qty: {it.Quantity} | Price: {it.UnitPrice} | Disc%: {it.DiscountPercent} | LineTotal: {it.LineTotal}");
                sumItems += it.LineTotal;
            }
            Console.WriteLine($"SUM OF ITEMS: {sumItems:N2}");
            return;
        }

        ApplicationConfiguration.Initialize();
        try
        {
            Application.SetDefaultFont(new Font("Segoe UI", 9.5f, FontStyle.Regular));
        }
        catch { }
        AppThemeService.InitializeTheme();

        // 1. Beklenmeyen çökmeleri yakalamak için global hata yakalayıcılar
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) =>
        {
            MessageBox.Show(
                $"Uygulama Hatası Oluştu:\n\n{e.Exception.Message}\n\nDetay:\n{e.Exception.StackTrace}",
                "Bilensis - Sistem Hatası",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            MessageBox.Show(
                $"Kritik Sistem Hatası:\n\n{ex?.Message ?? e.ExceptionObject?.ToString()}",
                "Bilensis - Kritik Hata",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        };

        // 2. Veritabanı Başlatma ve Bağlantı Güvencesi
        bool databaseReady = false;
        while (!databaseReady)
        {
            try
            {
                Database.Initialize();
                LicenseService.Initialize();
                UserService.Initialize();
                InvoiceService.MigrateExistingPdfsToDatabase();
                _ = Task.Run(() => MobileScannerService.Start());
                databaseReady = true;
            }
            catch (Exception ex)
            {
                var dialogResult = MessageBox.Show(
                    "Veritabanına bağlanılamadı!\n\n" +
                    "Olası Sebepler:\n" +
                    "1. Bu bilgisayarda Microsoft SQL Server veya SQL LocalDB kurulu olmayabilir.\n" +
                    "2. SQL Server servisi kapalı veya bağlantı ayarları farklı bir sunucuya ayarlı olabilir.\n\n" +
                    $"Hata Detayı: {ex.Message}\n\n" +
                    "Sunucu ve veritabanı ayarlarını yapılandırmak için 'Evet' butonuna tıklayınız.\n" +
                    "Çıkmak için 'Hayır' butonuna tıklayınız.",
                    "Veritabanı Bağlantı Uyarısı - Bilensis",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                );

                if (dialogResult == DialogResult.Yes)
                {
                    using var configForm = new DatabaseConfigDialog();
                    if (configForm.ShowDialog() != DialogResult.OK)
                    {
                        // Kullanıcı ayar yapmadan iptal ettiyse çık
                        return;
                    }
                }
                else
                {
                    return;
                }
            }
        }

        // 3. Lisans Kontrolü (Lisanssız veya süresi dolmuşsa aktivasyon açılır)
        if (!LicenseService.IsLicenseActive())
        {
            using var licForm = new LicenseActivationForm();
            if (licForm.ShowDialog() != DialogResult.OK || !LicenseService.IsLicenseActive())
            {
                // Geçerli lisans girilmediyse uygulama sonlandırılır
                return;
            }
        }

        // 4. Giriş (Login) ve Oturum Döngüsü
        while (true)
        {
            using var loginForm = new LoginForm();
            if (loginForm.ShowDialog() != DialogResult.OK || UserService.CurrentUser == null)
            {
                // Giriş yapılmadı veya kapatıldı
                return;
            }

            // 5. Ana Uygulama Formu
            var mainForm = new MainForm();
            Application.Run(mainForm);

            // Eğer kullanıcı oturumu kapattıysa (Logout) tekrar Login ekranına döner, normal kapatıldıysa çıkış yapılır
            if (!mainForm.IsLoggedOut)
            {
                break;
            }
        }
    }
}

