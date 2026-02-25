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
                Log("Initializing VLC with verbose logging");
                string[] vlcArgs = new[] { "--verbose=2", "--no-color", "--logfile=vlc-log.txt" };
                _libVLC = new LibVLC(vlcArgs);
                _mediaPlayer = new MediaPlayer(_libVLC);
                _mediaPlayer.Hwnd = this.Handle;
                
                // Add event handlers
                _mediaPlayer.Playing += (s, e) => Log("Event: Playing");
                _mediaPlayer.Stopped += (s, e) => Log("Event: Stopped");
                _mediaPlayer.EndReached += (s, e) => Log("Event: EndReached");
                _mediaPlayer.Buffering += (s, e) => Log($"Event: Buffering {e.CacheLevel}%");
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

                Log($"File exists: {path}, size: {new FileInfo(path).Length}");
                
                _currentFile = path;
                _mediaPlayer?.Stop();
                
                Log("Creating media with GIF options");
                
                // Check if it's a GIF
                string extension = Path.GetExtension(path).ToLower();
                string[] mediaOptions;
                
                if (extension == ".gif")
                {
                    mediaOptions = new[] { 
                        ":no-audio", 
                        ":input-repeat=65535",
                        ":gif-fps=25",
                        ":image-fps=25",
                        ":no-overlay"
                    };
                }
                else
                {
                    mediaOptions = new[] { 
                        ":no-audio", 
                        ":input-repeat=65535" 
                    };
                }
                
                var media = new Media(_libVLC, path, mediaOptions);
                
                Log("Playing media");
                _mediaPlayer.Play(media);
                
                // Don't dispose media yet
                Log($"MediaPlayer.IsPlaying = {_mediaPlayer.IsPlaying}");
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
