using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NovaNote
{
    public class AiToolDef
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }

    public static class AiTools
    {
        public static readonly string[] Names =
        {
            "get_document", "search_document", "goto_line", "select_lines",
            "replace_all_text", "replace_lines", "insert_at_line", "syntax_check", "format_code"
        };

        static readonly string[] EditTools = { "replace_all_text", "replace_lines", "insert_at_line" };

        public static bool IsEditTool(string name) =>
            Array.Exists(EditTools, t => string.Equals(t, name, StringComparison.OrdinalIgnoreCase));

        public static List<AiToolDef> Definitions() => new List<AiToolDef>
        {
            Def("get_document", "Aktuellen Editor-Text lesen (optional als Ausschnitt ab Zeile).",
                Props(("from_line", "integer", "Startzeile (1-basiert, optional)"),
                      ("count", "integer", "Anzahl Zeilen (optional)"))),
            Def("search_document", "Text im Dokument suchen und Zeilen/Spalten zurückgeben.",
                Props(("pattern", "string", "Suchbegriff"),
                      ("match_case", "boolean", "Groß-/Kleinunterschied"),
                      ("whole_word", "boolean", "Nur ganze Wörter"),
                      ("max_results", "integer", "Maximale Anzahl Treffer")
                ), new[] { "pattern" }),
            Def("replace_all_text", "Gesamten Dokumenttext durch den übergebenen Code ersetzen.",
                Props(("code", "string", "Neuer vollständiger Text")), new[] { "code" }),
            Def("replace_lines", "Ein Zeileninterval im Dokument ersetzen.",
                Props(("from_line", "integer", "Erste Zeile (1-basiert)"),
                      ("to_line", "integer", "Letzte Zeile (1-basiert)"),
                      ("text", "string", "Neuer Text für das Interval")),
                new[] { "from_line", "to_line", "text" }),
            Def("insert_at_line", "Text als neue Zeile vor einer Zeile einfügen.",
                Props(("line", "integer", "Zeile (1-basiert)"),
                      ("text", "string", "Einzufügender Text")),
                new[] { "line", "text" }),
            Def("select_lines", "Ein Zeileninterval im Editor markieren und hinrollen.",
                Props(("from_line", "integer", "Erste Zeile"), ("to_line", "integer", "Letzte Zeile")),
                new[] { "from_line", "to_line" }),
            Def("goto_line", "Cursor zu einer Zeile bewegen.",
                Props(("line", "integer", "Zeile (1-basiert)")), new[] { "line" }),
            Def("syntax_check", "Aktuellen Syntax-Check-Status des Dokuments zurückgeben.",
                Props()),
            Def("format_code", "Dokument formatieren (nur Python und C#).", Props())
        };

        static AiToolDef Def(string name, string description, Dictionary<string, object> props,
                             string[] required = null)
        {
            return new AiToolDef
            {
                Name = name,
                Description = description,
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = props,
                    ["required"] = required ?? new string[0]
                }
            };
        }

        static Dictionary<string, object> Props(params (string Key, string Type, string Desc)[] items)
        {
            var dict = new Dictionary<string, object>();
            foreach (var item in items)
                dict[item.Key] = new Dictionary<string, object> { ["type"] = item.Type, ["description"] = item.Desc };
            return dict;
        }

        public static async Task<string> Execute(string name, string argsJson, DocumentView view)
        {
            if (view == null) return "Kein aktives Dokument.";
            var args = ParseArgs(argsJson);

            switch ((name ?? "").ToLowerInvariant())
            {
                case "get_document":
                {
                    int from = Int(args, "from_line", 1);
                    int count = Int(args, "count", 0);
                    string body = count > 0 ? view.GetLines(from, count) : view.CurrentText;
                    if (body.Length > 4000) body = body.Substring(0, 4000) + "\n… (gekürzt)";
                    return $"{view.TabTitle} · {view.LineCount} Zeilen · {view.StatusLanguage} · " +
                           $"{(view.IsDirty ? "geändert" : "gespeichert")}\n{body}";
                }

                case "search_document":
                {
                    string pattern = Str(args, "pattern", "");
                    if (string.IsNullOrEmpty(pattern)) return "Kein Suchbegriff angegeben.";
                    var hits = view.FindAll(pattern, Bool(args, "match_case", false),
                        Bool(args, "whole_word", false), Int(args, "max_results", 40));
                    if (hits.Count == 0) return $"Keine Treffer für \"{pattern}\".";
                    var sb = new StringBuilder($"{hits.Count} Treffer für \"{pattern}\":");
                    foreach (var (line, col, len) in hits)
                    {
                        string snippet = view.GetLines(line, 1).Trim();
                        if (snippet.Length > 90) snippet = snippet.Substring(0, 90) + "…";
                        sb.Append($"\nZeile {line}, Spalte {col}: {snippet}");
                    }
                    return sb.ToString();
                }

                case "goto_line":
                {
                    int line = Int(args, "line", 1);
                    view.GoToLineQuiet(line);
                    return $"Zeile {line} ausgewählt.";
                }

                case "select_lines":
                {
                    int from = Int(args, "from_line", 1);
                    int to = Int(args, "to_line", from);
                    view.SelectLines(from, to);
                    return $"Zeilen {from}–{to} ausgewählt.";
                }

                case "replace_all_text":
                {
                    string code = Str(args, "code", "");
                    if (string.IsNullOrEmpty(code)) return "Kein Code übergeben.";
                    view.ReplaceAllText(code);
                    return $"Gesamter Text ersetzt ({view.LineCount} Zeilen).";
                }

                case "replace_lines":
                {
                    int from = Int(args, "from_line", 1);
                    int to = Int(args, "to_line", from);
                    string text = Str(args, "text", "");
                    view.ReplaceLines(from, to, text);
                    return $"Zeilen {from}–{to} ersetzt.";
                }

                case "insert_at_line":
                {
                    int line = Int(args, "line", 1);
                    string text = Str(args, "text", "");
                    view.InsertLine(line, text);
                    return $"Text in Zeile {line} eingefügt.";
                }

                case "syntax_check":
                {
                    if (!view.SyntaxCheckEnabled) return "Syntax-Check ist deaktiviert.";
                    return view.HasSyntaxErrors ? $"Syntax-Fehler: {view.SyntaxStatus}" : "Keine Syntaxfehler.";
                }

                case "format_code":
                {
                    if (!view.FormatSupported) return "Formatierung ist nur für Python und C# verfügbar.";
                    var err = await view.FormatCodeAsync();
                    return err == null ? "Code formatiert." : $"Formatfehler: {err}";
                }

                default:
                    return $"Unbekanntes Tool: {name}";
            }
        }

        public static string ShortArgs(string argsJson)
        {
            if (string.IsNullOrEmpty(argsJson)) return "{}";
            try
            {
                using var doc = JsonDocument.Parse(argsJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return Trim(argsJson);
                var parts = new List<string>();
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    string value = p.Value.ValueKind == JsonValueKind.String
                        ? p.Value.GetString().Replace("\r", " ").Replace("\n", " ")
                        : p.Value.ToString();
                    if (value.Length > 80) value = value.Substring(0, 80) + "…";
                    parts.Add($"{p.Name}={value}");
                }
                return parts.Count == 0 ? "{}" : string.Join(", ", parts);
            }
            catch
            {
                return Trim(argsJson);
            }
        }

        static string Trim(string text) => text.Length > 160 ? text.Substring(0, 160) + "…" : text;

        static Dictionary<string, JsonElement> ParseArgs(string json)
        {
            var dict = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(json)) return dict;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return dict;
                foreach (var p in doc.RootElement.EnumerateObject()) dict[p.Name] = p.Value.Clone();
            }
            catch
            {
            }
            return dict;
        }

        static string Str(Dictionary<string, JsonElement> args, string key, string fallback)
        {
            if (!args.TryGetValue(key, out var v)) return fallback;
            if (v.ValueKind == JsonValueKind.String) return v.GetString();
            return v.ToString();
        }

        static int Int(Dictionary<string, JsonElement> args, string key, int fallback)
        {
            if (args.TryGetValue(key, out var v))
            {
                if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n)) return n;
                if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out int s)) return s;
            }
            return fallback;
        }

        static bool Bool(Dictionary<string, JsonElement> args, string key, bool fallback)
        {
            if (args.TryGetValue(key, out var v))
            {
                if (v.ValueKind == JsonValueKind.True) return true;
                if (v.ValueKind == JsonValueKind.False) return false;
                if (v.ValueKind == JsonValueKind.String && bool.TryParse(v.GetString(), out bool b)) return b;
            }
            return fallback;
        }
    }
}
