using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NovaNote
{
    public sealed class PythonSyntaxChecker
    {
        public sealed class Result
        {
            public List<SyntaxError> Errors = new List<SyntaxError>();
            public string Status;
            public bool Cancelled;
            public bool TimedOut;
        }

        public static async Task<Result> ValidateAsync(string text, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return null;

            var py = await PythonToolRunner.GetPythonExeAsync(ct);
            if (py == null)
                return new Result { Status = "Python nicht gefunden" };

            var temp = PythonToolRunner.CreateTempFile(text, ".py");
            try
            {
                string lint = await PythonToolRunner.GetLintModuleAsync(ct);
                ProcessResult proc;

                if (lint == "ruff")
                    proc = await PythonToolRunner.RunAsync(py, new[] { "-m", "ruff", "check", "--no-cache", temp }, ct);
                else if (lint == "flake8")
                    proc = await PythonToolRunner.RunAsync(py, new[] { "-m", "flake8", temp }, ct);
                else
                    proc = await PythonToolRunner.RunAsync(py, new[] { "-m", "py_compile", temp }, ct);

                if (proc.Cancelled || ct.IsCancellationRequested) return null;

                var result = new Result();
                if (proc.TimedOut)
                {
                    result.Status = "Python-Check: Zeitüberschreitung";
                    return result;
                }

                if (proc.ExitCode == 0)
                {
                    result.Status = "Python: OK";
                    return result;
                }

                string output = Combine(proc.Stdout, proc.Stderr);
                if (lint == "ruff" || lint == "flake8")
                    result.Errors = ParseLinterOutput(text, output);
                else
                    result.Errors = ParsePyCompileOutput(text, proc.Stderr);

                if (result.Errors.Count > 0)
                    result.Status = null;
                else if (!string.IsNullOrEmpty(output.Trim()))
                    result.Status = output.Trim();
                else
                    result.Status = "Python: Fehler";

                return result;
            }
            finally
            {
                PythonToolRunner.DeleteTemp(temp);
            }
        }

        static string Combine(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b ?? "";
            if (string.IsNullOrEmpty(b)) return a;
            return a.TrimEnd() + "\n" + b.TrimEnd();
        }

        static List<SyntaxError> ParseLinterOutput(string text, string output)
        {
            var errors = new List<SyntaxError>();
            var outputLines = output.Replace("\r\n", "\n").Split('\n');

            var oldRegex = new Regex(@"^\s*.*?:(\d+):(\d+):\s*(.*)$");
            var arrowRegex = new Regex(@"^\s*-->\s.*?:(\d+):(\d+)\s*$");
            var pyrightRegex = new Regex(@"^\s*.*?:(\d+):(\d+)\s*-\s*(.*)$");

            for (int i = 0; i < outputLines.Length; i++)
            {
                string line = outputLines[i];

                var arrow = arrowRegex.Match(line);
                if (arrow.Success)
                {
                    if (!int.TryParse(arrow.Groups[1].Value, out int lineNo) ||
                        !int.TryParse(arrow.Groups[2].Value, out int colNo))
                        continue;
                    string msg = PreviousMessage(outputLines, i);
                    if (string.IsNullOrEmpty(msg)) msg = line.Trim();
                    errors.Add(MakeLine(text, lineNo, colNo, msg));
                    if (errors.Count >= SyntaxChecker.MaxErrors) break;
                    continue;
                }

                var pyright = pyrightRegex.Match(line);
                if (pyright.Success)
                {
                    if (!int.TryParse(pyright.Groups[1].Value, out int lineNo) ||
                        !int.TryParse(pyright.Groups[2].Value, out int colNo))
                        continue;
                    string msg = pyright.Groups[3].Value.Trim();
                    if (string.IsNullOrEmpty(msg)) msg = line.Trim();
                    errors.Add(MakeLine(text, lineNo, colNo, msg));
                    if (errors.Count >= SyntaxChecker.MaxErrors) break;
                    continue;
                }

                var old = oldRegex.Match(line);
                if (old.Success)
                {
                    if (!int.TryParse(old.Groups[1].Value, out int lineNo) ||
                        !int.TryParse(old.Groups[2].Value, out int colNo))
                        continue;
                    string msg = old.Groups[3].Value.Trim();
                    if (string.IsNullOrEmpty(msg)) msg = line.Trim();
                    errors.Add(MakeLine(text, lineNo, colNo, msg));
                    if (errors.Count >= SyntaxChecker.MaxErrors) break;
                }
            }
            return errors;
        }

        static string PreviousMessage(string[] lines, int index)
        {
            for (int i = index - 1; i >= 0; i--)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (line.StartsWith("-->", StringComparison.Ordinal)) continue;
                if (line.StartsWith("|", StringComparison.Ordinal)) continue;
                if (line.StartsWith("Found ", StringComparison.OrdinalIgnoreCase)) continue;
                return line;
            }
            return null;
        }

        static List<SyntaxError> ParsePyCompileOutput(string text, string output)
        {
            var errors = new List<SyntaxError>();
            var regex = new Regex(@"line\s+(\d+)", RegexOptions.IgnoreCase);
            var msg = output.Trim();
            var m = regex.Match(msg);
            int line = m.Success ? int.Parse(m.Groups[1].Value) : 1;
            if (string.IsNullOrEmpty(msg)) msg = "Python-Syntaxfehler";
            errors.Add(MakeLine(text, line, 1, msg));
            return errors;
        }

        static SyntaxError MakeLine(string text, int line1, int col1, string message)
        {
            int lineLen = text.Length;
            int lineStart = LineStart(text, line1);
            int col = Math.Max(col1 - 1, 0);
            int offset = Math.Clamp(lineStart + col, 0, lineLen);
            int remaining = Math.Max(LineLength(text, lineStart) - col, 1);
            int len = Math.Min(remaining, 60);
            return new SyntaxError { Offset = offset, Length = len, Message = message };
        }

        static int LineStart(string text, int line1)
        {
            int idx = 0;
            for (int l = 1; l < line1 && idx < text.Length; l++)
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
    }
}
