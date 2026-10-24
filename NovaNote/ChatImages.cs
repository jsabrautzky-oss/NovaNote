using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace NovaNote
{
    public class ChatImage
    {
        public string FileName;
        public string DataUri;
        public BitmapSource Thumbnail;
        public long Bytes;
        public string Note;
    }

    public static class ChatImages
    {
        public const int MaxEdge = 2048;
        public const int MaxImages = 6;
        public const long MaxBytes = 6L * 1024 * 1024;
        public const int TokenBlockEdge = 512;
        public const int TokensPerBlock = 64;

        static readonly HashSet<string> Supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp"
        };

        static readonly Dictionary<string, string> Mimes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ".png", "image/png" },
            { ".jpg", "image/jpeg" },
            { ".jpeg", "image/jpeg" },
            { ".webp", "image/webp" },
            { ".gif", "image/gif" },
            { ".bmp", "image/bmp" }
        };

        public static bool IsSupportedImage(string path) =>
            !string.IsNullOrEmpty(path) && Supported.Contains(Path.GetExtension(path));

        public static List<string> ImageFilesFromDrop(object data)
        {
            var files = data as string[];
            if (files == null) return new List<string>();
            var list = new List<string>();
            foreach (var f in files)
                if (IsSupportedImage(f) && File.Exists(f)) list.Add(f);
            return list;
        }

        public static ChatImage FromFile(string path)
        {
            try
            {
                byte[] raw = File.ReadAllBytes(path);
                BitmapSource decoded = TryDecode(raw);
                string mime = Mimes.TryGetValue(Path.GetExtension(path), out string m) ? m : null;

                if (decoded == null)
                {
                    if (raw.LongLength > MaxBytes) return null;
                    if (mime == null) return null;
                    return new ChatImage
                    {
                        FileName = Path.GetFileName(path),
                        DataUri = DataUri(mime, raw),
                        Thumbnail = null,
                        Bytes = raw.LongLength
                    };
                }

                var encoded = Encode(decoded, raw, mime, out string note);
                return new ChatImage
                {
                    FileName = Path.GetFileName(path),
                    DataUri = encoded.Item1,
                    Thumbnail = encoded.Item2,
                    Bytes = encoded.Item3.LongLength,
                    Note = note
                };
            }
            catch
            {
                return null;
            }
        }

        public static ChatImage FromClipboard()
        {
            try
            {
                if (Clipboard.ContainsImage())
                {
                    var src = Clipboard.GetImage();
                    if (src == null) return null;
                    var encoded = Encode(src, null, null, out string note);
                    return new ChatImage
                    {
                        FileName = "Zwischenablage",
                        DataUri = encoded.Item1,
                        Thumbnail = encoded.Item2,
                        Bytes = encoded.Item3.LongLength,
                        Note = note
                    };
                }
                if (Clipboard.ContainsFileDropList())
                {
                    var items = Clipboard.GetFileDropList();
                    foreach (var f in items)
                        if (IsSupportedImage(f) && File.Exists(f)) return FromFile(f);
                }
            }
            catch
            {
            }
            return null;
        }

        static BitmapSource TryDecode(byte[] raw)
        {
            try
            {
                using (var ms = new MemoryStream(raw))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch
            {
                return null;
            }
        }

        static Tuple<string, BitmapSource, byte[]> Encode(BitmapSource src, byte[] raw, string mime, out string note)
        {
            note = null;
            int w = src.PixelWidth, h = src.PixelHeight;
            int edge = Math.Max(w, h);

            if (edge > MaxEdge)
            {
                double scale = (double)MaxEdge / edge;
                var scaled = new TransformedBitmap(src, new System.Windows.Media.ScaleTransform(scale, scale));
                scaled.Freeze();
                note = $"auf {MaxEdge} px skaliert (war {edge} px)";
                var bytes = EncodeJpeg(scaled);
                return Tuple.Create(DataUri("image/jpeg", bytes), (BitmapSource)scaled, bytes);
            }

            if (raw != null && raw.LongLength <= MaxBytes && mime != null)
                return Tuple.Create(DataUri(mime, raw), src, raw);

            var re = EncodeJpeg(src);
            if (raw != null && raw.LongLength > MaxBytes) note = "als JPEG neu kodiert";
            return Tuple.Create(DataUri("image/jpeg", re), src, re);
        }

        static byte[] EncodeJpeg(BitmapSource src)
        {
            var enc = new JpegBitmapEncoder { QualityLevel = 85 };
            enc.Frames.Add(BitmapFrame.Create(src));
            using (var ms = new MemoryStream())
            {
                enc.Save(ms);
                return ms.ToArray();
            }
        }

        static string DataUri(string mime, byte[] bytes) =>
            "data:" + mime + ";base64," + Convert.ToBase64String(bytes);

        public static int EstimateTokens(ChatImage image)
        {
            if (image == null) return 0;
            int w = image.Thumbnail?.PixelWidth ?? 0;
            int h = image.Thumbnail?.PixelHeight ?? 0;
            if (w <= 0 || h <= 0)
                return Math.Clamp((int)(image.Bytes / 400), 48, 1200);
            int blocks = Math.Max(1, (int)Math.Ceiling(w / (double)TokenBlockEdge)) *
                         Math.Max(1, (int)Math.Ceiling(h / (double)TokenBlockEdge));
            return Math.Clamp(blocks * TokensPerBlock, TokensPerBlock, 2048);
        }
    }
}
