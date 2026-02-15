using System.Runtime.CompilerServices;

namespace AscierWeb.Core;

// mapowanie luminancji na znaki ascii
// zestawy znaków posortowane od najgęstszego (ciemny) do najrzadszego (jasny)
public static class AsciiMapper
{
    // domyślna paleta - 11 poziomów gęstości
    public static readonly char[] Default = { '@', '%', '#', '*', '+', '=', '-', ':', '~', '.', ' ' };

    // uproszczona paleta - 7 poziomów
    public static readonly char[] Simple = { '#', '=', '+', ':', '-', '.', ' ' };

    // szczegółowa paleta - 16 poziomów
    public static readonly char[] Detailed = { '$', '@', 'B', '%', '8', '&', 'W', 'M', '#', 'o', 'a', 'h', 'k', 'b', 'd', 'p', 'q', 'w', 'm', 'Z', 'O', '0', 'Q', 'L', 'C', 'J', 'U', 'Y', 'X', 'z', 'c', 'v', 'u', 'n', 'x', 'r', 'j', 'f', 't', '/', '\\', '|', '(', ')', '1', '{', '}', '[', ']', '?', '-', '_', '+', '~', '<', '>', 'i', '!', 'l', 'I', ';', ':', ',', '"', '^', '`', '\'', '.', ' ' };

    // blokowe elementy unicode - 5 poziomów
    public static readonly char[] Blocks = { '█', '▓', '▒', '░', ' ' };

    // zwraca znak na podstawie luminancji i zestawu znaków
    // mapowanie za pomocą mnożenia i przesunięcia bitowego zamiast dzielenia
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static char Map(byte luminance, char[] charset)
    {
        int idx = (luminance * charset.Length) >> 8;
        return charset[idx];
    }

    // odwrócone mapowanie - jasne piksele -> gęste znaki
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static char MapInverted(byte luminance, char[] charset)
    {
        int idx = ((255 - luminance) * charset.Length) >> 8;
        return charset[idx];
    }

    // pobiera zestaw znaków po nazwie
    public static char[] GetCharset(string name) => name switch
    {
        "simple" => Simple,
        "detailed" => Detailed,
        "blocks" => Blocks,
        _ => Default
    };
}
