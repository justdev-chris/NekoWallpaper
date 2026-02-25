using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LibVLCSharp.Shared;

namespace NekoWallpaper
{
    public class WallpaperWindow : Form
    {
        [DllImport("user32.dll")]
        static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        
        [DllImport("user32.dll")]
        static extern IntPtr FindWindow(string className, string windowName);
        
        [DllImport("user32.dll")]
        static extern int ShowWindow(IntPtr hWnd, int nCmdShow);

        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private string _currentFile;

        public WallpaperWindow()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.Bounds = Screen.PrimaryScreen.Bounds;
            this.TopMost = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.Black; // So we can see if window is there
            
            IntPtr progman = FindWindow("Progman", null);
            SetParent(this.Handle, progman);
            
            // Make window visible
            ShowWindow(this.Handle, 1);
            
            try
            {
                Core.Initialize();
                _libVLC = new LibVLC();
                _mediaPlayer = new MediaPlayer(_libVLC);
                _mediaPlayer.Hwnd = this.Handle;
                
                // Add some logging
                _mediaPlayer.Playing += (s, e) => Console.WriteLine("Video is playing");
                _mediaPlayer.Stopped += (s, e) => Console.WriteLine("Video stopped");
                _mediaPlayer.EndReached += (s, e) => Console.WriteLine("Video ended");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"VLC init error: {ex.Message}");
            }
        }

        public void PlayVideo(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    MessageBox.Show("File not found");
                    return;
                }

                _currentFile = path;
                
                // Stop current playback
                _mediaPlayer.Stop();
                
                // Create new media
                using (var media = new Media(_libVLC, path))
                {
                    // Configure media
                    media.AddOption(":input-repeat=65535"); // Loop forever
                    
                    // Play it
                    _mediaPlayer.Play(media);
                }
                
                Console.WriteLine($"Attempting to play: {path}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Play error: {ex.Message}");
            }
        }

        public void Stop()
        {
            _mediaPlayer?.Stop();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _mediaPlayer?.Dispose();
            _libVLC?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
