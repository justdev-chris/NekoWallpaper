using System;
using System.Windows.Forms;

namespace NekoWallpaper
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            var wallpaper = new WallpaperWindow();
            var tray = new TrayMenu(wallpaper);
            
            Application.Run();
        }
    }
}
