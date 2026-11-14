namespace IAlarm.Views;

/// <summary>
/// The retro watch colours. The XAML pages take theirs from Resources/Styles/Colors.xaml (same values);
/// this copy is for the native ringing screen. Keep the two in sync.
/// </summary>
public sealed record Palette(Color Background, Color Panel, Color Lit, Color Ghost, Color Glow, Color Text, Color Muted)
{
    /// <summary>Black background, bright LED-green digits.</summary>
    public static readonly Palette Dark = new(
        Color.FromArgb("#000000"), Color.FromArgb("#06120A"), Color.FromArgb("#3DFF5A"), Color.FromArgb("#0E2613"),
        Color.FromArgb("#B33DFF5A"), Color.FromArgb("#36E352"), Color.FromArgb("#1F9A3A"));

    /// <summary>Pale green-grey LCD with dark green digits.</summary>
    public static readonly Palette Light = new(
        Color.FromArgb("#C9D6BA"), Color.FromArgb("#BCCAAC"), Color.FromArgb("#1C361E"), Color.FromArgb("#B3C2A3"),
        Color.FromArgb("#661C361E"), Color.FromArgb("#1C361E"), Color.FromArgb("#4F6A4C"));
}
