using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ascier.Converters;
using Ascier.Screen;
using ImageMagick;
using SFML.Graphics;
using SFML.Window;

namespace Ascier
{
    class Manager
    {
        PictureConverter pictureConverter;
        Display display;

        public string[] paths;

        string FindFile(string name)
        {
            foreach (string path in paths)
                if (name == Path.GetFileName(path) | name == Path.GetFileNameWithoutExtension(path))
                    return path;

            return null;
        }

        public void Show(string cmd)
        {
            string path = FindFile(cmd);

            Program.Logger.info(path);

            if (path != null)
            {
                Program.Logger.info("File found");

                uint scale = 2;

                display = new Display(scale, path);

                display.PreviewFrame(new RenderWindow(new VideoMode((uint)display.size.X * scale, (uint)display.size.Y * scale), "Frame preview/configuration"));

                Program.Logger.info("Finished");
            }
            else
            {
                Program.Logger.info("File not found!");
            }
        }

        public void Import()
        {
            if (!Directory.Exists($"{Directory.GetCurrentDirectory()}/input"))
            {
                Program.Logger.info($"The following directory has been not found");
                Program.Logger.info($"{Directory.GetCurrentDirectory()}/input");
                Program.Logger.info($"Input directory has been created");

                Directory.CreateDirectory("input");
            }
            else
            {
                Program.Logger.info($"Directory has been found");
                Program.Logger.info($"Importing files");

                paths = Directory.GetFiles($"{Directory.GetCurrentDirectory()}/input");

                Program.Logger.info($"Files found: {paths.Length}");

                if (paths.Length != 0)
                    foreach (string path in paths)
                        Program.Logger.info($"Imported: {Path.GetFileName(path)}");
            }
        }
    }
}
