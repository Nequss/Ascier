using AscierWeb.Core;
using AscierWeb.Services;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<LogService>();
builder.Services.AddSingleton<ImageService>();
builder.Services.AddSingleton<VideoService>();

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 2 * 1024 * 1024;
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024;
});

// limity
const long MaxVideoSize = 50 * 1024 * 1024;  // 50MB
const long MaxImageSize = 20 * 1024 * 1024;  // 20MB
const double MaxVideoDuration = 60.0;         // 60s
const int MaxVideoResolution = 1280;          // px szerokości

var app = builder.Build();

// broadcast logów do klientów signalr
var logService = app.Services.GetRequiredService<LogService>();
var hubContext = app.Services.GetRequiredService<IHubContext<ConversionHub>>();
logService.OnLog += (entry) =>
{
    _ = hubContext.Clients.Group("logs").SendAsync("NewLog", new
    {
        timestamp = entry.Timestamp.ToString("HH:mm:ss.fff"),
        level = entry.Level,
        message = entry.Message
    });
};

logService.Info("ascier web uruchomiony");

app.UseDefaultFiles();
app.UseStaticFiles();

// lista efektów
app.MapGet("/api/effects", () =>
{
    return Results.Ok(EffectRegistry.List().Select(e => new { e.Name, e.Description }));
});

// ostatnie logi
app.MapGet("/api/logs", (LogService logs) =>
{
    var recent = logs.GetRecent(100);
    return Results.Ok(recent.Select(e => new
    {
        timestamp = e.Timestamp.ToString("HH:mm:ss.fff"),
        level = e.Level,
        message = e.Message
    }));
});

// konwersja obrazu
app.MapPost("/api/convert/image", async (HttpRequest request, ImageService imageService, LogService logs) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest("wymagany content-type: multipart/form-data");

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");

    if (file == null || file.Length == 0)
        return Results.BadRequest("brak pliku w żądaniu");

    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    var allowedImageExts = new HashSet<string> { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff" };

    if (!allowedImageExts.Contains(ext))
        return Results.BadRequest("nieobsługiwany format obrazu");

    if (file.Length > MaxImageSize)
        return Results.BadRequest($"plik za duży ({(file.Length / 1048576.0):F1}MB). limit: {MaxImageSize / 1048576}MB");

    var settings = ParseSettings(form);
    var sizeMb = (file.Length / 1048576.0).ToString("F2");
    logs.Info($"konwersja obrazu: {file.FileName} ({sizeMb}MB) efekt={settings.Effect} step={settings.Step}");

    var sw = System.Diagnostics.Stopwatch.StartNew();

    try
    {
        using var stream = file.OpenReadStream();
        var frame = await imageService.ConvertAsync(stream, settings);
        sw.Stop();

        string? colorsBase64 = frame.ColorRgb != null
            ? Convert.ToBase64String(frame.ColorRgb)
            : null;

        logs.Info($"gotowe: {frame.Columns}x{frame.Rows} ({(frame.Columns * frame.Rows).ToString("N0")} znaków) {sw.ElapsedMilliseconds}ms");

        return Results.Ok(new
        {
            text = frame.Text,
            columns = frame.Columns,
            rows = frame.Rows,
            colors = colorsBase64,
            frameNumber = 0,
            totalFrames = 1
        });
    }
    catch (Exception ex)
    {
        logs.Error($"błąd konwersji obrazu: {ex.Message}");
        return Results.Problem("błąd przetwarzania obrazu");
    }
});

// upload wideo i utworzenie sesji
app.MapPost("/api/convert/video", async (HttpRequest request, VideoService videoService, LogService logs) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest("wymagany content-type: multipart/form-data");

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");

    if (file == null || file.Length == 0)
        return Results.BadRequest("brak pliku w żądaniu");

    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    var allowedVideoExts = new HashSet<string> { ".mp4", ".avi", ".mkv", ".webm", ".mov", ".flv", ".wmv" };

    if (!allowedVideoExts.Contains(ext))
        return Results.BadRequest("nieobsługiwany format wideo");

    if (file.Length > MaxVideoSize)
        return Results.BadRequest($"plik za duży ({(file.Length / 1048576.0):F1}MB). limit: {MaxVideoSize / 1048576}MB");

    var sizeMb = (file.Length / 1048576.0).ToString("F2");
    logs.Info($"upload wideo: {file.FileName} ({sizeMb}MB)");

    try
    {
        using var stream = file.OpenReadStream();
        var session = await videoService.CreateSessionAsync(stream, file.FileName);

        if (session.Duration > MaxVideoDuration)
        {
            logs.Warn($"wideo za długie: {session.Duration:F1}s (limit: {MaxVideoDuration}s)");
            return Results.BadRequest($"wideo za długie ({session.Duration:F1}s). limit: {MaxVideoDuration}s");
        }

        logs.Info($"sesja wideo: {session.Id} {session.Width}x{session.Height} (efektywne: {session.EffectiveWidth}x{session.EffectiveHeight}) {session.Fps:F1}fps {session.Duration:F1}s ({session.TotalFrames} klatek)");

        return Results.Ok(new
        {
            sessionId = session.Id,
            width = session.Width,
            height = session.Height,
            effectiveWidth = session.EffectiveWidth,
            effectiveHeight = session.EffectiveHeight,
            fps = session.Fps,
            duration = session.Duration,
            totalFrames = session.TotalFrames
        });
    }
    catch (Exception ex)
    {
        logs.Error($"błąd uploadu wideo: {ex.Message}");
        return Results.Problem("błąd przetwarzania wideo");
    }
});

// pobranie klatki wideo
app.MapPost("/api/convert/frame", async (HttpRequest request, VideoService videoService, LogService logs) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest("wymagany content-type: multipart/form-data");

    var form = await request.ReadFormAsync();
    var sessionId = form["sessionId"].FirstOrDefault();
    var frameStr = form["frameNumber"].FirstOrDefault();

    if (string.IsNullOrEmpty(sessionId))
        return Results.BadRequest("brak sessionId");

    int frameNumber = 0;
    if (!string.IsNullOrEmpty(frameStr))
        int.TryParse(frameStr, out frameNumber);

    var settings = ParseSettings(form);

    try
    {
        var frame = await videoService.GetFrameAsync(sessionId, frameNumber, settings);
        if (frame == null)
        {
            logs.Warn($"klatka niedostępna: sesja={sessionId} klatka={frameNumber}");
            return Results.NotFound("sesja nie znaleziona lub klatka niedostępna");
        }

        string? colorsBase64 = frame.ColorRgb != null
            ? Convert.ToBase64String(frame.ColorRgb)
            : null;

        return Results.Ok(new
        {
            text = frame.Text,
            columns = frame.Columns,
            rows = frame.Rows,
            colors = colorsBase64,
            frameNumber = frame.FrameNumber,
            totalFrames = frame.TotalFrames
        });
    }
    catch (Exception ex)
    {
        logs.Error($"błąd klatki: sesja={sessionId} klatka={frameNumber} {ex.Message}");
        return Results.Problem("błąd przetwarzania klatki");
    }
});

// monitoring - metryki serwera i usage
app.MapGet("/api/status", (VideoService videoService) =>
{
    var proc = System.Diagnostics.Process.GetCurrentProcess();
    return Results.Ok(new
    {
        uptime = (DateTime.UtcNow - proc.StartTime.ToUniversalTime()).ToString(@"d\.hh\:mm\:ss"),
        activeSessions = videoService.ActiveSessions,
        totalSessions = videoService.TotalSessionsCreated,
        totalFrames = videoService.TotalFramesProcessed,
        memoryMb = Math.Round(proc.WorkingSet64 / 1048576.0, 1),
        gcMemoryMb = Math.Round(GC.GetTotalMemory(false) / 1048576.0, 1),
        cpuSeconds = Math.Round(proc.TotalProcessorTime.TotalSeconds, 1),
        threads = proc.Threads.Count,
        gcCollections = new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) }
    });
});

// limity dostępne dla frontendu
app.MapGet("/api/limits", () => Results.Ok(new
{
    maxVideoSizeMb = MaxVideoSize / 1048576,
    maxImageSizeMb = MaxImageSize / 1048576,
    maxVideoDurationS = MaxVideoDuration,
    maxVideoResolution = MaxVideoResolution
}));

app.MapHub<ConversionHub>("/hub/conversion");

app.Run();

// parsowanie ustawień konwersji z form data
static ConversionSettings ParseSettings(IFormCollection form)
{
    var settings = new ConversionSettings();

    if (form.TryGetValue("effect", out var effect))
        settings.Effect = effect.FirstOrDefault() ?? "classic";

    if (form.TryGetValue("step", out var step) && int.TryParse(step.FirstOrDefault(), out int s))
        settings.Step = Math.Clamp(s, 1, 64);

    if (form.TryGetValue("colorMode", out var color))
        settings.ColorMode = color.FirstOrDefault() == "true";

    if (form.TryGetValue("threshold", out var thresh) && int.TryParse(thresh.FirstOrDefault(), out int t))
        settings.Threshold = Math.Clamp(t, 0, 255);

    if (form.TryGetValue("invert", out var inv))
        settings.Invert = inv.FirstOrDefault() == "true";

    if (form.TryGetValue("maxColumns", out var maxCols) && int.TryParse(maxCols.FirstOrDefault(), out int mc))
        settings.MaxColumns = Math.Clamp(mc, 10, 500);

    return settings;
}
