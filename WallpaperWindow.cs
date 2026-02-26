using System;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.IO.Compression;

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
            
            // Set FFmpeg path
            _ffmpegPath = Path.Combine(Application.StartupPath, "ffmpeg");
            
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
                        Log("Downloading full FFmpeg...");
                        Directory.CreateDirectory(_ffmpegPath);
                        
                        using (var client = new HttpClient())
                        {
                            // Download full FFmpeg build (includes ffplay)
                            string url = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
                            string zipPath = Path.Combine(_ffmpegPath, "ffmpeg.zip");
                            
                            var response = await client.GetAsync(url);
                            using (var fs = new FileStream(zipPath, FileMode.Create))
                            {
                                await response.Content.CopyToAsync(fs);
                            }
                            
                            // Extract
                            ZipFile.ExtractToDirectory(zipPath, _ffmpegPath, true);
                            
                            // Find the extracted folder and move files up
                            var extractedFolder = Directory.GetDirectories(_ffmpegPath)[0];
                            foreach (var file in Directory.GetFiles(extractedFolder))
                            {
                                string fileName = Path.GetFileName(file);
                                File.Move(file, Path.Combine(_ffmpegPath, fileName));
                            }
                            
                            // Clean up
                            Directory.Delete(extractedFolder, true);
                            File.Delete(zipPath);
                            
                            Log("FFmpeg downloaded and extracted");
                        }
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
                while (!File.Exists(ffplayPath) && attempts < 60)
                {
                    Log($"Waiting for ffplay.exe... attempt {attempts}");
                    await Task.Delay(1000);
                    attempts++;
                }
                
                if (!File.Exists(ffplayPath))
                {
                    Log("ffplay.exe not found!");
                    MessageBox.Show("ffplay.exe not found. FFmpeg download may have failed.");
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
                        WindowStyle = ProcessWindowStyle.Hidden,
                        WorkingDirectory = _ffmpegPath
                    },
                    EnableRaisingEvents = true
                };
                
                _ffplayProcess.Exited += (s, e) => {
                    Log("ffplay exited");
                    _isPlaying = false;
                    
                    if (!_isExiting && _currentFile != null)
                    {
                        Log("Restarting video");
                        PlayVideo(_currentFile);
                    }
                };
                
                _ffplayProcess.Start();
                _isPlaying = true;
                
                // Reparent ffplay window to our panel
                await Task.Delay(1000);
                FindAndReparentFFplay();
                
                Log($"Playback started, _isPlaying = {_isPlaying}");
            }
            catch (Exception ex)
            {
                Log($"Play error: {ex}");
                _isPlaying = false;
            }
        }

        private void FindAndReparentFFplay()
        {
            IntPtr ffplayHwnd = IntPtr.Zero;
            int attempts = 0;
            
            while (ffplayHwnd == IntPtr.Zero && attempts < 20)
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
                ShowWindow(ffplayHwnd, 1);
            }
        }

        public void Stop()
        {
            Log($"Stop called, current state - _isPlaying: {_isPlaying}");
            _currentFile = null;
            
            if (_isPlaying && _ffplayProcess != null && !_ffplayProcess.HasExited)
            {
                _ffplayProcess.Kill();
                _ffplayProcess.Dispose();
                _ffplayProcess = null;
                _isPlaying = false;
                Log("Stopped playback");
            }
            else
            {
                Log("Nothing to stop");
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
