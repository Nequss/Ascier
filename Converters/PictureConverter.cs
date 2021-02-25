using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Ascier.Converters.Base;
using ImageMagick;
using SFML.Graphics;
using SFML.System;
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

            int index;

            foreach (var pixel in image.GetPixels())
            {
                if (pixel.ToColor().A == 0)
                    index = 10;
                else
                    index = pixel.ToColor().R / 25;

                if (pixel.X == image.Width - 1)
                    ascii += $"{(char)chars[index]}\n";
                else
                    ascii += $"{(char)chars[index]}";
            }

            return ascii;
        }

        public void ConvertToPictureGreyscale(string ascii)
        {
            Console.WriteLine($"Converting to picture");

            var text = new Text(ascii, new Font("cour.ttf"), 15);

            text.FillColor = Color.Black;
            text.Position = new Vector2f(0, 0);

            var bounds = text.GetGlobalBounds();
            var window = new RenderWindow(new VideoMode((uint)bounds.Width, (uint)bounds.Height), "ASCII");

            window.SetVisible(false);
            window.Clear(Color.White);
            window.Draw(text);

            Texture texture = new Texture((uint)bounds.Width, (uint)bounds.Height);
            texture.Update(window);
            texture.CopyToImage().SaveToFile(image.FileName + ".png");

            window.Close();

            Console.WriteLine($"finished to picture");
        }
    }
}
