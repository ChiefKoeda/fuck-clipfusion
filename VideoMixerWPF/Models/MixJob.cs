using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VideoMixer.Models;

public enum JobStatus { Pending, Running, Done, Error }

public class MixJob : INotifyPropertyChanged
{
    private JobStatus _status = JobStatus.Pending;
    private string _message = "";

    public int Index { get; set; }
    public string VideoPath { get; set; } = "";
    public VideoTemplate Template { get; set; } = new();
    public string OutputPath { get; set; } = "";

    public JobStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusIcon)); OnPropertyChanged(nameof(StatusColor)); }
    }

    public string Message
    {
        get => _message;
        set { _message = value; OnPropertyChanged(); }
    }

    public string VideoName => System.IO.Path.GetFileName(VideoPath);
    public string OutputName => System.IO.Path.GetFileName(OutputPath);

    public string StatusIcon => Status switch
    {
        JobStatus.Pending => "⏳",
        JobStatus.Running => "⚙",
        JobStatus.Done    => "✅",
        JobStatus.Error   => "❌",
        _                 => "?"
    };

    public string StatusColor => Status switch
    {
        JobStatus.Running => "#F59E0B",
        JobStatus.Done    => "#22C55E",
        JobStatus.Error   => "#EF4444",
        _                 => "#7C7F9A"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
