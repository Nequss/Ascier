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
            MagickGeometry mg = new MagickGeometry(100);
            mg.IgnoreAspectRatio = false;
            image.Resize(mg);

            List<PixelEntity> pixelEntities = new List<PixelEntity>();
                
            foreach (var pixel in image.GetPixels())
            {
                var pixelColor = pixel.ToColor();

                byte red   = (byte)((pixelColor.R + pixelColor.G + pixelColor.B) / 3);
                byte green = (byte)((pixelColor.R + pixelColor.G + pixelColor.B) / 3);
                byte blue  = (byte)((pixelColor.R + pixelColor.G + pixelColor.B) / 3);

                Color grayColor = new Color(red, green, blue);

                if (pixel.ToColor().A == 0)
                {
                    pixelEntities.Add(new PixelEntity(
                        chars[10],
                        Color.White,
                        new Vector2f(pixel.X, pixel.Y)));
                }
                else
                {
                    pixelEntities.Add(new PixelEntity(
                        chars[grayColor.R / 25],
                        new Color(pixelColor.R, pixelColor.G, pixelColor.B),
                        new Vector2f(pixel.X, pixel.Y)));
                }
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
