using Microsoft.AspNetCore.SignalR;
using AscierWeb.Core;

namespace AscierWeb.Services;

// hub signalr do komunikacji w czasie rzeczywistym
// klient wysyła ustawienia, serwer przetwarza i odsyła ramkę ascii
// minimalnny overhead - jeden hub obsługuje zarówno obrazy jak i wideo
public sealed class ConversionHub : Hub
{
    private readonly ImageService _imageService;
    private readonly VideoService _videoService;

    public ConversionHub(ImageService imageService, VideoService videoService)
    {
        _imageService = imageService;
        _videoService = videoService;
    }

    // przetwarzanie klatki wideo z nowymi ustawieniami
    public async Task RequestFrame(string sessionId, int frameNumber, ConversionSettings settings)
    {
        var frame = await _videoService.GetFrameAsync(sessionId, frameNumber, settings);

        if (frame != null)
        {
            // konwersja kolorów na base64 dla efektywnego transferu
            string? colorsBase64 = frame.ColorRgb != null
                ? Convert.ToBase64String(frame.ColorRgb)
                : null;

            await Clients.Caller.SendAsync("ReceiveFrame", new
            {
                text = frame.Text,
                columns = frame.Columns,
                rows = frame.Rows,
                colors = colorsBase64,
                frameNumber = frame.FrameNumber,
                totalFrames = frame.TotalFrames
            });
        }
        else
        {
            await Clients.Caller.SendAsync("Error", "nie udało się przetworzyć klatki");
        }
    }

    // lista dostępnych efektów
    public async Task GetEffects()
    {
        var effects = EffectRegistry.List()
            .Select(e => new { name = e.Name, description = e.Description });

        await Clients.Caller.SendAsync("EffectsList", effects);
    }
}
