using System;
using ImageMagick;
using Xabe.FFmpeg;
using System.IO;
using System.Threading;
using Ascier.Makers;

namespace Ascier.Makers
{
    class GifMaker
    {
        private string[] _paths;

        public GifMaker(string[] paths)
        {
            _paths = paths;
        }

    }
}
