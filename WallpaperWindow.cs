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
        static extern IntPtr GetDesktopWindow();
        
        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;

        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private Panel _videoPanel;  // This will hold the video
        private string _logPath;

        public WallpaperWindow()
        {
            _logPath = Path.Combine(Application.StartupPath, "debug.txt");
            Log("WallpaperWindow starting");
            
            // Form setup
            this.FormBorderStyle = FormBorderStyle.None;
            this.Bounds = Screen.PrimaryScreen.Bounds;
            this.TopMost = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.Black;
            
            // Create a panel for video
            _videoPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                Location = new Point(0, 0)
            };
            this.Controls.Add(_videoPanel);
            
            Log("Finding desktop window");
            IntPtr progman = FindWindow("Progman", null);
            SetParent(this.Handle, progman);
            
            // Force window to bottom
            SetWindowPos(this.Handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
            
            Log($"Form handle: {this.Handle}");
            Log($"Panel handle: {_videoPanel.Handle}");
            
            try
            {
                Log("Initializing VLC");
                _libVLC = new LibVLC();
                _mediaPlayer = new MediaPlayer(_libVLC);
                
                // IMPORTANT: Set video output to the panel's handle, not the form
                _mediaPlayer.Hwnd = _videoPanel.Handle;
                
                _mediaPlayer.Playing += (s, e) => Log("Event: Playing");
                _mediaPlayer.Stopped += (s, e) => Log("Event: Stopped");
                _mediaPlayer.EncounteredError += (s, e) => Log("Event: EncounteredError");
                
                Log("VLC initialized");
            }
            catch (Exception ex)
            {
                Log($"VLC init error: {ex}");
                MessageBox.Show($"VLC init error: {ex.Message}");
            }
        }

        public void PlayVideo(string path)
        {
            Log($"PlayVideo called with: {path}");
            
            try
            {
                if (!File.Exists(path))
                {
                    Log($"File not found: {path}");
                    MessageBox.Show("File not found");
                    return;
                }

                Log($"File exists: {path}, size: {new FileInfo(path).Length}");
                
                _mediaPlayer?.Stop();
                
                var media = new Media(_libVLC, path);
                media.AddOption(":no-audio");
                media.AddOption(":input-repeat=65535");
                
                _mediaPlayer.Play(media);
                
                Log($"MediaPlayer.State = {_mediaPlayer.State}");
            }
            catch (Exception ex)
            {
                Log($"Play error: {ex}");
                MessageBox.Show($"Play error: {ex.Message}");
            }
        }

        public void Stop()
        {
            Log("Stop called");
            _mediaPlayer?.Stop();
        }

        private void Log(string message)
        {
            try
            {
                string logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}{Environment.NewLine}";
                File.AppendAllText(_logPath, logLine);
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Log("Form closing");
            _mediaPlayer?.Dispose();
            _libVLC?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
