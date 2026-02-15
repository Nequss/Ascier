using Microsoft.AspNetCore.SignalR;
using AscierWeb.Core;
using System.Runtime.CompilerServices;

namespace AscierWeb.Services;

// hub signalr - streaming klatek ascii w czasie rzeczywistym + logi
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

    // streaming klatek wideo - real-time IAsyncEnumerable
    // klient odbiera klatki w miarę jak są wyciągane/konwertowane
    public async IAsyncEnumerable<object> StreamVideo(
        string sessionId,
        string effect,
        int step,
        bool colorMode,
        int threshold,
        bool invert,
        int maxColumns,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var settings = new ConversionSettings
        {
            Effect = effect,
            Step = Math.Clamp(step, 1, 64),
            ColorMode = colorMode,
            Threshold = Math.Clamp(threshold, 0, 255),
            Invert = invert,
            MaxColumns = Math.Clamp(maxColumns, 10, 500)
        };

        _logService.Info($"stream start: sesja={sessionId} efekt={effect} step={settings.Step} kolor={colorMode}");

        int count = 0;
        await foreach (var frame in _videoService.StreamFramesAsync(sessionId, settings, cancellationToken))
        {
            yield return new
            {
                text = frame.Text,
                columns = frame.Columns,
                rows = frame.Rows,
                colors = frame.ColorRgb != null ? Convert.ToBase64String(frame.ColorRgb) : (string?)null,
                frameNumber = frame.FrameNumber,
                totalFrames = frame.TotalFrames
            };
            count++;
        }

        _logService.Info($"stream done: sesja={sessionId} klatek={count}");
    }

    public async Task GetEffects()
    {
        var effects = EffectRegistry.List()
            .Select(e => new { name = e.Name, description = e.Description });
        await Clients.Caller.SendAsync("EffectsList", effects);
    }
}
