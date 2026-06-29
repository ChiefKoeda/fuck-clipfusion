using System.IO;
using System.Text.Json;
using VideoMixer.Models;

namespace VideoMixer.Services;

public class TemplateData
{
    public string Text      { get; set; } = "";
    public double PositionX { get; set; } = 0.5;
    public double PositionY { get; set; } = 0.85;
    public int    FontSize  { get; set; } = 52;
    public string Color     { get; set; } = "#FFFFFF";
    public string Style     { get; set; } = "Outline";
}

public class AppSettings
{
    public string  FFmpegPath   { get; set; } = "ffmpeg";
    public string  Codec        { get; set; } = "libx264";
    public int     CRF          { get; set; } = 23;
    public int     Threads      { get; set; } = 0;
    public int     ParallelJobs { get; set; } = Math.Max(1, Environment.ProcessorCount / 2);
    public bool    UseHWAccel   { get; set; } = false;
    public int     MixCount     { get; set; } = 10;
    public string  OutFolder    { get; set; } = "";
    public string  OutPrefix    { get; set; } = "mix_";
    public double  MusicVolume  { get; set; } = 50;
    public double  OrigVolume   { get; set; } = 30;
    public string? MusicPath    { get; set; }
    public string  VideoFolder  { get; set; } = "";
    public List<TemplateData> Templates { get; set; } = [];
}

public static class SettingsService
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FuckClipfusion", "settings.json");

    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), _opts) ?? new();
        }
        catch { }
        return new();
    }

    public static void Save(AppSettings s)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(s, _opts));
        }
        catch { }
    }

    public static TextStyle ParseStyle(string s)
        => Enum.TryParse<TextStyle>(s, out var v) ? v : TextStyle.Outline;
}
