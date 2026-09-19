using System.Windows;

namespace MemoryLab;

public partial class EditValueWindow : Window
{
    public string EditedValue => ValueTextBox.Text;

    public EditValueWindow(string address, string valueType, string currentValue)
    {
        InitializeComponent();

        InfoText.Text = $"{address} • {valueType}";
        ValueTextBox.Text = currentValue;
        ValueTextBox.SelectAll();
        ValueTextBox.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
