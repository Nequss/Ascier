using System;
using ImageMagick;
using Xabe.FFmpeg;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Windows;
using Ascier.Converters;
using CLI_Sharp;

namespace Ascier
{
    class Program
    {
        public static Logger Logger = new Logger();
        public static MyProcessor Processor = new MyProcessor();
        public static ConsoleDisplay Display = new ConsoleDisplay(Logger, Processor);

        static void Main()
        {
            Display.dynamicRefresh = true;
            Display.start();

            string[] directories = { "input", "output" };

            for (int i = 0; i < directories.Length; i++)
            {
                if (!Directory.Exists($"{Directory.GetCurrentDirectory()}/{directories[i]}"))
                {
                    Program.Logger.info($"The following directory has been not found");
                    Program.Logger.info($"{Directory.GetCurrentDirectory()}/{directories[i]}");

                    Directory.CreateDirectory(directories[i]);

                    Program.Logger.info($"{directories[i]} directory has been created");
                }
            }
        }
    }
}