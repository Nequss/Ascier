using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Ascier.Converters.Base;
using Ascier.Elements;
using ImageMagick;
using SFML.Graphics;
using SFML.System;
using SFML.Window;

namespace Ascier.Converters
{
    public class PictureConverter : Converter
    {
        public MagickImage image;

        public PictureConverter(MagickImage _image)
        {
            image = _image;
        }

        public override List<PixelEntity> MakePixels()
        {
            List<PixelEntity> pixelEntities = new List<PixelEntity>();
                
            int index;

            foreach (var pixel in image.GetPixels())
            {
                var pixelColor = pixel.ToColor();

                if (pixel.ToColor().A == 0)
                    index = 10;
                else
                    index = pixelColor.R / 25;

                pixelEntities.Add(new PixelEntity(
                        chars[index],
                        new Color(pixelColor.R, pixelColor.G, pixelColor.B),
                        new Vector2f(pixel.X, pixel.Y),
                        font));
             }

            return pixelEntities;
        }

        public override void SaveToFile()
        {
            /*
            Texture texture = new Texture((uint)bounds.Width, (uint)bounds.Height);
            texture.Update(window);
            texture.CopyToImage().SaveToFile(image.FileName + ".png");
            */

            throw new NotImplementedException();
        }
    }
}
