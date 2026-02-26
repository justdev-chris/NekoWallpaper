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
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        
        [DllImport("user32.dll")]
        static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
        
        [DllImport("user32.dll")]
        static extern int ShowWindow(IntPtr hWnd, int nCmdShow);
        
        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        [DllImport("user32.dll")]
        static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const int WM_CLOSE = 0x0010;

        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private Panel _videoPanel;
        private string _logPath;
        private Media _currentMedia;
        private bool _isExiting = false;

        public WallpaperWindow()
        {
            _logPath = Path.Combine(Application.StartupPath, "debug.txt");
            Log("WallpaperWindow starting");
            
            // Form setup - FULL SCREEN
            this.FormBorderStyle = FormBorderStyle.None;
            this.Bounds = Screen.PrimaryScreen.Bounds;
            this.TopMost = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.Black;
            
            // Panel fills the entire form
            _videoPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                Size = this.Size
            };
            this.Controls.Add(_videoPanel);
            
            Log($"Form size: {this.Width}x{this.Height}");
            
            // Find proper desktop layer (BELOW icons)
            IntPtr progman = FindWindow("Progman", null);
            IntPtr workerw = IntPtr.Zero;
            
            // Trigger WorkerW creation
            SendMessage(progman, 0x052C, 0, 0);
            
            // Find WorkerW that contains SHELLDLL_DefView (icons)
            while ((workerw = FindWindowEx(IntPtr.Zero, workerw, "WorkerW", null)) != IntPtr.Zero)
            {
                IntPtr shellView = FindWindowEx(workerw, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView != IntPtr.Zero)
                {
                    // Found the layer with icons, now get the next WorkerW (wallpaper layer)
                    workerw = FindWindowEx(IntPtr.Zero, workerw, "WorkerW", null);
                    break;
                }
            }
            
            IntPtr target = (workerw != IntPtr.Zero) ? workerw : progman;
            Log($"Setting parent to: {target}");
            SetParent(this.Handle, target);
            
            // Position behind icons
            SetWindowPos(this.Handle, HWND_BOTTOM, 0, 0, this.Width, this.Height, SWP_NOACTIVATE);
            ShowWindow(this.Handle, 1);
            
            Log($"Form handle: {this.Handle}");
            Log($"Panel handle: {_videoPanel.Handle}");
            
            try
            {
                Log("Initializing VLC with verbose logging");
                
                // Enable VLC logging
                string vlcLogPath = Path.Combine(Application.StartupPath, "vlc-log.txt");
                string[] vlcArgs = new[] { 
                    "--verbose=2", 
                    $"--logfile={vlcLogPath}",
                    "--no-color",
                    "--image-decoder=ffmpeg",
                    "--codec=ffmpeg"
                };
                
                _libVLC = new LibVLC(vlcArgs);
                _mediaPlayer = new MediaPlayer(_libVLC);
                _mediaPlayer.Hwnd = _videoPanel.Handle;
                
                // Handle events properly
                _mediaPlayer.Playing += (s, e) => Log("Event: Playing");
                _mediaPlayer.Stopped += (s, e) => Log("Event: Stopped");
                _mediaPlayer.EndReached += (s, e) => 
                {
                    Log("Event: EndReached - restarting");
                    if (!_isExiting && _currentMedia != null)
                    {
                        _mediaPlayer.Stop();
                        _mediaPlayer.Play(_currentMedia);
                    }
                };
                _mediaPlayer.EncounteredError += (s, e) => Log("Event: EncounteredError");
                
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

                // Clean up old media
                _mediaPlayer?.Stop();
                _currentMedia?.Dispose();
                
                // Create new media
                _currentMedia = new Media(_libVLC, path);
                
                // Add options based on file type
                string extension = Path.GetExtension(path).ToLower();
                
                // Common options
                _currentMedia.AddOption(":no-audio");
                _currentMedia.AddOption(":input-repeat=65535");
                _currentMedia.AddOption(":aspect-ratio=fill");
                
                if (extension == ".gif")
                {
                    Log("Applying GIF-specific options");
                    // Force image decoder and framerate
                    _currentMedia.AddOption(":image-decoder=ffmpeg");
                    _currentMedia.AddOption(":codec=ffmpeg");
                    _currentMedia.AddOption(":image-fps=30");
                    _currentMedia.AddOption(":gif-fps=30");
                    _currentMedia.AddOption(":no-overlay");
                    _currentMedia.AddOption(":scale=Auto");
                }
                else if (extension == ".mp4")
                {
                    Log("Applying MP4-specific options");
                    _currentMedia.AddOption(":video-filter=scale");
                    _currentMedia.AddOption(":scale=1.0");
                }
                
                _mediaPlayer.Play(_currentMedia);
                Log($"MediaPlayer.State = {_mediaPlayer.State}");
                
                // Check if playing after a moment
                System.Threading.Timer timer = null;
                timer = new System.Threading.Timer((_) =>
                {
                    Log($"Delayed check - IsPlaying: {_mediaPlayer.IsPlaying}, State: {_mediaPlayer.State}");
                    timer?.Dispose();
                }, null, 1000, System.Threading.Timeout.Infinite);
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
            _currentMedia?.Dispose();
            _currentMedia = null;
        }

        public void RestoreOriginalWallpaper()
        {
            Log("Restoring original wallpaper");
            _isExiting = true;
            Stop();
            
            // Force desktop to refresh
            IntPtr progman = FindWindow("Progman", null);
            SendMessage(progman, 0x052C, 0, 0);
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
            RestoreOriginalWallpaper();
            _mediaPlayer?.Dispose();
            _libVLC?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
