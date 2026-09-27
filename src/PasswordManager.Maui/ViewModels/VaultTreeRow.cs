using CommunityToolkit.Mvvm.ComponentModel;

namespace PasswordManager.Maui.ViewModels;

// A single flattened row of the group/site tree. Group rows can be expanded/collapsed to
// reveal their Site children, which are inserted/removed from the parent ObservableCollection
// immediately after them - there is no native TreeView control in .NET MAUI.
public partial class VaultTreeRow : ObservableObject
{
    public bool IsGroup { get; init; }
    public Guid Id { get; init; }
    public Guid ParentGroupId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Subtitle { get; init; }
    public bool CanWrite { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Chevron))]
    private bool isExpanded;

    public string Chevron => !IsGroup ? string.Empty : IsExpanded ? "▾" : "▸";
    public double Indent => IsGroup ? 12 : 36;
    public Microsoft.Maui.Controls.FontAttributes RowFontAttributes =>
        IsGroup ? Microsoft.Maui.Controls.FontAttributes.Bold : Microsoft.Maui.Controls.FontAttributes.None;
}
