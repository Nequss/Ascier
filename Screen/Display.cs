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
        public void ShowPicture(List<PixelEntity> pixelEntities, uint scale)
        {
            var window = new RenderWindow(new VideoMode
            (
                (uint)pixelEntities[pixelEntities.Count - 1].position.X,
                (uint)pixelEntities[pixelEntities.Count - 1].position.Y
            ), "ASCII");

            window.SetVisible(true);
            window.Clear(Color.White);

            foreach(var pixel in pixelEntities)
                window.Draw(pixel.GetPixel(scale));

            window.Close();
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
