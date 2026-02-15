using AscierWeb.Core;
using AscierWeb.Services;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<LogService>();
builder.Services.AddSingleton<ImageService>();
builder.Services.AddSingleton<VideoService>();

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 1024 * 1024;
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 500 * 1024 * 1024;
});

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

    var sizeMb = (file.Length / 1048576.0).ToString("F2");
    logs.Info($"upload wideo: {file.FileName} ({sizeMb}MB)");

    try
    {
        using var stream = file.OpenReadStream();
        var session = await videoService.CreateSessionAsync(stream, file.FileName);

        logs.Info($"sesja wideo: {session.Id} {session.Width}x{session.Height} {session.Fps:F1}fps {session.Duration:F1}s ({session.TotalFrames} klatek)");

        return Results.Ok(new
        {
            sessionId = session.Id,
            width = session.Width,
            height = session.Height,
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

// preload - ekstrakcja wszystkich klatek w jednym przebiegu ffmpeg
app.MapPost("/api/convert/preload", async (HttpRequest request, VideoService videoService, LogService logs) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest("wymagany content-type: multipart/form-data");

    var form = await request.ReadFormAsync();
    var sessionId = form["sessionId"].FirstOrDefault();

    if (string.IsNullOrEmpty(sessionId))
        return Results.BadRequest("brak sessionId");

    var session = videoService.GetSession(sessionId);
    if (session == null)
        return Results.NotFound("sesja nie znaleziona");

    logs.Info($"preload start: sesja={sessionId} ({session.TotalFrames} klatek)");
    var sw = System.Diagnostics.Stopwatch.StartNew();

    int extracted = await videoService.PreloadFramesAsync(sessionId, (done, total) =>
    {
        if (done % 10 == 0 || done == total)
            logs.Info($"preload: {done}/{total} klatek");
    });

    sw.Stop();
    logs.Info($"preload done: {extracted} klatek w {sw.ElapsedMilliseconds}ms");

    return Results.Ok(new { extracted, total = session.TotalFrames });
});

// batch - konwersja wielu klatek naraz (z cache)
app.MapPost("/api/convert/batch", async (HttpRequest request, VideoService videoService, LogService logs) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest("wymagany content-type: multipart/form-data");

    var form = await request.ReadFormAsync();
    var sessionId = form["sessionId"].FirstOrDefault();
    var startStr = form["startFrame"].FirstOrDefault();
    var countStr = form["count"].FirstOrDefault();

    if (string.IsNullOrEmpty(sessionId))
        return Results.BadRequest("brak sessionId");

    int startFrame = 0;
    if (!string.IsNullOrEmpty(startStr))
        int.TryParse(startStr, out startFrame);

    int count = 30;
    if (!string.IsNullOrEmpty(countStr))
        int.TryParse(countStr, out count);

    count = Math.Clamp(count, 1, 120);
    var settings = ParseSettings(form);

    try
    {
        var frames = await videoService.GetFrameBatchAsync(sessionId, startFrame, count, settings);

        var result = frames.Select(f => new
        {
            text = f.Text,
            columns = f.Columns,
            rows = f.Rows,
            colors = f.ColorRgb != null ? Convert.ToBase64String(f.ColorRgb) : (string?)null,
            frameNumber = f.FrameNumber,
            totalFrames = f.TotalFrames
        });

        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        logs.Error($"błąd batch: sesja={sessionId} {ex.Message}");
        return Results.Problem("błąd batch konwersji");
    }
});

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
