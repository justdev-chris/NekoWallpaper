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
        private string _logPath;

        public WallpaperWindow()
        {
            _logPath = Path.Combine(Application.StartupPath, "debug.txt");
            Log("WallpaperWindow starting");
            
            this.FormBorderStyle = FormBorderStyle.None;
            this.Bounds = Screen.PrimaryScreen.Bounds;
            this.TopMost = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.Black;
            
            Log("Setting parent to Progman");
            IntPtr progman = FindWindow("Progman", null);
            SetParent(this.Handle, progman);
            ShowWindow(this.Handle, 1);
            
            try
            {
                Log("Initializing VLC");
                Core.Initialize();
                _libVLC = new LibVLC();
                _mediaPlayer = new MediaPlayer(_libVLC);
                _mediaPlayer.Hwnd = this.Handle;
                Log("VLC initialized");
            }
            catch (Exception ex)
            {
                Log($"VLC init error: {ex}");
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
                    return;
                }

                Log($"File exists: {path}, size: {new FileInfo(path).Length}");
                
                _currentFile = path;
                _mediaPlayer?.Stop();
                
                Log("Creating media");
                using (var media = new Media(_libVLC, path))
                {
                    Log("Adding loop option");
                    media.AddOption(":input-repeat=65535");
                    
                    Log("Playing media");
                    _mediaPlayer.Play(media);
                    
                    Log($"MediaPlayer.IsPlaying = {_mediaPlayer.IsPlaying}");
                }
            }
            catch (Exception ex)
            {
                Log($"Play error: {ex}");
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
