using System.Buffers;

namespace AscierWeb.Core.Effects;

// efekt halftone - symuluje druk rastrowy za pomocą wzorów ascii
// inspirowane techniką druku offsetowego gdzie różne rozmiary punktów
// tworzą wrażenie odcieni szarości
// używa wzoru bayera 4x4 do ordered dithering (czyste operacje bitowe)
public sealed class HalftoneEffect : IEffect
{
    public string Name => "halftone";
    public string Description => "halftone - ordered dithering z matrycą bayera 4x4 na bitach";

    // matryca bayera 4x4 - progi ditheringu
    // wartości w zakresie 0-255 (przeskalowane z 0-15 przez << 4)
    private static readonly byte[] BayerMatrix =
    {
          0, 128,  32, 160,
        192,  64, 224,  96,
         48, 176,  16, 144,
        240, 112, 208,  80
    };

    // znaki od najgęstszego do najrzadszego dla halftone
    private static readonly char[] HalftoneChars = { '@', 'O', 'o', '.', ' ' };

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

        int outputLen = cols * rows + rows;
        char[] buffer = ArrayPool<char>.Shared.Rent(outputLen);
        byte[]? colorRgb = settings.ColorMode ? ArrayPool<byte>.Shared.Rent(cols * rows * 3) : null;

        try
        {
            int pos = 0;
            int colorPos = 0;
            ReadOnlySpan<byte> src = rgb24;
            int charCount = HalftoneChars.Length;

            for (int y = 0; y < rows; y++)
            {
                int srcY = y * step;
                for (int x = 0; x < cols; x++)
                {
                    int srcX = x * step;
                    int srcIdx = (srcY * width + srcX) * 3;

                    byte r = src[srcIdx];
                    byte g = src[srcIdx + 1];
                    byte b = src[srcIdx + 2];

                    int lum = BitOps.Luminance(r, g, b);
                    if (settings.Invert) lum = 255 - lum;

                    // indeks w matrycy bayera: pozycja mod 4 za pomocą bitowego and
                    int bayerIdx = (y & 3) * 4 + (x & 3);
                    int bayerVal = BayerMatrix[bayerIdx];

                    // modulacja luminancji przez matrycę bayera
                    // dodajemy offset bayera i normalizujemy
                    int dithered = lum + ((bayerVal - 128) >> 2);
                    byte clampedLum = BitOps.ClampByte(dithered);

                    buffer[pos++] = AsciiMapper.Map(clampedLum, HalftoneChars);

                    if (colorRgb != null)
                    {
                        colorRgb[colorPos++] = r;
                        colorRgb[colorPos++] = g;
                        colorRgb[colorPos++] = b;
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
