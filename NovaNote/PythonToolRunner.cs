using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NovaNote
{
    internal sealed class ProcessResult
    {
        public int ExitCode;
        public string Stdout = "";
        public string Stderr = "";
        public bool Cancelled;
        public bool TimedOut;
    }

    internal static class PythonToolRunner
    {
        static readonly SemaphoreSlim _pythonLock = new SemaphoreSlim(1, 1);
        static readonly SemaphoreSlim _lintLock = new SemaphoreSlim(1, 1);
        static readonly SemaphoreSlim _formatterLock = new SemaphoreSlim(1, 1);
        static readonly SemaphoreSlim _lspLock = new SemaphoreSlim(1, 1);

        static string _pythonExe;
        static bool _pythonChecked;
        static string _lintModule;
        static bool _lintChecked;
        static string _formatterModule;
        static bool _formatterChecked;
        static string[] _lspServer;
        static bool _lspChecked;

        public static async Task<string> GetPythonExeAsync(CancellationToken ct)
        {
            await _pythonLock.WaitAsync(ct);
            try
            {
                if (!_pythonChecked)
                {
                    _pythonExe = await DetectPythonExeAsync(ct);
                    _pythonChecked = true;
                }
                return _pythonExe;
            }
            finally { _pythonLock.Release(); }
        }

        public static async Task<string> GetLintModuleAsync(CancellationToken ct)
        {
            await _lintLock.WaitAsync(ct);
            try
            {
                if (!_lintChecked)
                {
                    _lintModule = await DetectLintModuleAsync(ct);
                    _lintChecked = true;
                }
                return _lintModule;
            }
            finally { _lintLock.Release(); }
        }

        public static async Task<string> GetFormatterModuleAsync(CancellationToken ct)
        {
            await _formatterLock.WaitAsync(ct);
            try
            {
                if (!_formatterChecked)
                {
                    _formatterModule = await DetectFormatterModuleAsync(ct);
                    _formatterChecked = true;
                }
                return _formatterModule;
            }
            finally { _formatterLock.Release(); }
        }

        public static async Task<string[]> GetLspServerAsync(CancellationToken ct)
        {
            await _lspLock.WaitAsync(ct);
            try
            {
                if (!_lspChecked)
                {
                    _lspServer = await DetectLspServerAsync(ct);
                    _lspChecked = true;
                }
                return _lspServer;
            }
            finally { _lspLock.Release(); }
        }

        static async Task<string> DetectPythonExeAsync(CancellationToken ct)
        {
            foreach (var exe in new[] { "python", "python3", "py" })
                if (await RunQuietAsync(exe, new[] { "--version" }, ct)) return exe;
            return null;
        }

        static async Task<string> DetectLintModuleAsync(CancellationToken ct)
        {
            var py = await GetPythonExeAsync(ct);
            if (py == null) return null;
            if (await RunQuietAsync(py, new[] { "-m", "ruff", "--version" }, ct)) return "ruff";
            if (await RunQuietAsync(py, new[] { "-m", "flake8", "--version" }, ct)) return "flake8";
            return null;
        }

        static async Task<string> DetectFormatterModuleAsync(CancellationToken ct)
        {
            var py = await GetPythonExeAsync(ct);
            if (py == null) return null;
            if (await RunQuietAsync(py, new[] { "-m", "ruff", "--version" }, ct)) return "ruff";
            if (await RunQuietAsync(py, new[] { "-m", "black", "--version" }, ct)) return "black";
            if (await RunQuietAsync(py, new[] { "-m", "autopep8", "--version" }, ct)) return "autopep8";
            return null;
        }

        static async Task<string[]> DetectLspServerAsync(CancellationToken ct)
        {
            if (await RunQuietAsync("pyright-langserver", new[] { "--version" }, ct))
                return new[] { "pyright-langserver", "--stdio" };

            var py = await GetPythonExeAsync(ct);
            if (py != null && await RunQuietAsync(py, new[] { "-m", "ruff", "--version" }, ct))
                return new[] { py, "-m", "ruff", "server" };

            return null;
        }

        static async Task<bool> RunQuietAsync(string exe, string[] args, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var arg in args) psi.ArgumentList.Add(arg);

            Process proc = null;
            try
            {
                proc = new Process { StartInfo = psi };
                proc.Start();
                var outTask = proc.StandardOutput.ReadToEndAsync();
                var errTask = proc.StandardError.ReadToEndAsync();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(3));
                await proc.WaitForExitAsync(timeout.Token);
                return proc.ExitCode == 0;
            }
            catch
            {
                return false;
            }
            finally
            {
                try { if (proc != null && !proc.HasExited) proc.Kill(true); }
                catch { }
                proc?.Dispose();
            }
        }

        public static async Task<ProcessResult> RunAsync(string exe, string[] args, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var arg in args) psi.ArgumentList.Add(arg);

            Process proc = null;
            try
            {
                proc = new Process { StartInfo = psi };
                proc.Start();

                var outTask = proc.StandardOutput.ReadToEndAsync();
                var errTask = proc.StandardError.ReadToEndAsync();

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(12));

                try
                {
                    await proc.WaitForExitAsync(timeout.Token);
                    await Task.WhenAll(outTask, errTask);
                    return new ProcessResult
                    {
                        ExitCode = proc.ExitCode,
                        Stdout = outTask.IsCompletedSuccessfully ? outTask.Result : "",
                        Stderr = errTask.IsCompletedSuccessfully ? errTask.Result : "",
                        TimedOut = false,
                        Cancelled = false
                    };
                }
                catch (OperationCanceledException)
                {
                    try { proc.Kill(true); }
                    catch { }
                    return new ProcessResult
                    {
                        ExitCode = ct.IsCancellationRequested ? -1 : 1,
                        Stdout = outTask.IsCompletedSuccessfully ? outTask.Result : "",
                        Stderr = errTask.IsCompletedSuccessfully ? errTask.Result : "",
                        Cancelled = ct.IsCancellationRequested,
                        TimedOut = !ct.IsCancellationRequested
                    };
                }
            }
            catch (Exception ex)
            {
                return new ProcessResult
                {
                    ExitCode = -1,
                    Stderr = "Programm konnte nicht gestartet werden: " + ex.Message,
                    TimedOut = false,
                    Cancelled = false
                };
            }
            finally
            {
                try { if (proc != null && !proc.HasExited) proc.Kill(true); }
                catch { }
                proc?.Dispose();
            }
        }

        public static string CreateTempFile(string text, string suffix)
        {
            string path = Path.Combine(Path.GetTempPath(), $"novanote-{Guid.NewGuid():N}{suffix}");
            File.WriteAllText(path, text ?? "", new UTF8Encoding(false));
            return path;
        }

        public static void DeleteTemp(string path)
        {
            try { File.Delete(path); }
            catch { }
        }
    }
}
