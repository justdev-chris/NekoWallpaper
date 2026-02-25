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
        
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
        
        [DllImport("user32.dll")]
        static extern int ShowWindow(IntPtr hWnd, int nCmdShow);
        
        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
        
        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
        
        private const int WM_CLOSE = 0x0010;

        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private Panel _videoPanel;
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
            
            _videoPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };
            this.Controls.Add(_videoPanel);
            
            // Find the desktop icon layer (WorkerW with SHELLDLL_DefView)
            IntPtr targetWindow = IntPtr.Zero;
            IntPtr progman = FindWindow("Progman", null);
            
            // Trigger creation of WorkerW if needed
            SendMessage(progman, 0x052C, IntPtr.Zero, IntPtr.Zero);
            
            // Find WorkerW that contains SHELLDLL_DefView (icons)
            IntPtr workerW = IntPtr.Zero;
            while ((workerW = FindWindowEx(IntPtr.Zero, workerW, "WorkerW", null)) != IntPtr.Zero)
            {
                IntPtr shellView = FindWindowEx(workerW, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView != IntPtr.Zero)
                {
                    targetWindow = workerW;
                    break;
                }
            }
            
            if (targetWindow != IntPtr.Zero)
            {
                Log($"Found WorkerW with icons: {targetWindow}");
                SetParent(this.Handle, targetWindow);
            }
            else
            {
                Log("Using Progman as fallback");
                SetParent(this.Handle, progman);
            }
            
            ShowWindow(this.Handle, 1);
            Log($"Form handle: {this.Handle}");
            
            try
            {
                Log("Initializing VLC");
                _libVLC = new LibVLC();
                _mediaPlayer = new MediaPlayer(_libVLC);
                _mediaPlayer.Hwnd = _videoPanel.Handle;
                
                _mediaPlayer.Playing += (s, e) => Log("Event: Playing");
                _mediaPlayer.Stopped += (s, e) => Log("Event: Stopped");
                
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
