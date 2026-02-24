using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;

namespace NekoWallpaper
{
    public class TrayMenu
    {
        private NotifyIcon _trayIcon;
        private WallpaperWindow _wallpaper;

        public TrayMenu(WallpaperWindow wallpaper)
        {
            _wallpaper = wallpaper;
            
            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "NekoWallpaper",
                Visible = true
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add("Choose Wallpaper", null, (s, e) => ChooseWallpaper());
            menu.Items.Add("Stop", null, (s, e) => _wallpaper.Stop());
            menu.Items.Add("-");
            menu.Items.Add("Exit", null, (s, e) => Application.Exit());

            _trayIcon.ContextMenuStrip = menu;
        }

        private void ChooseWallpaper()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Media Files|*.mp4;*.gif|MP4|*.mp4|GIF|*.gif";
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _wallpaper.PlayVideo(dialog.FileName);
                }
            }
        }
    }
}
