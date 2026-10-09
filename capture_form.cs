using System;
using System.Drawing;
using System.Windows.Forms;
using StokVeresiyeApp;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Services;

namespace TestScreenshot
{
    class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                Database.InitializeDatabase();
                UserService.Login("admin", "admin123");

                var form = new MainForm();
                form.Width = 1400;
                form.Height = 900;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(0, 0);
                form.Show();
                Application.DoEvents();

                using var bmp = new Bitmap(1400, 900);
                form.DrawToBitmap(bmp, new Rectangle(0, 0, 1400, 900));
                
                string outPath = @"C:\Users\emrullah.kocamaz.ASEKER\.gemini\antigravity-ide\brain\00e64a36-1d57-488e-a2f5-6a06ed54f156\modern_saas_live_screen.png";
                bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
                Console.WriteLine("SUCCESS: " + outPath);
                form.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.ToString());
            }
        }
    }
}
