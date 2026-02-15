namespace AscierWeb.Core;

// wynik konwersji jednej klatki/obrazu na ascii
public sealed record AsciiFrame
{
    // tekst ascii z podziałem na linie (zawiera \n)
    public string Text { get; init; } = "";

    // wymiary w znakach
    public int Columns { get; init; }
    public int Rows { get; init; }

    // opcjonalne dane kolorów rgb (3 bajty na znak: r,g,b)
    // null oznacza tryb monochromatyczny
    public byte[]? ColorRgb { get; init; }

    // numer klatki (dla wideo, 0 dla obrazów)
    public int FrameNumber { get; init; }

    // łączna liczba klatek (1 dla obrazów)
    public int TotalFrames { get; init; } = 1;
}
