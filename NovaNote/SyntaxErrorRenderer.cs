using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace NovaNote
{
    public class SyntaxErrorRenderer : IBackgroundRenderer
    {
        readonly List<SyntaxError> _errors = new List<SyntaxError>();
        readonly Func<Brush> _brushResolver;
        Pen _pen;
        Brush _cachedBrush;

        public KnownLayer Layer => KnownLayer.Selection;

        public SyntaxErrorRenderer(Func<Brush> brushResolver)
        {
            _brushResolver = brushResolver;
        }

        public void SetErrors(IEnumerable<SyntaxError> errors)
        {
            _errors.Clear();
            if (errors != null) _errors.AddRange(errors);
        }

        public void Clear() => _errors.Clear();

        public int ErrorCount => _errors.Count;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (_errors.Count == 0) return;
            var document = textView.Document;
            if (document == null) return;
            int length = document.TextLength;
            if (length == 0) return;

            var brush = _brushResolver();
            if (brush == null) brush = Brushes.Red;
            if (!ReferenceEquals(brush, _cachedBrush) || _pen == null)
            {
                _cachedBrush = brush;
                _pen = new Pen(brush, 1.5);
                _pen.Freeze();
            }

            foreach (var err in _errors)
            {
                int start = Math.Clamp(err.Offset, 0, length);
                int end = Math.Clamp(err.Offset + err.Length, start, length);
                if (end <= start) continue;
                var segment = new TextSegment { StartOffset = start, Length = end - start };
                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                {
                    if (rect.Width <= 0 || rect.Height <= 0) continue;
                    drawingContext.DrawGeometry(null, _pen, Squiggle(rect));
                }
            }
        }

        static Geometry Squiggle(Rect rect)
        {
            double y = rect.Bottom - 1.5;
            double amp = 2.0;
            double step = 3.0;
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(rect.X, y), false, false);
                double x = rect.X;
                bool up = false;
                while (x < rect.Right)
                {
                    double nx = Math.Min(x + step, rect.Right);
                    ctx.LineTo(new Point(nx, up ? y - amp : y + amp), true, true);
                    up = !up;
                    x = nx;
                }
            }
            geo.Freeze();
            return geo;
        }
    }
}
