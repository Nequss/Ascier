using System.Buffers;

namespace AscierWeb.Core.Effects;

// efekt ditheringu - floyd-steinberg z arytmetyką całkowitoliczbową
// rozkłada błąd kwantyzacji na sąsiednie piksele
// daje wrażenie większej ilości odcieni szarości niż mamy znaków
// mnożenie + shift >> 4 zamiast dzielenia przez 16
public sealed class DitherEffect : IEffect
{
    public string Name => "dither";
    public string Description => "floyd-steinberg dithering - rozkład błędu kwantyzacji na intach";

    public AsciiFrame Process(byte[] rgb24, int width, int height, ConversionSettings settings)
    {
        int step = settings.Step;
        int cols = width / step;
        int rows = height / step;

        if (cols == 0 || rows == 0)
            return new AsciiFrame { Text = "", Columns = 0, Rows = 0 };

        if (settings.MaxColumns > 0 && cols > settings.MaxColumns)
        {
            step = width / settings.MaxColumns;
            cols = width / step;
            rows = height / step;
        }

        var charset = AsciiMapper.Default;
        int charCount = charset.Length;
        int outputLen = cols * rows + rows;

        // bufor luminancji do propagacji błędu (int zamiast float)
        // dwa wiersze wystarczą - bieżący i następny
        int[] lumBuf = ArrayPool<int>.Shared.Rent(cols * (rows + 1));
        char[] buffer = ArrayPool<char>.Shared.Rent(outputLen);
        byte[]? colorRgb = settings.ColorMode ? ArrayPool<byte>.Shared.Rent(cols * rows * 3) : null;

        try
        {
            ReadOnlySpan<byte> src = rgb24;

            // inicjalizacja bufora luminancji z próbkowanych pikseli
            for (int y = 0; y < rows; y++)
            {
                int srcY = y * step;
                for (int x = 0; x < cols; x++)
                {
                    int srcX = x * step;
                    int srcIdx = (srcY * width + srcX) * 3;
                    int lum = BitOps.Luminance(src[srcIdx], src[srcIdx + 1], src[srcIdx + 2]);
                    if (settings.Invert) lum = 255 - lum;
                    lumBuf[y * cols + x] = lum << 4; // przeskalowanie o 4 bity dla precyzji
                }
            }

            int pos = 0;
            int colorPos = 0;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int idx = y * cols + x;
                    int oldVal = lumBuf[idx];

                    // kwantyzacja - mapowanie na najbliższy poziom znaku
                    int quantized = (oldVal >> 4); // powrót do zakresu 0-255
                    quantized = Math.Clamp(quantized, 0, 255);
                    int charIdx = (quantized * charCount) >> 8;
                    if (charIdx >= charCount) charIdx = charCount - 1;

                    buffer[pos++] = charset[charIdx];

                    // wartość po kwantyzacji (środek przedziału)
                    int newVal = ((charIdx << 8) / charCount) << 4;

                    // błąd kwantyzacji
                    int error = oldVal - newVal;

                    // propagacja błędu floyd-steinberg za pomocą shift >> 4 zamiast /16
                    // prawy: 7/16, dolny-lewy: 3/16, dolny: 5/16, dolny-prawy: 1/16
                    if (x + 1 < cols)
                        lumBuf[idx + 1] += (error * 7) >> 4;
                    if (y + 1 < rows)
                    {
                        if (x > 0)
                            lumBuf[idx + cols - 1] += (error * 3) >> 4;
                        lumBuf[idx + cols] += (error * 5) >> 4;
                        if (x + 1 < cols)
                            lumBuf[idx + cols + 1] += (error * 1) >> 4;
                    }

                    if (colorRgb != null)
                    {
                        int srcY = y * step;
                        int srcX = x * step;
                        int srcIdx = (srcY * width + srcX) * 3;
                        colorRgb[colorPos++] = src[srcIdx];
                        colorRgb[colorPos++] = src[srcIdx + 1];
                        colorRgb[colorPos++] = src[srcIdx + 2];
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
            ArrayPool<int>.Shared.Return(lumBuf);
            ArrayPool<char>.Shared.Return(buffer);
            if (colorRgb != null)
                ArrayPool<byte>.Shared.Return(colorRgb);
        }
    }
}
