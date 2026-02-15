namespace AscierWeb.Core;

// ustawienia konwersji ascii - kontrolują jak przetwarzamy piksele
public sealed class ConversionSettings
{
    // nazwa efektu (classic, color, edge, matrix, dither, braille, block, invert, threshold)
    public string Effect { get; set; } = "classic";

    // rozmiar kroku w pikselach - co ile pikseli próbkujemy
    // mniejszy krok = wyższa rozdzielczość ascii = więcej znaków
    public int Step { get; set; } = 8;

    // tryb kolorowy - zachowuje oryginalne kolory pikseli
    public bool ColorMode { get; set; }

    // próg binarny (0-255) dla efektów threshold i braille
    public int Threshold { get; set; } = 128;

    // odwrócenie luminancji
    public bool Invert { get; set; }

    // ziarno losowości (dla efektu matrix, deterministic per frame)
    public int Seed { get; set; }

    // maksymalna szerokość wyjścia w kolumnach (0 = bez limitu)
    public int MaxColumns { get; set; } = 300;
}
