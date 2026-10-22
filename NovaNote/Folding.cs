using System;
using System.Collections.Generic;
using ICSharpCode.AvalonEdit.Folding;

namespace NovaNote
{
    public static class FoldingScanner
    {
        public const int MaxFoldedChars = 1_000_000;
        public const int MaxFoldings = 500;

        public static bool ShouldFold(DocLanguage lang)
        {
            return lang == DocLanguage.Json
                || lang == DocLanguage.Xml
                || lang == DocLanguage.Html
                || lang == DocLanguage.Css
                || lang == DocLanguage.Cpp
                || lang == DocLanguage.JavaScript;
        }

        static char Closing(char open) => open switch
        {
            '{' => '}',
            '[' => ']',
            _ => open
        };

        public static List<NewFolding> CreateBrackets(string text, DocLanguage lang, out int firstErrorOffset)
        {
            firstErrorOffset = -1;
            var foldings = new List<NewFolding>();
            var stack = new List<(char Open, int Start)>();

            bool allowBrackets = lang == DocLanguage.Json;
            bool blockComments = lang != DocLanguage.Json;
            bool lineComments = lang == DocLanguage.Cpp || lang == DocLanguage.JavaScript;
            bool templates = lang == DocLanguage.JavaScript;

            int i = 0, n = text.Length;
            int blockStart = -1;
            int lineCommentStart = -1;
            int stringStart = -1;
            char quote = '"';

            while (i < n)
            {
                char c = text[i];

                if (blockStart >= 0)
                {
                    if (c == '*' && i + 1 < n && text[i + 1] == '/')
                    {
                        blockStart = -1;
                        i += 2;
                    }
                    else i++;
                    continue;
                }
                if (lineCommentStart >= 0)
                {
                    if (c == '\n') lineCommentStart = -1;
                    i++;
                    continue;
                }
                if (stringStart >= 0)
                {
                    if (c == '\\' && quote != '`') { i += 2; continue; }
                    if (c == quote) { stringStart = -1; i++; continue; }
                    if (c == '\n' && quote != '`') stringStart = -1;
                    i++;
                    continue;
                }

                if (c == '/' && i + 1 < n)
                {
                    if (text[i + 1] == '*' && blockComments) { blockStart = i; i += 2; continue; }
                    if (text[i + 1] == '/' && lineComments) { lineCommentStart = i; i += 2; continue; }
                }

                if (c == '"' || c == '\'') { stringStart = i; quote = c; i++; continue; }
                if (c == '`' && templates) { stringStart = i; quote = '`'; i++; continue; }

                if (c == '{' || (allowBrackets && c == '['))
                {
                    stack.Add((c, i));
                    i++;
                    continue;
                }

                if (c == '}' || (allowBrackets && c == ']'))
                {
                    if (stack.Count == 0)
                    {
                        if (firstErrorOffset < 0) firstErrorOffset = i;
                    }
                    else
                    {
                        var top = stack[stack.Count - 1];
                        if (Closing(top.Open) == c)
                        {
                            stack.RemoveAt(stack.Count - 1);
                            int nl = text.IndexOf('\n', top.Start);
                            if (nl >= 0 && nl < i)
                            {
                                int end = EndOfLine(text, i);
                                if (end > top.Start)
                                {
                                    foldings.Add(new NewFolding(top.Start, end)
                                    {
                                        Name = top.Open == '{' ? "{...}" : "[...]"
                                    });
                                }
                            }
                        }
                        else if (firstErrorOffset < 0)
                        {
                            firstErrorOffset = i;
                        }
                    }
                    i++;
                    continue;
                }

                i++;
            }

            if (foldings.Count > MaxFoldings) foldings.RemoveRange(MaxFoldings, foldings.Count - MaxFoldings);
            foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
            return foldings;
        }

        static int EndOfLine(string text, int offset)
        {
            int nl = text.IndexOf('\n', Math.Min(offset, text.Length - 1));
            return nl < 0 ? text.Length : nl + 1;
        }
    }
}
