using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NovaNote
{
    public class AiModelInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int ContextLength { get; set; }
        public bool Vision { get; set; }
        public bool SupportsTools { get; set; }
    }

    public class AiScanResult
    {
        public List<AiModelInfo> Models { get; set; } = new List<AiModelInfo>();
        public string Error { get; set; } = null;
    }

    public class AiToolCall
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Arguments { get; set; } = "{}";
    }

    public class AiMessage
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = "";
        public string ToolCallId { get; set; } = null;
        public List<AiToolCall> ToolCalls { get; set; } = null;
    }

    public class AiReply
    {
        public string Content { get; set; } = "";
        public List<AiToolCall> ToolCalls { get; set; } = new List<AiToolCall>();
        public bool ToolsAccepted { get; set; }
        public string Error { get; set; } = null;
    }

    public static class LocalAiClient
    {
        public const string DefaultBaseUrl = "http://localhost:1234/v1";
        public const string OllamaBaseUrl = "http://localhost:11434/v1";

        static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        static readonly HttpClient ChatHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };

        static readonly JsonSerializerOptions ReqOpt = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        static readonly Regex Fence = new Regex(
            @"```([A-Za-z0-9_+-]*)?\r?\n(.*?)```", RegexOptions.Singleline | RegexOptions.Compiled);

        const string FallbackProtocol =
            "Dieser Server nimmt keine native Tool-Aufrufe. Wenn du den Editor ändern sollst, gib genau einen Block " +
            "```nova\n{\"action\":\"replace_all_text\",\"code\":\"...\"}\n``` aus. Erlaubte Aktionen: " +
            "replace_all_text (code), replace_lines (from_line,to_line,text), insert_at_line (line,text), " +
            "search_document (pattern), goto_line (line), select_lines (from_line,to_line).";

        public static string NormalizeBase(string url)
        {
            var u = string.IsNullOrWhiteSpace(url) ? DefaultBaseUrl : url.Trim();
            if (!u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                u = "http://" + u;
            u = u.TrimEnd('/');
            foreach (var suffix in new[] { "/chat/completions", "/api/v1/models", "/api/tags", "/v1/models", "/models", "/v1" })
            {
                if (u.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    u = u.Substring(0, u.Length - suffix.Length).TrimEnd('/');
            }
            return u;
        }

        public static string RootBase(string url) => NormalizeBase(url);

        public static string OpenAiBase(string url) => NormalizeBase(url) + "/v1";

        public static async Task<AiScanResult> ScanAsync(AiProvider provider, CancellationToken ct)
        {
            var result = new AiScanResult();
            if (provider == null)
            {
                result.Error = "Kein Anbieter gewählt.";
                return result;
            }

            var map = new Dictionary<string, AiModelInfo>(StringComparer.OrdinalIgnoreCase);
            string root = RootBase(provider.BaseUrl);

            try
            {
                using var doc = await GetJsonAsync(OpenAiBase(provider.BaseUrl) + "/models", provider.ApiKey, ct);
                CollectModels(doc.RootElement, map);
            }
            catch (OperationCanceledException)
            {
                result.Error = "Scan abgebrochen.";
                return result;
            }
            catch (Exception ex)
            {
                result.Error = $"{provider.Name} nicht erreichbar: {ex.Message}";
                return result;
            }

            if (map.Count == 0)
            {
                result.Error = $"Keine Modelle unter {OpenAiBase(provider.BaseUrl)}/models gefunden.";
                return result;
            }

            if (provider.Kind == AiKind.LmStudio)
                await EnrichLmStudioAsync(root, provider.ApiKey, map, ct);
            else if (provider.Kind == AiKind.Ollama)
                await EnrichOllamaAsync(root, provider.ApiKey, map, ct);

            result.Models = map.Values.ToList();
            return result;
        }

        static async Task EnrichLmStudioAsync(string root, string key, Dictionary<string, AiModelInfo> map, CancellationToken ct)
        {
            try
            {
                using var doc = await GetJsonAsync(root + "/api/v1/models", key, ct);
                var arr = doc.RootElement.TryGetProperty("models", out var m) ? m : doc.RootElement;
                if (arr.ValueKind != JsonValueKind.Array) return;
                foreach (var item in arr.EnumerateArray())
                {
                    string id = ReadString(item, "id") ?? ReadString(item, "name");
                    if (string.IsNullOrEmpty(id)) continue;
                    if (!map.TryGetValue(id, out var info))
                    {
                        info = new AiModelInfo { Id = id, Name = ReadString(item, "name") ?? id };
                        map[id] = info;
                    }
                    ApplyMetadata(info, item);
                }
            }
            catch
            {
            }
        }

        static async Task EnrichOllamaAsync(string root, string key, Dictionary<string, AiModelInfo> map, CancellationToken ct)
        {
            try
            {
                using var doc = await GetJsonAsync(root + "/api/tags", key, ct);
                var arr = doc.RootElement.TryGetProperty("models", out var m) ? m : doc.RootElement;
                if (arr.ValueKind != JsonValueKind.Array) return;
                foreach (var item in arr.EnumerateArray())
                {
                    string id = ReadString(item, "name") ?? ReadString(item, "model");
                    if (string.IsNullOrEmpty(id)) continue;
                    if (!map.TryGetValue(id, out var info))
                    {
                        info = new AiModelInfo { Id = id, Name = id };
                        map[id] = info;
                    }
                    ApplyMetadata(info, item);
                }
            }
            catch
            {
            }

            int shown = 0;
            foreach (var info in map.Values.Where(m => m.ContextLength <= 0 || !m.SupportsTools).ToList())
            {
                if (shown >= 20) break;
                shown++;
                try
                {
                    using var doc = await GetJsonAsync(root + "/api/show/" + info.Id, key, ct);
                    ApplyMetadata(info, doc.RootElement);
                }
                catch
                {
                }
            }
        }

        static void CollectModels(JsonElement root, Dictionary<string, AiModelInfo> map)
        {
            JsonElement arr;
            if (root.ValueKind == JsonValueKind.Array) arr = root;
            else if (root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Array) arr = d;
            else return;

            foreach (var item in arr.EnumerateArray())
            {
                string id = ReadString(item, "id") ?? ReadString(item, "name") ?? ReadString(item, "model");
                if (string.IsNullOrEmpty(id)) continue;
                if (map.TryGetValue(id, out var info))
                {
                    ApplyMetadata(info, item);
                    continue;
                }
                info = new AiModelInfo { Id = id, Name = ReadString(item, "name") ?? id };
                ApplyMetadata(info, item);
                map[id] = info;
            }
        }

        static void ApplyMetadata(AiModelInfo info, JsonElement item)
        {
            int ctx = ReadContext(item);
            if (ctx > 0) info.ContextLength = ctx;
            if (ReadVision(item, info.Id)) info.Vision = true;
            if (ReadTools(item, info.Id)) info.SupportsTools = true;
            string name = ReadString(item, "name") ?? ReadString(item, "alias");
            if (!string.IsNullOrWhiteSpace(name)) info.Name = name;
        }

        static string ReadString(JsonElement e, string key)
        {
            if (e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String) return v.GetString();
            return null;
        }

        static int ReadContext(JsonElement e)
        {
            foreach (var key in new[] { "context_length", "contextWindow", "max_context_length", "n_ctx", "num_ctx", "context_window", "max_prompt_length" })
            {
                if (e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) && n > 0)
                    return n;
            }
            if (e.TryGetProperty("parameters", out var p) && p.ValueKind == JsonValueKind.Object &&
                p.TryGetProperty("num_ctx", out var n2) && n2.TryGetInt32(out int v2) && v2 > 0)
                return v2;
            if (e.TryGetProperty("metadata", out var md) && md.ValueKind == JsonValueKind.Object)
                return ReadContext(md);
            return 0;
        }

        public static bool IsVisionName(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            return s.Contains("vision", StringComparison.OrdinalIgnoreCase) ||
                   s.Contains("llava", StringComparison.OrdinalIgnoreCase) ||
                   s.Contains("-vl", StringComparison.OrdinalIgnoreCase) ||
                   s.Contains("vl-", StringComparison.OrdinalIgnoreCase) ||
                   s.Contains("minicpm-v", StringComparison.OrdinalIgnoreCase);
        }

        static bool ReadBool(JsonElement e, string key, out bool value)
        {
            value = false;
            if (!e.TryGetProperty(key, out var v)) return false;
            if (v.ValueKind == JsonValueKind.True)
            {
                value = true;
                return true;
            }
            if (v.ValueKind == JsonValueKind.False)
            {
                value = false;
                return true;
            }
            return false;
        }

        static bool ReadVision(JsonElement e, string id)
        {
            if (ReadBool(e, "vision", out bool flag)) return flag;
            if (HasCapability(e, "vision") || HasCapability(e, "image") || HasCapability(e, "multimodal")) return true;
            return IsVisionName(id);
        }

        static bool ReadTools(JsonElement e, string id)
        {
            if (ReadBool(e, "supports_tools", out bool flag)) return flag;
            if (ReadBool(e, "supports_tool", out bool flag2)) return flag2;
            if (HasCapability(e, "tools") || HasCapability(e, "tool") || HasCapability(e, "tool_use")) return true;
            return false;
        }

        static bool HasCapability(JsonElement e, string cap)
        {
            foreach (var key in new[] { "capabilities", "input", "inputs", "modalities", "supported_modalities", "supported_inputs" })
            {
                if (e.TryGetProperty(key, out var c) && c.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in c.EnumerateArray())
                        if (item.ValueKind == JsonValueKind.String &&
                            item.GetString().IndexOf(cap, StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                }
            }
            if (e.TryGetProperty("details", out var d) && d.ValueKind == JsonValueKind.Object)
                return HasCapability(d, cap);
            return false;
        }

        public static async Task<AiReply> ChatAsync(
            AiProvider provider, AiModel model, List<AiMessage> messages, List<AiToolDef> tools, CancellationToken ct)
        {
            var reply = new AiReply();
            if (provider == null) { reply.Error = "Kein Anbieter gewählt."; return reply; }
            if (model == null) { reply.Error = "Kein Modell gewählt. KI → Modelle scannen."; return reply; }

            string url = OpenAiBase(provider.BaseUrl) + "/chat/completions";

            bool useTools = tools != null && tools.Count > 0;
            try
            {
                using var response = await PostChatAsync(url, provider.ApiKey,
                    BuildRequest(model, messages, useTools ? tools : null), ct);

                if (!response.IsSuccessStatusCode && useTools)
                {
                    using var retry = await PostChatAsync(url, provider.ApiKey, BuildRequest(model, messages, null), ct);
                    if (retry.IsSuccessStatusCode)
                    {
                        ParseReply(await SafeTextAsync(retry), reply);
                        reply.ToolsAccepted = false;
                        AppendFallbackHint(messages);
                        reply.ToolCalls = ParseFallbackToolCalls(reply.Content);
                        return reply;
                    }
                    reply.Error = $"HTTP {(int)retry.StatusCode}: {Trim(await SafeTextAsync(retry))}";
                    return reply;
                }

                if (!response.IsSuccessStatusCode)
                {
                    reply.Error = $"HTTP {(int)response.StatusCode}: {Trim(await SafeTextAsync(response))}";
                    return reply;
                }

                ParseReply(await SafeTextAsync(response), reply);
                reply.ToolsAccepted = useTools;
                if (reply.ToolCalls.Count == 0)
                    reply.ToolCalls = ParseFallbackToolCalls(reply.Content);
                return reply;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                reply.Error = $"{provider.Name}: {ex.Message}";
                return reply;
            }
        }

        public static async Task<AiReply> ChatStreamAsync(
            AiProvider provider, AiModel model, List<AiMessage> messages, List<AiToolDef> tools,
            CancellationToken ct, Action<string> onChunk)
        {
            var reply = new AiReply();
            if (provider == null) { reply.Error = "Kein Anbieter gewählt."; return reply; }
            if (model == null) { reply.Error = "Kein Modell gewählt. KI → Modelle scannen."; return reply; }

            string url = OpenAiBase(provider.BaseUrl) + "/chat/completions";
            bool useTools = tools != null && tools.Count > 0;
            HttpResponseMessage response = null;
            try
            {
                response = await PostChatAsync(url, provider.ApiKey,
                    BuildRequest(model, messages, useTools ? tools : null, true), ct);

                if (!response.IsSuccessStatusCode)
                {
                    int code = (int)response.StatusCode;
                    string body = await SafeTextAsync(response);
                    response.Dispose();
                    response = null;

                    if (useTools)
                    {
                        using var retry = await PostChatAsync(url, provider.ApiKey,
                            BuildRequest(model, messages, null, false), ct);
                        if (retry.IsSuccessStatusCode)
                        {
                            ParseReply(await SafeTextAsync(retry), reply);
                            reply.ToolsAccepted = false;
                            AppendFallbackHint(messages);
                            reply.ToolCalls = ParseFallbackToolCalls(reply.Content);
                            if (!string.IsNullOrEmpty(reply.Content)) onChunk?.Invoke(reply.Content);
                            return reply;
                        }
                        reply.Error = $"HTTP {(int)retry.StatusCode}: {Trim(await SafeTextAsync(retry))}";
                        return reply;
                    }

                    reply.Error = $"HTTP {code}: {Trim(body)}";
                    return reply;
                }

                var content = new StringBuilder();
                var partial = new Dictionary<int, AiToolCall>();
                var order = new List<int>();

                using (response)
                using (var stream = await response.Content.ReadAsStreamAsync(ct))
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string line;
                    while (!ct.IsCancellationRequested && (line = await reader.ReadLineAsync()) != null)
                    {
                        if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
                        string payload = line.Substring(5).Trim();
                        if (payload == "[DONE]") break;
                        try
                        {
                            using var doc = JsonDocument.Parse(payload);
                            ApplyStreamChunk(doc.RootElement, content, partial, order, reply);
                        }
                        catch
                        {
                        }
                        if (reply.Error != null) break;
                        if (content.Length > 0)
                        {
                            string delta = content.ToString();
                            content.Clear();
                            onChunk?.Invoke(delta);
                        }
                    }
                }

                string text = reply.Content ?? "";
                reply.Content = text;
                if (reply.Error == null)
                {
                    foreach (var i in order) reply.ToolCalls.Add(partial[i]);
                    reply.ToolsAccepted = useTools;
                    if (reply.ToolCalls.Count == 0)
                        reply.ToolCalls = ParseFallbackToolCalls(reply.Content);
                }
                return reply;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                reply.Error = $"{provider.Name}: {ex.Message}";
                return reply;
            }
            finally
            {
                response?.Dispose();
            }
        }

        static void ApplyStreamChunk(JsonElement root, StringBuilder content,
            Dictionary<int, AiToolCall> partial, List<int> order, AiReply reply)
        {
            if (root.TryGetProperty("error", out var err))
            {
                reply.Error = err.ValueKind == JsonValueKind.String ? err.GetString() : err.ToString();
                return;
            }
            if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
                return;
            var choice = choices[0];
            if (!choice.TryGetProperty("delta", out var delta) || delta.ValueKind != JsonValueKind.Object)
                return;

            if (delta.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
            {
                string piece = c.GetString();
                if (!string.IsNullOrEmpty(piece))
                {
                    content.Append(piece);
                    reply.Content = (reply.Content ?? "") + piece;
                }
            }

            if (delta.TryGetProperty("reasoning_content", out var r) && r.ValueKind == JsonValueKind.String)
            {
                string piece = r.GetString();
                if (!string.IsNullOrEmpty(piece))
                {
                    content.Append(piece);
                    reply.Content = (reply.Content ?? "") + piece;
                }
            }

            if (!delta.TryGetProperty("tool_calls", out var calls) || calls.ValueKind != JsonValueKind.Array)
                return;

            foreach (var call in calls.EnumerateArray())
            {
                int index = partial.Count;
                if (call.TryGetProperty("index", out var ix) && ix.TryGetInt32(out int i)) index = i;
                if (!partial.TryGetValue(index, out var acc))
                {
                    acc = new AiToolCall { Id = "", Name = "", Arguments = "" };
                    partial[index] = acc;
                    order.Add(index);
                }
                string id = ReadString(call, "id");
                if (!string.IsNullOrEmpty(id)) acc.Id = id;
                if (call.TryGetProperty("function", out var fn) && fn.ValueKind == JsonValueKind.Object)
                {
                    string name = ReadString(fn, "name");
                    if (!string.IsNullOrEmpty(name)) acc.Name += name;
                    string args = ReadString(fn, "arguments");
                    if (!string.IsNullOrEmpty(args)) acc.Arguments += args;
                }
            }
        }

        static async Task<JsonDocument> GetJsonAsync(string url, string key, CancellationToken ct)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(key)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            var response = await Http.SendAsync(req, ct);
            req.Dispose();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"HTTP {(int)response.StatusCode} ({url})");
            var text = await response.Content.ReadAsStringAsync(ct);
            response.Dispose();
            return JsonDocument.Parse(text);
        }

        static async Task<HttpResponseMessage> PostChatAsync(string url, string key, ChatRequestDto body, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Content = new StringContent(JsonSerializer.Serialize(body, ReqOpt), Encoding.UTF8, "application/json");
            if (!string.IsNullOrEmpty(key)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            return await ChatHttp.SendAsync(req, ct);
        }

        static ChatRequestDto BuildRequest(AiModel model, List<AiMessage> messages, List<AiToolDef> tools, bool stream = false)
        {
            var body = new ChatRequestDto
            {
                Model = model.Id,
                Stream = stream,
                Temperature = 0.2,
                Messages = messages.Select(ToDto).ToList()
            };
            if (tools != null && tools.Count > 0)
            {
                body.Tools = tools.Select(t => new ToolDto
                {
                    Type = "function",
                    Function = new FunctionDto
                    {
                        Name = t.Name,
                        Description = t.Description,
                        Parameters = t.Parameters
                    }
                }).ToList();
                body.ToolChoice = "auto";
            }
            return body;
        }

        static ChatMessageDto ToDto(AiMessage m)
        {
            var dto = new ChatMessageDto
            {
                Role = m.Role,
                Content = m.Content,
                ToolCallId = m.ToolCallId
            };
            if (m.ToolCalls != null && m.ToolCalls.Count > 0)
            {
                dto.ToolCalls = m.ToolCalls.Select(c => new ToolCallDto
                {
                    Id = c.Id,
                    Type = "function",
                    Function = new FunctionCallDto { Name = c.Name, Arguments = c.Arguments }
                }).ToList();
            }
            return dto;
        }

        static void AppendFallbackHint(List<AiMessage> messages)
        {
            var system = messages.FirstOrDefault(m => m.Role == "system");
            if (system == null) messages.Insert(0, new AiMessage { Role = "system", Content = FallbackProtocol });
            else if (!system.Content.Contains(FallbackProtocol))
                system.Content = system.Content + "\n" + FallbackProtocol;
        }

        static void ParseReply(string body, AiReply reply)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.TryGetProperty("error", out var err))
                {
                    reply.Error = err.ValueKind == JsonValueKind.String ? err.GetString() : err.ToString();
                    return;
                }
                if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array ||
                    choices.GetArrayLength() == 0)
                {
                    reply.Error = "Antwort ohne choices.";
                    return;
                }
                var choice = choices[0];
                if (!choice.TryGetProperty("message", out var msg))
                {
                    reply.Error = "Antwort ohne message.";
                    return;
                }
                reply.Content = ReadString(msg, "content") ?? "";
                if (msg.TryGetProperty("tool_calls", out var calls) && calls.ValueKind == JsonValueKind.Array)
                {
                    int i = 0;
                    foreach (var call in calls.EnumerateArray())
                    {
                        var fn = call.TryGetProperty("function", out var f) ? f : default;
                        string name = fn.ValueKind == JsonValueKind.Object ? ReadString(fn, "name") : null;
                        string args = fn.ValueKind == JsonValueKind.Object ? ReadString(fn, "arguments") : null;
                        if (args == null && fn.ValueKind == JsonValueKind.Object &&
                            fn.TryGetProperty("arguments", out var a) && a.ValueKind == JsonValueKind.Object)
                            args = a.ToString();
                        if (string.IsNullOrEmpty(name)) continue;
                        reply.ToolCalls.Add(new AiToolCall
                        {
                            Id = ReadString(call, "id") ?? $"call_{i}",
                            Name = name,
                            Arguments = string.IsNullOrEmpty(args) ? "{}" : args
                        });
                        i++;
                    }
                }
            }
            catch (Exception ex)
            {
                reply.Error = $"Antwort nicht lesbar: {ex.Message}";
            }
        }

        static async Task<string> SafeTextAsync(HttpResponseMessage response)
        {
            try { return await response.Content.ReadAsStringAsync(); }
            catch { return ""; }
        }

        static string Trim(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ");
            return text.Length > 300 ? text.Substring(0, 300) + "…" : text;
        }

        public static string ExtractCodeBlock(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            foreach (Match m in Fence.Matches(text))
            {
                if (string.Equals(m.Groups[1].Value, "nova", StringComparison.OrdinalIgnoreCase)) continue;
                return TrimTrailingNewlines(m.Groups[2].Value);
            }
            int start = text.IndexOf("```", StringComparison.Ordinal);
            if (start < 0) return null;
            int nl = text.IndexOf('\n', start);
            if (nl < 0) return null;
            if (string.Equals(text.Substring(start + 3, nl - start - 3).Trim(), "nova", StringComparison.OrdinalIgnoreCase))
                return null;
            return TrimTrailingNewlines(text.Substring(nl + 1));
        }

        static string TrimTrailingNewlines(string text)
        {
            int end = text.Length;
            while (end > 0 && (text[end - 1] == '\n' || text[end - 1] == '\r')) end--;
            return text.Substring(0, end);
        }

        public static List<AiToolCall> ParseFallbackToolCalls(string text)
        {
            var calls = new List<AiToolCall>();
            if (string.IsNullOrEmpty(text)) return calls;
            foreach (Match m in Fence.Matches(text))
            {
                if (!string.Equals(m.Groups[1].Value, "nova", StringComparison.OrdinalIgnoreCase)) continue;
                AddFallbackJson(m.Groups[2].Value, calls);
            }
            int idx = text.IndexOf("{\"action\"", StringComparison.Ordinal);
            if (idx >= 0) AddFallbackJson(text.Substring(idx), calls);
            return calls;
        }

        static void AddFallbackJson(string json, List<AiToolCall> calls)
        {
            try
            {
                using var doc = JsonDocument.Parse(json.Trim());
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return;
                string action = ReadString(root, "action");
                if (string.IsNullOrEmpty(action)) return;
                var payload = new Dictionary<string, object>();
                foreach (var p in root.EnumerateObject())
                    if (!string.Equals(p.Name, "action", StringComparison.OrdinalIgnoreCase))
                        payload[p.Name] = p.Value.Clone();
                calls.Add(new AiToolCall
                {
                    Id = $"fallback_{calls.Count}",
                    Name = action,
                    Arguments = JsonSerializer.Serialize(payload)
                });
            }
            catch
            {
            }
        }

        class ChatRequestDto
        {
            [JsonPropertyName("model")] public string Model { get; set; }
            [JsonPropertyName("messages")] public List<ChatMessageDto> Messages { get; set; } = new List<ChatMessageDto>();
            [JsonPropertyName("stream")] public bool Stream { get; set; }
            [JsonPropertyName("temperature")] public double Temperature { get; set; }
            [JsonPropertyName("tools")] public List<ToolDto> Tools { get; set; }
            [JsonPropertyName("tool_choice")] public string ToolChoice { get; set; }
        }

        class ChatMessageDto
        {
            [JsonPropertyName("role")] public string Role { get; set; }
            [JsonPropertyName("content")] public string Content { get; set; }
            [JsonPropertyName("tool_calls")] public List<ToolCallDto> ToolCalls { get; set; }
            [JsonPropertyName("tool_call_id")] public string ToolCallId { get; set; }
        }

        class ToolDto
        {
            [JsonPropertyName("type")] public string Type { get; set; }
            [JsonPropertyName("function")] public FunctionDto Function { get; set; }
        }

        class FunctionDto
        {
            [JsonPropertyName("name")] public string Name { get; set; }
            [JsonPropertyName("description")] public string Description { get; set; }
            [JsonPropertyName("parameters")] public Dictionary<string, object> Parameters { get; set; }
        }

        class ToolCallDto
        {
            [JsonPropertyName("id")] public string Id { get; set; }
            [JsonPropertyName("type")] public string Type { get; set; }
            [JsonPropertyName("function")] public FunctionCallDto Function { get; set; }
        }

        class FunctionCallDto
        {
            [JsonPropertyName("name")] public string Name { get; set; }
            [JsonPropertyName("arguments")] public string Arguments { get; set; }
        }
    }
}
