using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NovaNote
{
    public enum NovaNoteEncoding
    {
        Utf8,
        Utf8Bom,
        Utf16Le,
        Utf16Be,
        Ansi
    }

    public enum LineEnding
    {
        CrLf,
        Lf,
        Cr
    }

    public static class Codec
    {
        static Codec()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public static string Name(NovaNoteEncoding enc) => enc switch
        {
            NovaNoteEncoding.Utf8 => "UTF-8",
            NovaNoteEncoding.Utf8Bom => "UTF-8 (BOM)",
            NovaNoteEncoding.Utf16Le => "UTF-16 LE",
            NovaNoteEncoding.Utf16Be => "UTF-16 BE",
            NovaNoteEncoding.Ansi => "ANSI",
            _ => "UTF-8"
        };

        public static string EolName(LineEnding eol) => eol switch
        {
            LineEnding.CrLf => "Windows (CRLF)",
            LineEnding.Lf => "Unix (LF)",
            LineEnding.Cr => "Macintosh (CR)",
            _ => "Windows (CRLF)"
        };

        public static string EolString(LineEnding eol) => eol switch
        {
            LineEnding.CrLf => "\r\n",
            LineEnding.Lf => "\n",
            LineEnding.Cr => "\r",
            _ => "\r\n"
        };

        public static NovaNoteEncoding Detect(byte[] data, out int skip)
        {
            skip = 0;
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            {
                skip = 3;
                return NovaNoteEncoding.Utf8Bom;
            }
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
            {
                skip = 2;
                return NovaNoteEncoding.Utf16Le;
            }
            if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
            {
                skip = 2;
                return NovaNoteEncoding.Utf16Be;
            }
            int limit = Math.Min(data.Length, 512);
            int even = 0, evenZero = 0, odd = 0, oddZero = 0;
            for (int i = 0; i < limit; i++)
            {
                if ((i & 1) == 0) { even++; if (data[i] == 0) evenZero++; }
                else { odd++; if (data[i] == 0) oddZero++; }
            }
            if (odd > 0 && oddZero == odd) return NovaNoteEncoding.Utf16Le;
            if (even > 1 && evenZero == even) return NovaNoteEncoding.Utf16Be;
            try
            {
                var strict = new UTF8Encoding(false, true);
                strict.GetString(data, skip, data.Length - skip);
                return NovaNoteEncoding.Utf8;
            }
            catch (DecoderFallbackException)
            {
                return NovaNoteEncoding.Ansi;
            }
        }

        public static string Decode(byte[] data, NovaNoteEncoding enc, int skip)
        {
            var body = new byte[data.Length - skip];
            Array.Copy(data, skip, body, 0, body.Length);
            Encoding e = enc switch
            {
                NovaNoteEncoding.Utf8 => new UTF8Encoding(false),
                NovaNoteEncoding.Utf8Bom => new UTF8Encoding(false),
                NovaNoteEncoding.Utf16Le => Encoding.Unicode,
                NovaNoteEncoding.Utf16Be => Encoding.BigEndianUnicode,
                NovaNoteEncoding.Ansi => GetAnsi(),
                _ => new UTF8Encoding(false)
            };
            return e.GetString(body);
        }

        public static byte[] Encode(string text, NovaNoteEncoding enc)
        {
            Encoding e = enc switch
            {
                NovaNoteEncoding.Utf8 => new UTF8Encoding(false),
                NovaNoteEncoding.Utf8Bom => new UTF8Encoding(true),
                NovaNoteEncoding.Utf16Le => new UnicodeEncoding(false, true),
                NovaNoteEncoding.Utf16Be => new UnicodeEncoding(true, true),
                NovaNoteEncoding.Ansi => GetAnsi(),
                _ => new UTF8Encoding(false)
            };
            byte[] body = e.GetBytes(text);
            byte[] preamble = e.GetPreamble();
            if (preamble.Length == 0) return body;
            var result = new byte[preamble.Length + body.Length];
            Array.Copy(preamble, 0, result, 0, preamble.Length);
            Array.Copy(body, 0, result, preamble.Length, body.Length);
            return result;
        }

        private static Encoding GetAnsi()
        {
            try { return Encoding.GetEncoding(1252); }
            catch { return new UTF8Encoding(false); }
        }

        public static LineEnding DetectEol(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\r')
                    return (i + 1 < text.Length && text[i + 1] == '\n') ? LineEnding.CrLf : LineEnding.Cr;
                if (text[i] == '\n') return LineEnding.Lf;
            }
            return LineEnding.CrLf;
        }

        public static string NormalizeToLf(string text)
        {
            var sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\r')
                {
                    sb.Append('\n');
                    if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        public static string ApplyEol(string text, LineEnding eol)
        {
            string nl = EolString(eol);
            if (nl == "\n") return text;
            return text.Replace("\n", nl);
        }
    }
}
