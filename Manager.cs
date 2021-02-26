using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ascier.Converters;
using Ascier.Screen;
using ImageMagick;

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
                Program.Logger.info("Starting conversion");
                pictureConverter = new PictureConverter(new MagickImage(path));

                Program.Logger.info("Displaying");

                display = new Display();
                display.ShowPicture(pictureConverter.MakePixels(), 1, pictureConverter.image);

                Program.Logger.info("Finished");
            }
            else
            {
                Program.Logger.info("File not found!");
            }
        }

        public void ConvertImages()
        {

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
