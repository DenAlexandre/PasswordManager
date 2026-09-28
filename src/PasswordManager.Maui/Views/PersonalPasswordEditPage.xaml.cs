using PasswordManager.Maui.ViewModels;

namespace PasswordManager.Maui.Views;

public partial class PersonalPasswordEditPage : ContentPage
{
    public PersonalPasswordEditPage(PersonalPasswordEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
