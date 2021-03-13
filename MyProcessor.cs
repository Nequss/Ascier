using Ascier.Screen;
using System;
using System.IO;
using Ascier.Converters;

namespace Ascier
{
    public class MyProcessor : CLI_Sharp.CommandProcessor
    {
        public override void processCommand(string cmd)
        { 
            switch(cmd)
            {
                case "make video":
                    VideoConverter videoConverter = new VideoConverter();
                    videoConverter.MakeVideo();
                    break;
                default:
                    Preview(cmd);
                    break;
            }
        }

        private void Preview(string cmd)
        {
            string path = FindFile(cmd);

            if (isVideo(path))
            {
                Program.Logger.info("Found video!");
                VideoConverter videoConverter = new VideoConverter(path);
                videoConverter.Start();
            }
            else 
            {
                if (path != null)
                {
                    Program.Logger.info("File found!");
                    Program.Logger.info("Displaying configurable preview");

                    Display display = new Display(path, false);
                    display.PreviewFrame();
                }
                else
                {
                    Program.Logger.info("File not found!");
                }
            }
        }

        private string FindFile(string name)
        {
            foreach (string path in Directory.GetFiles($"{Directory.GetCurrentDirectory()}/input"))
                if (name == Path.GetFileName(path) | name == Path.GetFileNameWithoutExtension(path))
                    return path;

            return null;
        }

        private bool isVideo(string path)
            => Path.GetExtension(path) == ".mp4" ? true : false;
    }
}