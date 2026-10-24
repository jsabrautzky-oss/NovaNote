using System;
using System.Collections.Generic;
using System.Linq;
using Tiktoken;
using Tiktoken.Encodings;

namespace NovaNote
{
    public enum TokenKind
    {
        Heuristic,
        Cl100k,
        O200k
    }

    public class BudgetResult
    {
        public List<AiMessage> Messages = new List<AiMessage>();
        public int Tokens;
        public int DroppedMessages;
        public int DroppedImages;
        public bool TruncatedText;
    }

    public static class ContextBudget
    {
        public const int FallbackContext = 4096;
        public const int MinContext = 512;
        public const int MinReserve = 256;
        public const int MaxReserve = 1024;
        public const int MaxKeptMessages = 40;

        const int MessageOverhead = 4;

        static readonly string[] O200kNames =
        {
            "gpt-4o", "gpt-4.1", "gpt-5", "-o1", "-o2", "-o3", "-o4", "o1-", "o3-", "o4-", "codex"
        };

        static readonly object Gate = new object();
        static Encoder _cl100k;
        static Encoder _o200k;
        static readonly Dictionary<string, double> _calib = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        public static int ContextFor(AiModel model) => Math.Max(MinContext, model?.EffectiveContext ?? FallbackContext);

        public static int Reserve(int ctx) => Math.Clamp(ctx / 4, MinReserve, MaxReserve);

        public static int BudgetFor(AiModel model)
        {
            int ctx = ContextFor(model);
            return Math.Max(MinContext, ctx - Reserve(ctx));
        }

        public static TokenKind KindFor(AiProvider provider, AiModel model)
        {
            if (provider != null && (provider.Kind == AiKind.LmStudio || provider.Kind == AiKind.Ollama))
                return TokenKind.Heuristic;
            string hay = ((model?.Id ?? "") + " " + (model?.Name ?? "")).ToLowerInvariant();
            foreach (var token in O200kNames)
                if (hay.Contains(token, StringComparison.Ordinal)) return TokenKind.O200k;
            return TokenKind.Cl100k;
        }

        public static string ModeLabel(TokenKind kind) => kind switch
        {
            TokenKind.O200k => "exakt (o200k)",
            TokenKind.Cl100k => "exakt (cl100k)",
            _ => "geschätzt"
        };

        static Encoder EncoderFor(TokenKind kind)
        {
            if (kind == TokenKind.Heuristic) return null;
            lock (Gate)
            {
                if (kind == TokenKind.O200k)
                {
                    _o200k ??= new Encoder(new O200KBase());
                    return _o200k;
                }
                _cl100k ??= new Encoder(new Cl100KBase());
                return _cl100k;
            }
        }

        public static int TextTokens(string text, TokenKind kind)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var enc = EncoderFor(kind);
            if (enc != null)
            {
                try { return enc.CountTokens(text); }
                catch { }
            }
            return Heuristic(text);
        }

        static int Heuristic(string text)
        {
            int ascii = 0, wide = 0;
            foreach (char c in text)
                if (c > 0x2000) wide++;
                else ascii++;
            return Math.Max(1, (int)Math.Ceiling(ascii / 4.0 + wide / 1.8));
        }

        public static int ImageTokenCost(List<string> images, int explicitTokens)
        {
            if (images == null || images.Count == 0) return explicitTokens;
            if (explicitTokens > 0) return explicitTokens;
            int total = 0;
            foreach (var uri in images)
                total += Math.Clamp(uri.Length / 600, 48, 1200);
            return total;
        }

        public static int MessageTokens(AiMessage m, TokenKind kind)
        {
            if (m == null) return 0;
            int t = MessageOverhead;
            t += TextTokens(m.Content, kind);
            if (m.ToolCalls != null)
                foreach (var call in m.ToolCalls)
                    t += MessageOverhead + TextTokens(call.Name, kind) + TextTokens(call.Arguments, kind);
            t += ImageTokenCost(m.Images, m.ImageTokens);
            return t;
        }

        public static int Count(List<AiMessage> messages, AiProvider provider, AiModel model)
        {
            if (messages == null || messages.Count == 0) return 0;
            var kind = KindFor(provider, model);
            int raw = 0;
            foreach (var m in messages) raw += MessageTokens(m, kind);
            return (int)Math.Round(raw * Factor(provider, model));
        }

        static string Key(AiProvider provider, AiModel model) => (provider?.Id ?? "") + "|" + (model?.Id ?? "");

        public static double Factor(AiProvider provider, AiModel model) =>
            _calib.TryGetValue(Key(provider, model), out double f) ? f : 1.0;

        public static void Calibrate(AiProvider provider, AiModel model, int measured, int estimated)
        {
            if (measured <= 0 || estimated <= 0) return;
            _calib[Key(provider, model)] = Math.Clamp((double)measured / estimated, 0.35, 4.0);
        }

        public static void ResetCalibration(AiProvider provider, AiModel model) => _calib.Remove(Key(provider, model));

        static List<List<AiMessage>> BuildGroups(List<AiMessage> rest)
        {
            var groups = new List<List<AiMessage>>();
            for (int i = 0; i < rest.Count; i++)
            {
                var m = rest[i];
                if (m.Role == "tool" && groups.Count > 0 && groups[^1].Count > 0 &&
                    groups[^1][0].Role == "assistant" && groups[^1][0].ToolCalls != null)
                {
                    groups[^1].Add(m);
                    continue;
                }
                groups.Add(new List<AiMessage> { m });
            }
            return groups;
        }

        static int Estimate(List<AiMessage> system, List<List<AiMessage>> groups, TokenKind kind, double factor)
        {
            int raw = 0;
            foreach (var s in system) raw += MessageTokens(s, kind);
            foreach (var g in groups)
                foreach (var m in g) raw += MessageTokens(m, kind);
            return (int)Math.Round(raw * factor);
        }

        public static BudgetResult ApplyBudget(AiProvider provider, AiModel model, List<AiMessage> messages)
        {
            var res = new BudgetResult();
            if (messages == null || messages.Count == 0) return res;

            var kind = KindFor(provider, model);
            double factor = Factor(provider, model);
            int budget = BudgetFor(model);

            var system = messages.Where(m => m.Role == "system").ToList();
            var rest = messages.Where(m => m.Role != "system").ToList();
            if (rest.Count > MaxKeptMessages)
            {
                res.DroppedMessages = rest.Count - MaxKeptMessages;
                rest = rest.Skip(rest.Count - MaxKeptMessages).ToList();
            }
            var groups = BuildGroups(rest);
            while (groups.Count > 0 && groups[0][0].Role == "tool")
            {
                res.DroppedMessages += groups[0].Count;
                groups.RemoveAt(0);
            }
            res.Messages = Flatten(system, groups);
            res.Tokens = Estimate(system, groups, kind, factor);
            if (res.Tokens <= budget) return res;

            while (groups.Count > 1 && Estimate(system, groups, kind, factor) > budget)
            {
                var g = groups[0];
                groups.RemoveAt(0);
                res.DroppedMessages += g.Count;
            }
            res.Messages = Flatten(system, groups);

            if (Estimate(system, groups, kind, factor) > budget)
            {
                var lastUser = groups.LastOrDefault(g => g.Any(m => m.Role == "user" && m.Images != null && m.Images.Count > 0));
                if (lastUser != null)
                {
                    foreach (var g in groups)
                        foreach (var m in g)
                            if (m != lastUser[0] && m.Images != null && m.Images.Count > 0)
                            {
                                res.DroppedImages += m.Images.Count;
                                m.Images = null;
                                m.ImageTokens = 0;
                            }
                }
            }

            if (Estimate(system, groups, kind, factor) > budget)
            {
                var target = groups.SelectMany(g => g).LastOrDefault(m => m.Role == "user");
                if (target == null) target = groups.SelectMany(g => g).LastOrDefault();
                if (target != null && !string.IsNullOrEmpty(target.Content))
                {
                    int used = Estimate(system, groups, kind, factor);
                    int over = used - budget;
                    int contentTokens = (int)Math.Round(MessageTokens(target, kind) * factor);
                    int keepTokens = Math.Max(64, contentTokens - over);
                    int chars = CharsForTokens(target.Content, keepTokens, kind, factor);
                    if (chars < target.Content.Length)
                    {
                        target.Content = target.Content.Substring(0, Math.Max(0, chars)) + "\n… (gekürzt — Kontext voll)";
                        res.TruncatedText = true;
                    }
                }
            }

            res.Messages = Flatten(system, groups);
            res.Tokens = Estimate(system, groups, kind, factor);
            return res;
        }

        static List<AiMessage> Flatten(List<AiMessage> system, List<List<AiMessage>> groups)
        {
            var list = new List<AiMessage>();
            list.AddRange(system);
            foreach (var g in groups) list.AddRange(g);
            return list;
        }

        static int CharsForTokens(string text, int tokenBudget, TokenKind kind, double factor)
        {
            if (string.IsNullOrEmpty(text) || tokenBudget <= 0) return 0;
            int tokens = (int)Math.Round(TextTokens(text, kind) * factor);
            if (tokens <= 0) return text.Length;
            double perChar = (double)tokens / text.Length;
            int marker = (int)Math.Round(TextTokens("\n… (gekürzt — Kontext voll)", kind) * factor);
            int allowed = Math.Max(1, tokenBudget - marker);
            int chars = (int)(allowed / Math.Max(0.1, perChar));
            return Math.Min(text.Length, chars);
        }
    }
}
