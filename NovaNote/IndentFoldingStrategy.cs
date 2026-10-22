using System;
using System.Collections.Generic;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;

namespace NovaNote
{
    public sealed class IndentFoldingStrategy
    {
        public void UpdateFoldings(FoldingManager manager, TextDocument document)
        {
            manager.UpdateFoldings(CreateFoldings(document, FoldingScanner.MaxFoldings), -1);
        }

        public List<NewFolding> CreateFoldings(TextDocument document, int maxFoldings)
        {
            var foldings = new List<NewFolding>();
            int lineCount = document.LineCount;
            if (lineCount == 0) return foldings;

            for (int i = 1; i <= lineCount && foldings.Count < maxFoldings; i++)
            {
                var line = document.GetLineByNumber(i);
                if (IsBlank(document.GetText(line.Offset, line.Length))) continue;
                int indent = IndentOf(document.GetText(line.Offset, line.Length));
                if (indent < 0) continue;

                int last = -1;
                int j = i + 1;
                while (j <= lineCount)
                {
                    var next = document.GetLineByNumber(j);
                    if (IsBlank(document.GetText(next.Offset, next.Length)))
                    {
                        j++;
                        continue;
                    }
                    int nextIndent = IndentOf(document.GetText(next.Offset, next.Length));
                    if (nextIndent > indent)
                    {
                        last = j;
                        j++;
                    }
                    else break;
                }

                if (last > i)
                {
                    int end = document.GetLineByNumber(last).EndOffset;
                    foldings.Add(new NewFolding
                    {
                        StartOffset = line.Offset,
                        EndOffset = end,
                        Name = "YAML"
                    });
                }
            }

            foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
            return foldings;
        }

        static bool IsBlank(string text)
        {
            foreach (char c in text)
                if (c != ' ' && c != '\t') return false;
            return true;
        }

        static int IndentOf(string text)
        {
            int spaces = 0;
            foreach (char c in text)
            {
                if (c == ' ') spaces++;
                else if (c == '\t') spaces += 2;
                else break;
            }
            return spaces;
        }
    }
}
