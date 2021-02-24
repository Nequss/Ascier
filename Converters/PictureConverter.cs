using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ascier.Converters.Base;
using ImageMagick;
using SFML.Graphics;
using SFML.Window;

namespace Ascier.Converters
{
    class PictureConverter : Converter
    {
        public MagickImage image;
        public PictureConverter(string path)
        {
            image = new MagickImage(path);
        }
        public PictureConverter()
        {

        }

        public string ConvertToAscii()
        {
            string ascii = string.Empty;

            foreach (var pixel in image.GetPixels())
            {
                if (pixel.ToColor().A == 0)
                {
                    if (pixel.X == image.Width - 1)
                    {
                        ascii += $"{(char)chars[10]}\n";
                    }
                    else
                    {
                        ascii += $"{(char)chars[10]}";
                    }
                }
                else
                {
                    if (pixel.X == image.Width - 1)
                    {
                        ascii += $"{(char)chars[(pixel.ToColor().R + pixel.ToColor().G + pixel.ToColor().B) / 3 * 10 / 255]}\n";
                    }
                    else
                    {
                        ascii += (char)chars[(pixel.ToColor().R + pixel.ToColor().G + pixel.ToColor().B) / 3 * 10 / 255];
                    }
                }
            }

            return ascii;
        }

        public void ConvertToPicture(string ascii)
        {
            var window = new RenderWindow(new VideoMode((uint)image.Width, (uint)image.Height), "ASCII");
            var text = new Text(ascii, new Font("cour.ttf"), 1);

            window.Draw(text);

            Texture texture = new Texture((uint)image.Width, (uint)image.Height);
            texture.Update(window);
            texture.CopyToImage().SaveToFile(image.FileName + ".png");

            window.Close();
        }
    }
}
