using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VideoMixer.Models;

public enum TextStyle { Normal, Bold, Shadow, Outline }

public class VideoTemplate : INotifyPropertyChanged
{
    private string    _text      = "";
    private double    _positionX = 0.5;   // 0=gauche … 1=droite  (centre du bloc)
    private double    _positionY = 0.85;  // 0=haut  … 1=bas     (centre du bloc)
    private int       _fontSize  = 52;
    private string    _color     = "#FFFFFF";
    private TextStyle _style     = TextStyle.Outline;

    public Guid Id { get; } = Guid.NewGuid();

    public string Text
    {
        get => _text;
        set { _text = value; OnPropertyChanged(); OnPropertyChanged(nameof(Preview)); }
    }
    public double PositionX
    {
        get => _positionX;
        set { _positionX = Math.Clamp(value, 0, 1); OnPropertyChanged(); }
    }
    public double PositionY
    {
        get => _positionY;
        set { _positionY = Math.Clamp(value, 0, 1); OnPropertyChanged(); }
    }
    public int FontSize
    {
        get => _fontSize;
        set { _fontSize = Math.Max(8, value); OnPropertyChanged(); }
    }
    public string Color
    {
        get => _color;
        set { _color = value; OnPropertyChanged(); }
    }
    public TextStyle Style
    {
        get => _style;
        set { _style = value; OnPropertyChanged(); }
    }

    public string Preview => Text.Length > 60 ? Text[..60] + "…" : Text;

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
