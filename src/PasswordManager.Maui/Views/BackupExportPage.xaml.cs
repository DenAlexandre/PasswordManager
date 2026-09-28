using PasswordManager.Maui.ViewModels;

namespace PasswordManager.Maui.Views;

public partial class BackupExportPage : ContentPage
{
    private readonly BackupExportViewModel _vm;

    public BackupExportPage(BackupExportViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadCommand.Execute(null);
    }
}
