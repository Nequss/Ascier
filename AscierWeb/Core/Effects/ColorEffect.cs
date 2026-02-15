using System.Buffers;

namespace AscierWeb.Core.Effects;

// efekt kolorowy - jak classic ale zawsze zachowuje kolory pikseli
// kolory wysyłane jako rgb do renderowania na canvasie klienta
public sealed class ColorEffect : IEffect
{
    public string Name => "color";
    public string Description => "kolorowe ascii - znaki z oryginalnymi kolorami pikseli";

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
        byte[] colorRgb = ArrayPool<byte>.Shared.Rent(cols * rows * 3);

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

                    // zawsze zapisujemy kolory w tym efekcie
                    colorRgb[colorPos++] = r;
                    colorRgb[colorPos++] = g;
                    colorRgb[colorPos++] = b;
                }
                buffer[pos++] = '\n';
            }

            string text = new string(buffer, 0, pos);
            byte[] resultColors = new byte[colorPos];
            Buffer.BlockCopy(colorRgb, 0, resultColors, 0, colorPos);

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
            ArrayPool<byte>.Shared.Return(colorRgb);
        }
    }
}
