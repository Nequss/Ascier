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
using System.IO;

namespace Ascier.Screen
{
    public class Display
    {
        PictureConverter pictureConverter = new PictureConverter();

        public Vector2i size { get; private set; }
        private string path;
        private bool mode = true;
        private uint fontSize = 2;
        private int frame = 1;
        private string padded = "000";
        private bool isVideo;

        public Display(string _path, bool _isVideo)
        {
            path = _path;
            isVideo = _isVideo;
            size = (Vector2i)new Image(path).Size;
        }

        public void PreviewFrame()
        {
            RenderWindow window = new RenderWindow(new VideoMode((uint)size.X, (uint)size.Y), "ASCII Preview");
            window.SetVisible(true);
            window.SetFramerateLimit(30);
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
                case Keyboard.Key.P:
                    if (isVideo)
                    {
                        string[] files = Directory.GetFiles(Path.GetDirectoryName(path));

                        foreach (string file in files)
                        {
                            path = file;
                            Draw(window, mode, fontSize);
                        }
                    }
                    break;

                case Keyboard.Key.Up:
                    if (isVideo)
                    {
                        frame++;
                        path = Path.Combine(Path.GetDirectoryName(path), $"{padded.Substring(frame.ToString().Length) + frame}{Path.GetExtension(path)}");
                        Draw(window, mode, fontSize);
                    }
                    break;

                case Keyboard.Key.Down:
                    if (isVideo)
                    {
                        frame--;

                        if (frame < 1)
                        {
                            frame = 1;
                        }
                        else
                        {
                            path = Path.Combine(Path.GetDirectoryName(path), $"{padded.Substring(frame.ToString().Length) + frame}{Path.GetExtension(path)}");
                            Draw(window, mode, fontSize);
                        }
                    }
                    break;

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

                case Keyboard.Key.S:
                    SavePreview(window, path);
                    break;
            }
        }

        private void SavePreview(RenderWindow window, string path)
        {
            Texture texture = new Texture((uint)size.X, (uint)size.Y);
            texture.Update(window);

            path = $"{Directory.GetCurrentDirectory()}/output/{Path.GetFileNameWithoutExtension(path)}_{DateTime.Now.Ticks}{Path.GetExtension(path)}";

            if (texture.CopyToImage().SaveToFile(path))
            {
                Program.Logger.info("Saved:");
                Program.Logger.info(path);
            }
            else
            {
                Program.Logger.info("Failed to save the image!");
            }
        }

        private void Draw(RenderWindow window, bool mode, uint fontSize)
        {
            pictureConverter.DrawPreview(window, mode, path, fontSize);
            window.Display();
        }
    }
}
