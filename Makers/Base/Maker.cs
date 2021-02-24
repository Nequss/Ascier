using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ImageMagick;

namespace Ascier.Makers.Base
{
    abstract class Maker
    {
        public byte[] chars = { 35, 64, 37, 61, 43, 42, 58, 45, 126, 46, 32 };

        public abstract void Convert(string path, bool html);

        public abstract MagickImage Resize(MagickImage image);

    }
}
