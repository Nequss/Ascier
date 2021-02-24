using System;
using ImageMagick;
using Xabe.FFmpeg;
using System.IO;
using System.Threading;
using Ascier.Makers;
using Ascier.Makers.Base;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;
using SFML.Graphics;
using SFML.Window;
using SFML.System;

namespace Ascier.Makers
{
    class ImageMaker : Maker
    {
        public ImageMaker()
        {

        }

        void ToTextFile(List<string> lines, string file)
        {
            using (StreamWriter sw = File.AppendText($"{file}.txt"))
            {
                foreach (string line in lines)
                    sw.WriteLine(line);
            }
        }

        void ToHtmlFile(List<string> lines, string file)
        {
            using (StreamWriter sw = File.AppendText($"{file}.html"))
            {
                sw.WriteLine("<html><body style=\"font-family: Courier New\">");

                foreach (string line in lines)
                    sw.WriteLine(line);

                sw.WriteLine(@"</body></html>");
            }
        }

        void ShowSDLWindow(List<string> lines, MagickImage image)
        {
            var window = new RenderWindow(new VideoMode((uint)(image.Width * 8), (uint)(image.Height * 8)), "ASCII");

            string textToDisplay = string.Empty;
            foreach (string line in lines)
                textToDisplay += line + "\n";

            var text = new Text(textToDisplay, new Font("cour.ttf"), 1 *8);

            window.Draw(text);

            Texture texture = new Texture((uint)(image.Width * 8), (uint)(image.Height * 8));
            texture.Update(window);
            texture.CopyToImage().SaveToFile(image.FileName + ".png");
        }

        void DisplayInConsole(List<string> lines)
        {
            foreach (string line in lines)
                Console.WriteLine(line);
        }

        public override void Convert(string path, bool html)
        {
            Console.WriteLine($"Converting {path}");

            var image = Resize(new MagickImage(path));

            MagickColor color;

            List<string> lines = new List<string>();
            string line = string.Empty;

            foreach (var pixel in image.GetPixels())
            {
                if (pixel.ToColor().A == 0)
                {
                    color = MagickColor.FromRgb(255, 255, 255);
                }
                else
                {
                    color = MagickColor.FromRgb
                    (
                        (byte)((pixel.ToColor().R + pixel.ToColor().G + pixel.ToColor().B) / 3),
                        (byte)((pixel.ToColor().R + pixel.ToColor().G + pixel.ToColor().B) / 3),
                        (byte)((pixel.ToColor().R + pixel.ToColor().G + pixel.ToColor().B) / 3)
                    );
                }

                int index = color.R * 10 / 255;

                if (pixel.X == image.Width - 1)
                {
                    if(html)
                        lines.Add(line + "<br>");
                    else
                        lines.Add(line);

                    line = string.Empty;
                }
                else
                {
                    if (html)
                        if (chars[index] == 32)
                            line += "&nbsp;";
                        else
                            line += (char)chars[index];
                    else
                        line += (char)chars[index];

                }
            }

            Console.WriteLine($"Finished getting text");
            Console.WriteLine($"Ascii rows: {lines.Count}");
            Console.WriteLine($"Started saving text to file");
             
            if (html)
                ToHtmlFile(lines, Path.GetFileNameWithoutExtension(path));
            else
                ToTextFile(lines, Path.GetFileNameWithoutExtension(path));

            Console.WriteLine($"Finished saving text");
            Console.WriteLine($"Starting writting on image");

            ShowSDLWindow(lines, image);

            Console.WriteLine($"Finished {path}");
        }

        public override MagickImage Resize(MagickImage image)
        {
            Console.WriteLine($"Resizing");

            //var size = new MagickGeometry(600, 600);
            //size.IgnoreAspectRatio = false;
            //image.Resize(size);

            Console.WriteLine($"Finished resizing");

            return image;
        }
    }
}