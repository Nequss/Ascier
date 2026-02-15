using System.Buffers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AscierWeb.Services;

// serwis konwersji obrazów na ascii
// dekoduje obraz do surowych bajtów rgb24, potem deleguje do efektu
// używa ImageSharp do dekodowania (czyste c#, bez zależności natywnych)
// bufor pikseli z ArrayPool minimalizuje alokacje GC
public sealed class ImageService
{
    // konwertuje plik obrazu na ramkę ascii
    public async Task<AscierWeb.Core.AsciiFrame> ConvertAsync(
        Stream imageStream,
        AscierWeb.Core.ConversionSettings settings)
    {
        // dekodowanie obrazu do rgb24
        using var image = await Image.LoadAsync<Rgb24>(imageStream);

        int width = image.Width;
        int height = image.Height;
        int pixelCount = width * height;
        int byteCount = pixelCount * 3;

        // wypożyczanie bufora z puli zamiast alokacji
        byte[] rgb24 = ArrayPool<byte>.Shared.Rent(byteCount);

        try
        {
            // kopiowanie pikseli do ciągłego bufora rgb24
            // ImageSharp przechowuje dane wierszami - kopiujemy wiersz po wierszu
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    int offset = y * width * 3;

                    for (int x = 0; x < width; x++)
                    {
                        int idx = offset + x * 3;
                        rgb24[idx] = row[x].R;
                        rgb24[idx + 1] = row[x].G;
                        rgb24[idx + 2] = row[x].B;
                    }
                }
            });

            // wybór efektu i przetwarzanie
            var effect = AscierWeb.Core.EffectRegistry.Get(settings.Effect);
            return effect.Process(rgb24, width, height, settings);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rgb24);
        }
    }

    // konwertuje surowe bajty rgb24 (np. z ffmpeg) na ramkę ascii
    public AscierWeb.Core.AsciiFrame ConvertRaw(
        byte[] rgb24,
        int width,
        int height,
        AscierWeb.Core.ConversionSettings settings)
    {
        var effect = AscierWeb.Core.EffectRegistry.Get(settings.Effect);
        return effect.Process(rgb24, width, height, settings);
    }
}
