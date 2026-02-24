using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LibVLCSharp.Shared;

namespace NekoWallpaper
{
    public class WallpaperWindow : Form
    {
        // Windows API stuff to stick to desktop
        [DllImport("user32.dll")]
        static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        
        [DllImport("user32.dll")]
        static extern IntPtr FindWindow(string className, string windowName);

        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private string _currentFile;

        public WallpaperWindow()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.Bounds = Screen.PrimaryScreen.Bounds;
            this.TopMost = false;
            this.ShowInTaskbar = false;
            
            // Stick to desktop
            IntPtr progman = FindWindow("Progman", null);
            SetParent(this.Handle, progman);
            
            // Setup VLC
            Core.Initialize();
            _libVLC = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVLC);
            _mediaPlayer.Hwnd = this.Handle;
        }

        public void PlayVideo(string path)
        {
            _currentFile = path;
            var media = new Media(_libVLC, path);
            
            // Loop when video ends
            _mediaPlayer.EndReached += (s, e) => 
            {
                _mediaPlayer.Stop();
                _mediaPlayer.Play(media);
            };
            
            _mediaPlayer.Play(media);
        }

        public void Stop()
        {
            _mediaPlayer.Stop();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _mediaPlayer?.Dispose();
            _libVLC?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
