using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Xml;
using Acornima;
using HtmlAgilityPack;

namespace NovaNote
{
    public sealed class SyntaxError
    {
        public int Offset;
        public int Length;
        public string Message;
    }

    public static class SyntaxChecker
    {
        public const int MaxErrors = 100;
        public const int MaxCheckedChars = 2_000_000;

        public static bool ShouldCheck(DocLanguage lang, string path)
        {
            if (lang == DocLanguage.None) return false;
            if (lang == DocLanguage.JavaScript && !string.IsNullOrEmpty(path))
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".ts" || ext == ".mts" || ext == ".cts") return false;
            }
            return true;
        }

        public static List<SyntaxError> Validate(DocLanguage lang, string text)
        {
            switch (lang)
            {
                case DocLanguage.Json: return ValidateJson(text);
                case DocLanguage.Xml: return ValidateXml(text);
                case DocLanguage.Html: return ValidateHtml(text);
                case DocLanguage.JavaScript: return ValidateJs(text);
                case DocLanguage.Css: return ValidateCss(text);
                case DocLanguage.Cpp: return ValidateCpp(text);
                default: return new List<SyntaxError>();
            }
        }

        static SyntaxError Make(string text, int offset, string message, int maxLength = 60)
        {
            int len = text.Length;
            if (len == 0) return new SyntaxError { Offset = 0, Length = 0, Message = message };
            int start = Math.Clamp(offset, 0, len - 1);
            int rest = len - start;
            return new SyntaxError
            {
                Offset = start,
                Length = Math.Min(Math.Max(rest, 1), maxLength),
                Message = message
            };
        }

        static void Limit(List<SyntaxError> errors)
        {
            if (errors.Count > MaxErrors) errors.RemoveRange(MaxErrors, errors.Count - MaxErrors);
        }

        static int LineStart(string text, int line1)
        {
            int idx = 0;
            for (int l = 1; l < line1; l++)
            {
                int nl = text.IndexOf('\n', idx);
                if (nl < 0) return text.Length;
                idx = nl + 1;
            }
            return idx;
        }

        static int LineLength(string text, int lineStart)
        {
            int nl = text.IndexOf('\n', lineStart);
            return nl < 0 ? text.Length - lineStart : nl - lineStart;
        }

        static List<SyntaxError> ValidateJson(string text)
        {
            var errors = new List<SyntaxError>();
            try
            {
                using var doc = JsonDocument.Parse(text,
                    new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            }
            catch (JsonException ex)
            {
                int offset = 0;
                int line = (int)(ex.LineNumber ?? 0);
                int bytePos = (int)(ex.BytePositionInLine ?? 0);
                if (line > 0)
                {
                    int lineStart = LineStart(text, line);
                    int col = CharColumnFromBytes(text, lineStart, LineLength(text, lineStart), bytePos);
                    offset = lineStart + col;
                }
                else
                {
                    offset = CharColumnFromBytes(text, 0, text.Length, bytePos);
                }
                offset = TokenStart(text, offset);
                errors.Add(Make(text, offset, ex.Message, 40));
            }
            return errors;
        }

        static int CharColumnFromBytes(string text, int lineStart, int lineLen, int bytePos)
        {
            if (bytePos <= 0) return 0;
            int bytes = 0, chars = 0;
            int end = Math.Min(lineStart + lineLen, text.Length);
            while (lineStart + chars < end && bytes < bytePos)
            {
                char c = text[lineStart + chars];
                if (c < 0x80) bytes += 1;
                else if (c < 0x800) bytes += 2;
                else if (char.IsSurrogate(c)) { bytes += 4; chars++; continue; }
                else bytes += 3;
                chars++;
            }
            return chars;
        }

        static int TokenStart(string text, int offset)
        {
            if (text.Length == 0) return 0;
            int start = Math.Min(offset, text.Length - 1);
            while (start > 0 && IsTokenChar(text[start - 1])) start--;
            return start;
        }

        static bool IsTokenChar(char c) =>
            char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-' || c == '+' || c == '$';

        static List<SyntaxError> ValidateXml(string text)
        {
            var errors = new List<SyntaxError>();
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreWhitespace = true,
                CloseInput = true
            };
            try
            {
                using var reader = XmlReader.Create(new StringReader(text), settings);
                while (!reader.EOF) reader.Read();
            }
            catch (XmlException ex)
            {
                int offset = 0;
                if (ex.LineNumber > 0)
                {
                    int lineStart = LineStart(text, ex.LineNumber);
                    int col = Math.Max(0, ex.LinePosition - 1);
                    offset = lineStart + Math.Min(col, LineLength(text, lineStart));
                }
                errors.Add(Make(text, offset, ex.Message, 40));
            }
            return errors;
        }

        static List<SyntaxError> ValidateHtml(string text)
        {
            var errors = new List<SyntaxError>();
            var html = new HtmlDocument();
            HtmlDocument.MaxDepthLevel = 1000;
            try
            {
                html.LoadHtml(text);
            }
            catch (Exception ex)
            {
                errors.Add(Make(text, 0, ex.Message, 40));
                return errors;
            }
            foreach (var e in html.ParseErrors)
            {
                if (e.Code == HtmlParseErrorCode.EndTagNotRequired || e.Code == HtmlParseErrorCode.CharsetMismatch)
                    continue;
                int offset = e.StreamPosition >= 0 && e.StreamPosition < text.Length
                    ? e.StreamPosition
                    : LineStart(text, e.Line) + Math.Max(0, e.LinePosition);
                string reason = string.IsNullOrEmpty(e.Reason) ? e.Code.ToString() : e.Reason;
                errors.Add(Make(text, offset, reason, 40));
                if (errors.Count >= MaxErrors) break;
            }
            return errors;
        }

        static List<SyntaxError> ValidateJs(string text)
        {
            var errors = new List<SyntaxError>();
            var collector = new ParseErrorCollector();
            var options = new ParserOptions { Tolerant = true, ErrorHandler = collector };
            try
            {
                new Parser(options).ParseScript(text);
            }
            catch (ParseErrorException ex)
            {
                var pe = ex.Error;
                if (pe != null) errors.Add(Make(text, OffsetOf(text, pe), pe.Description, 40));
            }
            catch (Exception ex)
            {
                errors.Add(Make(text, 0, ex.Message, 40));
            }
            foreach (var pe in collector.Errors)
            {
                errors.Add(Make(text, OffsetOf(text, pe), pe.Description, 40));
                if (errors.Count >= MaxErrors) break;
            }
            Limit(errors);
            return errors;
        }

        static int OffsetOf(string text, ParseError error)
        {
            if (error.IsIndexDefined && error.Index >= 0 && error.Index < text.Length) return error.Index;
            if (error.IsPositionDefined)
            {
                int lineStart = LineStart(text, error.LineNumber);
                return lineStart + Math.Min(Math.Max(0, error.Column), LineLength(text, lineStart));
            }
            return 0;
        }

        static List<SyntaxError> ValidateCss(string text)
        {
            var errors = new List<SyntaxError>();
            var braces = new List<int>();
            var parens = new List<int>();
            int i = 0, n = text.Length;
            int commentStart = -1;
            int stringStart = -1;
            char quote = '"';

            while (i < n)
            {
                char c = text[i];
                if (commentStart >= 0)
                {
                    if (c == '*' && i + 1 < n && text[i + 1] == '/') { commentStart = -1; i += 2; continue; }
                    i++;
                    continue;
                }
                if (stringStart >= 0)
                {
                    if (c == '\\') { i += 2; continue; }
                    if (c == quote) { stringStart = -1; i++; continue; }
                    if (c == '\n')
                    {
                        errors.Add(Make(text, stringStart, "nicht abgeschlossene Zeichenkette", 40));
                        stringStart = -1;
                        i++;
                        continue;
                    }
                    i++;
                    continue;
                }
                if (c == '/' && i + 1 < n && text[i + 1] == '*') { commentStart = i; i += 2; continue; }
                if (c == '"' || c == '\'') { stringStart = i; quote = c; i++; continue; }
                if (c == '{') { braces.Add(i); i++; continue; }
                if (c == '}')
                {
                    if (braces.Count == 0) errors.Add(Make(text, i, "unerwartetes '}'", 1));
                    else braces.RemoveAt(braces.Count - 1);
                    i++;
                    continue;
                }
                if (c == '(') { parens.Add(i); i++; continue; }
                if (c == ')')
                {
                    if (parens.Count == 0) errors.Add(Make(text, i, "unerwartetes ')'", 1));
                    else parens.RemoveAt(parens.Count - 1);
                    i++;
                    continue;
                }
                i++;
            }

            if (commentStart >= 0) errors.Add(Make(text, commentStart, "nicht abgeschlossener Kommentar /* … */", 2));
            if (stringStart >= 0) errors.Add(Make(text, stringStart, "nicht abgeschlossene Zeichenkette", 40));
            foreach (int open in braces) errors.Add(Make(text, open, "fehlendes '}'", 1));
            foreach (int open in parens) errors.Add(Make(text, open, "fehlendes ')'", 1));
            Limit(errors);
            return errors;
        }

        static char Closing(char open) => open switch
        {
            '{' => '}',
            '(' => ')',
            '[' => ']',
            _ => open
        };

        static List<SyntaxError> ValidateCpp(string text)
        {
            var errors = new List<SyntaxError>();
            var stack = new List<(char Open, int Offset)>();
            int i = 0, n = text.Length;
            int commentStart = -1;
            int stringStart = -1;
            char quote = '"';

            while (i < n)
            {
                char c = text[i];
                if (commentStart >= 0)
                {
                    if (c == '*' && i + 1 < n && text[i + 1] == '/') { commentStart = -1; i += 2; continue; }
                    i++;
                    continue;
                }
                if (stringStart >= 0)
                {
                    if (c == '\\') { i += 2; continue; }
                    if (c == quote) { stringStart = -1; i++; continue; }
                    if (c == '\n')
                    {
                        errors.Add(Make(text, stringStart, "nicht abgeschlossene Zeichenkette", 40));
                        stringStart = -1;
                        i++;
                        continue;
                    }
                    i++;
                    continue;
                }
                if (c == '/' && i + 1 < n)
                {
                    if (text[i + 1] == '*') { commentStart = i; i += 2; continue; }
                    if (text[i + 1] == '/')
                    {
                        while (i < n && text[i] != '\n') i++;
                        continue;
                    }
                }
                if (c == '"' || c == '\'') { stringStart = i; quote = c; i++; continue; }
                if (c == '{' || c == '(' || c == '[') { stack.Add((c, i)); i++; continue; }
                if (c == '}' || c == ')' || c == ']')
                {
                    if (stack.Count == 0)
                    {
                        errors.Add(Make(text, i, $"unerwartetes '{c}'", 1));
                    }
                    else
                    {
                        var (open, off) = stack[stack.Count - 1];
                        stack.RemoveAt(stack.Count - 1);
                        if (Closing(open) != c)
                            errors.Add(Make(text, i, $"unerwartetes '{c}' – erwartet '{Closing(open)}'", 1));
                    }
                    i++;
                    continue;
                }
                i++;
            }

            if (commentStart >= 0) errors.Add(Make(text, commentStart, "nicht abgeschlossener Kommentar /* … */", 2));
            if (stringStart >= 0) errors.Add(Make(text, stringStart, "nicht abgeschlossene Zeichenkette", 40));
            for (int k = stack.Count - 1; k >= 0; k--)
                errors.Add(Make(text, stack[k].Offset, $"fehlendes '{Closing(stack[k].Open)}'", 1));
            Limit(errors);
            return errors;
        }
    }
}
