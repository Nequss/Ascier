using System.Buffers;

namespace AscierWeb.Core.Effects;

// efekt progu binarnego - czarno-białe ascii
// każdy piksel jest albo '#' albo ' ' na podstawie progu
// porównanie za pomocą operacji bitowej (branchless threshold)
public sealed class ThresholdEffect : IEffect
{
    public string Name => "threshold";
    public string Description => "próg binarny - czyste czarno-białe ascii bez pośrednich odcieni";

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

        int threshold = settings.Threshold;
        int outputLen = cols * rows + rows;
        char[] buffer = ArrayPool<char>.Shared.Rent(outputLen);

        try
        {
            int pos = 0;
            ReadOnlySpan<byte> src = rgb24;

            for (int y = 0; y < rows; y++)
            {
                int srcY = y * step;
                for (int x = 0; x < cols; x++)
                {
                    int srcX = x * step;
                    int srcIdx = (srcY * width + srcX) * 3;

                    byte lum = BitOps.Luminance(src[srcIdx], src[srcIdx + 1], src[srcIdx + 2]);

                    // branchless: bit = 1 jeśli lum > threshold
                    int bit = BitOps.ThresholdBit(lum, threshold);
                    if (settings.Invert) bit ^= 1;

                    // bit=1 -> '#' (0x23), bit=0 -> ' ' (0x20)
                    // '#' = 0x23, ' ' = 0x20, różnica = 3
                    // 0x20 + bit * 3 = branchless wybór znaku
                    buffer[pos++] = (char)(0x20 + bit * 3);
                }
                buffer[pos++] = '\n';
            }

            return new AsciiFrame
            {
                Text = new string(buffer, 0, pos),
                Columns = cols,
                Rows = rows,
                ColorRgb = null
            };
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }
}
