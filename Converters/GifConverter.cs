using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ascier.Converters.Base;
using ImageMagick;
using SFML.Graphics;
using SFML.Window;

namespace Ascier.Converters
{
    class GifConverter : Converter
    {
        public PictureConverter pictureConverter = new PictureConverter();
        public MagickImageCollection collection;
        public MagickImageCollection newCollection = new MagickImageCollection();
        public List<string> asciiCollection = new List<string>();
        public string convertedPath = $"{Directory.GetCurrentDirectory()}/converted/gif";
        public GifConverter(string path)
        {
            collection = new MagickImageCollection(path);
        }

        public GifConverter()
        {

        }

        public void ConvertToAscii()
        {
            foreach(var item in collection)
            {
                pictureConverter.image = new MagickImage(item);
                asciiCollection.Add(pictureConverter.ConvertToAscii());
            }
        }

        public void ConvertToGif(List<string> asciiCollection)
        {
            for(int i = 0; i < collection.Count; i++)
            {
                MagickImage temp = new MagickImage(collection[i]);
                var window = new RenderWindow(new VideoMode((uint)temp.Width, (uint)temp.Height), "ASCII");
                var text = new Text(asciiCollection[i], new Font("cour.ttf"), 1);

                window.Draw(text);

                Texture texture = new Texture((uint)temp.Width, (uint)temp.Height);
                texture.Update(window);
                texture.CopyToImage().SaveToFile(convertedPath + $"{i}.png");

                window.Close();
            }

            string[] files = Directory.GetFiles($"{Directory.GetCurrentDirectory()}/converted");

            for (int i = 0; i < files.Length; i++)
            {
                collection.Add(files[i]);
                collection[i].AnimationDelay = 100;
            }

            collection.Write($"{Directory.GetCurrentDirectory()}/converted/gif/converted.gif");
        }
    }
}
