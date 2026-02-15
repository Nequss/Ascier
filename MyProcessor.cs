using Ascier.Screen;
using System;
using System.IO;
using Ascier.Converters;

namespace Ascier
{
    public class MyProcessor : CLI_Sharp.CommandProcessor
    {
        private static readonly string[] VideoExtensions = { ".mp4", ".avi", ".mkv", ".webm", ".mov" };

        public override void processCommand(string cmd)
        {
            Preview(cmd);
        }

        private void Preview(string cmd)
        {
            string path = FindFile(cmd);

            if (path == null)
            {
                Program.Logger.info("File not found!");
                return;
            }

            if (isVideo(path))
            {
                Program.Logger.info("Found video!");
                VideoConverter videoConverter = new VideoConverter(path);
                videoConverter.Start();
            }
            else
            {
                Program.Logger.info("File found!");
                Program.Logger.info("Displaying configurable preview");

                Display display = new Display(path, false);
                display.PreviewFrame();
            }
        }

        private string FindFile(string name)
        {
            string inputDir = Path.Combine(Directory.GetCurrentDirectory(), "input");
            foreach (string path in Directory.GetFiles(inputDir))
                if (name == Path.GetFileName(path) || name == Path.GetFileNameWithoutExtension(path))
                    return path;

            return null;
        }

        private bool isVideo(string path)
        {
            string ext = Path.GetExtension(path);
            for (int i = 0; i < VideoExtensions.Length; i++)
                if (string.Equals(ext, VideoExtensions[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}