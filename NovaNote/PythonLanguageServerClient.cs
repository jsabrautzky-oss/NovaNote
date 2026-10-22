using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace NovaNote
{
    public sealed class LspDiagnostic
    {
        public string Message = "";
        public int StartLine;
        public int StartChar;
        public int EndLine;
        public int EndChar;
        public int Severity;
    }

    public sealed class PythonLanguageServerClient : IDisposable
    {
        public string DocumentUri { get; }
        public string RootUri { get; }
        string _rootPath;

        Process _process;
        CancellationTokenSource _cts;
        SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
        int _id;
        int _version;
        string _pendingText;
        bool _ready;
        bool _disposed;
        Task _readTask;
        Task _stderrTask;

        static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
        static readonly JsonSerializerOptions _responseOptions = new JsonSerializerOptions
        {
            WriteIndented = false
        };
        static readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(15);
        static readonly TimeSpan _diagnosticTimeout = TimeSpan.FromSeconds(4);

        public bool IsReady => _ready;
        public int DocumentVersion => _version;
        public event Action<List<LspDiagnostic>> DiagnosticsReady;

        public PythonLanguageServerClient(string documentUri, string rootUri, string rootPath)
        {
            DocumentUri = documentUri ?? "file:///novanote/untitled.py";
            RootUri = rootUri ?? "file:///tmp";
            _rootPath = string.IsNullOrWhiteSpace(rootPath) ? Path.GetTempPath() : rootPath;
        }

        public async Task<bool> StartAsync(string text)
        {
            if (_disposed) return false;
            _cts = new CancellationTokenSource();
            _pendingText = text ?? "";

            var server = await PythonToolRunner.GetLspServerAsync(_cts.Token);
            if (server == null) return false;

            var psi = new ProcessStartInfo
            {
                FileName = server[0],
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Directory.Exists(_rootPath) ? _rootPath : Path.GetTempPath()
            };
            for (int i = 1; i < server.Length; i++) psi.ArgumentList.Add(server[i]);

            try
            {
                _process = new Process { StartInfo = psi };
                _process.Start();
                _stderrTask = _process.StandardError.ReadToEndAsync();
                _readTask = ReadLoopAsync(_process.StandardOutput.BaseStream, _cts.Token);
            }
            catch
            {
                Dispose();
                return false;
            }

            try
            {
                await RequestAsync("initialize", BuildInitParams());
                await SendNotificationAsync("initialized", new Dictionary<string, object>());

                _version = 1;
                var openParams = new Dictionary<string, object>
                {
                    ["textDocument"] = new Dictionary<string, object>
                    {
                        ["uri"] = DocumentUri,
                        ["languageId"] = "python",
                        ["version"] = _version,
                        ["text"] = _pendingText ?? text ?? ""
                    }
                };
                await SendNotificationAsync("textDocument/didOpen", openParams);

                _ready = true;
                return true;
            }
            catch
            {
                Dispose();
                return false;
            }
        }

        public void DidChange(string text)
        {
            if (_disposed) return;
            _pendingText = text ?? "";
            if (!_ready) return;
            _version++;
            _ = SendNotificationAsync("textDocument/didChange", new Dictionary<string, object>
            {
                ["textDocument"] = new Dictionary<string, object>
                {
                    ["uri"] = DocumentUri,
                    ["version"] = _version
                },
                ["contentChanges"] = new List<object> { new Dictionary<string, object> { ["text"] = _pendingText } }
            });
        }

        public Task<JsonElement> RequestCompletion(int line, int character)
        {
            return RequestAsync("textDocument/completion", new Dictionary<string, object>
            {
                ["textDocument"] = new Dictionary<string, object> { ["uri"] = DocumentUri },
                ["position"] = new Dictionary<string, object> { ["line"] = line, ["character"] = character }
            });
        }

        public Task<JsonElement> RequestHover(int line, int character)
        {
            return RequestAsync("textDocument/hover", new Dictionary<string, object>
            {
                ["textDocument"] = new Dictionary<string, object> { ["uri"] = DocumentUri },
                ["position"] = new Dictionary<string, object> { ["line"] = line, ["character"] = character }
            });
        }

        public async Task<List<LspDiagnostic>> RequestDiagnosticsAsync(int version)
        {
            var result = await RequestAsync("textDocument/diagnostic", new Dictionary<string, object>
            {
                ["textDocument"] = new Dictionary<string, object> { ["uri"] = DocumentUri },
                ["version"] = version
            }, _diagnosticTimeout);
            var diagnostics = ParseDiagnosticReport(result);
            DiagnosticsReady?.Invoke(diagnostics);
            return diagnostics;
        }

        async Task<JsonElement> RequestAsync(string method, Dictionary<string, object> parameters)
        {
            return await RequestAsync(method, parameters, _requestTimeout);
        }

        async Task<JsonElement> RequestAsync(string method, Dictionary<string, object> parameters, TimeSpan timeout)
        {
            if (_disposed || _process == null || _cts == null)
                throw new InvalidOperationException("Python-LSP ist nicht verbunden.");

            int id = Interlocked.Increment(ref _id);
            var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[id] = tcs;

            var json = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = method,
                ["params"] = parameters ?? new Dictionary<string, object>()
            }, _jsonOptions);

            try
            {
                await SendAsync(json, _cts.Token);
            }
            catch
            {
                _pending.TryRemove(id, out _);
                throw;
            }

            var timeoutTask = Task.Delay(timeout, _cts.Token);
            var winner = await Task.WhenAny(tcs.Task, timeoutTask);
            _pending.TryRemove(id, out _);

            if (winner == tcs.Task) return await tcs.Task;
            if (timeoutTask.IsCanceled) throw new OperationCanceledException(_cts.Token);
            throw new TimeoutException("Python-LSP antwortete nicht.");
        }

        async Task SendNotificationAsync(string method, Dictionary<string, object> parameters)
        {
            if (_disposed || _process == null || _cts == null) return;
            var json = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method,
                ["params"] = parameters ?? new Dictionary<string, object>()
            }, _jsonOptions);
            await SendAsync(json, _cts.Token);
        }

        async Task SendAsync(string json, CancellationToken ct)
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
            await _writeLock.WaitAsync(ct);
            try
            {
                var stream = _process?.StandardInput?.BaseStream;
                if (stream == null) return;
                await stream.WriteAsync(header, 0, header.Length, ct);
                await stream.WriteAsync(body, 0, body.Length, ct);
                await stream.FlushAsync(ct);
            }
            finally { _writeLock.Release(); }
        }

        async Task ReadLoopAsync(Stream stream, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    string json = await ReadMessageAsync(stream, ct);
                    if (json == null) break;
                    ProcessMessage(json);
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        async Task<string> ReadMessageAsync(Stream stream, CancellationToken ct)
        {
            int len = -1;
            while (!ct.IsCancellationRequested)
            {
                string line = await ReadLineAsync(stream, ct);
                if (line == null) return null;
                if (line.Length == 0)
                {
                    if (len >= 0) break;
                    continue;
                }
                if (line.StartsWith("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    int colon = line.IndexOf(':');
                    if (colon >= 0 && int.TryParse(line.Substring(colon + 1).Trim(), out int n)) len = n;
                }
            }
            if (len < 0) return null;

            byte[] body = new byte[len];
            int total = 0;
            while (total < len && !ct.IsCancellationRequested)
            {
                int read = await stream.ReadAsync(body, total, len - total, ct);
                if (read == 0) break;
                total += read;
            }
            return Encoding.UTF8.GetString(body, 0, total);
        }

        static async Task<string> ReadLineAsync(Stream stream, CancellationToken ct)
        {
            var sb = new StringBuilder();
            byte[] buf = new byte[1];
            while (!ct.IsCancellationRequested)
            {
                int read = await stream.ReadAsync(buf, 0, 1, ct);
                if (read == 0) return sb.Length > 0 ? sb.ToString() : null;
                char c = (char)buf[0];
                if (c == '\n')
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] == '\r') sb.Length--;
                    return sb.ToString();
                }
                sb.Append(c);
                if (sb.Length > 2048) return sb.ToString();
            }
            return null;
        }

        void ProcessMessage(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement.Clone();
                bool hasId = root.TryGetProperty("id", out var idElement);

                if (root.TryGetProperty("method", out var method))
                {
                    string m = method.GetString();
                    JsonElement parameters = default;
                    bool hasParameters = root.TryGetProperty("params", out parameters);

                    if (hasId && !string.IsNullOrEmpty(m))
                    {
                        int rid = ReadId(idElement);
                        if (rid > 0)
                        {
#pragma warning disable CS4014
                            _ = SendServerResponseAsync(m, rid, hasParameters ? parameters.Clone() : null);
#pragma warning restore CS4014
                        }
                        return;
                    }

                    if (m == "textDocument/publishDiagnostics" && hasParameters)
                        PublishDiagnostics(parameters);
                    return;
                }

                if (hasId)
                {
                    int rid = ReadId(idElement);
                    if (rid <= 0) return;
                    if (root.TryGetProperty("result", out var result))
                    {
                        if (_pending.TryRemove(rid, out var tcs)) tcs.SetResult(result.Clone());
                    }
                    else if (root.TryGetProperty("error", out var error))
                    {
                        if (_pending.TryRemove(rid, out var tcs)) tcs.SetException(new Exception(error.ToString()));
                    }
                }
            }
            catch
            {
            }
        }

        async Task SendServerResponseAsync(string method, int id, JsonElement? parameters)
        {
            if (_disposed || _process == null || _cts == null) return;
            object result = BuildServerResponse(method, parameters);

            var json = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["result"] = result
            }, _responseOptions);

            await SendAsync(json, _cts.Token);
        }

        object BuildServerResponse(string method, JsonElement? parameters)
        {
            switch (method)
            {
                case "workspace/configuration":
                    var items = new List<object>();
                    if (parameters.HasValue && parameters.Value.TryGetProperty("items", out var arr) &&
                        arr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in arr.EnumerateArray())
                        {
                            items.Add(new Dictionary<string, object>());
                        }
                    }
                    return items;
                default:
                    return null;
            }
        }

        static int ReadId(JsonElement id)
        {
            if (id.TryGetInt32(out int v)) return v;
            if (id.TryGetInt64(out long l)) return (int)l;
            if (id.ValueKind == JsonValueKind.String && int.TryParse(id.GetString(), out int s)) return s;
            return -1;
        }

        void PublishDiagnostics(JsonElement parameters)
        {
            if (!parameters.TryGetProperty("uri", out var uri)) return;
            string u = uri.GetString();
            if (!string.Equals(u, DocumentUri, StringComparison.OrdinalIgnoreCase)) return;

            if (!parameters.TryGetProperty("diagnostics", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return;

            var list = new List<LspDiagnostic>();
            foreach (var item in arr.EnumerateArray())
            {
                var diag = new LspDiagnostic();
                if (item.TryGetProperty("message", out var msg)) diag.Message = msg.GetString() ?? "";
                if (item.TryGetProperty("severity", out var sev)) sev.TryGetInt32(out diag.Severity);
                if (item.TryGetProperty("range", out var range))
                {
                    ReadPosition(range, "start", out diag.StartLine, out diag.StartChar);
                    ReadPosition(range, "end", out diag.EndLine, out diag.EndChar);
                }
                list.Add(diag);
                if (list.Count > 200) break;
            }
            DiagnosticsReady?.Invoke(list);
        }

        static List<LspDiagnostic> ParseDiagnosticReport(JsonElement report)
        {
            var list = new List<LspDiagnostic>();
            if (report.ValueKind != JsonValueKind.Object) return list;

            JsonElement arr;
            if (!report.TryGetProperty("diagnostics", out arr) &&
                !report.TryGetProperty("items", out arr))
                return list;
            if (arr.ValueKind != JsonValueKind.Array)
                return list;

            foreach (var item in arr.EnumerateArray())
            {
                var diag = new LspDiagnostic();
                if (item.TryGetProperty("message", out var msg)) diag.Message = msg.GetString() ?? "";
                if (item.TryGetProperty("severity", out var sev)) sev.TryGetInt32(out diag.Severity);
                if (item.TryGetProperty("range", out var range))
                {
                    ReadPosition(range, "start", out diag.StartLine, out diag.StartChar);
                    ReadPosition(range, "end", out diag.EndLine, out diag.EndChar);
                }
                list.Add(diag);
                if (list.Count > 200) break;
            }
            return list;
        }

        static void ReadPosition(JsonElement range, string name, out int line, out int character)
        {
            line = 0;
            character = 0;
            if (range.TryGetProperty(name, out var pos))
            {
                if (pos.TryGetProperty("line", out var l)) l.TryGetInt32(out line);
                if (pos.TryGetProperty("character", out var c)) c.TryGetInt32(out character);
            }
        }

        Dictionary<string, object> BuildInitParams()
        {
            var completionItem = new Dictionary<string, object>
            {
                ["snippetSupport"] = true,
                ["documentationFormat"] = new List<object> { "markdown", "plaintext" }
            };
            var textDocument = new Dictionary<string, object>
            {
                ["completion"] = new Dictionary<string, object> { ["completionItem"] = completionItem },
                ["hover"] = new Dictionary<string, object>
                {
                    ["contentFormat"] = new List<object> { "markdown", "plaintext" }
                },
                ["formatting"] = new Dictionary<string, object>()
            };
            var workspaceFolders = new List<object>
            {
                new Dictionary<string, object> { ["uri"] = RootUri, ["name"] = "NovaNote" }
            };
            var workspaceCapability = new Dictionary<string, object>
            {
                ["workspaceFolders"] = true
            };
            return new Dictionary<string, object>
            {
                ["clientInfo"] = new Dictionary<string, object> { ["name"] = "NovaNote", ["version"] = "0.1.0-beta" },
                ["processId"] = Environment.ProcessId,
                ["locale"] = "de",
                ["rootUri"] = RootUri,
                ["workspaceFolders"] = workspaceFolders,
                ["capabilities"] = new Dictionary<string, object>
                {
                    ["textDocument"] = textDocument,
                    ["workspace"] = workspaceCapability
                }
            };
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _ready = false;
            _cts?.Cancel();
            _pending.Clear();
            try
            {
                if (_process != null && !_process.HasExited) _process.Kill(true);
            }
            catch { }
            _process?.Dispose();
        }
    }
}
