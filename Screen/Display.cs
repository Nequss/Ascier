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
        byte index = 0;

        List<PixelEntity> pixelEntities;
        MagickImage image;
        uint scale;

        public Display(List<PixelEntity> _pixelEntities, uint _scale, MagickImage _image)
        {
            pixelEntities = _pixelEntities;
            image = _image;
            scale = _scale;
        }

        public void Preview()
        {
            var window = new RenderWindow(new VideoMode((uint)image.Width * scale, (uint)image.Height * scale), "ASCII");

            window.SetVisible(true);
            window.Clear(Color.Black);

            window.Closed += (_, __) => window.Close();
            window.KeyReleased += Window_KeyReleased;

            Draw(window);

            while (window.IsOpen)
            {
                window.DispatchEvents();
            }
        }

        private void Window_KeyReleased(object sender, KeyEventArgs e)
        {
            RenderWindow window = (RenderWindow)sender;

            switch (e.Code)
            {
                case Keyboard.Key.R:
                    window.Clear(new Color(
                        (byte)new Random().Next(256),
                        (byte)new Random().Next(256),
                        (byte)new Random().Next(256)));
                    Draw(window);
                    break;

                case Keyboard.Key.B:
                    Color[] colorArray = {
                        Color.Blue,
                        Color.White,
                        Color.Cyan,
                        Color.Green,
                        Color.Magenta,
                        Color.Red,
                        Color.Black,
                        Color.Yellow };

                    for (int i = 0; i < colorArray.Length; i++)
                    {
                        if (i == index)
                        {
                            index += 1;

                            if (index > colorArray.Length - 1)
                            {
                                index = 0;
                                window.Clear(colorArray[index]);
                                Draw(window);
                            }
                            else
                            {
                                window.Clear(colorArray[index]);
                                Draw(window);
                            }

                            break;
                        }
                    }
                    break;
            }
        }

        public void Draw(RenderWindow window)
        {
            foreach (var pixel in pixelEntities)
                window.Draw(pixel.GetPixel(scale));

            window.Display();
        }
    }
}
