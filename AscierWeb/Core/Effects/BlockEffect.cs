using System.Buffers;

namespace AscierWeb.Core.Effects;

// efekt blokowy - elementy unicode ░▒▓█ zamiast znaków ascii
// 5 poziomów gęstości daje gładsze cieniowanie
// prosty i szybki - jedna operacja mapowania na piksel
public sealed class BlockEffect : IEffect
{
    public string Name => "block";
    public string Description => "bloki unicode ░▒▓█ - gładkie cieniowanie pięcioma poziomami";

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

        var charset = AsciiMapper.Blocks;
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

                    byte lum = BitOps.Luminance(r, g, b);

                    buffer[pos++] = settings.Invert
                        ? AsciiMapper.MapInverted(lum, charset)
                        : AsciiMapper.Map(lum, charset);

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
