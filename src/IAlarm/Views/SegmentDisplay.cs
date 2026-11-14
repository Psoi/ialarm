namespace IAlarm.Views;

/// <summary>A 7-segment readout. Colours come from the implicit style in Styles.xaml, so it follows the theme.</summary>
public sealed class SegmentDisplay : GraphicsView
{
    readonly SegmentDrawable drawable = new();

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(SegmentDisplay), "",
        propertyChanged: (b, _, v) => ((SegmentDisplay)b).Redraw(d => d.Text = (string?)v ?? ""));

    public static readonly BindableProperty LitColorProperty = BindableProperty.Create(
        nameof(LitColor), typeof(Color), typeof(SegmentDisplay), Palette.Dark.Lit,
        propertyChanged: (b, _, v) => ((SegmentDisplay)b).Redraw(d => d.LitColor = (Color)v));

    public static readonly BindableProperty GhostColorProperty = BindableProperty.Create(
        nameof(GhostColor), typeof(Color), typeof(SegmentDisplay), Palette.Dark.Ghost,
        propertyChanged: (b, _, v) => ((SegmentDisplay)b).Redraw(d => d.GhostColor = (Color)v));

    public static readonly BindableProperty GlowColorProperty = BindableProperty.Create(
        nameof(GlowColor), typeof(Color), typeof(SegmentDisplay), Palette.Dark.Glow,
        propertyChanged: (b, _, v) => ((SegmentDisplay)b).Redraw(d => d.GlowColor = (Color?)v));

    public SegmentDisplay()
    {
        Drawable = drawable;
        BackgroundColor = Colors.Transparent;
        InputTransparent = true;
    }

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public Color LitColor { get => (Color)GetValue(LitColorProperty); set => SetValue(LitColorProperty, value); }
    public Color GhostColor { get => (Color)GetValue(GhostColorProperty); set => SetValue(GhostColorProperty, value); }
    public Color? GlowColor { get => (Color?)GetValue(GlowColorProperty); set => SetValue(GlowColorProperty, value); }

    void Redraw(Action<SegmentDrawable> change)
    {
        change(drawable);
        Invalidate();
    }
}
