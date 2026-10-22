using System;

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
        Cpp
    }

    public static class DocLanguages
    {
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
                default: return "";
            }
        }
    }
}
