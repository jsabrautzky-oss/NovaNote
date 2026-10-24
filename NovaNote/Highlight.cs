using System;
using System.Text;

namespace NovaNote
{
    public enum DocLanguage
    {
        None = 0,
        Json,
        Xml,
        Html,
        Css,
        JavaScript,
        Cpp,
        Markdown,
        Python,
        CSharp,
        Yaml
    }

    public static class DocLanguages
    {
        static readonly (DocLanguage Lang, string Label, string Pattern, string Ext)[] Kinds =
        {
            (DocLanguage.None, "Textdokumente", "*.txt", "txt"),
            (DocLanguage.Markdown, "Markdown", "*.md;*.markdown", "md"),
            (DocLanguage.Json, "JSON", "*.json;*.jsonc;*.json5", "json"),
            (DocLanguage.Xml, "XML", "*.xml;*.xsl;*.xhtml;*.svg", "xml"),
            (DocLanguage.Html, "HTML", "*.html;*.htm", "html"),
            (DocLanguage.Css, "CSS", "*.css", "css"),
            (DocLanguage.JavaScript, "JavaScript", "*.js;*.mjs;*.ts", "js"),
            (DocLanguage.Cpp, "C++", "*.cpp;*.cc;*.cxx;*.c;*.h;*.hpp", "cpp"),
            (DocLanguage.Python, "Python", "*.py;*.pyw", "py"),
            (DocLanguage.CSharp, "C#", "*.cs;*.csx", "cs"),
            (DocLanguage.Yaml, "YAML", "*.yaml;*.yml", "yaml")
        };

        public static string Filter
        {
            get
            {
                var sb = new StringBuilder();
                foreach (var k in Kinds)
                    sb.Append($"{k.Label} ({k.Pattern})|{k.Pattern}|");
                sb.Append("Alle Dateien (*.*)|*.*");
                return sb.ToString();
            }
        }

        public static int FilterIndex(DocLanguage lang)
        {
            for (int i = 0; i < Kinds.Length; i++)
                if (Kinds[i].Lang == lang) return i + 1;
            return 1;
        }

        public static string Extension(DocLanguage lang)
        {
            foreach (var k in Kinds)
                if (k.Lang == lang) return k.Ext;
            return "txt";
        }

        public static DocLanguage FromExtension(string path)
        {
            if (string.IsNullOrEmpty(path)) return DocLanguage.None;
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".json":
                case ".jsonc":
                case ".json5": return DocLanguage.Json;
                case ".xml":
                case ".xsl":
                case ".xhtml":
                case ".svg": return DocLanguage.Xml;
                case ".html":
                case ".htm": return DocLanguage.Html;
                case ".css": return DocLanguage.Css;
                case ".js":
                case ".mjs":
                case ".ts": return DocLanguage.JavaScript;
                case ".cpp":
                case ".cc":
                case ".cxx":
                case ".c":
                case ".h":
                case ".hpp": return DocLanguage.Cpp;
                case ".md":
                case ".markdown": return DocLanguage.Markdown;
                case ".py":
                case ".pyw": return DocLanguage.Python;
                case ".cs":
                case ".csx": return DocLanguage.CSharp;
                case ".yaml":
                case ".yml": return DocLanguage.Yaml;
                default: return DocLanguage.None;
            }
        }

        public static DocLanguage DetectContent(string text)
        {
            if (string.IsNullOrEmpty(text)) return DocLanguage.None;
            string t = text.TrimStart();
            if (t.StartsWith("<?xml")) return DocLanguage.Xml;
            if (t.StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase) ||
                t.StartsWith("<html", StringComparison.OrdinalIgnoreCase)) return DocLanguage.Html;
            if (t.Length > 0 && (t[0] == '{' || t[0] == '['))
            {
                try
                {
                    System.Text.Json.JsonDocument.Parse(text,
                        new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip });
                    return DocLanguage.Json;
                }
                catch { return DocLanguage.None; }
            }
            return DocLanguage.None;
        }

        public static string AllowedNames =>
            "text, json, xml, html, css, javascript, cpp, markdown, python, csharp, yaml";

        public static DocLanguage? Parse(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            switch (name.Trim().ToLowerInvariant())
            {
                case "text":
                case "plain":
                case "plain text":
                case "plaintext":
                case "none":
                case "no language":
                case "nolang":
                case "null": return DocLanguage.None;
                case "json":
                case "jsonc":
                case "json5": return DocLanguage.Json;
                case "xml":
                case "xsl":
                case "svg": return DocLanguage.Xml;
                case "html":
                case "htm": return DocLanguage.Html;
                case "css": return DocLanguage.Css;
                case "javascript":
                case "js":
                case "ts":
                case "typescript": return DocLanguage.JavaScript;
                case "cpp":
                case "c++":
                case "c":
                case "cc":
                case "cxx": return DocLanguage.Cpp;
                case "markdown":
                case "md": return DocLanguage.Markdown;
                case "python":
                case "py":
                case "pyw": return DocLanguage.Python;
                case "csharp":
                case "c#":
                case "cs":
                case "csx": return DocLanguage.CSharp;
                case "yaml":
                case "yml": return DocLanguage.Yaml;
                default: return null;
            }
        }

        public static string DisplayName(DocLanguage lang)
        {
            switch (lang)
            {
                case DocLanguage.Json: return "JSON";
                case DocLanguage.Xml: return "XML";
                case DocLanguage.Html: return "HTML";
                case DocLanguage.Css: return "CSS";
                case DocLanguage.JavaScript: return "JavaScript";
                case DocLanguage.Cpp: return "C++";
                case DocLanguage.Markdown: return "Markdown";
                case DocLanguage.Python: return "Python";
                case DocLanguage.CSharp: return "C#";
                case DocLanguage.Yaml: return "YAML";
                default: return "Text";
            }
        }
    }
}
