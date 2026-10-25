using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NovaNote
{
    public static class AiHelp
    {
        static readonly object Lock = new object();
        static List<Section> _sections;

        class Section
        {
            public string Title;
            public int Level;
            public string Body;
        }

        public static string Version()
        {
            var asm = typeof(AiHelp).Assembly;
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrWhiteSpace(info))
                info = asm.GetName()?.Version?.ToString() ?? "unbekannt";
            return info.Split('+')[0].TrimStart('v');
        }

        static void Ensure()
        {
            if (_sections != null) return;
            lock (Lock)
            {
                if (_sections != null) return;
                var text = LoadRaw().Replace("%VERSION%", Version());
                _sections = Parse(text);
            }
        }

        static string LoadRaw()
        {
            try
            {
                var asm = typeof(AiHelp).Assembly;
                string name = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("hilfe.md", StringComparison.OrdinalIgnoreCase));
                if (name == null) return "";
                using var stream = asm.GetManifestResourceStream(name);
                if (stream == null) return "";
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch
            {
                return "";
            }
        }

        static List<Section> Parse(string text)
        {
            var list = new List<Section>();
            var lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            string title = "Überblick";
            int level = 2;
            var body = new StringBuilder();

            foreach (var raw in lines)
            {
                string line = raw.TrimEnd();
                bool h1 = line.StartsWith("# ") && !line.StartsWith("## ");
                bool h2 = line.StartsWith("## ");
                bool h3 = line.StartsWith("### ");
                if (h1)
                {
                    title = "Überblick";
                    level = 2;
                    body.Clear();
                    continue;
                }
                if (h2 || h3)
                {
                    if (body.Length > 0 || title != "Überblick")
                        list.Add(new Section { Title = title, Level = level, Body = body.ToString().Trim() });
                    title = (h2 ? line.Substring(3) : line.Substring(4)).Trim();
                    level = h2 ? 2 : 3;
                    body.Clear();
                }
                else
                {
                    body.AppendLine(line);
                }
            }
            list.Add(new Section { Title = title, Level = level, Body = body.ToString().Trim() });
            return list;
        }

        public static string Index()
        {
            Ensure();
            var sb = new StringBuilder();
            sb.Append("NovaNote-Hilfe (Version ").Append(Version())
              .Append(") – Abschnitte abrufen mit get_help \"<Thema>\":\n");
            foreach (var sec in _sections)
            {
                if (string.Equals(sec.Title, "Überblick", StringComparison.OrdinalIgnoreCase)) continue;
                if (sec.Level == 3) sb.Append("  ");
                sb.Append("- ").Append(sec.Title).Append('\n');
            }
            return sb.ToString().TrimEnd();
        }

        public static string Get(string topic)
        {
            Ensure();
            if (_sections == null || _sections.Count == 0)
                return "Hilfe nicht verfügbar.";

            string t = (topic ?? "").Trim();
            if (t.Length == 0) return Index();

            string tl = t.ToLowerInvariant();
            var terms = tl.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            var scored = new List<(Section Sec, int Score)>();
            foreach (var sec in _sections)
            {
                string titleL = sec.Title.ToLowerInvariant();
                string bodyL = sec.Body.ToLowerInvariant();
                int score = 0;
                if (titleL == tl) score += 100;
                else if (titleL.Contains(tl) || tl.Contains(titleL)) score += 40;
                foreach (var term in terms)
                {
                    if (term.Length < 2) continue;
                    score += Count(titleL, term) * 15 + Count(bodyL, term) * 2;
                }
                if (sec.Level == 2) score += 1;
                if (score > 0) scored.Add((sec, score));
            }

            if (scored.Count == 0)
                return "Kein Abschnitt für \"" + t + "\". Inhaltsverzeichnis:\n\n" + Index();

            scored = scored.OrderByDescending(x => x.Score).ToList();
            const int budget = 4000;
            var sb = new StringBuilder();
            int used = 0, added = 0;
            for (int i = 0; i < scored.Count && i < 3; i++)
            {
                var sec = scored[i].Sec;
                string chunk = (used > 0 ? "\n\n" : "") + "[" + sec.Title + "]\n" + sec.Body;
                if (used + chunk.Length > budget)
                {
                    int remain = budget - used;
                    if (remain > 120) sb.Append(chunk, 0, remain).Append("\n… (gekürzt)");
                    break;
                }
                sb.Append(chunk);
                used += chunk.Length;
                added++;
            }
            return sb.ToString();
        }

        static int Count(string haystack, string needle)
        {
            int count = 0, idx = 0;
            while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += needle.Length;
            }
            return count;
        }
    }
}
