using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using VideoMixer.Models;

namespace VideoMixer.Converters;

// bool → Visibility
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => v is Visibility.Visible;
}

// bool inverted → Visibility (also handles int: 0=Visible, >0=Collapsed)
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        if (v is int i) return i == 0 ? Visibility.Visible : Visibility.Collapsed;
        return v is true ? Visibility.Collapsed : Visibility.Visible;
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => v is Visibility.Collapsed;
}

// Hex string → SolidColorBrush
public class HexToBrushConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        try
        {
            if (v is string hex && hex.StartsWith('#'))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
        catch { }
        return new SolidColorBrush(Colors.White);
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

// Any string → SolidColorBrush (for StatusColor hex values)
public class StringToBrushConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        try
        {
            if (v is string hex)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
        catch { }
        return new SolidColorBrush(Colors.Gray);
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

// double → "xx%"
public class DoubleToPercentStringConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is double d ? $"{d:F0}%" : "0%";
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

// int count → Visibility (>0 = Visible)
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is int i && i > 0 ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

// file path string → filename only
public class PathToFilenameConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is string s ? Path.GetFileName(s) : v;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}


// TextStyle enum → French string
public class StyleToFrenchConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c) => v switch
    {
        TextStyle.Normal  => "Normal",
        TextStyle.Bold    => "Gras",
        TextStyle.Shadow  => "Ombre",
        TextStyle.Outline => "Contour",
        _ => v?.ToString() ?? ""
    };
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}
