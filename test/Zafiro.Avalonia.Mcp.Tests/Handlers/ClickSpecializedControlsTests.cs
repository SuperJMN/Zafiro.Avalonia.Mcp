using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class ClickSpecializedControlsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public ClickSpecializedControlsTests(AvaloniaTestFixture _)
    {
        NodeRegistry.Clear();
    }

    [Fact]
    public void Click_ComboBoxNestedInListBoxItem_OpensDropDownWithoutSelectingOuterItem() => Dispatcher.UIThread.Invoke(() =>
    {
        var comboBox = new ComboBox { Name = "Choice", ItemsSource = new[] { "One", "Two" } };
        var listBox = new ListBox { Items = { new ListBoxItem { Content = comboBox } } };

        var result = InputHandler.Click(comboBox);

        Assert.IsNotType<HandlerErrorResult>(result);
        Assert.True(comboBox.IsDropDownOpen);
        Assert.Equal(-1, listBox.SelectedIndex);
        var json = JsonSerializer.SerializeToElement(result, JsonOptions);
        Assert.Equal("combobox_toggle", json.GetProperty("method").GetString());
    });

    [Fact]
    public void Click_CalendarDayButton_SelectsItsDate() => WithCalendar(() => new Calendar
    {
        DisplayDate = new DateTime(2026, 1, 1),
        SelectedDate = new DateTime(2026, 1, 10),
    }, calendar =>
    {
        var result = InputHandler.Click(DayButton(calendar, new DateTime(2026, 1, 20)));

        Assert.Equal(new DateTime(2026, 1, 20), calendar.SelectedDate);
        var json = JsonSerializer.SerializeToElement(result, JsonOptions);
        Assert.Equal("calendar_day_select", json.GetProperty("method").GetString());
        Assert.Equal("2026-01-20", json.GetProperty("selectedDate").GetString());
    });

    [Fact]
    public void Click_CalendarDayButton_BlackoutDate_ReportsUnsupported() => WithCalendar(() => new Calendar
    {
        DisplayDate = new DateTime(2026, 1, 1),
        SelectedDate = new DateTime(2026, 1, 10),
    }, calendar =>
    {
        calendar.BlackoutDates.Add(new CalendarDateRange(new DateTime(2026, 1, 20)));

        var result = InputHandler.Click(DayButton(calendar, new DateTime(2026, 1, 20)));

        Assert.Equal(DiagnosticErrorCodes.UnsupportedOperation, Assert.IsType<HandlerErrorResult>(result).Error.Code);
        Assert.Equal(new DateTime(2026, 1, 10), calendar.SelectedDate);
    });

    [Fact]
    public void Click_CalendarDatePickerDay_SelectsDateAndClosesPicker() => Dispatcher.UIThread.Invoke(() =>
    {
        var picker = new CalendarDatePicker
        {
            DisplayDate = new DateTime(2026, 1, 1),
            SelectedDate = new DateTime(2026, 1, 10),
        };
        var window = new Window { Width = 400, Height = 400 };
        window.Styles.Add(new FluentTheme());
        window.Content = picker;
        window.Show();

        try
        {
            picker.IsDropDownOpen = true;
            var popup = picker.GetVisualDescendants().OfType<Popup>().Single();
            var calendar = popup.Child!.GetSelfAndVisualDescendants().OfType<Calendar>().Single();

            var result = InputHandler.Click(DayButton(calendar, new DateTime(2026, 1, 15)));

            Assert.IsNotType<HandlerErrorResult>(result);
            Assert.Equal(new DateTime(2026, 1, 15), picker.SelectedDate);
            Assert.False(picker.IsDropDownOpen);
        }
        finally
        {
            window.Close();
        }
    });

    private static void WithCalendar(Func<Calendar> create, Action<Calendar> test) => Dispatcher.UIThread.Invoke(() =>
    {
        var calendar = create();
        var window = new Window { Width = 400, Height = 400 };
        window.Styles.Add(new FluentTheme());
        window.Content = calendar;
        window.Show();
        try
        {
            test(calendar);
        }
        finally
        {
            window.Close();
        }
    });

    private static CalendarDayButton DayButton(Calendar calendar, DateTime date) =>
        calendar.GetVisualDescendants().OfType<CalendarDayButton>()
            .First(b => b.DataContext is DateTime d && d == date);
}
