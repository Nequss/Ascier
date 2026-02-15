using System.Runtime.CompilerServices;

namespace AscierWeb.Core;

// operacje bitowe - fundament wszystkich obliczeń
// unikamy dzielenia i floatów, wszystko na intach i shiftach
public static class BitOps
{
    // konwersja rgb na luminancję metodą ważoną
    // przybliżenie: 0.299*r + 0.587*g + 0.114*b
    // 77/256 ≈ 0.301, 150/256 ≈ 0.586, 29/256 ≈ 0.113
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte Luminance(byte r, byte g, byte b)
        => (byte)((r * 77 + g * 150 + b * 29) >> 8);

    // luminancja z offsetu w tablicy rgb24
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte LuminanceAt(ReadOnlySpan<byte> rgb24, int offset)
        => (byte)((rgb24[offset] * 77 + rgb24[offset + 1] * 150 + rgb24[offset + 2] * 29) >> 8);

    // mapowanie wartości 0-255 na indeks 0..(n-1) bez dzielenia
    // (val * n) >> 8 daje prawidłowy zakres
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MapIndex(int value, int buckets)
        => (value * buckets) >> 8;

    // branchless clamp do zakresu 0-255
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ClampByte(int v)
    {
        v &= ~(v >> 31);       // max(v, 0) - zeruje ujemne
        v -= 255;
        v &= v >> 31;          // min(v+255, 255) - obcina nadmiar
        v += 255;
        return (byte)v;
    }

    // szybka wartość bezwzlędna bez rozgałęzień
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Abs(int v)
    {
        int mask = v >> 31;
        return (v ^ mask) - mask;
    }

    // hash pozycji dla efektów pseudolosowych (deterministyczny)
    // używa mnożenia fibonacciego i xor
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint PositionHash(int x, int y, int seed)
    {
        uint h = (uint)(x * 73856093 ^ y * 19349669 ^ seed * 83492791);
        h *= 2654435761u; // mnożnik fibonacciego
        h ^= h >> 16;
        return h;
    }

    // próg binarny - zwraca 1 jeśli val > threshold, 0 w przeciwnym razie
    // bez rozgałęzień, czyste operacje bitowe
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ThresholdBit(int val, int threshold)
        => (int)((uint)(threshold - val) >> 31);
}
