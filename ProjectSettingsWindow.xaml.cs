using System.Windows;

namespace MemoryLab;

public partial class ProjectSettingsWindow : Window
{
    public string ProjectName => ProjectNameTextBox.Text.Trim();
    public string ProcessName => ProcessNameTextBox.Text.Trim();

    public ProjectSettingsWindow(
        string projectName,
        string processName)
    {
        InitializeComponent();

        ProjectNameTextBox.Text = projectName;
        ProcessNameTextBox.Text = processName;
    }

    private void Save_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            MessageBox.Show(
                "Project name tidak boleh kosong.",
                "MemoryLab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }
}
