using Ascier.Converters;
using System;
using System.IO;
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
        private bool mode = true;
        private uint fontSize = 2;
        private int frame = 1;
        private int lastFrame;
        private string padded = "000";
        private bool isVideo;
        private int index = 6;

        // 8 entries (power of 2) — enables & 7 bitwise wrap
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

            using (var img = new Image(path))
                size = (Vector2i)img.Size;

            if (isVideo)
            {
                string tempDir = Path.Combine(Directory.GetCurrentDirectory(), "temp");
                lastFrame = Directory.GetFiles(tempDir).Length;

                if (lastFrame > 999)
                {
                    padded = new string('0', lastFrame.ToString().Length);
                }
            }
        }

        public void PreviewFrame()
        {
            using (RenderWindow window = new RenderWindow(new VideoMode((uint)size.X, (uint)size.Y), "ASCII Preview"))
            {
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
                            path = BuildFramePath();
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
                            path = BuildFramePath();
                            Draw(window, mode, fontSize);
                        }
                    }
                    break;
            }
        }

        private string BuildFramePath()
        {
            string frameStr = frame.ToString();
            string paddedFrame = padded.Substring(frameStr.Length) + frameStr;
            return Path.Combine(Path.GetDirectoryName(path), $"{paddedFrame}{Path.GetExtension(path)}");
        }

        private void Window_KeyReleased(object sender, KeyEventArgs e)
        {
            RenderWindow window = (RenderWindow)sender;

            switch (e.Code)
            {
                case Keyboard.Key.C:
                    if (isVideo)
                    {
                        string asciiTempDir = Path.Combine(Directory.GetCurrentDirectory(), "ascii_temp");

                        if (Directory.Exists(asciiTempDir))
                        {
                            Program.Logger.info("Deleting ascii temp existing files...");
                            Program.Display.forceRedraw();
                            Directory.Delete(asciiTempDir, true);
                        }

                        Directory.CreateDirectory(asciiTempDir);

                        string tempDir = Path.Combine(Directory.GetCurrentDirectory(), "temp");
                        string[] files = Directory.GetFiles(tempDir);

                        Program.Logger.info($"Frames to convert: {files.Length}");

                        for (int i = 0; i < files.Length; i++)
                        {
                            Program.Logger.info($"Converting {i + 1}/{files.Length} frame");
                            Program.Display.forceRedraw();

                            pictureConverter.DrawPreview(window, mode, files[i], fontSize, colorArray[index]);
                            window.Display();

                            string savePath = Path.Combine(asciiTempDir, Path.GetFileName(files[i]));

                            using (Texture texture = new Texture((uint)size.X, (uint)size.Y))
                            {
                                texture.Update(window);
                                using (var img = texture.CopyToImage())
                                    img.SaveToFile(savePath);
                            }
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
                    // colorArray.Length is 8 (power of 2) — bitwise AND replaces modulo
                    index = (index + 1) & 7;
                    window.Clear(colorArray[index]);
                    Draw(window, mode, fontSize);
                    break;
            }
        }

        private void SavePreview(RenderWindow window, string savePath)
        {
            using (Texture texture = new Texture((uint)size.X, (uint)size.Y))
            {
                texture.Update(window);

                string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "output",
                    $"{Path.GetFileNameWithoutExtension(savePath)}_{DateTime.Now.Ticks}{Path.GetExtension(savePath)}");

                using (var img = texture.CopyToImage())
                {
                    if (img.SaveToFile(outputPath))
                    {
                        Program.Logger.info("Saved:");
                        Program.Logger.info(outputPath);
                    }
                    else
                    {
                        Program.Logger.info("Failed to save the image!");
                    }
                }
            }
        }

        private void Draw(RenderWindow window, bool mode, uint fontSize)
        {
            pictureConverter.DrawPreview(window, mode, path, fontSize, colorArray[index]);
            window.Display();
        }
    }
}
