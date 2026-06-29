using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using VideoMixer.ViewModels;

namespace VideoMixer.Views;

public partial class MainWindow : Window
{
    // ── Drag / Resize state ───────────────────────────────────────────────────
    private bool  _isDragging   = false;
    private bool  _isResizing   = false;
    private Point _dragOrigin;
    private Point _elemOrigin;
    private int   _sizeOrigin;

    public MainWindow()
    {
        InitializeComponent();
        ShowPage("Templates");

        if (DataContext is MainViewModel vm)
            vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.LogText))
            Dispatcher.BeginInvoke(() => LogScrollViewer?.ScrollToEnd());
        if (e.PropertyName == nameof(MainViewModel.DoneJobs))
            Dispatcher.BeginInvoke(() => JobScrollViewer?.ScrollToEnd());

        // Re-position preview text when properties that affect layout change
        if (e.PropertyName is nameof(MainViewModel.TextXPct)
                           or nameof(MainViewModel.TextYPct)
                           or nameof(MainViewModel.NewText)
                           or nameof(MainViewModel.PreviewFontSize))
            Dispatcher.BeginInvoke(SyncPreviewPosition);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.SaveSettings();
        base.OnClosing(e);
    }

    // ── Preview canvas ────────────────────────────────────────────────────────
    private void PreviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm && e.NewSize.Width > 10)
        {
            vm.PreviewScale = e.NewSize.Width / 1080.0;
            vm.SetPreviewCanvasSize((int)e.NewSize.Width, (int)e.NewSize.Height);
            SyncPreviewPosition();
        }
    }

    private void SyncPreviewPosition()
    {
        if (DataContext is not MainViewModel vm) return;
        if (PreviewCanvas == null || PreviewTextContainer == null) return;

        PreviewTextContainer.UpdateLayout();

        // Utiliser la zone vidéo réelle (avec letterbox) pour le positionnement
        double vx = vm.PreviewVideoX;
        double vy = vm.PreviewVideoY;
        double vw = vm.PreviewVideoW;
        double vh = vm.PreviewVideoH;
        double tw = PreviewTextContainer.ActualWidth;
        double th = PreviewTextContainer.ActualHeight;

        // Centre du texte dans la zone vidéo, converti en coordonnées canvas
        double x = vx + vm.TextXPct * vw - tw / 2;
        double y = vy + vm.TextYPct * vh - th / 2;

        x = Math.Clamp(x, vx, Math.Max(vx, vx + vw - tw));
        y = Math.Clamp(y, vy, Math.Max(vy, vy + vh - th));

        Canvas.SetLeft(PreviewTextContainer, x);
        Canvas.SetTop (PreviewTextContainer, y);
    }

    // ── Drag to move ──────────────────────────────────────────────────────────
    private void Preview_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.Source is FrameworkElement fe && IsChildOrSelf(fe, PreviewTextContainer)
            && e.Source != ResizeHandle)
        {
            _isDragging = true;
            _dragOrigin = e.GetPosition(PreviewCanvas);
            _elemOrigin = new Point(
                Canvas.GetLeft(PreviewTextContainer),
                Canvas.GetTop (PreviewTextContainer));
            PreviewCanvas.CaptureMouse();
            e.Handled = true;
        }
    }

    private void Preview_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging && !_isResizing) return;

        var pos = e.GetPosition(PreviewCanvas);
        double dx = pos.X - _dragOrigin.X;
        double dy = pos.Y - _dragOrigin.Y;

        if (_isDragging)
        {
            if (DataContext is not MainViewModel vm) return;
            double vx = vm.PreviewVideoX;
            double vy = vm.PreviewVideoY;
            double vw = vm.PreviewVideoW;
            double vh = vm.PreviewVideoH;
            double tw = PreviewTextContainer.ActualWidth;
            double th = PreviewTextContainer.ActualHeight;

            double nx = Math.Clamp(_elemOrigin.X + dx, vx, Math.Max(vx, vx + vw - tw));
            double ny = Math.Clamp(_elemOrigin.Y + dy, vy, Math.Max(vy, vy + vh - th));

            Canvas.SetLeft(PreviewTextContainer, nx);
            Canvas.SetTop (PreviewTextContainer, ny);

            if (vw > 0 && vh > 0)
            {
                vm.TextXPct = (nx - vx + tw / 2) / vw;
                vm.TextYPct = (ny - vy + th / 2) / vh;
            }
        }
        else if (_isResizing)
        {
            if (DataContext is MainViewModel vm)
            {
                double newSize = Math.Max(8, _sizeOrigin + (dx + dy) / 3.0);
                vm.NewSize = (int)newSize;
            }
        }
    }

    private void Preview_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        _isResizing = false;
        PreviewCanvas?.ReleaseMouseCapture();
    }

    private void Preview_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_isDragging || _isResizing)
        {
            _isDragging = false;
            _isResizing = false;
            PreviewCanvas?.ReleaseMouseCapture();
        }
    }

    // ── Resize handle ─────────────────────────────────────────────────────────
    private void Resize_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isResizing  = true;
        _isDragging  = false;
        _dragOrigin  = e.GetPosition(PreviewCanvas);
        _sizeOrigin  = (DataContext as MainViewModel)?.NewSize ?? 52;
        PreviewCanvas.CaptureMouse();
        e.Handled = true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static bool IsChildOrSelf(DependencyObject? child, DependencyObject? parent)
    {
        while (child != null)
        {
            if (child == parent) return true;
            child = System.Windows.Media.VisualTreeHelper.GetParent(child);
        }
        return false;
    }

    // ── Titlebar ──────────────────────────────────────────────────────────────
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ── Navigation ────────────────────────────────────────────────────────────
    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string page)
            ShowPage(page);
    }

    private void ShowPage(string page)
    {
        PageTemplates.Visibility = Visibility.Collapsed;
        PageVideos   .Visibility = Visibility.Collapsed;
        PageMusic    .Visibility = Visibility.Collapsed;
        PageMix      .Visibility = Visibility.Collapsed;
        PageSettings .Visibility = Visibility.Collapsed;

        NavTemplates.Tag = null;
        NavVideos   .Tag = null;
        NavMusic    .Tag = null;
        NavMix      .Tag = null;
        NavSettings .Tag = null;

        switch (page)
        {
            case "Templates": PageTemplates.Visibility = Visibility.Visible; NavTemplates.Tag = "active"; break;
            case "Videos":    PageVideos   .Visibility = Visibility.Visible; NavVideos   .Tag = "active"; break;
            case "Music":     PageMusic    .Visibility = Visibility.Visible; NavMusic    .Tag = "active"; break;
            case "Mix":       PageMix      .Visibility = Visibility.Visible; NavMix      .Tag = "active"; break;
            case "Settings":  PageSettings .Visibility = Visibility.Visible; NavSettings .Tag = "active"; break;
        }
    }

    private void QuickGo_Click(object sender, RoutedEventArgs e)
    {
        ShowPage("Mix");
        if (DataContext is MainViewModel vm && vm.CanMix)
            vm.StartMixCmd.Execute(null);
    }

    private void BrowseFFmpeg_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Sélectionne ffmpeg.exe", Filter = "Exécutable|ffmpeg.exe;ffmpeg|Tous|*.*" };
        if (dlg.ShowDialog() == true && DataContext is MainViewModel vm)
            vm.FFmpegPath = dlg.FileName;
    }
}
