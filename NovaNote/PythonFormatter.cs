using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NovaNote
{
    public sealed class PythonFormatter
    {
        public sealed class Result
        {
            public string Text;
            public string Error;
        }

        public static async Task<Result> FormatAsync(string text, CancellationToken ct = default)
        {
            var py = await PythonToolRunner.GetPythonExeAsync(ct);
            var formatter = await PythonToolRunner.GetFormatterModuleAsync(ct);
            if (py == null)
                return new Result { Error = "Python nicht gefunden" };
            if (formatter == null)
                return new Result { Error = "Kein Python-Formatter gefunden (ruff, black oder autopep8)" };

            var temp = PythonToolRunner.CreateTempFile(text, ".py");
            try
            {
                string[] args = formatter switch
                {
                    "ruff" => new[] { "-m", "ruff", "format", temp },
                    "black" => new[] { "-m", "black", "--fast", temp },
                    "autopep8" => new[] { "-m", "autopep8", "--in-place", temp },
                    _ => new[] { "-m", "ruff", "format", temp }
                };

                var proc = await PythonToolRunner.RunAsync(py, args, ct);
                if (proc.Cancelled || ct.IsCancellationRequested)
                    return new Result { Error = "Python-Formatierung abgebrochen" };
                if (proc.TimedOut)
                    return new Result { Error = "Python-Formatierung: Zeitüberschreitung" };
                if (proc.ExitCode != 0)
                    return new Result { Error = Trim(proc.Stderr) ?? Trim(proc.Stdout) ?? "Python-Formatierung fehlgeschlagen" };

                string formatted = File.ReadAllText(temp, Encoding.UTF8);
                if (string.IsNullOrEmpty(formatted)) formatted = text ?? "";
                return new Result { Text = formatted };
            }
            finally
            {
                PythonToolRunner.DeleteTemp(temp);
            }
        }

        static string Trim(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
