using System;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading.Tasks;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

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
        private bool _isPlaying = false;
        private string _currentFile;
        private bool _isExiting = false;
        private string _ffmpegPath;

        public WallpaperWindow()
        {
            _logPath = Path.Combine(Application.StartupPath, "debug.txt");
            Log("WallpaperWindow starting");
            
            // Set FFmpeg path
            _ffmpegPath = Path.Combine(Application.StartupPath, "ffmpeg");
            FFmpeg.SetExecutablesPath(_ffmpegPath);
            
            // Form setup
            this.FormBorderStyle = FormBorderStyle.None;
            this.Bounds = Screen.PrimaryScreen.Bounds;
            this.TopMost = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.Black;
            
            // Panel for video
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
            
            // Download FFmpeg if needed
            Task.Run(async () => {
                try
                {
                    if (!Directory.Exists(_ffmpegPath) || !File.Exists(Path.Combine(_ffmpegPath, "ffplay.exe")))
                    {
                        Log("Downloading FFmpeg...");
                        Directory.CreateDirectory(_ffmpegPath);
                        await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, _ffmpegPath);
                        Log("FFmpeg downloaded");
                    }
                    else
                    {
                        Log("FFmpeg already exists");
                    }
                }
                catch (Exception ex)
                {
                    Log($"FFmpeg download error: {ex}");
                }
            });
        }

        public async void PlayVideo(string path)
        {
            Log($"PlayVideo called with: {path}");
            
            try
            {
                if (!File.Exists(path))
                {
                    Log($"File not found: {path}");
                    return;
                }

                Stop();
                _currentFile = path;
                
                // Wait for FFmpeg to be ready
                string ffplayPath = Path.Combine(_ffmpegPath, "ffplay.exe");
                int attempts = 0;
                while (!File.Exists(ffplayPath) && attempts < 30)
                {
                    Log($"Waiting for ffplay.exe... attempt {attempts}");
                    await Task.Delay(100);
                    attempts++;
                }
                
                if (!File.Exists(ffplayPath))
                {
                    Log("ffplay.exe not found!");
                    return;
                }
                
                // Build ffplay command
                string args = $"-window_title \"NekoWallpaper\" -left 0 -top 0 -x {this.Width} -y {this.Height} -loop 0 -noborder -alwaysontop false \"{path}\"";
                
                Log($"Starting ffplay: {ffplayPath} {args}");
                
                _ffplayProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffplayPath,
                        Arguments = args,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    },
                    EnableRaisingEvents = true
                };
                
                _ffplayProcess.Exited += (s, e) => {
                    Log("ffplay exited");
                    _isPlaying = false;
                    
                    // Auto-restart if we're not exiting
                    if (!_isExiting && _currentFile != null)
                    {
                        Log("Restarting video");
                        PlayVideo(_currentFile);
                    }
                };
                
                _ffplayProcess.Start();
                _isPlaying = true;
                
                // Reparent ffplay window to our panel
                await Task.Delay(500);
                FindAndReparentFFplay();
                
                Log("Playback started");
            }
            catch (Exception ex)
            {
                Log($"Play error: {ex}");
            }
        }

        private void FindAndReparentFFplay()
        {
            // Find the ffplay window and set its parent to our panel
            IntPtr ffplayHwnd = IntPtr.Zero;
            int attempts = 0;
            
            while (ffplayHwnd == IntPtr.Zero && attempts < 10)
            {
                ffplayHwnd = FindWindow(null, "NekoWallpaper");
                attempts++;
                System.Threading.Thread.Sleep(100);
            }
            
            if (ffplayHwnd != IntPtr.Zero)
            {
                Log($"Found ffplay window: {ffplayHwnd}");
                SetParent(ffplayHwnd, _videoPanel.Handle);
                SetWindowPos(ffplayHwnd, HWND_BOTTOM, 0, 0, this.Width, this.Height, SWP_NOACTIVATE);
            }
        }

        public void Stop()
        {
            Log("Stop called");
            _isPlaying = false;
            _currentFile = null;
            
            if (_ffplayProcess != null && !_ffplayProcess.HasExited)
            {
                _ffplayProcess.Kill();
                _ffplayProcess.Dispose();
                _ffplayProcess = null;
            }
        }

        public void RestoreOriginalWallpaper()
        {
            Log("Restoring original wallpaper");
            _isExiting = true;
            Stop();
            
            // Kill and restart Explorer
            foreach (var process in Process.GetProcessesByName("explorer"))
            {
                try { process.Kill(); } catch { }
            }
            
            // Explorer will auto-restart
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
