using System;
using ImageMagick;
using Xabe.FFmpeg;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Windows;
using Ascier.Converters;

namespace Ascier
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine($"Importing files from {$"{Directory.GetCurrentDirectory()}/images"} directory.");

            string[] files = Directory.GetFiles($"{Directory.GetCurrentDirectory()}/images");

            Console.WriteLine($"Files found: {files.Length}");

            foreach (string file in files)
                Console.WriteLine($"Imported: {file}");

            foreach(string file in files)
            {
                PictureConverter pictureConverter =  new PictureConverter(file);
                pictureConverter.ConvertToPicture(pictureConverter.ConvertToAscii());
            }
        }
    }
}
