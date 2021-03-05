using Ascier.Screen;
using System;
using System.IO;

namespace Ascier
{
    public class MyProcessor : CLI_Sharp.CommandProcessor
    {
        public string[] paths;

        public override void processCommand(string cmd)
        {
            switch (cmd)
            {
                case "i":
                    Import();
                    break;
                default:
                    Preview(cmd);
                    break;
            }
        }

        private void Preview(string cmd)
        {
            string path = FindFile(cmd);

            Program.Logger.info(path);

            if (path != null)
            {
                Program.Logger.info("File found!");
                Program.Logger.info("Displaying configurable preview");

                Display display = new Display(path);
                display.PreviewFrame();


            }
            else
            {
                Program.Logger.info("File not found!");
            }
        }

        private string FindFile(string name)
        {
            foreach (string path in paths)
                if (name == Path.GetFileName(path) | name == Path.GetFileNameWithoutExtension(path))
                    return path;

            return null;
        }

        private void Import()
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