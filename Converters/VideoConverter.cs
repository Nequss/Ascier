using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xabe.FFmpeg;
using Ascier.Screen;

namespace Ascier.Converters
{
    public class VideoConverter
    {
        private readonly string path;

        public VideoConverter(string _path)
        {
            FFmpeg.SetExecutablesPath(Path.Combine(Directory.GetCurrentDirectory(), "ffmpeg"));
            path = _path;
        }

        public void Start() => RunConversion().GetAwaiter().GetResult();

        private async Task RunConversion()
        {
            string tempDir = Path.Combine(Directory.GetCurrentDirectory(), "temp");

            if (Directory.Exists(tempDir))
            {
                Program.Logger.info("Deleting temp existing files...");
                Directory.Delete(tempDir, true);
            }

            Directory.CreateDirectory(tempDir);

            Program.Logger.info("Splitting video to frames...");
            Program.Display.dynamicRefresh = false;
            Program.Display.forceRedraw();
            await ExtractFrames();
            Program.Logger.info("Finished splitting video to frames...");

            Program.Logger.info("Displaying configurable preview");
            Program.Display.forceRedraw();
            DisplayPreview();
        }

        private void DisplayPreview()
        {
            string firstFrame = Path.Combine(Directory.GetCurrentDirectory(), "temp", "001.png");
            Display display = new Display(firstFrame, true);
            display.PreviewFrame();
        }

        private async Task ExtractFrames()
        {
            string tempDir = Path.Combine(Directory.GetCurrentDirectory(), "temp");
            Func<string, string> outputFileNameBuilder = (number)
                => { return Path.Combine(tempDir, $"{number.TrimStart('_')}.png"); };

            IMediaInfo info = await FFmpeg.GetMediaInfo(path).ConfigureAwait(false);
            IVideoStream videoStream = info.VideoStreams.FirstOrDefault()?.SetCodec(VideoCodec.png);

            if (videoStream == null)
            {
                Program.Logger.info("No video stream found in file!");
                return;
            }

            await FFmpeg.Conversions.New()
                .AddStream(videoStream)
                .ExtractEveryNthFrame(30, outputFileNameBuilder)
                .Start();
        }

        public async Task MakeVideo()
        {
            string asciiTempDir = Path.Combine(Directory.GetCurrentDirectory(), "ascii_temp");
            string[] files = Directory.GetFiles(asciiTempDir);

            string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "output",
                $"{Path.GetFileNameWithoutExtension(path)}.mp4");

            await FFmpeg.Conversions.New()
                .SetInputFrameRate(30)
                .BuildVideoFromImages(files)
                .SetFrameRate(30)
                .SetPixelFormat(PixelFormat.yuv420p)
                .SetOutput(outputPath)
                .Start();
        }
    }
}
