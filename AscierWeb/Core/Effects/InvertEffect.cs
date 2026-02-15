using System.Buffers;

namespace AscierWeb.Core.Effects;

// efekt inwersji - odwrócona luminancja
// ciemne obszary stają się jasne i odwrotnie
// przydatne gdy tło terminala jest jasne
public sealed class InvertEffect : IEffect
{
    public string Name => "invert";
    public string Description => "odwrócona luminancja - ciemne staje się jasne i odwrotnie";

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
        int outputLen = cols * rows + rows;
        char[] buffer = ArrayPool<char>.Shared.Rent(outputLen);
        byte[]? colorRgb = settings.ColorMode ? ArrayPool<byte>.Shared.Rent(cols * rows * 3) : null;

        try
        {
            int pos = 0;
            int colorPos = 0;
            ReadOnlySpan<byte> src = rgb24;

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

                    // inwersja luminancji za pomocą xor 0xff (bitowe not na dolnych 8 bitach)
                    byte lum = (byte)(BitOps.Luminance(r, g, b) ^ 0xFF);

                    buffer[pos++] = AsciiMapper.Map(lum, charset);

                    if (colorRgb != null)
                    {
                        // odwrócone kolory: xor 0xff na każdym kanale
                        colorRgb[colorPos++] = (byte)(r ^ 0xFF);
                        colorRgb[colorPos++] = (byte)(g ^ 0xFF);
                        colorRgb[colorPos++] = (byte)(b ^ 0xFF);
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
