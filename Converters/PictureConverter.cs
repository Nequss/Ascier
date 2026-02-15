using System;
using SFML.Graphics;
using SFML.System;

namespace Ascier.Converters
{
    public class PictureConverter
    {
        // ASCII density ramp: dark ('#') to light (' ')
        private static readonly byte[] chars = { 35, 64, 37, 61, 43, 42, 58, 45, 126, 46, 32 };

        // Pre-built string lookup: eliminates per-pixel ToString() allocation
        private static readonly string[] charStrings;

        // 256-byte LUT: maps grey value -> char index, eliminates /25 division in hot loop
        private static readonly byte[] greyToCharIndex;

        private readonly Text text;
        private Vector2f position = new Vector2f();

        static PictureConverter()
        {
            charStrings = new string[chars.Length];
            for (int i = 0; i < chars.Length; i++)
                charStrings[i] = ((char)chars[i]).ToString();

            greyToCharIndex = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                int idx = i / 25;
                greyToCharIndex[i] = (byte)(idx < chars.Length ? idx : chars.Length - 1);
            }
        }

        public PictureConverter()
        {
            text = new Text(" ", new Font("font.ttf"));
        }

        public void DrawPreview(RenderWindow window, bool mode, string path, uint fontSize, Color background)
        {
            window.Clear(background);

            using (var image = new Image(path))
            {
                uint imgH = image.Size.Y;
                uint imgW = image.Size.X;

                for (uint y = 0; y < imgH; y += fontSize)
                {
                    for (uint x = 0; x < imgW; x += fontSize)
                    {
                        Color pixel = image.GetPixel(x, y);

                        // Fast greyscale: (R + 2G + B) >> 2  — perceptual 1:2:1 weighting via bit shift
                        byte grey = (byte)((pixel.R + (pixel.G << 1) + pixel.B) >> 2);

                        position.X = x;
                        position.Y = y;
                        text.Position = position;
                        text.DisplayedString = charStrings[greyToCharIndex[grey]];
                        text.CharacterSize = fontSize;
                        text.FillColor = mode ? pixel : new Color(grey, grey, grey);

                        window.Draw(text);
                    }
                }
            }
        }
    }
}
