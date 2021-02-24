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

            Console.WriteLine($"Importing files from {$"{Directory.GetCurrentDirectory()}/assets"} directory.");

            string[] files = Directory.GetFiles($"{Directory.GetCurrentDirectory()}/assets");

            Console.WriteLine($"Files found: {files.Length}");

            foreach (string file in files)
                Console.WriteLine($"Imported: {file}");

            foreach (string file in files)
            {
                string ext = Path.GetExtension(file);

                switch (ext)
                {
                    case ".png": case ".jpg":
                        PictureConverter pictureConverter = new PictureConverter(file);
                        pictureConverter.ConvertToPicture(pictureConverter.ConvertToAscii());
                        break;

                    case ".gif":
                        GifConverter gifConverter = new GifConverter(file);
                        gifConverter.ConvertToAscii();
                        gifConverter.ConvertToGif(gifConverter.asciiCollection);
                        break;
                }
            }
        }
    }
}
