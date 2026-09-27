namespace PasswordManager.Maui.ViewModels;

// Plain decrypted snapshot of a credential, passed as a Shell navigation parameter to
// CredentialEditPage when opening an existing entry for editing.
public class CredentialItem
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Notes { get; set; }
}
