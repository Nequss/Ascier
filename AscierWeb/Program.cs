using AscierWeb.Core;
using AscierWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// serwisy - singleton bo są bezstanowe (poza video który zarządza sesjami)
builder.Services.AddSingleton<ImageService>();
builder.Services.AddSingleton<VideoService>();

// signalr z limitem wiadomości 1mb (wystarczy na duże ramki ascii)
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 1024 * 1024;
});

// ograniczenie rozmiaru uploadu do 500mb
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 500 * 1024 * 1024;
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// endpoint: lista efektów
app.MapGet("/api/effects", () =>
{
    return Results.Ok(EffectRegistry.List().Select(e => new { e.Name, e.Description }));
});

// endpoint: konwersja obrazu
app.MapPost("/api/convert/image", async (HttpRequest request, ImageService imageService) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest("wymagany content-type: multipart/form-data");

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");

    if (file == null || file.Length == 0)
        return Results.BadRequest("brak pliku w żądaniu");

    // walidacja rozszerzenia
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    var allowedImageExts = new HashSet<string> { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff" };

    if (!allowedImageExts.Contains(ext))
        return Results.BadRequest("nieobsługiwany format obrazu");

    // parsowanie ustawień z form data
    var settings = ParseSettings(form);

    using var stream = file.OpenReadStream();
    var frame = await imageService.ConvertAsync(stream, settings);

    string? colorsBase64 = frame.ColorRgb != null
        ? Convert.ToBase64String(frame.ColorRgb)
        : null;

    return Results.Ok(new
    {
        text = frame.Text,
        columns = frame.Columns,
        rows = frame.Rows,
        colors = colorsBase64,
        frameNumber = 0,
        totalFrames = 1
    });
});

// endpoint: upload wideo i utworzenie sesji
app.MapPost("/api/convert/video", async (HttpRequest request, VideoService videoService) =>
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

    using var stream = file.OpenReadStream();
    var session = await videoService.CreateSessionAsync(stream, file.FileName);

    return Results.Ok(new
    {
        sessionId = session.Id,
        width = session.Width,
        height = session.Height,
        fps = session.Fps,
        duration = session.Duration,
        totalFrames = session.TotalFrames
    });
});

// endpoint: pobranie klatki wideo
app.MapPost("/api/convert/frame", async (HttpRequest request, VideoService videoService, ImageService imageService) =>
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

    var frame = await videoService.GetFrameAsync(sessionId, frameNumber, settings);
    if (frame == null)
        return Results.NotFound("sesja nie znaleziona lub klatka niedostępna");

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
});

// endpoint: pobranie ascii jako plik tekstowy
app.MapPost("/api/download/text", async (HttpRequest request, ImageService imageService) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest("wymagany content-type: multipart/form-data");

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");

    if (file == null || file.Length == 0)
        return Results.BadRequest("brak pliku w żądaniu");

    var settings = ParseSettings(form);

    using var stream = file.OpenReadStream();
    var frame = await imageService.ConvertAsync(stream, settings);

    var bytes = System.Text.Encoding.UTF8.GetBytes(frame.Text);
    return Results.File(bytes, "text/plain", "ascii_art.txt");
});

// hub signalr do streamingu klatek w czasie rzeczywistym
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
