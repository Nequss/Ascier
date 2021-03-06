using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using ImageMagick;
using SFML.Graphics;
using SFML.System;
using SFML.Window;

namespace Ascier.Converters
{
    public class PictureConverter
    {
        private byte[] chars = { 35, 64, 37, 61, 43, 42, 58, 45, 126, 46, 32 };
        private Text text = new Text(" ", new Font("font.ttf"));
        private Vector2f position = new Vector2f();

        public void DrawPreview(RenderWindow window, bool mode, string path, uint fontSize)
        {
            window.Clear(Color.Black);

            var image = new Image(path);

            for (uint y = fontSize / 2; y < image.Size.Y; y += fontSize)
            {
                for (uint x = fontSize / 2; x < image.Size.X; x += fontSize)
                {
                    var tmpPixel = image.GetPixel((uint) x,(uint) y);
                    var greyPixel = GetGreyscale(tmpPixel);

                    if(mode) //color mode
                    {
                        position.X = x;
                        position.Y = y;
                        text.Position = position;
                        text.DisplayedString = ((char)chars[greyPixel.R / 25]).ToString();
                        text.CharacterSize = fontSize;
                        text.FillColor = tmpPixel;
                    }
                    else //greyscale mode
                    {
                        position.X = x;
                        position.Y = y;
                        text.Position = position;
                        text.DisplayedString = ((char)chars[greyPixel.R / 25]).ToString();
                        text.CharacterSize = fontSize;
                        text.FillColor = greyPixel;
                    }

                    window.Draw(text);
                }
            }
        }

        private Color GetGreyscale(Color pixelColor)
        {
            int red = (pixelColor.R + pixelColor.G + pixelColor.B) / 3;
            int green = (pixelColor.R + pixelColor.G + pixelColor.B) / 3;
            int blue = (pixelColor.R + pixelColor.G + pixelColor.B) / 3;

            return new Color((byte)red, (byte)green, (byte)blue);
        }
    }
}
