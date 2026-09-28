using PasswordManager.Maui.ViewModels;

namespace PasswordManager.Maui.Views;

public partial class VaultTreePage : ContentPage
{
    private readonly VaultTreeViewModel _vm;

    public VaultTreePage(VaultTreeViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadCommand.Execute(null);
    }

    // Context-menu (right click) handlers for the left tree. Wired via Clicked rather than
    // Command binding - Command execution on MenuFlyoutItem inside a CollectionView DataTemplate
    // is unreliable (see dotnet/maui#15616); Clicked always fires.
    private void OnAddFolderClicked(object sender, EventArgs e)
    {
        if (((MenuFlyoutItem)sender).CommandParameter is VaultTreeRow row) _vm.AddFolderCommand.Execute(row);
    }

    private void OnAddEntryClicked(object sender, EventArgs e)
    {
        if (((MenuFlyoutItem)sender).CommandParameter is VaultTreeRow row) _vm.AddEntryCommand.Execute(row);
    }

    private void OnRenameNodeClicked(object sender, EventArgs e)
    {
        if (((MenuFlyoutItem)sender).CommandParameter is VaultTreeRow row) _vm.EditNodeCommand.Execute(row);
    }

    private void OnDeleteNodeClicked(object sender, EventArgs e)
    {
        if (((MenuFlyoutItem)sender).CommandParameter is VaultTreeRow row) _vm.DeleteNodeCommand.Execute(row);
    }
}
