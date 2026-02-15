using System.Buffers;

namespace AscierWeb.Core.Effects;

// efekt matrycy - zielone kaskadowe znaki na czarnym tle
// inspirowane filmem "matrix" - losowe znaki ascii spadają jak deszcz
// hash pozycji zapewnia deterministyczny wzór dla każdej klatki
public sealed class MatrixEffect : IEffect
{
    public string Name => "matrix";
    public string Description => "efekt matrycy - zielone kaskadowe znaki jak w filmie matrix";

    // znaki używane w efekcie matrycy (katakana + ascii + symbole)
    private static readonly char[] MatrixChars =
        "ｱｲｳｴｵｶｷｸｹｺｻｼｽｾｿﾀﾁﾂﾃﾄﾅﾆﾇﾈﾉﾊﾋﾌﾍﾎﾏﾐﾑﾒﾓﾔﾕﾖﾗﾘﾙﾚﾛﾜﾝ0123456789ABCDEF".ToCharArray();

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
        byte[] colorRgb = ArrayPool<byte>.Shared.Rent(cols * rows * 3);

        try
        {
            int pos = 0;
            int colorPos = 0;
            ReadOnlySpan<byte> src = rgb24;
            int seed = settings.Seed;
            int charCount = MatrixChars.Length;

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

                    // hash pozycji generuje pseudolosowy indeks znaku
                    uint hash = BitOps.PositionHash(x, y, seed);

                    // prawdopodobieństwo wyświetlenia znaku zależy od luminancji
                    // ciemne piksele = mniej znaków, jasne = więcej
                    int charIdx = (int)(hash % (uint)charCount);
                    int brightness = settings.Invert ? 255 - lum : lum;

                    // próg widoczności - hash moduluje czy znak jest widoczny
                    bool visible = (int)(hash & 0xFF) < brightness;

                    if (visible)
                    {
                        buffer[pos++] = MatrixChars[charIdx];

                        // kolor zielony z wariacją jasności zależną od luminancji
                        // r=0, g=luminancja, b=luminancja/4 (lekki cyjan)
                        byte greenVal = BitOps.ClampByte(brightness + (int)((hash >> 8) & 0x1F));
                        colorRgb[colorPos++] = (byte)(greenVal >> 3); // minimalny czerwony
                        colorRgb[colorPos++] = greenVal;
                        colorRgb[colorPos++] = (byte)(greenVal >> 2); // trochę niebieskiego
                    }
                    else
                    {
                        buffer[pos++] = ' ';
                        colorRgb[colorPos++] = 0;
                        colorRgb[colorPos++] = (byte)(brightness >> 4); // delikatna zielona poświata
                        colorRgb[colorPos++] = 0;
                    }
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
