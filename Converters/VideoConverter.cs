using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xabe.FFmpeg;
using System.Threading;
using Ascier.Screen;

namespace Ascier.Converters
{
    public class VideoConverter
    {
        private PictureConverter pictureConverter;
        private string path;

        public VideoConverter(string _path)
        {
            FFmpeg.SetExecutablesPath($"{Directory.GetCurrentDirectory()}/ffmpeg");
            pictureConverter = new PictureConverter();
            path = _path;
        }

        public void Start() => RunConversion();

        private async Task RunConversion()
        {
            Program.Logger.info("Splitting video to frames...");
            var status = ExtractFrames();

            Program.Display.dynamicRefresh = false;
            Program.Display.forceRedraw();

            while (!status.IsCompleted);
            Program.Logger.info("Finished splitting video to frames...");
            

            Program.Logger.info("Displaying preview");
            DisplayPreview();
        }

        private void DisplayPreview()
        {
            Program.Logger.info("Displaying configurable preview");

            Display display = new Display($"{Directory.GetCurrentDirectory()}/temp/001.png", true);
            display.PreviewFrame();
        }

        private async Task ExtractFrames()
        {
            Func<string, string> outputFileNameBuilder = (number) 
                => { return $"{Directory.GetCurrentDirectory()}/temp/{number.Trim('_')}.png"; };

            IMediaInfo info = await FFmpeg.GetMediaInfo(path).ConfigureAwait(false);
            IVideoStream videoStream = info.VideoStreams.First()?.SetCodec(VideoCodec.png);

            IConversionResult conversionResult = await FFmpeg.Conversions.New()
                .AddStream(videoStream)
                .ExtractEveryNthFrame(2, outputFileNameBuilder)
                .Start();
        }

        private void ConvertFrames()
        {

        }

        private void MakeVideo()
        {

        }
    }
}
