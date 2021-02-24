using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageMagick;
using System.IO;

namespace Ascier.Converters.Base
{
    class Converter
    {
        public byte[] chars = { 35, 64, 37, 61, 43, 42, 58, 45, 126, 46, 32 };

        public MagickImage Resize(MagickImage image, int width, int height)
        {
            var size = new MagickGeometry(width, height);
            size.IgnoreAspectRatio = false;
            image.Resize(size);

            return image;
        }
    }
}
