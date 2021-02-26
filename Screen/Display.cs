using Ascier.Converters;
using Ascier.Elements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageMagick;
using SFML.Graphics;
using SFML.System;
using SFML.Window;

namespace Ascier.Screen
{
    public class Display
    {
        public void ShowPicture(List<PixelEntity> pixelEntities, uint scale, MagickImage image)
        {
            var window = new RenderWindow(new VideoMode((uint)image.Width * scale, (uint)image.Height * scale), "ASCII");

            window.SetVisible(true);
            window.Clear(Color.White);

            for (int i = 0; i < 50; i++)
                foreach (var pixel in pixelEntities)
                    window.Draw(pixel.GetPixel(scale));

            window.Display();
        }

        public void ShowGif()
        {
            //TODO
        }

        public void ShowVideo()
        {
            //TODO
        }
    }
}
