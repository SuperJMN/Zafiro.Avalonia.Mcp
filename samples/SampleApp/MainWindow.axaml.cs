using Avalonia.Controls;
using Avalonia.Media;

namespace SampleApp;

public partial class MainWindow : Window
{
    private int _clickCount;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnClickMe(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _clickCount++;
        StatusText.Text = $"Button clicked {_clickCount} time(s)";
    }

    private void OnAnimateClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var current = ColorTarget.Background as ISolidColorBrush;
        ColorTarget.Background = current?.Color == Colors.DodgerBlue
            ? new SolidColorBrush(Colors.OrangeRed)
            : new SolidColorBrush(Colors.DodgerBlue);
    }
}