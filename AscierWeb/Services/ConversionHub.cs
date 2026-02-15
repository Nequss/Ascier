using Microsoft.AspNetCore.SignalR;
using AscierWeb.Core;

namespace AscierWeb.Services;

// hub signalr - ramki ascii + logi w czasie rzeczywistym
public sealed class ConversionHub : Hub
{
    private readonly ImageService _imageService;
    private readonly VideoService _videoService;
    private readonly LogService _logService;

    public ConversionHub(ImageService imageService, VideoService videoService, LogService logService)
    {
        _imageService = imageService;
        _videoService = videoService;
        _logService = logService;
    }

    // subskrypcja logów w czasie rzeczywistym
    public async Task SubscribeLogs()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "logs");

        // wyślij ostatnie logi do nowego subskrybenta
        var recent = _logService.GetRecent(50);
        foreach (var entry in recent)
        {
            await Clients.Caller.SendAsync("NewLog", new
            {
                timestamp = entry.Timestamp.ToString("HH:mm:ss.fff"),
                level = entry.Level,
                message = entry.Message
            });
        }
    }

    // przetwarzanie klatki wideo
    public async Task RequestFrame(string sessionId, int frameNumber, ConversionSettings settings)
    {
        var frame = await _videoService.GetFrameAsync(sessionId, frameNumber, settings);

        if (frame != null)
        {
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

    public async Task GetEffects()
    {
        var effects = EffectRegistry.List()
            .Select(e => new { name = e.Name, description = e.Description });
        await Clients.Caller.SendAsync("EffectsList", effects);
    }
}
