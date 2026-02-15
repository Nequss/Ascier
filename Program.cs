using System;
using System.IO;
using CLI_Sharp;
using Xabe.FFmpeg.Downloader;

namespace Ascier
{
    class Program
    {
        public static Logger Logger = new Logger();
        public static MyProcessor Processor = new MyProcessor();
        public static ConsoleDisplay Display = new ConsoleDisplay(Logger, Processor);

        static void Main()
        {
            Display.title = "Ascier";
            Display.dynamicRefresh = true;
            Display.start();

            string[] directories = { "input", "output", "ffmpeg" };

            for (int i = 0; i < directories.Length; i++)
            {
                string dirPath = Path.Combine(Directory.GetCurrentDirectory(), directories[i]);
                if (!Directory.Exists(dirPath))
                {
                    Program.Logger.info($"The following directory has not been found.");
                    Program.Logger.info(dirPath);

                    Directory.CreateDirectory(directories[i]);

                    Program.Logger.info($"{directories[i]} directory has been created.");
                }
                else
                {
                    Program.Logger.info($"{directories[i]} directory has been found.");
                }
            }

            Display.forceRedraw();

            string ffmpegPath = Path.Combine(Directory.GetCurrentDirectory(), "ffmpeg");
            var status = FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, ffmpegPath);

            if (!status.IsCompleted)
            {
                Program.Logger.info("Downloading FFMPEG library for video conversion.");
                status.GetAwaiter().GetResult();
                Program.Logger.info("Done.");
            }
            else
            {
                Program.Logger.info("ffmpeg has been found.");
            }
        }
    }
}