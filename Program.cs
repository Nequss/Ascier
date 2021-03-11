using System;
using ImageMagick;
using Xabe.FFmpeg;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Windows;
using Ascier.Converters;
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
                if (!Directory.Exists($"{Directory.GetCurrentDirectory()}/{directories[i]}"))
                {
                    Program.Logger.info($"The following directory has not been found.");
                    Program.Logger.info($"{Directory.GetCurrentDirectory()}/{directories[i]}");

                    Directory.CreateDirectory(directories[i]);

                    Program.Logger.info($"{directories[i]} directory has been created.");
                }
                else
                {
                    Program.Logger.info($"{directories[i]} directory has been found.");
                }
            }

            Display.forceRedraw();

            var status = FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, $"{Directory.GetCurrentDirectory()}/ffmpeg");

            if (!status.IsCompleted)
            {
                Program.Logger.info("Downloading FFMPEG library for video conversion.");
                while (!status.IsCompleted);
                Program.Logger.info("Done.");
            }
            else
            {
                Program.Logger.info("ffmpeg has been found.");
            }
        }
    }
}