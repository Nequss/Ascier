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

            Logger.info("CLI has started");
        }
    }
}
