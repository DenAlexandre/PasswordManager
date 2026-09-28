using PasswordManager.Maui.ViewModels;

namespace PasswordManager.Maui.Views;

public partial class BackupImportPage : ContentPage
{
    public BackupImportPage(BackupImportViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
