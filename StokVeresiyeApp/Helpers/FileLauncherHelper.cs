using System.Diagnostics;

namespace StokVeresiyeApp.Helpers;

public static class FileLauncherHelper
{
    /// <summary>
    /// PDF veya herhangi bir belgeyi sistemdeki varsayılan uygulama, Edge, Chrome veya Explorer ile
    /// hata vermeden güvenli şekilde açar. Karşı bilgisayarda PDF okuyucu tanımlı olmasa bile Edge ile açmayı dener.
    /// </summary>
    public static bool OpenDocument(string? filePath, string documentTitle = "Belge")
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            MessageBox.Show($"{documentTitle} dosyası belirtilen konumda bulunamadı:\n\n{filePath}", 
                "Dosya Bulunamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        // 1. ADIM: Standart Windows Dosya İlişkilendirmesi (Varsayılan PDF Görüntüleyici)
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            // ShellExecute başarısız oldu (Karşı PC'de dosya ilişkilendirmesi eksik veya 'Uygulama bulunamadı')
        }

        // 2. ADIM: Microsoft Edge (Windows 10 ve Windows 11'in tamamında yerleşik PDF okuyucudur)
        try
        {
            string[] edgePaths =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application\msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft\Edge\Application\msedge.exe"),
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Microsoft\Edge\Application\msedge.exe"
            };

            foreach (var edge in edgePaths)
            {
                if (File.Exists(edge))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = edge,
                        Arguments = $"\"{filePath}\"",
                        UseShellExecute = false
                    });
                    return true;
                }
            }
        }
        catch { }

        // 3. ADIM: Google Chrome
        try
        {
            string[] chromePaths =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe"),
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"
            };

            foreach (var chrome in chromePaths)
            {
                if (File.Exists(chrome))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = chrome,
                        Arguments = $"\"{filePath}\"",
                        UseShellExecute = false
                    });
                    return true;
                }
            }
        }
        catch { }

        // 4. ADIM: cmd.exe start komutu ile Windows Shell'e devret
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c start \"\" \"{filePath}\"",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false
            });
            return true;
        }
        catch { }

        // 5. ADIM: Windows Gezgini (explorer.exe) ile aç
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{filePath}\"",
                UseShellExecute = false
            });
            return true;
        }
        catch { }

        // 6. ADIM: Windows "Birlikte Aç" (Open With) diyaloğunu göster
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "rundll32.exe",
                Arguments = $"shell32.dll,OpenAs_RunDLL \"{filePath}\"",
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Bu bilgisayarda PDF dosyalarını açacak bir uygulama (Microsoft Edge, Google Chrome veya Adobe Reader) bulunamadı.\n\n" +
                $"Lütfen bilgisayarınıza bir PDF görüntüleyici veya tarayıcı kurunuz.\n\nSistem Hatası: {ex.Message}",
                "PDF Görüntüleyici Bulunamadı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            return false;
        }
    }
}
