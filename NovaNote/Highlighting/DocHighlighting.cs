using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;

namespace NovaNote
{
    public static class DocHighlighting
    {
        public const string TextKey = "TextBrush";
        public const string StringKey = "SyntaxStringBrush";
        public const string NumberKey = "SyntaxNumberBrush";
        public const string KeywordKey = "SyntaxKeywordBrush";
        public const string KeyKey = "SyntaxKeyBrush";
        public const string PunctKey = "SyntaxPunctBrush";
        public const string TagKey = "SyntaxTagBrush";
        public const string AttrKey = "SyntaxAttrBrush";
        public const string CommentKey = "SyntaxCommentBrush";
        public const string PreprocKey = "SyntaxPreprocBrush";

        static Dictionary<string, Brush> _brushCache;
        static bool _jsonRegistered;
        static bool _markdownRegistered;
        static bool _yamlRegistered;

        public static Brush BrushForKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            var cache = _brushCache;
            if (cache == null)
            {
                cache = new Dictionary<string, Brush>(StringComparer.Ordinal);
                _brushCache = cache;
            }
            if (cache.TryGetValue(key, out var cached)) return cached;
            Brush brush = null;
            var res = Application.Current?.Resources;
            if (res != null && res.Contains(key)) brush = res[key] as Brush;
            cache[key] = brush;
            return brush;
        }

        public static void InvalidateBrushes() => _brushCache = null;

        public static Brush BrushForColorName(string name)
        {
            var key = MapColorName(name);
            return BrushForKey(key);
        }

        static string MapColorName(string name)
        {
            if (string.IsNullOrEmpty(name)) return TextKey;
            string n = name.ToLowerInvariant();
            if (n.Contains("heading") || n.Contains("strong") || n.Contains("emphasis")) return KeywordKey;
            if (n.Contains("quote") || n.Contains("inlinecode") || n.Contains("code")) return CommentKey;
            if (n.Contains("link")) return StringKey;
            if (n.Contains("list")) return TagKey;
            if (n.Contains("comment")) return CommentKey;
            if (n == "attributevalue") return StringKey;
            if (n.Contains("attribute")) return AttrKey;
            if (n.Contains("preprocessor") || n.Contains("preproc") ||
                n == "doctype" || n == "xmldeclaration" || n.Contains("cdata")) return PreprocKey;
            if (n.Contains("tag") || n == "selector" || n == "class") return TagKey;
            if (n == "fieldname" || n == "key" || n == "property" || n == "identifier") return KeyKey;
            if (n == "bool" || n == "null" || n.Contains("keyword") || n.Contains("reserved")) return KeywordKey;
            if (n.Contains("number") || n.Contains("digit") || n.Contains("integer") || n.Contains("real")) return NumberKey;
            if (n.Contains("string") || n.Contains("character") || n.Contains("entity") || n == "value") return StringKey;
            if (n.Contains("punctuation") || n.Contains("operator") || n.Contains("curly") ||
                n.Contains("brace") || n == "colon" || n == "slash" || n == "assignment") return PunctKey;
            return TextKey;
        }

        public static IHighlightingDefinition DefinitionFor(DocLanguage lang)
        {
            switch (lang)
            {
                case DocLanguage.Json:
                    EnsureJsonRegistered();
                    return HighlightingManager.Instance.GetDefinition("JSON");
                case DocLanguage.Xml: return HighlightingManager.Instance.GetDefinition("XML");
                case DocLanguage.Html: return HighlightingManager.Instance.GetDefinition("HTML");
                case DocLanguage.Css: return HighlightingManager.Instance.GetDefinition("CSS");
                case DocLanguage.JavaScript: return HighlightingManager.Instance.GetDefinition("JavaScript");
                case DocLanguage.Cpp: return HighlightingManager.Instance.GetDefinition("C++");
                case DocLanguage.Markdown:
                    EnsureMarkdownRegistered();
                    return HighlightingManager.Instance.GetDefinition("Markdown");
                case DocLanguage.Python: return HighlightingManager.Instance.GetDefinition("Python");
                case DocLanguage.CSharp: return HighlightingManager.Instance.GetDefinition("C#");
                case DocLanguage.Yaml:
                    var yaml = HighlightingManager.Instance.GetDefinition("YAML");
                    if (yaml != null) return yaml;
                    EnsureYamlCustomRegistered();
                    return HighlightingManager.Instance.GetDefinition("NovaNote YAML");
                default: return null;
            }
        }

        static void EnsureJsonRegistered()
        {
            if (_jsonRegistered) return;
            _jsonRegistered = true;
            HighlightingManager.Instance.RegisterHighlighting("JSON", new[] { "json", "jsonc", "json5" }, LoadJsonDefinition);
        }

        static void EnsureMarkdownRegistered()
        {
            if (_markdownRegistered) return;
            _markdownRegistered = true;
            HighlightingManager.Instance.RegisterHighlighting("Markdown", new[] { "md", "markdown" }, LoadMarkdownDefinition);
        }

        static void EnsureYamlCustomRegistered()
        {
            if (_yamlRegistered) return;
            _yamlRegistered = true;
            HighlightingManager.Instance.RegisterHighlighting("NovaNote YAML", new[] { "yaml", "yml" }, LoadYamlDefinition);
        }

        static IHighlightingDefinition LoadYamlDefinition()
        {
            try
            {
                var asm = typeof(DocHighlighting).Assembly;
                string name = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("Yaml.xshd", StringComparison.OrdinalIgnoreCase));
                if (name == null) return null;
                using var stream = asm.GetManifestResourceStream(name);
                if (stream == null) return null;
                using var reader = XmlReader.Create(stream);
                var xshd = HighlightingLoader.LoadXshd(reader);
                return HighlightingLoader.Load(xshd, HighlightingManager.Instance);
            }
            catch
            {
                return null;
            }
        }

        static IHighlightingDefinition LoadMarkdownDefinition()
        {
            try
            {
                var asm = typeof(DocHighlighting).Assembly;
                string name = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("Markdown.xshd", StringComparison.OrdinalIgnoreCase));
                if (name == null) return null;
                using var stream = asm.GetManifestResourceStream(name);
                if (stream == null) return null;
                using var reader = XmlReader.Create(stream);
                var xshd = HighlightingLoader.LoadXshd(reader);
                return HighlightingLoader.Load(xshd, HighlightingManager.Instance);
            }
            catch
            {
                return null;
            }
        }

        static IHighlightingDefinition LoadJsonDefinition()
        {
            try
            {
                var asm = typeof(DocHighlighting).Assembly;
                string name = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("Json.xshd", StringComparison.OrdinalIgnoreCase));
                if (name == null) return null;
                using var stream = asm.GetManifestResourceStream(name);
                if (stream == null) return null;
                using var reader = XmlReader.Create(stream);
                var xshd = HighlightingLoader.LoadXshd(reader);
                return HighlightingLoader.Load(xshd, HighlightingManager.Instance);
            }
            catch
            {
                return null;
            }
        }
    }

    public class ThemeHighlightingColorizer : HighlightingColorizer
    {
        public ThemeHighlightingColorizer(IHighlightingDefinition definition) : base(definition) { }

        protected override void ApplyColorToElement(VisualLineElement element, HighlightingColor color)
        {
            var brush = DocHighlighting.BrushForColorName(color.Name);
            if (brush != null) element.TextRunProperties.SetForegroundBrush(brush);
            if (color.FontStyle != null || color.FontWeight != null || color.FontFamily != null)
            {
                var tf = element.TextRunProperties.Typeface;
                element.TextRunProperties.SetTypeface(new Typeface(
                    color.FontFamily ?? tf.FontFamily,
                    color.FontStyle ?? tf.Style,
                    color.FontWeight ?? tf.Weight,
                    tf.Stretch));
            }
            if (color.Underline ?? false) element.TextRunProperties.SetTextDecorations(TextDecorations.Underline);
            if (color.Strikethrough ?? false) element.TextRunProperties.SetTextDecorations(TextDecorations.Strikethrough);
        }
    }
}
