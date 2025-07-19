using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ever
{
    internal class SpringApplicationHandler
    {
        private Process _springProcess;
        private readonly string _javaPath;
        private readonly string _springJarPath;
        private readonly string _workingDirectory;
        private readonly string _jvmArguments;
        private readonly string _springArguments;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _disposed = false;

        public event EventHandler<string> OutputReceived;
        public event EventHandler<string> ErrorReceived;
        public event EventHandler<int> ProcessExited;

        public bool IsRunning => _springProcess != null && !_springProcess.HasExited;
        public int? ProcessId => _springProcess?.Id;

        public SpringApplicationHandler(
            string springJarPath,
            string javaPath = "java",
            string workingDirectory = null,
            string jvmArguments = "-Xmx512m",
            string springArguments = "")
        {
            _springJarPath = springJarPath ?? throw new ArgumentNullException(nameof(springJarPath));
            _javaPath = javaPath;
            _workingDirectory = workingDirectory ?? Path.GetDirectoryName(springJarPath);
            _jvmArguments = jvmArguments;
            _springArguments = springArguments;
            _cancellationTokenSource = new CancellationTokenSource();

            ValidateConfiguration();
        }

        private void ValidateConfiguration()
        {
            if (!File.Exists(_springJarPath))
                throw new FileNotFoundException($"Spring JAR file not found: {_springJarPath}");

            if (!string.IsNullOrEmpty(_workingDirectory) && !Directory.Exists(_workingDirectory))
                throw new DirectoryNotFoundException($"Working directory not found: {_workingDirectory}");
        }

        public async Task<bool> StartAsync(int timeoutMs = 30000)
        {
            if (IsRunning)
                throw new InvalidOperationException("Spring application is already running");

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = _javaPath,
                    Arguments = BuildCommandArguments(),
                    WorkingDirectory = _workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true
                };

                _springProcess = new Process { StartInfo = startInfo };

                // Set up event handlers
                _springProcess.OutputDataReceived += OnOutputReceived;
                _springProcess.ErrorDataReceived += Process_ErrorDataReceived;
                _springProcess.Exited += OnProcessExited;
                _springProcess.EnableRaisingEvents = true;

                // Start the process
                if (!_springProcess.Start())
                    return false;

                // Begin async reading
                _springProcess.BeginOutputReadLine();
                _springProcess.BeginErrorReadLine();

                // Wait for application to start (look for Spring Boot startup messages)
                return await WaitForStartupAsync(timeoutMs);
            }
            catch (Exception ex)
            {
                OnErrorReceived(this, $"Failed to start Spring application: {ex.Message}");
                return false;
            }
        }

        

        public bool StartSync(int timeoutMs = 30000)
        {
            return StartAsync(timeoutMs).GetAwaiter().GetResult();
        }

        private string BuildCommandArguments()
        {
            var args = "";

            if (!string.IsNullOrEmpty(_jvmArguments))
                args += $"{_jvmArguments} ";

            args += $"-jar \"{_springJarPath}\"";

            if (!string.IsNullOrEmpty(_springArguments))
                args += $" {_springArguments}";

            return args;
        }

        private async Task<bool> WaitForStartupAsync(int timeoutMs)
        {
            var timeout = DateTime.Now.AddMilliseconds(timeoutMs);
            var startupDetected = false;

            // Create a temporary event handler to detect startup
            EventHandler<string> startupHandler = (sender, output) =>
            {
                if (output != null && (
                    output.Contains("Started") && output.Contains("Application") ||
                    output.Contains("Tomcat started on port") ||
                    output.Contains("Netty started on port") ||
                    output.Contains("JVM running for")))
                {
                    startupDetected = true;
                }
            };

            OutputReceived += startupHandler;

            try
            {
                while (DateTime.Now < timeout && IsRunning && !startupDetected)
                {
                    await Task.Delay(100, _cancellationTokenSource.Token);
                }

                return startupDetected && IsRunning;
            }
            finally
            {
                OutputReceived -= startupHandler;
            }
        }

        public async Task<bool> StopAsync(int timeoutMs = 10000)
        {
            if (!IsRunning)
                return true;

            try
            {
                // Try graceful shutdown first
                if (!_springProcess.HasExited)
                {
                    _springProcess.StandardInput.WriteLine(); // Send empty line to potentially trigger shutdown
                    await Task.Delay(1000);
                }

                // If still running, try to terminate gracefully
                if (!_springProcess.HasExited)
                {
                    _springProcess.CloseMainWindow();
                    if (!_springProcess.WaitForExit(timeoutMs))
                    {
                        // Force kill if necessary
                        _springProcess.Kill();
                        return _springProcess.WaitForExit(5000);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                OnErrorReceived(this, $"Error stopping Spring application: {ex.Message}");
                return false;
            }
        }

        public bool StopSync(int timeoutMs = 10000)
        {
            return StopAsync(timeoutMs).GetAwaiter().GetResult();
        }

        public async Task RestartAsync(int stopTimeoutMs = 10000, int startTimeoutMs = 30000)
        {
            await StopAsync(stopTimeoutMs);
            await Task.Delay(2000); // Give some time between stop and start
            await StartAsync(startTimeoutMs);
        }

        public void SendCommand(string command)
        {
            if (IsRunning && _springProcess.StandardInput != null)
            {
                _springProcess.StandardInput.WriteLine(command);
            }
        }

        private void OnOutputReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                OutputReceived?.Invoke(this, e.Data);
            }
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                OnErrorReceived(sender,e.Data);
            }
        }

        private void OnErrorReceived(object sender, String e)
        {
            if (e != null)
            {
                ErrorReceived?.Invoke(this, e);
            }
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            var exitCode = _springProcess?.ExitCode ?? -1;
            ProcessExited?.Invoke(this, exitCode);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cancellationTokenSource?.Cancel();

                if (IsRunning)
                {
                    try
                    {
                        _springProcess.Kill();
                        _springProcess.WaitForExit(5000);
                    }
                    catch { }
                }

                _springProcess?.Dispose();
                _cancellationTokenSource?.Dispose();
                _disposed = true;
            }
        }
    }
}
