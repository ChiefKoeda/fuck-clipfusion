using System.Diagnostics;
using System.IO;
using System.Text;
using VideoMixer.Models;

namespace VideoMixer.Services;

public class MixerService
{
    public string  FFmpegPath  { get; set; } = "ffmpeg";
    public double  MusicVolume { get; set; } = 0.5;
    public double  OrigVolume  { get; set; } = 0.3;
    public string? MusicPath   { get; set; }
    public string  Codec       { get; set; } = "libx264";
    public int     CRF         { get; set; } = 23;
    public int     Threads     { get; set; } = 0;
    public bool    UseHWAccel  { get; set; } = false;

    public event Action<string>? LogLine;

    // ── Traite un job ──────────────────────────────────────────────────────────
    public async Task<bool> ProcessJobAsync(MixJob job, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(job.OutputPath)!);

        // 1. Obtenir les dimensions de la vidéo pour le rendu PNG
        var (vw, vh) = await GetVideoDimensions(job.VideoPath, ct);

        // 2. Générer l'overlay PNG via SkiaSharp (emojis couleur, thread-safe)
        var tempDir     = Path.GetTempPath();
        var overlayPath = TextOverlayRenderer.Render(job.Template, vw, vh, tempDir);

        try
        {
            var args = BuildArgs(job, overlayPath);
            Log($"CMD: {FFmpegPath} {args}");

            var psi = new ProcessStartInfo
            {
                FileName               = FFmpegPath,
                Arguments              = args,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

            var sb = new StringBuilder();
            proc.ErrorDataReceived  += (_, e) => { if (e.Data != null) { Log(e.Data); sb.AppendLine(e.Data); } };
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) Log(e.Data); };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            await proc.WaitForExitAsync(ct);

            if (proc.ExitCode != 0)
            {
                job.Message = sb.ToString()
                    .Split('\n')
                    .LastOrDefault(l => l.Contains("Error") || l.Contains("error"), "Erreur FFmpeg")
                    ?? "Erreur FFmpeg";
                return false;
            }
            return true;
        }
        finally
        {
            // Nettoyer le PNG temporaire
            try { if (File.Exists(overlayPath)) File.Delete(overlayPath); } catch { }
        }
    }

    // ── Obtenir les dimensions vidéo via ffprobe ───────────────────────────────
    private async Task<(int width, int height)> GetVideoDimensions(string path, CancellationToken ct)
    {
        try
        {
            var probe = FFmpegPath.Replace("ffmpeg", "ffprobe");
            if (!File.Exists(probe)) probe = "ffprobe";

            var psi = new ProcessStartInfo
            {
                FileName               = probe,
                Arguments              = $"-v error -select_streams v:0 -show_entries stream=width,height -of csv=p=0 \"{path}\"",
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };

            using var p = Process.Start(psi)!;
            var output  = await p.StandardOutput.ReadToEndAsync(ct);
            await p.WaitForExitAsync(ct);

            var parts = output.Trim().Split(',');
            if (parts.Length >= 2 &&
                int.TryParse(parts[0], out int w) &&
                int.TryParse(parts[1], out int h))
                return (w, h);
        }
        catch { }

        return (1920, 1080); // fallback
    }

    // ── Construit les arguments FFmpeg avec overlay PNG ───────────────────────
    private string BuildArgs(MixJob job, string overlayPath)
    {
        var sb = new StringBuilder();
        sb.Append($"-y -i \"{job.VideoPath}\" ");
        sb.Append($"-i \"{overlayPath}\" ");   // overlay PNG avec transparence

        bool hasMusic = !string.IsNullOrEmpty(MusicPath) && File.Exists(MusicPath);

        if (hasMusic)
        {
            sb.Append($"-i \"{MusicPath}\" ");

            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var audioFilter =
                $"[0:a]volume={OrigVolume.ToString("F2", ic)}[a0];" +
                $"[2:a]volume={MusicVolume.ToString("F2", ic)},aloop=loop=-1:size=2000000000[a1];" +
                $"[a0][a1]amix=inputs=2:duration=first:normalize=0[aout]";

            sb.Append($"-filter_complex \"[0:v][1:v]overlay=0:0[vout];{audioFilter}\" ");
            sb.Append("-map [vout] -map [aout] ");
        }
        else
        {
            // overlay : superpose PNG sur la vidéo
            sb.Append("-filter_complex \"[0:v][1:v]overlay=0:0[vout]\" ");
            sb.Append("-map [vout] -map 0:a? ");
        }

        if (UseHWAccel)
        {
            var hwCodec = Codec == "libx265" ? "hevc_nvenc" : "h264_nvenc";
            sb.Append($"-c:v {hwCodec} -preset p1 -rc vbr -cq {CRF} ");
        }
        else
        {
            sb.Append($"-c:v {Codec} -crf {CRF} -preset ultrafast ");
        }

        sb.Append("-c:a aac -b:a 128k ");

        if (Threads > 0)
            sb.Append($"-threads {Threads} ");
        else if (!UseHWAccel)
            sb.Append($"-threads {Environment.ProcessorCount} ");

        sb.Append($"\"{job.OutputPath}\"");
        return sb.ToString();
    }

    private void Log(string msg) => LogLine?.Invoke(msg);
}
