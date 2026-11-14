using IAlarm.Core;

namespace IAlarm.Views;

/// <summary>
/// Draws text as a slanted 7-segment display: unlit "ghost" segments first, like a real LCD, then the lit ones
/// with a soft glow. Digits after '.' are drawn smaller (stopwatch hundredths). ':' is a lit colon, ';' an unlit one.
/// Used by <see cref="SegmentDisplay"/> in pages and directly by the native ringing screen.
/// </summary>
public sealed class SegmentDrawable : IDrawable
{
    // Sizes in units of the full digit height.
    const float DigitWidth = 0.54f;
    const float DigitSpacing = 0.12f;
    const float ColonWidth = 0.22f;
    const float DotWidth = 0.2f;
    const float Thickness = 0.105f;
    const float SegmentGap = 0.014f;
    const float Slant = 0.08f;
    const float SmallScale = 0.6f;
    const float Margin = 0.06f;

    public string Text { get; set; } = "";
    public Color LitColor { get; set; } = Palette.Dark.Lit;
    public Color GhostColor { get; set; } = Palette.Dark.Ghost;
    public Color? GlowColor { get; set; } = Palette.Dark.Glow;

    public void Draw(ICanvas canvas, RectF rect)
    {
        var text = Text;
        if (text.Length == 0 || rect.Width <= 0 || rect.Height <= 0)
            return;

        var cells = Layout(text, out var width);
        var unit = Math.Min(rect.Height / (1 + 2 * Margin), rect.Width / (width + Slant + 2 * Margin));
        var left = rect.Center.X - (width + Slant) * unit / 2;
        var bottom = rect.Center.Y + unit / 2;

        canvas.Antialias = true;
        canvas.FillColor = GhostColor;
        foreach (var cell in cells)
            DrawCell(canvas, cell, left, bottom, unit, lit: false);

        canvas.SaveState();
        if (GlowColor is { } glow)
            canvas.SetShadow(SizeF.Zero, unit * 0.14f, glow);
        canvas.FillColor = LitColor;
        foreach (var cell in cells)
            DrawCell(canvas, cell, left, bottom, unit, lit: true);
        canvas.RestoreState();
    }

    readonly record struct Cell(char C, float X, float Scale);

    static List<Cell> Layout(string text, out float width)
    {
        var cells = new List<Cell>(text.Length);
        var x = 0f;
        var scale = 1f;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (i > 0)
                x += DigitSpacing * scale * (SevenSegment.IsDigitCell(c) && SevenSegment.IsDigitCell(text[i - 1]) ? 1 : 0.5f);
            cells.Add(new Cell(c, x, scale));
            x += c switch
            {
                ':' or ';' => ColonWidth,
                '.' => DotWidth * scale,
                _ => DigitWidth * scale,
            };
            if (c == '.')
                scale = SmallScale;
        }
        width = x;
        return cells;
    }

    static void DrawCell(ICanvas canvas, Cell cell, float left, float bottom, float unit, bool lit)
    {
        var x = left + cell.X * unit;
        var h = unit * cell.Scale;
        var t = Thickness * h;
        switch (cell.C)
        {
            case ':':
            case ';':
                if (lit && cell.C == ';')
                    return;
                var cx = x + ColonWidth * unit / 2;
                Dot(canvas, cx, bottom - unit * 0.3f, t, bottom);
                Dot(canvas, cx, bottom - unit * 0.7f, t, bottom);
                return;
            case '.':
                Dot(canvas, x + DotWidth * cell.Scale * unit / 2, bottom - t / 2, t, bottom);
                return;
        }

        var mask = lit ? SevenSegment.Mask(cell.C) : SevenSegment.All;
        var w = DigitWidth * h;
        var gap = SegmentGap * h;
        var half = t / 2;
        float l = x + half, r = x + w - half, top = bottom - h + half, mid = bottom - h / 2, bot = bottom - half;

        if ((mask & SevenSegment.A) != 0) Horizontal(canvas, l, r, top, half, gap, bottom);
        if ((mask & SevenSegment.G) != 0) Horizontal(canvas, l, r, mid, half, gap, bottom);
        if ((mask & SevenSegment.D) != 0) Horizontal(canvas, l, r, bot, half, gap, bottom);
        if ((mask & SevenSegment.F) != 0) Vertical(canvas, l, top, mid, half, gap, bottom);
        if ((mask & SevenSegment.B) != 0) Vertical(canvas, r, top, mid, half, gap, bottom);
        if ((mask & SevenSegment.E) != 0) Vertical(canvas, l, mid, bot, half, gap, bottom);
        if ((mask & SevenSegment.C) != 0) Vertical(canvas, r, mid, bot, half, gap, bottom);
    }

    /// <summary>A hexagonal bar along y, from the centre of the left upright to the right one.</summary>
    static void Horizontal(ICanvas canvas, float x0, float x1, float y, float half, float gap, float bottom) =>
        Fill(canvas, bottom,
            (x0 + gap, y), (x0 + gap + half, y - half), (x1 - gap - half, y - half),
            (x1 - gap, y), (x1 - gap - half, y + half), (x0 + gap + half, y + half));

    static void Vertical(ICanvas canvas, float x, float y0, float y1, float half, float gap, float bottom) =>
        Fill(canvas, bottom,
            (x, y0 + gap), (x + half, y0 + gap + half), (x + half, y1 - gap - half),
            (x, y1 - gap), (x - half, y1 - gap - half), (x - half, y0 + gap + half));

    static void Dot(ICanvas canvas, float cx, float cy, float size, float bottom)
    {
        var h = size * 0.55f;
        Fill(canvas, bottom, (cx - h, cy - h), (cx + h, cy - h), (cx + h, cy + h), (cx - h, cy + h));
    }

    /// <summary>Fills a polygon, slanting it to the right the higher it is, like watch digits.</summary>
    static void Fill(ICanvas canvas, float bottom, params (float X, float Y)[] points)
    {
        var path = new PathF();
        for (var i = 0; i < points.Length; i++)
        {
            var (px, py) = points[i];
            var sx = px + Slant * (bottom - py);
            if (i == 0)
                path.MoveTo(sx, py);
            else
                path.LineTo(sx, py);
        }
        path.Close();
        canvas.FillPath(path);
    }
}
