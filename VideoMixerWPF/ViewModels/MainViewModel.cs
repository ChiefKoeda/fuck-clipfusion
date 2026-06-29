using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using VideoMixer.Models;
using VideoMixer.Services;

namespace VideoMixer.ViewModels;

// ── RelayCommand ──────────────────────────────────────────────────────────────
public class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add    { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }
    public bool CanExecute(object? p) => canExecute?.Invoke(p) ?? true;
    public void Execute(object? p) => execute(p);
}

// ── MainViewModel ─────────────────────────────────────────────────────────────
public class MainViewModel : INotifyPropertyChanged
{
    // ── Collections ──────────────────────────────────────────────────────────
    public ObservableCollection<VideoTemplate> Templates { get; } = [];
    public ObservableCollection<string>        Videos    { get; } = [];
    public ObservableCollection<MixJob>        Jobs      { get; } = [];

    // ── Template en cours d'édition / création ───────────────────────────────
    private string    _newText   = "";
    private double    _textXPct  = 0.5;
    private double    _textYPct  = 0.85;
    private int       _newSize   = 52;
    private string    _newColor  = "#FFFFFF";
    private TextStyle _newStyle  = TextStyle.Outline;
    private Guid?     _editingId = null;

    public string    NewText  { get => _newText;  set { _newText  = value; OnPropertyChanged(); SchedulePreviewUpdate(); } }
    public double    TextXPct { get => _textXPct; set { _textXPct = Math.Clamp(value,0,1); OnPropertyChanged(); SchedulePreviewUpdate(); } }
    public double    TextYPct { get => _textYPct; set { _textYPct = Math.Clamp(value,0,1); OnPropertyChanged(); SchedulePreviewUpdate(); } }
    public int       NewSize  { get => _newSize;  set { _newSize  = Math.Max(8,value); OnPropertyChanged(); OnPropertyChanged(nameof(PreviewFontSize)); SchedulePreviewUpdate(); } }
    public string    NewColor { get => _newColor; set { _newColor = value; OnPropertyChanged(); SchedulePreviewUpdate(); } }
    public TextStyle NewStyle { get => _newStyle; set { _newStyle = value; OnPropertyChanged(); SchedulePreviewUpdate(); } }
    public bool      IsEditing => _editingId.HasValue;

    // ── Preview ───────────────────────────────────────────────────────────────
    private double _previewScale = 0.28;
    public double PreviewScale
    {
        get => _previewScale;
        set { _previewScale = value; OnPropertyChanged(nameof(PreviewFontSize)); }
    }
    public double PreviewFontSize => Math.Max(6, NewSize * _previewScale);

    public Array Styles => Enum.GetValues<TextStyle>();

    // ── Dossier vidéos ────────────────────────────────────────────────────────
    private string _videoFolder = "";
    public string VideoFolder
    {
        get => _videoFolder;
        set { _videoFolder = value; OnPropertyChanged(); }
    }

    // ── Musique ───────────────────────────────────────────────────────────────
    private string? _musicPath;
    private double  _musicVolume = 50;
    private double  _origVolume  = 30;

    public string? MusicPath
    {
        get => _musicPath;
        set { _musicPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(MusicName)); OnPropertyChanged(nameof(HasMusic)); }
    }
    public double MusicVolume
    {
        get => _musicVolume;
        set { _musicVolume = value; OnPropertyChanged(); }
    }
    public double OrigVolume
    {
        get => _origVolume;
        set { _origVolume = value; OnPropertyChanged(); }
    }
    public string MusicName => MusicPath != null ? Path.GetFileName(MusicPath) : "Aucune";
    public bool   HasMusic  => MusicPath != null;

    // ── Mix ───────────────────────────────────────────────────────────────────
    private string _outFolder    = "";
    private string _outPrefix    = "mix_";
    private int    _mixCount     = 10;
    private bool   _isMixing     = false;
    private double _progress     = 0;
    private int    _doneJobs     = 0;
    private string _logText      = "";
    private string _codec        = "libx264";
    private int    _crf          = 23;
    private int    _threads      = 0;
    private string _ffmpegPath   = FindFFmpeg();
    private int    _parallelJobs = Math.Max(1, Environment.ProcessorCount / 2);
    private bool   _useHWAccel   = false;
    private bool   _showResults  = false;

    public string OutFolder    { get => _outFolder;    set { _outFolder    = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanMix)); } }
    public string OutPrefix    { get => _outPrefix;    set { _outPrefix    = value; OnPropertyChanged(); } }
    public int    MixCount     { get => _mixCount;     set { _mixCount     = value; OnPropertyChanged(); OnPropertyChanged(nameof(MaxCombos)); } }
    public bool   IsMixing     { get => _isMixing;     set { _isMixing     = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanMix)); } }
    public double Progress     { get => _progress;     set { _progress     = value; OnPropertyChanged(); } }
    public int    DoneJobs     { get => _doneJobs;     set { _doneJobs     = value; OnPropertyChanged(); } }
    public string LogText      { get => _logText;      set { _logText      = value; OnPropertyChanged(); } }
    public string Codec        { get => _codec;        set { _codec        = value; OnPropertyChanged(); } }
    public int    CRF          { get => _crf;          set { _crf          = value; OnPropertyChanged(); } }
    public int    Threads      { get => _threads;      set { _threads      = value; OnPropertyChanged(); } }
    public string FFmpegPath   { get => _ffmpegPath;   set { _ffmpegPath   = value; OnPropertyChanged(); } }
    public int    ParallelJobs { get => _parallelJobs; set { _parallelJobs = Math.Max(1, value); OnPropertyChanged(); } }
    public bool   UseHWAccel   { get => _useHWAccel;   set { _useHWAccel   = value; OnPropertyChanged(); } }
    public bool   ShowResults  { get => _showResults;  set { _showResults  = value; OnPropertyChanged(); } }

    // ── Prévisualisation ──────────────────────────────────────────────────────
    private string? _previewFramePath;
    private string? _previewOverlayPath;
    private System.Threading.Timer? _previewDebounce;
    private int _previewCanvasW = 300;
    private int _previewCanvasH = 533;

    // Métriques de la zone vidéo dans le canvas (pour positionner le drag handle)
    public int PreviewVideoX { get; private set; } = 0;
    public int PreviewVideoY { get; private set; } = 0;
    public int PreviewVideoW { get; private set; } = 300;
    public int PreviewVideoH { get; private set; } = 533;

    public string? PreviewFramePath
    {
        get => _previewFramePath;
        set { _previewFramePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasPreview)); SchedulePreviewUpdate(); }
    }
    public string? PreviewOverlayPath
    {
        get => _previewOverlayPath;
        set { _previewOverlayPath = value; OnPropertyChanged(); }
    }
    // La préview composite inclut déjà la frame — HasPreview reflète juste si on a des données
    public bool HasPreview => !string.IsNullOrEmpty(PreviewFramePath) && File.Exists(PreviewFramePath);

    public void SetPreviewCanvasSize(int w, int h)
    {
        if (w < 10 || h < 10) return;
        _previewCanvasW = w;
        _previewCanvasH = h;
        SchedulePreviewUpdate();
    }

    private void SchedulePreviewUpdate()
    {
        _previewDebounce?.Dispose();
        _previewDebounce = new System.Threading.Timer(_ =>
            Application.Current.Dispatcher.BeginInvoke(RenderPreviewOverlay),
            null, 120, System.Threading.Timeout.Infinite);
    }

    private void RenderPreviewOverlay()
    {
        if (string.IsNullOrWhiteSpace(NewText)) { PreviewOverlayPath = null; return; }
        try
        {
            var tpl = new VideoMixer.Models.VideoTemplate
            {
                Text      = NewText,
                PositionX = TextXPct,
                PositionY = TextYPct,
                FontSize  = NewSize,
                Color     = NewColor,
                Style     = NewStyle,
            };

            // Nettoyer l'ancien fichier
            if (!string.IsNullOrEmpty(_previewOverlayPath) && File.Exists(_previewOverlayPath))
                try { File.Delete(_previewOverlayPath); } catch { }

            // Composite : frame vidéo + texte (garantit fidélité parfaite)
            var (path, vx, vy, vw, vh) = VideoMixer.Services.TextOverlayRenderer.RenderPreview(
                tpl, _previewCanvasW, _previewCanvasH,
                Path.GetTempPath(), PreviewFramePath);

            PreviewVideoX = vx;
            PreviewVideoY = vy;
            PreviewVideoW = vw > 0 ? vw : _previewCanvasW;
            PreviewVideoH = vh > 0 ? vh : _previewCanvasH;

            PreviewOverlayPath = path;
        }
        catch { }
    }

    public bool   CanMix    => !IsMixing && Templates.Count > 0 && Videos.Count > 0 && !string.IsNullOrEmpty(OutFolder);
    public int    MaxCombos => Templates.Count * Videos.Count;
    public int    CPUCores  => Environment.ProcessorCount;
    public string[] CodecOptions => ["libx264", "libx265"];

    // ── Commandes ─────────────────────────────────────────────────────────────
    public ICommand AddTemplateCmd     { get; }
    public ICommand DeleteTemplateCmd  { get; }
    public ICommand EditTemplateCmd    { get; }
    public ICommand CancelEditCmd      { get; }
    public ICommand PickVideoFolderCmd { get; }
    public ICommand ScanVideosCmd      { get; }
    public ICommand PickMusicCmd       { get; }
    public ICommand RemoveMusicCmd     { get; }
    public ICommand PickOutFolderCmd   { get; }
    public ICommand StartMixCmd        { get; }

    private readonly MixerService _mixer = new();
    private readonly Random _rng = new();

    public MainViewModel()
    {
        AddTemplateCmd     = new RelayCommand(_ => AddTemplate());
        DeleteTemplateCmd  = new RelayCommand(o => { if (o is VideoTemplate t) { Templates.Remove(t); if (_editingId == t.Id) CancelEditImpl(); } });
        EditTemplateCmd    = new RelayCommand(o => { if (o is VideoTemplate t) StartEdit(t); });
        CancelEditCmd      = new RelayCommand(_ => CancelEditImpl());
        PickVideoFolderCmd = new RelayCommand(_ => PickVideoFolder());
        ScanVideosCmd      = new RelayCommand(_ => ScanVideos());
        PickMusicCmd       = new RelayCommand(_ => PickMusic());
        RemoveMusicCmd     = new RelayCommand(_ => MusicPath = null);
        PickOutFolderCmd   = new RelayCommand(_ => PickOutFolder());
        StartMixCmd        = new RelayCommand(_ => _ = StartMixAsync(), _ => CanMix);

        Templates.CollectionChanged += (_, _) => { OnPropertyChanged(nameof(MaxCombos)); OnPropertyChanged(nameof(CanMix)); };
        Videos.CollectionChanged    += (_, _) => { OnPropertyChanged(nameof(MaxCombos)); OnPropertyChanged(nameof(CanMix)); };

        _mixer.LogLine += line => AppendLog(line);

        LoadSettings();
    }

    // ── Sauvegarde / Chargement ───────────────────────────────────────────────
    public void SaveSettings()
    {
        var s = new AppSettings
        {
            FFmpegPath   = FFmpegPath,
            Codec        = Codec,
            CRF          = CRF,
            Threads      = Threads,
            ParallelJobs = ParallelJobs,
            UseHWAccel   = UseHWAccel,
            MixCount     = MixCount,
            OutFolder    = OutFolder,
            OutPrefix    = OutPrefix,
            MusicVolume  = MusicVolume,
            OrigVolume   = OrigVolume,
            MusicPath    = MusicPath,
            VideoFolder  = VideoFolder,
            Templates    = Templates.Select(t => new TemplateData
            {
                Text      = t.Text,
                PositionX = t.PositionX,
                PositionY = t.PositionY,
                FontSize  = t.FontSize,
                Color     = t.Color,
                Style     = t.Style.ToString(),
            }).ToList(),
        };
        SettingsService.Save(s);
    }

    private void LoadSettings()
    {
        var s = SettingsService.Load();

        FFmpegPath   = string.IsNullOrEmpty(s.FFmpegPath) ? FindFFmpeg() : s.FFmpegPath;
        Codec        = s.Codec;
        CRF          = s.CRF;
        Threads      = s.Threads;
        ParallelJobs = s.ParallelJobs;
        UseHWAccel   = s.UseHWAccel;
        MixCount     = s.MixCount;
        OutFolder    = s.OutFolder;
        OutPrefix    = s.OutPrefix;
        MusicVolume  = s.MusicVolume;
        OrigVolume   = s.OrigVolume;
        MusicPath    = s.MusicPath;
        VideoFolder  = s.VideoFolder;

        foreach (var t in s.Templates)
        {
            Templates.Add(new VideoTemplate
            {
                Text      = t.Text,
                PositionX = t.PositionX,
                PositionY = t.PositionY,
                FontSize  = t.FontSize,
                Color     = t.Color,
                Style     = SettingsService.ParseStyle(t.Style),
            });
        }

        // Rescanner les vidéos si le dossier existe encore
        if (!string.IsNullOrEmpty(VideoFolder) && Directory.Exists(VideoFolder))
        {
            var exts = new[] { ".mp4", ".mov", ".avi", ".mkv", ".webm" };
            foreach (var f in Directory.EnumerateFiles(VideoFolder)
                                       .Where(f => exts.Contains(Path.GetExtension(f).ToLower())))
                Videos.Add(f);

            if (Videos.Count > 0)
                _ = ExtractPreviewFrameAsync(Videos[0]);
        }
    }

    // ── Détection FFmpeg ──────────────────────────────────────────────────────
    private static string FindFFmpeg()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("ffmpeg", "-version")
                { RedirectStandardOutput = true, RedirectStandardError = true,
                  UseShellExecute = false, CreateNoWindow = true });
            p?.Kill();
            return "ffmpeg";
        }
        catch { }

        string[] candidates =
        [
            @"C:\ffmpeg\bin\ffmpeg.exe",
            @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
            @"C:\Program Files (x86)\ffmpeg\bin\ffmpeg.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ffmpeg", "bin", "ffmpeg.exe"),
        ];
        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        return "ffmpeg";
    }

    // ── Template ──────────────────────────────────────────────────────────────
    private void AddTemplate()
    {
        if (string.IsNullOrWhiteSpace(NewText))
        {
            MessageBox.Show("Entre un texte pour le template.", "FUCK CLIPFUSION", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_editingId.HasValue)
        {
            var existing = Templates.FirstOrDefault(t => t.Id == _editingId.Value);
            if (existing != null)
            {
                existing.Text      = NewText;
                existing.PositionX = TextXPct;
                existing.PositionY = TextYPct;
                existing.FontSize  = NewSize;
                existing.Color     = NewColor;
                existing.Style     = NewStyle;
            }
            CancelEditImpl();
        }
        else
        {
            Templates.Add(new VideoTemplate
            {
                Text      = NewText,
                PositionX = TextXPct,
                PositionY = TextYPct,
                FontSize  = NewSize,
                Color     = NewColor,
                Style     = NewStyle,
            });
            ResetForm();
        }
    }

    private void StartEdit(VideoTemplate tpl)
    {
        _editingId = tpl.Id;
        NewText    = tpl.Text;
        TextXPct   = tpl.PositionX;
        TextYPct   = tpl.PositionY;
        NewSize    = tpl.FontSize;
        NewColor   = tpl.Color;
        NewStyle   = tpl.Style;
        OnPropertyChanged(nameof(IsEditing));
    }

    private void CancelEditImpl()
    {
        _editingId = null;
        OnPropertyChanged(nameof(IsEditing));
        ResetForm();
    }

    private void ResetForm()
    {
        NewText  = "";
        TextXPct = 0.5;
        TextYPct = 0.85;
        NewSize  = 52;
        NewColor = "#FFFFFF";
        NewStyle = TextStyle.Outline;
    }

    // ── Vidéos ────────────────────────────────────────────────────────────────
    private void PickVideoFolder()
    {
        var dlg = new OpenFolderDialog { Title = "Sélectionne le dossier des vidéos brutes" };
        if (dlg.ShowDialog() == true)
            VideoFolder = dlg.FolderName;
    }

    private void ScanVideos()
    {
        if (string.IsNullOrEmpty(VideoFolder) || !Directory.Exists(VideoFolder))
        {
            MessageBox.Show("Sélectionne un dossier valide.", "FUCK CLIPFUSION", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var exts = new[] { ".mp4", ".mov", ".avi", ".mkv", ".webm" };
        Videos.Clear();
        foreach (var f in Directory.EnumerateFiles(VideoFolder)
                                   .Where(f => exts.Contains(Path.GetExtension(f).ToLower())))
            Videos.Add(f);

        MessageBox.Show($"{Videos.Count} vidéo(s) trouvée(s) !", "FUCK CLIPFUSION",
            MessageBoxButton.OK, Videos.Count > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);

        if (Videos.Count > 0)
            _ = ExtractPreviewFrameAsync(Videos[0]);
    }

    // ── Extraction frame de prévisualisation ─────────────────────────────────
    private async Task ExtractPreviewFrameAsync(string videoPath)
    {
        try
        {
            var outPath = Path.Combine(Path.GetTempPath(), $"preview_{Guid.NewGuid():N}.jpg");
            var args    = $"-y -i \"{videoPath}\" -vf \"select=eq(n\\,0)\" -vframes 1 -q:v 3 \"{outPath}\"";
            var psi     = new ProcessStartInfo
            {
                FileName               = FFmpegPath,
                Arguments              = args,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardError  = true,
                RedirectStandardOutput = true,
            };
            using var p = Process.Start(psi)!;
            await p.WaitForExitAsync();
            if (File.Exists(outPath))
                PreviewFramePath = outPath;
        }
        catch { }
    }

    // ── Musique ───────────────────────────────────────────────────────────────
    private void PickMusic()
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Sélectionne un fichier audio",
            Filter = "Audio|*.mp3;*.wav;*.m4a;*.aac|Tous|*.*"
        };
        if (dlg.ShowDialog() == true)
            MusicPath = dlg.FileName;
    }

    // ── Dossier sortie ────────────────────────────────────────────────────────
    private void PickOutFolder()
    {
        var dlg = new OpenFolderDialog { Title = "Sélectionne le dossier de sortie" };
        if (dlg.ShowDialog() == true)
        {
            OutFolder = dlg.FolderName;
            OnPropertyChanged(nameof(CanMix));
        }
    }

    // ── Mix ───────────────────────────────────────────────────────────────────
    private async Task StartMixAsync()
    {
        if (!CanMix) return;

        IsMixing    = true;
        ShowResults = true;
        Jobs.Clear();
        DoneJobs = 0;
        Progress = 0;
        LogText  = "";

        _mixer.FFmpegPath  = FFmpegPath;
        _mixer.MusicPath   = MusicPath;
        _mixer.MusicVolume = MusicVolume / 100.0;
        _mixer.OrigVolume  = OrigVolume  / 100.0;
        _mixer.Codec       = Codec;
        _mixer.CRF         = CRF;
        _mixer.Threads     = Threads;
        _mixer.UseHWAccel  = UseHWAccel;

        for (int i = 0; i < MixCount; i++)
        {
            var tpl = Templates[_rng.Next(Templates.Count)];
            var vid = Videos[_rng.Next(Videos.Count)];
            var num = (i + 1).ToString().PadLeft(3, '0');
            Jobs.Add(new MixJob
            {
                Index      = i,
                Template   = tpl,
                VideoPath  = vid,
                OutputPath = Path.Combine(OutFolder, $"{OutPrefix}{num}.mp4"),
                Status     = JobStatus.Pending,
            });
        }

        var cts       = new CancellationTokenSource();
        int errCount  = 0;
        int doneCount = 0;
        var sem       = new SemaphoreSlim(ParallelJobs, ParallelJobs);

        var tasks = Jobs.ToList().Select(async job =>
        {
            await sem.WaitAsync(cts.Token);
            try
            {
                _ = Application.Current.Dispatcher.BeginInvoke(() => job.Status = JobStatus.Running);
                AppendLog($"▶ [{job.Index + 1}/{MixCount}]  {job.VideoName} → {job.OutputName}");

                bool ok;
                try   { ok = await Task.Run(() => _mixer.ProcessJobAsync(job, cts.Token), cts.Token); }
                catch (Exception ex) { job.Message = ex.Message; ok = false; }

                if (!ok) Interlocked.Increment(ref errCount);
                int d = Interlocked.Increment(ref doneCount);

                _ = Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    job.Status = ok ? JobStatus.Done : JobStatus.Error;
                    DoneJobs   = d;
                    Progress   = (double)d / MixCount * 100;
                });

                AppendLog(ok ? $"✓ {job.OutputName}" : $"✗ {job.Message}");
            }
            finally { sem.Release(); }
        });

        try   { await Task.WhenAll(tasks); }
        catch { }

        IsMixing = false;
        var msg  = errCount == 0
            ? $"✅ {MixCount} vidéo(s) générée(s) dans :\n{OutFolder}"
            : $"⚠️ Terminé avec {errCount} erreur(s). Voir la console.";

        MessageBox.Show(msg, "FUCK CLIPFUSION", MessageBoxButton.OK,
            errCount == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void AppendLog(string line)
        => Application.Current.Dispatcher.Invoke(() => LogText += line + "\n");

    // ── INotifyPropertyChanged ────────────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
