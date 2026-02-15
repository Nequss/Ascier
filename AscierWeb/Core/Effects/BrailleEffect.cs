using System.Buffers;
using System.Text;

namespace AscierWeb.Core.Effects;

// efekt braille - znaki unicode braille (⠁⠂⠃...) dla 2x większej gęstości
// każdy znak braille reprezentuje siatkę 2x4 pikseli (8 bitów)
// idealna demonstracja operacji bitowych - każdy piksel = 1 bit w znaku
// siatka braille:
//   col0  col1
//   bit0  bit3   row0
//   bit1  bit4   row1
//   bit2  bit5   row2
//   bit6  bit7   row3
public sealed class BrailleEffect : IEffect
{
    public string Name => "braille";
    public string Description => "braille unicode - 2x4 pikseli zakodowane jako bity w jednym znaku";

    // mapowanie pozycji w siatce 2x4 na bit w znaku braille
    // [row, col] -> numer bitu
    private static readonly int[] BrailleBitMap = {
        0, 3,  // row 0: col0=bit0, col1=bit3
        1, 4,  // row 1: col0=bit1, col1=bit4
        2, 5,  // row 2: col0=bit2, col1=bit5
        6, 7   // row 3: col0=bit6, col1=bit7
    };

    public AsciiFrame Process(byte[] rgb24, int width, int height, ConversionSettings settings)
    {
        int step = settings.Step;

        // braille używa bloków 2x4 pikseli
        // step kontroluje ile pikseli źródłowych przypada na jeden subpiksel braille
        int subStep = Math.Max(1, step / 2);
        int cols = width / (subStep * 2);  // 2 kolumny pikseli na znak
        int rows = height / (subStep * 4); // 4 wiersze pikseli na znak

        if (cols == 0 || rows == 0)
            return new AsciiFrame { Text = "", Columns = 0, Rows = 0 };

        if (settings.MaxColumns > 0 && cols > settings.MaxColumns)
        {
            subStep = width / (settings.MaxColumns * 2);
            if (subStep < 1) subStep = 1;
            cols = width / (subStep * 2);
            rows = height / (subStep * 4);
        }

        int threshold = settings.Threshold;
        bool invert = settings.Invert;
        int charCount = cols * rows;
        char[] buffer = ArrayPool<char>.Shared.Rent(charCount + rows + 1);
        byte[]? colorRgb = settings.ColorMode ? ArrayPool<byte>.Shared.Rent(charCount * 3) : null;

        try
        {
            int pos = 0;
            int colorPos = 0;
            ReadOnlySpan<byte> src = rgb24;

            for (int cy = 0; cy < rows; cy++)
            {
                for (int cx = 0; cx < cols; cx++)
                {
                    int bits = 0;
                    int avgR = 0, avgG = 0, avgB = 0;
                    int samples = 0;

                    // iteracja po siatce 2x4 wewnątrz bloku braille
                    for (int dy = 0; dy < 4; dy++)
                    {
                        for (int dx = 0; dx < 2; dx++)
                        {
                            int px = cx * subStep * 2 + dx * subStep;
                            int py = cy * subStep * 4 + dy * subStep;

                            if (px >= width || py >= height) continue;

                            int srcIdx = (py * width + px) * 3;
                            byte r = src[srcIdx];
                            byte g = src[srcIdx + 1];
                            byte b = src[srcIdx + 2];
                            byte lum = BitOps.Luminance(r, g, b);

                            // bit jest ustawiany gdy piksel przekracza próg
                            // branchless: używa operacji bitowej zamiast if
                            int isAbove = BitOps.ThresholdBit(lum, threshold);
                            if (invert) isAbove ^= 1;

                            // mapowanie pozycji na bit braille
                            int bitIndex = BrailleBitMap[dy * 2 + dx];
                            bits |= isAbove << bitIndex;

                            avgR += r;
                            avgG += g;
                            avgB += b;
                            samples++;
                        }
                    }

                    // znak braille: U+2800 + wzór bitowy
                    buffer[pos++] = (char)(0x2800 + bits);

                    if (colorRgb != null && samples > 0)
                    {
                        // średni kolor bloku za pomocą shift zamiast dzielenia
                        // dla samples=8: >> 3
                        // dla innych wartości: zwykłe dzielenie (rzadki przypadek brzegowy)
                        if (samples == 8)
                        {
                            colorRgb[colorPos++] = (byte)(avgR >> 3);
                            colorRgb[colorPos++] = (byte)(avgG >> 3);
                            colorRgb[colorPos++] = (byte)(avgB >> 3);
                        }
                        else
                        {
                            colorRgb[colorPos++] = (byte)(avgR / samples);
                            colorRgb[colorPos++] = (byte)(avgG / samples);
                            colorRgb[colorPos++] = (byte)(avgB / samples);
                        }
                    }
                }
                buffer[pos++] = '\n';
            }

            string text = new string(buffer, 0, pos);
            byte[]? resultColors = null;

            if (colorRgb != null)
            {
                resultColors = new byte[colorPos];
                Buffer.BlockCopy(colorRgb, 0, resultColors, 0, colorPos);
            }

            return new AsciiFrame
            {
                Text = text,
                Columns = cols,
                Rows = rows,
                ColorRgb = resultColors
            };
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
            if (colorRgb != null)
                ArrayPool<byte>.Shared.Return(colorRgb);
        }
    }
}
