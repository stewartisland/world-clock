using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace WorldClock;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        ThemeManager.Attach(this);
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        VersionText.Text = $"Version {version?.ToString(3)}";
        CopyrightRun.Text = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright?.Split(". ")[0] + ".";
    }

    private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
