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
        private int lastFrame;
        private string padded = "000";
        private bool isVideo;
        private int index = 6;

        private Color[] colorArray = {
                        Color.Blue,
                        Color.White,
                        Color.Cyan,
                        Color.Green,
                        Color.Magenta,
                        Color.Red,
                        Color.Black,
                        Color.Yellow };

        public Display(string _path, bool _isVideo)
        {
            path = _path;
            isVideo = _isVideo;
            size = (Vector2i)new Image(path).Size;

            lastFrame = Directory.GetFiles($"{Directory.GetCurrentDirectory()}/temp").Length;

            if (isVideo)
                if (lastFrame > 999)
                {
                    padded = "";
                    foreach (char c in lastFrame.ToString())
                        padded += "0";
                }
        }

        public void PreviewFrame()
        {
            RenderWindow window = new RenderWindow(new VideoMode((uint)size.X, (uint)size.Y), "ASCII Preview");
            window.SetVisible(true);
            window.SetFramerateLimit(30);

            window.Closed += (_, __) => window.Close();
            window.KeyPressed += Window_KeyPressed;
            window.KeyReleased += Window_KeyReleased;

            Draw(window, mode, fontSize);

            while (window.IsOpen)
            {
                window.DispatchEvents();
            }
        }

        private void Window_KeyPressed(object sender, KeyEventArgs e)
        {
            RenderWindow window = (RenderWindow)sender;

            switch (e.Code)
            {
                case Keyboard.Key.Up:
                    if (isVideo)
                    {
                        if (frame < lastFrame)
                        {
                            frame++;
                            path = Path.Combine(Path.GetDirectoryName(path), $"{padded.Substring(frame.ToString().Length) + frame}{Path.GetExtension(path)}");
                            Draw(window, mode, fontSize);
                        }
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
            }
        }

        private void Window_KeyReleased(object sender, KeyEventArgs e)
        {
            RenderWindow window = (RenderWindow)sender;

            switch (e.Code)
            {
                case Keyboard.Key.C:
                    if (isVideo)
                    {
                        if (Directory.Exists($"{Directory.GetCurrentDirectory()}/ascii_temp"))
                        {
                            Program.Logger.info("Deleting ascii temp existing files...");
                            Program.Display.forceRedraw();
                            Directory.Delete($"{Directory.GetCurrentDirectory()}/ascii_temp", true);
                        }

                        Directory.CreateDirectory($"{Directory.GetCurrentDirectory()}/ascii_temp");

                        string[] files = Directory.GetFiles($"{Directory.GetCurrentDirectory()}/temp");

                        Program.Logger.info($"Frames to convert: {files.Length}");

                        for (int i = 0; i < files.Length; i++)
                        {
                            Program.Logger.info($"Converting {i + 1}/{files.Length} frame");
                            Program.Display.forceRedraw();

                            pictureConverter.DrawPreview(window, mode, files[i], fontSize, colorArray[index]);
                            window.Display();

                            path = $"{Directory.GetCurrentDirectory()}/ascii_temp/{Path.GetFileName(files[i])}";

                            Texture texture = new Texture((uint)size.X, (uint)size.Y);
                            texture.Update(window);
                            texture.CopyToImage().SaveToFile(path);
                        }

                        window.Close();
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

                case Keyboard.Key.M:
                    mode = !mode;
                    Draw(window, mode, fontSize);
                    break;

                case Keyboard.Key.S:
                    SavePreview(window, path);
                    break;

                case Keyboard.Key.B:


                    for (int i = 0; i < colorArray.Length; i++)
                    {
                        if (i == index)
                        {
                            index += 1;

                            if (index > colorArray.Length - 1)
                            {
                                index = 0;
                                window.Clear(colorArray[index]);
                                Draw(window, mode, fontSize);
                            }
                            else
                            {
                                window.Clear(colorArray[index]);
                                Draw(window, mode, fontSize);
                            }

                            break;
                        }
                    }
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
            pictureConverter.DrawPreview(window, mode, path, fontSize, colorArray[index]);
            window.Display();
        }
    }
}
