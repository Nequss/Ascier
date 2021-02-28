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
        public int stepSize = 4;
        
        public override List<PixelEntity> MakePixels(MagickImage image)
        {
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
                        (char)chars[10],
                        Color.White,
                        new Vector2f(pixel.X, pixel.Y)));
                }
                else
                {
                    pixelEntities.Add(new PixelEntity(
                        (char)chars[grayColor.R / 25],
                        new Color(pixelColor.R, pixelColor.G, pixelColor.B),
                        new Vector2f(pixel.X, pixel.Y)));
                }
             }

            return pixelEntities;
        }

        public List<PixelEntity> MakePixels(String img)
        {
            List<PixelEntity> tmpPixels = new List<PixelEntity>();
            var image = new Image(img);

            for (int i = 0; i < image.Size.X; i+=stepSize)
            {
                for (int j = 0; j < image.Size.Y; j+=stepSize)
                {
                    var tmpPixel = image.GetPixel((uint) i,(uint) j);
                    
                    tmpPixels.Add(
                        new PixelEntity((char)chars[tmpPixel.R/25],
                            new Color(tmpPixel.R, tmpPixel.G, tmpPixel.B),
                            new Vector2f(i,j)));
                }
            }
            return tmpPixels;
        }
    }
}
