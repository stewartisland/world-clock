using System.Windows;

namespace WorldClock.Sync;

/// <summary>First sign-in when this PC's clocks differ from the saved ones.</summary>
public partial class SyncChoiceWindow : Window
{
    /// <summary>True = use saved clocks, false = keep this PC's, null = cancel.</summary>
    public bool? UseSaved { get; private set; }

    public SyncChoiceWindow(AppSettings local, AppSettings saved)
    {
        InitializeComponent();
        ThemeManager.Attach(this);
        SavedText.Text = Describe(saved);
        SavedDetail.Text = saved.DeviceName is { Length: > 0 } device
            ? $"Last changed on {device}, {saved.UpdatedAt.ToLocalTime():d MMM yyyy h:mm tt}"
            : $"Last changed {saved.UpdatedAt.ToLocalTime():d MMM yyyy h:mm tt}";
        LocalText.Text = Describe(local);
    }

    private static string Describe(AppSettings settings) =>
        settings.Clocks.Count == 0 ? "No clocks" : string.Join(", ", settings.Clocks.Select(c => c.City));

    private void UseSaved_Click(object sender, RoutedEventArgs e)
    {
        UseSaved = true;
        DialogResult = true;
    }

    private void KeepLocal_Click(object sender, RoutedEventArgs e)
    {
        UseSaved = false;
        DialogResult = true;
    }
}
