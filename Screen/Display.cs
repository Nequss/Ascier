﻿using Ascier.Converters;
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
        uint scale;
        bool mode;
        public Vector2i size { get; private set; }

        private String imgPath;

        public Display(uint _scale, String path)
        {
            size = (Vector2i)new Image(path).Size;
            imgPath = path;
            scale = _scale;
        }

        public void PreviewFrame(RenderWindow window)
        {
            window.SetVisible(true);

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
                case Keyboard.Key.Up:
                    scale++;
                    window.Close();
                    PreviewFrame(new RenderWindow(new VideoMode((uint)size.X * scale, (uint)size.Y * scale), "Frame preview/configuration"));
                    break;

                case Keyboard.Key.Down:
                    scale--;

                    if (scale <= 1)
                        scale = 1;

                    window.Close();
                    PreviewFrame(new RenderWindow(new VideoMode((uint)size.X * scale, (uint)size.Y * scale), "Frame preview/configuration"));
                    break;

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
            PixelEntity.text.CharacterSize = 4*scale;
            
            foreach (var pixel in new PictureConverter().MakePixels(imgPath))
                window.Draw(pixel.GetPixel(scale));

            window.Display();
        }
    }
}
