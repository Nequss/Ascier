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
        public static ConsoleDisplay Display = new ConsoleDisplay(Logger,Processor);
        static void Main(string[] args)
        {
            Display.dynamicRefresh = false;
            Display.start();
            
            Logger.info($"Importing files");
            string[] files = Directory.GetFiles($"{Directory.GetCurrentDirectory()}/files");
            Logger.info($"Files found: {files.Length}");


            foreach (string file in files)
                Logger.info($"Imported: {file}");

            foreach (string file in files)
            {
                string ext = Path.GetExtension(file);

                switch (ext)
                {
                    case ".png": case ".jpg":
                        break;
                }
            }

            Console.ReadKey();
        }
    }
}
