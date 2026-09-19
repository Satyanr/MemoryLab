using System.Windows;
using System.Windows.Input;

namespace MemoryLab;

public partial class RecentProjectsWindow : Window
{
    public string? SelectedPath { get; private set; }

    public RecentProjectsWindow(
        IEnumerable<string> recentProjects)
    {
        InitializeComponent();

        RecentListBox.ItemsSource =
            recentProjects.ToList();
    }

    private void Open_Click(
        object sender,
        RoutedEventArgs e)
    {
        SelectAndClose();
    }

    private void RecentListBox_MouseDoubleClick(
        object sender,
        MouseButtonEventArgs e)
    {
        SelectAndClose();
    }

    private void SelectAndClose()
    {
        if (RecentListBox.SelectedItem is not string path)
            return;

        SelectedPath = path;
        DialogResult = true;
    }
}
