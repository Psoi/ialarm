namespace IAlarm.Core;

/// <summary>
/// Which of the seven segments are lit for a character. Bits: a (top) 1, b (top right) 2, c (bottom right) 4,
/// d (bottom) 8, e (bottom left) 16, f (top left) 32, g (middle) 64.
/// </summary>
public static class SevenSegment
{
    public const int A = 1, B = 2, C = 4, D = 8, E = 16, F = 32, G = 64, All = 127;

    public static int Mask(char c) => c switch
    {
        '0' => A | B | C | D | E | F,
        '1' => B | C,
        '2' => A | B | G | E | D,
        '3' => A | B | G | C | D,
        '4' => F | G | B | C,
        '5' => A | F | G | C | D,
        '6' => A | F | G | E | D | C,
        '7' => A | B | C,
        '8' => All,
        '9' => A | B | C | D | F | G,
        '-' => G,
        _ => 0,
    };

    /// <summary>Characters drawn as a digit cell; ':' ';' and '.' are narrow separators.</summary>
    public static bool IsDigitCell(char c) => c is not (':' or ';' or '.');
}
