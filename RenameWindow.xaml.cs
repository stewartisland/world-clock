using System.Windows;
using System.Windows.Controls;

namespace WorldClock;

public partial class RenameWindow : Window
{
    public string NewName => NameBox.Text.Trim();

    public RenameWindow(string current)
    {
        InitializeComponent();
        ThemeManager.Attach(this);
        NameBox.Text = current;
        NameBox.SelectAll();
    }

    private void NameBox_TextChanged(object sender, TextChangedEventArgs e) =>
        OkButton.IsEnabled = !string.IsNullOrWhiteSpace(NameBox.Text);

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
