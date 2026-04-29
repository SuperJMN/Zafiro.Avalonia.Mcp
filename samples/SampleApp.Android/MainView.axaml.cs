using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SampleApp.Android;

public partial class MainView : UserControl
{
    private int _clickCount;

    public MainView()
    {
        InitializeComponent();
    }

    private void OnClickMe(object? sender, RoutedEventArgs e)
    {
        _clickCount++;
        StatusText.Text = $"Clicked {_clickCount} time(s)";
    }
}
