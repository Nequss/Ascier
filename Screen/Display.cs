using Ascier.Converters;
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
        PictureConverter pictureConverter = new PictureConverter();
        public Vector2i size { get; private set; }

        private string path;

        private uint fontSize = 2;
        private bool mode = true;

        public Display(string _path)
        {
            path = _path;
            size = (Vector2i)new Image(path).Size;
        }

        public void PreviewFrame()
        {
            RenderWindow window = new RenderWindow(new VideoMode((uint)size.X, (uint)size.Y), "ASCII Preview");
            window.SetVisible(true);

            window.Closed += (_, __) => window.Close();
            window.KeyReleased += Window_KeyReleased;

            Draw(window, mode, fontSize);

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
                case Keyboard.Key.Right:
                    fontSize += 2;
                    Draw(window, mode, fontSize);
                    break;

                case Keyboard.Key.Left:
                    if (fontSize <= 2)
                    {
                        fontSize = 2;
                    }
                    else
                    {
                        fontSize -= 2;
                        Draw(window, mode, fontSize);
                    }
                    break;

                case Keyboard.Key.C:
                    mode = !mode;
                    Draw(window, mode, fontSize);
                    break;
            }
        }

        private void Draw(RenderWindow window, bool mode, uint fontSize)
        {
            pictureConverter.DrawPreview(window, mode, path, fontSize);
            window.Display();
        }
    }
}
