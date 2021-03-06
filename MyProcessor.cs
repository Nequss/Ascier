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
    }
}