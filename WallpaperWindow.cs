using System;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading.Tasks;

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

        private Panel _videoPanel;
        private string _logPath;
        private Process _ffplayProcess;
        private string _currentFile;
        private bool _isPlaying = false;
        private bool _isExiting = false;
        private string _ffmpegPath;

        public WallpaperWindow()
        {
            _logPath = Path.Combine(Application.StartupPath, "debug.txt");
            Log("WallpaperWindow starting");
            
            _ffmpegPath = Path.Combine(Application.StartupPath, "ffmpeg");
            
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
            
            // Desktop layering
            IntPtr progman = FindWindow("Progman", null);
            IntPtr workerw = IntPtr.Zero;
            
            SendMessage(progman, 0x052C, 0, 0);
            
            while ((workerw = FindWindowEx(IntPtr.Zero, workerw, "WorkerW", null)) != IntPtr.Zero)
            {
                IntPtr shellView = FindWindowEx(workerw, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView != IntPtr.Zero)
                {
                    workerw = FindWindowEx(IntPtr.Zero, workerw, "WorkerW", null);
                    break;
                }
            }
            
            IntPtr target = (workerw != IntPtr.Zero) ? workerw : progman;
            SetParent(this.Handle, target);
            SetWindowPos(this.Handle, HWND_BOTTOM, 0, 0, this.Width, this.Height, SWP_NOACTIVATE);
            ShowWindow(this.Handle, 1);
            
            Log($"Form handle: {this.Handle}");
            Log($"FFmpeg path: {_ffmpegPath}");
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

                Stop();
                _currentFile = path;
                
                string ffplayPath = Path.Combine(_ffmpegPath, "ffplay.exe");
                
                if (!File.Exists(ffplayPath))
                {
                    Log($"ffplay.exe not found at: {ffplayPath}");
                    MessageBox.Show("ffplay.exe not found in ffmpeg folder");
                    return;
                }
                
                // SUPER SIMPLE - just try to play the video in a window
                string args = $"\"{path}\"";
                
                Log($"Starting: {ffplayPath} {args}");
                
                _ffplayProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffplayPath,
                        Arguments = args,
                        UseShellExecute = false,
                        CreateNoWindow = false, // Let's see the window for debugging
                        WindowStyle = ProcessWindowStyle.Normal
                    },
                    EnableRaisingEvents = true
                };
                
                _ffplayProcess.Exited += (s, e) => {
                    Log("ffplay exited");
                    _isPlaying = false;
                };
                
                _ffplayProcess.Start();
                _isPlaying = true;
                
                Log($"Process started: {_ffplayProcess.Id}");
            }
            catch (Exception ex)
            {
                Log($"Play error: {ex}");
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        public void Stop()
        {
            Log($"Stop called");
            _currentFile = null;
            
            if (_ffplayProcess != null && !_ffplayProcess.HasExited)
            {
                _ffplayProcess.Kill();
                _ffplayProcess.Dispose();
                _ffplayProcess = null;
                _isPlaying = false;
                Log("Process killed");
            }
        }

        public void RestoreOriginalWallpaper()
        {
            Log("Restoring original wallpaper");
            _isExiting = true;
            Stop();
            
            foreach (var process in Process.GetProcessesByName("explorer"))
            {
                try { process.Kill(); } catch { }
            }
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
            base.OnFormClosing(e);
        }
    }
}
