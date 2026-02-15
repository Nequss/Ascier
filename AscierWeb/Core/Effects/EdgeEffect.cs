using System.Buffers;

namespace AscierWeb.Core.Effects;

// efekt detekcji krawędzi - operator sobela na operacjach całkowitoliczbowych
// kernele gx i gy zastosowane do sąsiedztwa 3x3 każdego próbkowanego piksela
// wynik: kontury obiektów jako znaki ascii
public sealed class EdgeEffect : IEffect
{
    public string Name => "edge";
    public string Description => "detekcja krawędzi - operator sobela na bitach, pokazuje kontury";

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

                    // obliczanie gradientu sobela z sąsiedztwa 3x3
                    // kernel gx: [-1,0,1 / -2,0,2 / -1,0,1]
                    // kernel gy: [-1,-2,-1 / 0,0,0 / 1,2,1]
                    int gx = 0, gy = 0;

                    for (int ky = -1; ky <= 1; ky++)
                    {
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            // clamp do granic obrazu bez rozgałęzień
                            int px = srcX + kx;
                            int py = srcY + ky;

                            // branchless clamp: max(0, min(val, max))
                            px &= ~(px >> 31); // max(px, 0)
                            py &= ~(py >> 31); // max(py, 0)
                            int dxw = px - (width - 1);
                            px -= dxw & ~(dxw >> 31); // min(px, width-1)
                            int dyh = py - (height - 1);
                            py -= dyh & ~(dyh >> 31); // min(py, height-1)

                            int idx = (py * width + px) * 3;
                            byte lum = BitOps.Luminance(src[idx], src[idx + 1], src[idx + 2]);

                            // wagi kernela sobela - bez rozgałęzień
                            // gx: kolumna -1 -> -1/-2/-1, kolumna 0 -> 0, kolumna 1 -> 1/2/1
                            // wiersz środkowy ma wagę 2, reszta 1
                            int wx = kx; // -1, 0, 1
                            int wy = ky; // -1, 0, 1
                            int centerMul = (ky == 0 ? 2 : 1); // waga 2 dla środka
                            int centerMulY = (kx == 0 ? 2 : 1);

                            gx += lum * wx * centerMul;
                            gy += lum * wy * centerMulY;
                        }
                    }

                    // magnityda gradientu: |gx| + |gy| (przybliżenie manhattan)
                    // shift >> 2 normalizuje do zakresu 0-255
                    int mag = (BitOps.Abs(gx) + BitOps.Abs(gy)) >> 2;
                    byte edge = BitOps.ClampByte(mag);

                    buffer[pos++] = settings.Invert
                        ? AsciiMapper.Map(edge, charset)
                        : AsciiMapper.MapInverted(edge, charset);

                    if (colorRgb != null)
                    {
                        // krawędzie w oryginalnym kolorze piksela
                        int cIdx = (srcY * width + srcX) * 3;
                        colorRgb[colorPos++] = src[cIdx];
                        colorRgb[colorPos++] = src[cIdx + 1];
                        colorRgb[colorPos++] = src[cIdx + 2];
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
