using Ascier.Elements;
using SFML.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageMagick;

namespace Ascier.Converters.Base
{
    public abstract class Converter
    {
        public byte[] chars = { 35, 64, 37, 61, 43, 42, 58, 45, 126, 46, 32 };

        public Font font = new Font("cour.ttf");

        public abstract List<PixelEntity> MakePixels();


    }
}
