using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;

namespace ever
{
    public partial class App : System.Windows.Application
    {
        private NotifyIcon _trayIcon;
        private SpringApplicationHandler _springHandler;
        private bool _isShuttingDown = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Prevent multiple instances
            if (IsAnotherInstanceRunning())
            {
                System.Windows.Forms.MessageBox.Show("EVER is already running. Check the system tray.", "EVER",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                Shutdown();
                return;
            }

            // Initialize Spring handler first
            InitializeSpringHandler();

            // Initialize tray icon
            InitializeTrayIcon();

            // Start Spring application asynchronously
            _ = StartSpringApplicationAsync();
        }

        private bool IsAnotherInstanceRunning()
        {
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var processes = System.Diagnostics.Process.GetProcessesByName(currentProcess.ProcessName);
            return processes.Length > 1;
        }

        private void InitializeSpringHandler()
        {
            try
            {
                _springHandler = new SpringApplicationHandler(
                    springJarPath: GetSpringJarPath(),
                    javaPath: GetJavaPath(),
                    jvmArguments: "-Xmx2g -Dspring.profiles.active=prod",
                    springArguments: "--server.port=8080"
                );

                // Subscribe to events with proper error handling
                _springHandler.OutputReceived += OnSpringOutput;
                _springHandler.ErrorReceived += OnSpringError;
                _springHandler.ProcessExited += OnSpringProcessExited;
            }
            catch (Exception ex)
            {
                ShowTrayMessage($"Failed to initialize Spring handler: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private string GetSpringJarPath()
        {
            const string jarFileName = "server.jar";

            // 1. Look in current directory first
            var currentDirPath = Path.Combine(Directory.GetCurrentDirectory(), jarFileName);
            if (File.Exists(currentDirPath))
                return currentDirPath;

            // 2. Look in $HOME/server directory
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var serverDirPath = Path.Combine(homeDir, "server", jarFileName);
            if (File.Exists(serverDirPath))
                return serverDirPath;

            // 3. Fallback to current directory (even if file doesn't exist)
            Console.WriteLine($"Warning: JAR file '{jarFileName}' not found. Expected locations:");
            Console.WriteLine($"  - {currentDirPath}");
            Console.WriteLine($"  - {serverDirPath}");

            return currentDirPath;
        }

        private string GetJavaPath()
        {
            // Try to find Java automatically
            var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome))
            {
                var javaExe = Path.Combine(javaHome, "bin", "java.exe");
                if (File.Exists(javaExe))
                    return javaExe;
            }

            // Try common installation paths
            var commonPaths = new[]
            {
                @"C:\Program Files\Java\jdk-17\bin\java.exe",
                @"C:\Program Files\Java\jdk-11\bin\java.exe",
                @"C:\Program Files (x86)\Java\jdk-17\bin\java.exe",
                @"C:\Program Files (x86)\Java\jdk-11\bin\java.exe"
            };

            foreach (var path in commonPaths)
            {
                if (File.Exists(path))
                    return path;
            }

            return "java"; // Fallback to PATH
        }

        private async Task StartSpringApplicationAsync()
        {
            if (_springHandler == null || _isShuttingDown)
                return;

            try
            {
                ShowTrayMessage("Starting Spring application...", ToolTipIcon.Info);

                bool started = await _springHandler.StartAsync(timeoutMs: 60000);

                if (started)
                {
                    ShowTrayMessage($"Spring application started successfully! PID: {_springHandler.ProcessId}",
                        ToolTipIcon.Info);
                }
                else
                {
                    ShowTrayMessage("Failed to start Spring application", ToolTipIcon.Error);
                }
            }
            catch (Exception ex)
            {
                ShowTrayMessage($"Error starting Spring: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private void OnSpringOutput(object sender, string output)
        {
            // Log to file or debug console instead of Console.WriteLine
            System.Diagnostics.Debug.WriteLine($"Spring Output: {output}");
        }

        private void OnSpringError(object sender, string error)
        {
            System.Diagnostics.Debug.WriteLine($"Spring Error: {error}");

            // Show critical errors in tray
            if (error.Contains("Exception") || error.Contains("Error"))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ShowTrayMessage($"Spring Error: {error}", ToolTipIcon.Warning);
                }));
            }
        }

        private void OnSpringProcessExited(object sender, int exitCode)
        {
            if (!_isShuttingDown)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ShowTrayMessage($"Spring application exited unexpectedly (code: {exitCode})",
                        ToolTipIcon.Warning);

                    // Optionally restart Spring application
                    var result = System.Windows.Forms.MessageBox.Show("Spring application has stopped. Restart it?",
                        "EVER", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        _ = StartSpringApplicationAsync();
                    }
                }));
            }
        }

        private void InitializeTrayIcon()
        {
            _trayIcon = new NotifyIcon
            {
                Icon = GetApplicationIcon(),
                Text = "EVER - Spring Application Manager",
                Visible = true
            };

            _trayIcon.MouseClick += HandleTrayIconClick;
            _trayIcon.ContextMenuStrip = CreateContextMenu();
        }

        public static System.Drawing.Icon GetApplicationIcon()
        {
            try
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "roseEver.ico");
                if (File.Exists(iconPath))
                    return new System.Drawing.Icon(iconPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load icon: {ex.Message}");
            }

            // Fallback to default application icon
            return System.Drawing.SystemIcons.Application;
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var contextMenu = new ContextMenuStrip();

            // Spring status item
            var statusItem = new ToolStripMenuItem("Spring Status: Checking...");
            statusItem.Enabled = false;
            contextMenu.Items.Add(statusItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            // Show/Hide window
            contextMenu.Items.Add("Show Window", null, (s, args) => ShowWindow());

            // Spring controls
            contextMenu.Items.Add("Restart Spring", null, async (s, args) => await RestartSpringAsync());
            contextMenu.Items.Add("Stop Spring", null, async (s, args) => await StopSpringAsync());

            contextMenu.Items.Add(new ToolStripSeparator());

            // Exit
            contextMenu.Items.Add("Exit", null, (s, args) => ExitApplication());

            // Update status periodically
            var timer = new System.Windows.Forms.Timer { Interval = 5000 };
            timer.Tick += (s, e) => UpdateSpringStatus(statusItem);
            timer.Start();

            return contextMenu;
        }

        private void UpdateSpringStatus(ToolStripMenuItem statusItem)
        {
            if (_springHandler != null)
            {
                statusItem.Text = _springHandler.IsRunning
                    ? $"Spring Status: Running (PID: {_springHandler.ProcessId})"
                    : "Spring Status: Stopped";
            }
        }

        private void HandleTrayIconClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ToggleWindow();
            }
        }

        private void ToggleWindow()
        {
            if (MainWindow == null)
            {
                MainWindow = new MainWindow();
                MainWindow.Closed += (s, e) => MainWindow = null; // Clear reference on close
               

                // Window configuration
                MainWindow.Width = 300;
                MainWindow.Height = 180;
                MainWindow.ResizeMode = ResizeMode.CanResize;
                var mousePos = Cursor.Position;
                MainWindow.Left = mousePos.X - (MainWindow.Width / 2);
                MainWindow.Top = mousePos.Y - 250;
               
                MainWindow.Title = "EVER";



            }
            if (!MainWindow.IsVisible)
            {
                ShowWindow();
            }
            else
            {
                MainWindow.Hide();
            }
        }

        private void ShowWindow()
        {
            

            // Position near cursor but ensure it's visible
            var mousePos = Cursor.Position;
            //var workingArea = Screen.FromPoint(mousePos).WorkingArea;

            //MainWindow.Left = Math.Max(0, Math.Min(mousePos.X - MainWindow.Width / 2,
            //    workingArea.Right - MainWindow.Width));
            //MainWindow.Top = Math.Max(0, Math.Min(mousePos.Y - 180,
            //    workingArea.Bottom - MainWindow.Height));

            MainWindow.Left = mousePos.X - (MainWindow.Width / 2);
            MainWindow.Top = mousePos.Y - 180;

            MainWindow.Show();
            MainWindow.WindowState = WindowState.Normal;
            MainWindow.Activate();
        }

        private async Task RestartSpringAsync()
        {
            if (_springHandler != null)
            {
                ShowTrayMessage("Restarting Spring application...", ToolTipIcon.Info);
                await _springHandler.RestartAsync();
            }
        }

        private async Task StopSpringAsync()
        {
            if (_springHandler != null && _springHandler.IsRunning)
            {
                ShowTrayMessage("Stopping Spring application...", ToolTipIcon.Info);
                await _springHandler.StopAsync();
            }
        }

        public void ShowTrayMessage(string message, ToolTipIcon icon)
        {
            _trayIcon?.ShowBalloonTip(3000, "EVER", message, icon);
        }

        private async void ExitApplication()
        {
            _isShuttingDown = true;

            // Stop Spring application gracefully
            if (_springHandler != null && _springHandler.IsRunning)
            {
                ShowTrayMessage("Stopping Spring application...", ToolTipIcon.Info);
                await _springHandler.StopAsync(timeoutMs: 5000);
            }

            // Clean up tray icon
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }

            // Dispose Spring handler
            _springHandler?.Dispose();

            // Shutdown application
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (!_isShuttingDown)
            {
                _isShuttingDown = true;

                // Cleanup resources
                _springHandler?.Dispose();
                _trayIcon?.Dispose();
            }

            base.OnExit(e);
        }
    }
}