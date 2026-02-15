using System.Buffers;
using System.Diagnostics;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace AscierWeb.Services;

// serwis konwersji wideo na ascii
// real-time streaming: ffmpeg pipe → ascii konwersja → signalr do klienta
// cache klatek raw rgb24 na dysku dla seekowania i re-stream z innymi ustawieniami
public sealed class VideoService : IDisposable
{
    private readonly ImageService _imageService;
    private readonly string _tempRoot;
    private readonly ConcurrentDictionary<string, VideoSession> _sessions = new();
    private readonly Timer _cleanupTimer;

    private const int MaxProcessingWidth = 1280;

    // metryki wydajności
    private long _totalFramesProcessed;
    private long _totalSessionsCreated;

    public long TotalFramesProcessed => Interlocked.Read(ref _totalFramesProcessed);
    public long TotalSessionsCreated => Interlocked.Read(ref _totalSessionsCreated);
    public int ActiveSessions => _sessions.Count;

    public VideoService(ImageService imageService)
    {
        _imageService = imageService;
        _tempRoot = Path.Combine(Path.GetTempPath(), "ascier_video");
        Directory.CreateDirectory(_tempRoot);
        _cleanupTimer = new Timer(_ => CleanupSessions(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public async Task<VideoSession> CreateSessionAsync(Stream videoStream, string fileName)
    {
        string sessionId = Guid.NewGuid().ToString("N")[..12];
        string sessionDir = Path.Combine(_tempRoot, sessionId);
        Directory.CreateDirectory(sessionDir);

        string videoPath = Path.Combine(sessionDir, SanitizeFileName(fileName));
        using (var fs = File.Create(videoPath))
        {
            await videoStream.CopyToAsync(fs);
        }

        var probeInfo = await ProbeVideoAsync(videoPath);

        // skalowanie - max 1280px szerokości, utrzymanie proporcji
        int effW = probeInfo.Width;
        int effH = probeInfo.Height;
        if (effW > MaxProcessingWidth)
        {
            effH = (int)Math.Round((double)probeInfo.Height * MaxProcessingWidth / probeInfo.Width);
            if (effH % 2 != 0) effH++; // ffmpeg wymaga parzystej wysokości
            effW = MaxProcessingWidth;
        }

        var session = new VideoSession
        {
            Id = sessionId,
            VideoPath = videoPath,
            TempDir = sessionDir,
            Width = probeInfo.Width,
            Height = probeInfo.Height,
            EffectiveWidth = effW,
            EffectiveHeight = effH,
            Fps = probeInfo.Fps,
            Duration = probeInfo.Duration,
            TotalFrames = probeInfo.TotalFrames,
            LastAccess = DateTime.UtcNow
        };

        _sessions[sessionId] = session;
        Interlocked.Increment(ref _totalSessionsCreated);
        return session;
    }

    // streaming klatek - wyciąganie z ffmpeg lub cache, konwersja w locie
    // cache-aware: pierwszy raz z ffmpeg (cache'uje), kolejne z cache (szybka ścieżka)
    public async IAsyncEnumerable<AscierWeb.Core.AsciiFrame> StreamFramesAsync(
        string sessionId,
        AscierWeb.Core.ConversionSettings settings,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            yield break;

        session.LastAccess = DateTime.UtcNow;
        int frameSize = session.EffectiveWidth * session.EffectiveHeight * 3;

        bool fromCache = File.Exists(Path.Combine(session.TempDir, "frame_000000.rgb"));

        if (fromCache)
        {
            // szybka ścieżka - re-konwersja z cache (zmiana efektu/ustawień)
            for (int i = 0; i < session.TotalFrames && !ct.IsCancellationRequested; i++)
            {
                string path = Path.Combine(session.TempDir, $"frame_{i:D6}.rgb");
                if (!File.Exists(path)) break;

                byte[] rgb24 = await File.ReadAllBytesAsync(path, ct);
                var s = new AscierWeb.Core.ConversionSettings
                {
                    Effect = settings.Effect,
                    Step = settings.Step,
                    ColorMode = settings.ColorMode,
                    Threshold = settings.Threshold,
                    Invert = settings.Invert,
                    MaxColumns = settings.MaxColumns,
                    Seed = i
                };

                var frame = _imageService.ConvertRaw(rgb24, session.EffectiveWidth, session.EffectiveHeight, s);
                Interlocked.Increment(ref _totalFramesProcessed);

                yield return frame with { FrameNumber = i, TotalFrames = session.TotalFrames };
            }
        }
        else
        {
            // pierwszy stream - ekstrakcja z ffmpeg + cache na dysk + konwersja
            string scaleFilter = session.EffectiveWidth != session.Width
                ? $"-vf scale={session.EffectiveWidth}:{session.EffectiveHeight} "
                : "";
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-nostdin -i \"{session.VideoPath}\" {scaleFilter}-f rawvideo -pix_fmt rgb24 -v quiet pipe:1",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process? process = null;
            try
            {
                process = Process.Start(psi);
                if (process == null) yield break;

                // drainuj stderr żeby uniknąć deadlocka pipe'a
                _ = process.StandardError.ReadToEndAsync();
                var stdout = process.StandardOutput.BaseStream;
                byte[] buffer = new byte[frameSize];
                int frameNum = 0;
                bool eof = false;

                while (!ct.IsCancellationRequested && frameNum < session.TotalFrames && !eof)
                {
                    int totalRead = 0;
                    while (totalRead < frameSize)
                    {
                        int read = await stdout.ReadAsync(buffer.AsMemory(totalRead, frameSize - totalRead), ct);
                        if (read == 0) { eof = true; break; }
                        totalRead += read;
                    }

                    if (eof || totalRead < frameSize) break;

                    // cache raw rgb24 na dysk dla seekowania
                    string cachePath = Path.Combine(session.TempDir, $"frame_{frameNum:D6}.rgb");
                    await File.WriteAllBytesAsync(cachePath, buffer, ct);

                    // konwersja na ascii w locie
                    var s = new AscierWeb.Core.ConversionSettings
                    {
                        Effect = settings.Effect,
                        Step = settings.Step,
                        ColorMode = settings.ColorMode,
                        Threshold = settings.Threshold,
                        Invert = settings.Invert,
                        MaxColumns = settings.MaxColumns,
                        Seed = frameNum
                    };

                    var frame = _imageService.ConvertRaw(buffer, session.EffectiveWidth, session.EffectiveHeight, s);
                    Interlocked.Increment(ref _totalFramesProcessed);

                    yield return frame with { FrameNumber = frameNum, TotalFrames = session.TotalFrames };

                    frameNum++;
                }

                session.PreloadedFrames = frameNum;
            }
            finally
            {
                if (process != null && !process.HasExited)
                    try { process.Kill(); } catch { }
                process?.Dispose();
            }
        }
    }

    // seekowanie - jednorazowa klatka z cache lub ffmpeg seek
    public async Task<AscierWeb.Core.AsciiFrame?> GetFrameAsync(
        string sessionId,
        int frameNumber,
        AscierWeb.Core.ConversionSettings settings)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return null;

        session.LastAccess = DateTime.UtcNow;
        frameNumber = Math.Clamp(frameNumber, 0, session.TotalFrames - 1);

        string cachePath = Path.Combine(session.TempDir, $"frame_{frameNumber:D6}.rgb");
        byte[] rgb24;

        if (File.Exists(cachePath))
        {
            rgb24 = await File.ReadAllBytesAsync(cachePath);
        }
        else
        {
            rgb24 = await ExtractSingleFrameAsync(session, frameNumber);
            if (rgb24.Length == 0) return null;
            await File.WriteAllBytesAsync(cachePath, rgb24);
        }

        settings.Seed = frameNumber;
        var frame = _imageService.ConvertRaw(rgb24, session.EffectiveWidth, session.EffectiveHeight, settings);
        Interlocked.Increment(ref _totalFramesProcessed);

        return frame with
        {
            FrameNumber = frameNumber,
            TotalFrames = session.TotalFrames
        };
    }

    public VideoSession? GetSession(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    // fallback seek - wolne, używane tylko gdy brak cache
    private async Task<byte[]> ExtractSingleFrameAsync(VideoSession session, int frameNumber)
    {
        double timeSeconds = session.Fps > 0 ? frameNumber / session.Fps : 0;
        int expectedSize = session.EffectiveWidth * session.EffectiveHeight * 3;
        string scaleFilter = session.EffectiveWidth != session.Width
            ? $"-vf scale={session.EffectiveWidth}:{session.EffectiveHeight} "
            : "";

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-nostdin -ss {timeSeconds:F4} -i \"{session.VideoPath}\" " +
                        $"{scaleFilter}-vframes 1 -f rawvideo -pix_fmt rgb24 -v quiet pipe:1",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) return Array.Empty<byte>();

        // drainuj stderr żeby uniknąć deadlocka pipe'a
        _ = process.StandardError.ReadToEndAsync();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(expectedSize);
        int totalRead = 0;

        try
        {
            var stdout = process.StandardOutput.BaseStream;
            while (totalRead < expectedSize)
            {
                int read = await stdout.ReadAsync(buffer, totalRead, expectedSize - totalRead);
                if (read == 0) break;
                totalRead += read;
            }

            await process.WaitForExitAsync();

            if (totalRead < expectedSize)
                return Array.Empty<byte>();

            byte[] result = new byte[expectedSize];
            Buffer.BlockCopy(buffer, 0, result, 0, expectedSize);
            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task<VideoProbeResult> ProbeVideoAsync(string path)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffprobe",
            Arguments = $"-v quiet -print_format json -show_streams -show_format \"{path}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            return new VideoProbeResult();

        string json = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("streams", out var streams))
            {
                foreach (var stream in streams.EnumerateArray())
                {
                    if (stream.TryGetProperty("codec_type", out var type) &&
                        type.GetString() == "video")
                    {
                        int w = stream.TryGetProperty("width", out var wp) ? wp.GetInt32() : 0;
                        int h = stream.TryGetProperty("height", out var hp) ? hp.GetInt32() : 0;

                        double fps = 30;
                        if (stream.TryGetProperty("r_frame_rate", out var fpsStr))
                        {
                            var parts = fpsStr.GetString()?.Split('/');
                            if (parts?.Length == 2 &&
                                double.TryParse(parts[0], out double num) &&
                                double.TryParse(parts[1], out double den) && den > 0)
                            {
                                fps = num / den;
                            }
                        }

                        double duration = 0;
                        if (root.TryGetProperty("format", out var format) &&
                            format.TryGetProperty("duration", out var durProp))
                        {
                            double.TryParse(durProp.GetString(), System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out duration);
                        }

                        if (duration <= 0 && stream.TryGetProperty("duration", out var sdur))
                        {
                            double.TryParse(sdur.GetString(), System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out duration);
                        }

                        int totalFrames = (int)(duration * fps);
                        if (totalFrames <= 0)
                        {
                            if (stream.TryGetProperty("nb_frames", out var nbf) &&
                                int.TryParse(nbf.GetString(), out int nb))
                            {
                                totalFrames = nb;
                            }
                            else
                            {
                                totalFrames = 1;
                            }
                        }

                        return new VideoProbeResult
                        {
                            Width = w,
                            Height = h,
                            Fps = fps,
                            Duration = duration,
                            TotalFrames = totalFrames
                        };
                    }
                }
            }
        }
        catch { }

        return new VideoProbeResult();
    }

    private void CleanupSessions()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-30);
        var expired = _sessions.Where(kv => kv.Value.LastAccess < cutoff).ToList();

        foreach (var (id, session) in expired)
        {
            _sessions.TryRemove(id, out _);
            try
            {
                if (Directory.Exists(session.TempDir))
                    Directory.Delete(session.TempDir, true);
            }
            catch { }
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrEmpty(sanitized) ? "video.mp4" : sanitized;
    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();
        foreach (var (_, session) in _sessions)
        {
            try
            {
                if (Directory.Exists(session.TempDir))
                    Directory.Delete(session.TempDir, true);
            }
            catch { }
        }
    }
}

public sealed class VideoSession
{
    public string Id { get; init; } = "";
    public string VideoPath { get; init; } = "";
    public string TempDir { get; init; } = "";
    public int Width { get; init; }
    public int Height { get; init; }
    public int EffectiveWidth { get; init; }
    public int EffectiveHeight { get; init; }
    public double Fps { get; init; }
    public double Duration { get; init; }
    public int TotalFrames { get; init; }
    public int PreloadedFrames { get; set; }
    public DateTime LastAccess { get; set; }
}

internal struct VideoProbeResult
{
    public int Width;
    public int Height;
    public double Fps;
    public double Duration;
    public int TotalFrames;
}
