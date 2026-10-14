using System.Windows;
using System.Windows.Controls;

namespace Tideglass;

public partial class NewRunDialog : Window
{
    public int Seed { get; private set; }
    public NewRunDialog(Window owner)
    {
        InitializeComponent();
        Owner = owner;
        Resources[typeof(Button)] = owner.FindResource(typeof(Button));
        Loaded += (_, _) => SeedInput.Focus();
    }
    private void StartClick(object sender, RoutedEventArgs e)
    {
        bool random = string.IsNullOrWhiteSpace(SeedInput.Text);
        int entered = 0;
        if (!random && !int.TryParse(SeedInput.Text, out entered))
        {
            ValidationMessage.Visibility = Visibility.Visible;
            SeedInput.Focus(); SeedInput.SelectAll();
            return;
        }
        Seed = random ? Random.Shared.Next() : entered;
        DialogResult = true;
    }
}
