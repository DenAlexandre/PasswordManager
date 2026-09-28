using CommunityToolkit.Mvvm.ComponentModel;

namespace PasswordManager.Maui.ViewModels;

public enum TreeRowKind { Group, Folder, Entry }

// A single flattened row of the KeePass-style tree: SiteGroup > Folder (nested arbitrarily via
// ParentSiteId) > Entry (Credential, a leaf). There is no native TreeView control in .NET MAUI,
// so Folder/Group rows insert/remove their direct children from the parent ObservableCollection
// on expand/collapse instead.
public partial class VaultTreeRow : ObservableObject
{
    public TreeRowKind Kind { get; init; }
    public Guid Id { get; init; }
    public Guid GroupId { get; init; }
    public Guid? ParentFolderId { get; init; }
    public int Depth { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Subtitle { get; init; }
    public bool CanWrite { get; init; }

    // Entry-only decrypted fields (kept off any Group/Folder row).
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string? Url { get; init; }
    public string? Notes { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Chevron))]
    private bool isExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaskedPassword))]
    private bool isRevealed;

    [ObservableProperty]
    private bool isSelected;

    public bool IsEntry => Kind == TreeRowKind.Entry;
    // Group and Folder rows can both hold sub-folders; only a Folder can directly hold entries.
    public bool CanAddFolder => CanWrite && Kind != TreeRowKind.Entry;
    public bool CanAddEntry => CanWrite && Kind == TreeRowKind.Folder;
    public bool CanManageNode => CanWrite && Kind != TreeRowKind.Entry;
    public string Chevron => Kind == TreeRowKind.Entry ? string.Empty : IsExpanded ? "▾" : "▸";
    public double Indent => 12 + Depth * 24;
    public Microsoft.Maui.Controls.FontAttributes RowFontAttributes =>
        Kind == TreeRowKind.Group ? Microsoft.Maui.Controls.FontAttributes.Bold : Microsoft.Maui.Controls.FontAttributes.None;
    public string MaskedPassword => IsRevealed ? Password : new string('•', Math.Max(Password.Length, 8));

    public CredentialItem ToCredentialItem() => new()
    {
        Id = Id,
        Label = Name,
        Username = Username,
        Password = Password,
        Url = Url,
        Notes = Notes
    };
}
