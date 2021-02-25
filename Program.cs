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
            Console.WriteLine($"Importing files");
            string[] files = Directory.GetFiles($"{Directory.GetCurrentDirectory()}/files");
            Console.WriteLine($"Files found: {files.Length}");

            foreach (string file in files)
                Console.WriteLine($"Imported: {Path.GetFileName(file)}");

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
