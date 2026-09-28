# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

A password manager for internal client-site credentials, with a zero-knowledge encryption model:
- **`src/PasswordManager.Api`** — ASP.NET Core (.NET 10) Web API + EF Core / PostgreSQL. Stores only ciphertext for anything sensitive; never sees plaintext credentials, master passwords, or vault keys.
- **`src/PasswordManager.Maui`** — .NET MAUI client (Windows, Android, iOS/MacCatalyst — no Linux target, MAUI doesn't support it). Does all encryption/decryption locally and caches encrypted blobs in a local SQLite file for offline access.

Both projects target **`net10.0`** (Maui: `net10.0-windows10.0.19041.0`, `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst`).

Accounts are self-service (email + verification code, see below); there is no per-user provisioning UI anymore. Every user gets two independent spaces once their vault is set up: the shared, role-based **SiteGroup** model (admin-managed "Databases", can hold nested folders/credentials, shareable across users) and a single-owner **Personal Vault** (passwords + file attachments, never shared, not admin-visible).

## Commands

### Backend
```bash
# Run full stack (Postgres + API) via Docker for local dev (see the note right below the block
# about this same file also being used for production).
cp .env.example .env   # set real POSTGRES_PASSWORD / JWT_SECRET before any real use
docker compose up -d --build

# API only, without Docker (needs local Postgres reachable per appsettings.json ConnectionStrings:Default)
cd src/PasswordManager.Api
dotnet run

# EF Core migrations (run from src/PasswordManager.Api)
dotnet ef migrations add <Name>
dotnet ef database update   # only needed outside Docker - Program.cs calls db.Database.Migrate() on startup in-container
```
There is no test project in this repo yet. Verify backend changes with the Swagger UI (`/swagger`, exposed by `AddSwaggerGen`/`UseSwaggerUI` in `Program.cs`) or `curl` against `http://localhost:8080/api/...`.

**This `docker-compose.yml` is also what the production Portainer stack builds from** (see README) — `ASPNETCORE_ENVIRONMENT` therefore defaults to `Production` in the compose file itself and must be set to `Development` in your local `.env` (see `.env.example`), never hardcoded in the compose file. Setting it wrong there would leak a dev-only convenience — `AuthController` logs the generated email-verification code (`docker compose logs back | grep "DEV ONLY"`, gated on `IHostEnvironment.IsDevelopment()`) so registration is testable without a working SMTP relay — into production logs. Real email delivery needs `SMTP_HOST`/`SMTP_PORT`/`SMTP_USERNAME`/`SMTP_PASSWORD`/`SMTP_FROM_EMAIL` in `.env`; without them, `SmtpEmailSender` fails silently (caught and logged, doesn't fail the request, see `AuthController.SendVerificationEmailAsync`).

### Production deployment
The backend is deployed as a **Git-repository-based Portainer stack** (`https://github.com/DenAlexandre/PasswordManager.git`, manual redeploy only, no GitOps polling) on the `geekinfo-server` host, reachable at `https://passwordmanager.geekinfo.org` through a Cloudflare Tunnel. See the README's "Déploiement en production" section for the exact Portainer steps — in particular, the first-admin bootstrap (`POST /api/auth/setup`) must be done via the container console **before** the Cloudflare public hostname is added, since that endpoint is unauthenticated by design (it disables itself once any user exists).

The MAUI client is not a hosted service — it's distributed as platform installers via `deploy/{Windows,Android,iOS}/` (publish scripts + prerequisites per platform). `Services/AppSettings.cs` defaults `ApiBaseUrl` to the production URL above in `Release` builds and to `localhost`/`10.0.2.2` in `Debug` builds; the value is cached in `Preferences`, so a stale override persists across rebuilds until explicitly reset.

### MAUI client
```bash
cd src/PasswordManager.Maui

# Build/run the Windows target (fastest inner loop on a Windows dev machine)
dotnet build -f net10.0-windows10.0.19041.0
# then launch: bin/Debug/net10.0-windows10.0.19041.0/win-x64/PasswordManager.Maui.exe

# Android
dotnet build -f net10.0-android
```
**Important**: the Windows build fails with `MSB3027`/`MSB3021` (file locked) if a previous run of `PasswordManager.Maui.exe` is still open — kill the process before rebuilding.

The client needs the API reachable at the URL configured in `Services/AppSettings.cs` (defaults: `http://localhost:8080` on Windows/iOS, `http://10.0.2.2:8080` on the Android emulator).

### Bootstrapping a fresh database
The very first account must be created via the one-time-only endpoint, since there's no seed data:
```bash
curl -X POST http://localhost:8080/api/auth/setup -H "Content-Type: application/json" \
  -d '{"email":"admin@example.com","password":"..."}'
```
This creates the first user as admin (email pre-verified) and disables itself once any user exists (`AuthController.SetupAdmin`). Every account after that is self-registered by the user themselves (`POST /api/auth/register` → email code → `POST /api/auth/verify-email`, see below) and is never admin by default; promote further admins via `AdminUsersController` (`PUT /api/admin/users/{id}`, `IsAdmin: true`).

## Architecture

### Zero-knowledge crypto model — the core design constraint
Every feature must preserve this: **the server never has the means to decrypt vault contents.**

- **Login password** vs **master password** are two different secrets. The login password authenticates to the API (Argon2id-hashed server-side via `PasswordHasher`, JWT issued). The master password never leaves the device — it's only used to derive a local key (`Crypto/KeyDerivation.cs`, Argon2id) that decrypts the user's RSA private key.
- Each user has an RSA keypair; the private key is stored server-side only as `EncryptedPrivateKey` (AES-GCM, encrypted with the master-password-derived key). The server stores/serves this blob but can never open it.
- Each `SiteGroup` (a client/tenant grouping of sites) has one AES-256-GCM symmetric key. That key is never stored in the clear — it's wrapped (RSA-OAEP) per-member in `UserSiteGroupAccess.EncryptedGroupKey`, once per user who has access.
- **Granting access to a group requires an already-authorized member's client to do the wrapping**: fetch the group key (unwrap with your own private key), re-wrap it with the *target* user's RSA public key, then `POST /api/admin/sitegroups/{id}/access`. This only works once the target user has logged in at least once and completed vault setup (i.e. has a public key) — there is no way around this while staying zero-knowledge. See `AdminUsersViewModel.GrantAccessAsync` for the client-side pattern.
- The **Personal Vault** reuses the same primitives but skips the per-member wrap table entirely, since there's exactly one owner: `User.EncryptedPersonalVaultKey` holds a single AES-256 key, RSA-wrapped once for the owner's own public key, generated during `AuthService.SetupVaultAsync` (or lazily via `POST /api/auth/personal-vault-key` for accounts that predate this feature). `VaultSession` keeps it in a dedicated field, deliberately separate from the multi-group `_groupKeys` dictionary.
- `Site.Name/Url/Notes` are stored in the clear (they're metadata, not secrets), as are `PersonalDocument.ContentType`/`FileSizeBytes`. `Credential.Encrypted*`, `PersonalPassword.Encrypted*`, `PersonalDocument.EncryptedFileName`/`EncryptedContent`, `UserSiteGroupAccess.EncryptedGroupKey`, and `User.EncryptedPrivateKey`/`EncryptedPersonalVaultKey` are the only ciphertext fields — they're all self-contained base64 blobs (`AesGcmCipher.Encrypt`/`Decrypt` for strings, `EncryptBytes`/`DecryptBytes` for raw bytes: nonce + ciphertext + tag). All are stored as Postgres `text` (base64), never `bytea` — stay consistent with this when adding new encrypted fields.
- When adding any new field that could contain a client secret, encrypt it client-side with the owning SiteGroup's (or Personal Vault's) key before it ever reaches an API DTO.

### Data model / access control (`PasswordManager.Api`)
`User` → (via `UserSiteGroupAccess`, role `None`/`Read`/`Write`) → `SiteGroup` → `Site` → `Credential`. `AccessControlService` resolves a user's effective role for a group or (indirectly, via the site's group) a site; every Sites/Credentials controller action checks this before reading or writing. Admin-only endpoints (`AdminUsersController`, `AdminSiteGroupsController`) are gated by the `AdminOnly` policy (`is_admin` JWT claim), independent of per-group roles — an admin is not automatically a member of every group's key (see zero-knowledge note above). `AdminSiteGroupsController.GetExportData` is the one deliberate exception: admin-only, no membership check, returns a group's full Sites/Credentials ciphertext regardless — used for full-system backup export (see below), safe because it never exposes anything beyond what the server already stores as opaque ciphertext.

`PersonalPassword`/`PersonalDocument` are a separate, flat (`UserId`-only) tree with no `AccessControlService` involvement — every query is implicitly `WHERE UserId == CurrentUserId`, enforced in `PersonalVaultController`. `PersonalDocument` listing (`GET .../documents`) deliberately omits the encrypted content column (fetch it separately via `GET .../documents/{id}/content`) to keep listing cheap.

`GET /api/sync?since=` is the one endpoint the MAUI client uses to pull everything it's allowed to see in one shot (accessible SiteGroups + their Sites + their Credentials, including soft-deleted rows so the client can prune its cache). Regular CRUD endpoints under `/api/sitegroups/{id}/sites` and `/api/sites/{id}/credentials` are used for individual writes. The Personal Vault is **not** synced/cached this way — `PersonalVaultViewModel` fetches fresh from `PersonalVaultController` every time the page loads (no offline support for personal data, unlike the SiteGroup tree).

### Self-registration and email verification
`POST /api/auth/register` (validates email/password, creates an unverified `User`, generates a 6-digit code + 15-minute expiry, emails it) → `POST /api/auth/verify-email` (checks code/expiry/attempt-count, then returns the same `LoginResponse` shape as `Login`/`SetupAdmin` so the client can go straight into vault setup) → `POST /api/auth/resend-verification`. These three plus the one-time `/api/auth/setup` are the only unauthenticated endpoints; all four are rate-limited (`[EnableRateLimiting("auth-public")]`, a per-IP fixed-window policy defined in `Program.cs`) since — unlike `/api/auth/setup`, which self-disables — they stay reachable forever and are abusable (email spam, code brute-forcing). A rejected request gets a real body via `options.OnRejected` (the ASP.NET Core default is an empty 429 body, which silently breaks the client's `error ?? fallback` display pattern if not overridden).

### Backup export/restore (admin-only, disaster recovery)
`Services/BackupService.cs` (MAUI) builds a versioned JSON file containing, per selected SiteGroup: every member's RSA-wrapped group key (from `AdminSiteGroupsController.ListAccess`, admin-only) plus the group's full Sites/Credentials ciphertext (from `GetExportData`, not the admin's own `LocalCacheDb` — the export covers every Database in the system, not just ones the exporting admin belongs to). Restoring is deliberately **not** symmetric with export: re-creating a fully-missing SiteGroup requires admin rights (mints a new group + new key, decrypts each credential with the old member-wrapped key and re-encrypts with the new one), while restoring missing Sites/Credentials into a SiteGroup that still exists only needs Write access — see `BackupService.RestoreAsync`'s two code paths for the exact split.

### MAUI client structure
MVVM via `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`), DI wired in `MauiProgram.cs`, navigation via `.NET MAUI Shell` (routes registered in `AppShell.xaml.cs`).

- `Services/VaultSession` — in-memory only (never persisted): holds the unwrapped RSA private key while the vault is unlocked, a cache of unwrapped per-SiteGroup AES keys, and the unwrapped Personal Vault key (kept separate, see crypto section above).
- `Services/AuthService` — registration, login, session restore, vault setup/unlock. `TryRestoreSessionAsync()` re-validates the stored JWT against the server (distinguishing 401 "session expired" from "offline" from "vault genuinely not set up yet") rather than trusting a cached flag — get this wrong and users get routed to "create a vault" over an existing one.
- `Data/LocalCacheDb` (SQLite, `sqlite-net-pcl`) — stores exactly what `/api/sync` returns, i.e. already-encrypted blobs, for the shared SiteGroup tree only (not the Personal Vault). This is what makes offline viewing of credentials possible; it is not additionally at-rest encrypted as a file (relies entirely on the field-level crypto above).
- `Services/SyncService` — pulls `/api/sync` into `LocalCacheDb`. Screens read from the cache, not directly from the API, so they work offline; writes (create/update credential or site) go straight to the API and are mirrored into the cache on success.
- `ViewModels/VaultTreeViewModel` + `Views/VaultTreePage` — master-detail: the left pane is a compact, flattened `ObservableRangeCollection<VaultTreeRow>` of **Group/Folder rows only** (entries never live in the tree; expand/collapse inserts/removes only sub-folder rows). Selecting a row populates the right pane: a Folder's entries list (`NodeEntries`), and clicking an entry shows its full decrypted detail there. Right-click on a row (Windows/Mac Catalyst only, see gotchas) opens a context menu for add folder/add entry/rename/delete. New SiteGroups get an auto-created "Racine" folder and are auto-expanded on creation (`CreateDatabaseAsync`); an existing empty SiteGroup shows a one-click "Créer un dossier « Racine »" fallback in the right pane (`CanCreateRootFolder`).
- `ViewModels/PersonalVaultViewModel` + `Views/PersonalVaultPage` — same visual idiom as the SiteGroup entries list, but flat (no folders) and fetched fresh every load (no `LocalCacheDb`). `Services/DocumentService` handles file attachment pick/encrypt/upload and download/decrypt/open (via `Launcher.OpenAsync` against a temp file) — reuses `AesGcmCipher.EncryptBytes`/`DecryptBytes` as-is, so files are limited to what fits comfortably in memory (client-enforced ~20 MB ceiling; no streaming cipher).
- `ViewModels/CredentialEditViewModel` / `PersonalPasswordEditViewModel` — near-identical single modals for create/edit (label/username/password/url/notes together), kept as two separate classes rather than one generalized viewmodel because they're scoped differently (Site/SiteGroup vs. bare User) and merging the two scoping models would complicate both. Both navigate with `Shell.Current.GoToAsync(route, IDictionary<string,object>)` passing the actual `CredentialItem` object (not query-string params) for the edit case.
- `Services/BackupService` (export/restore, see above) and `Utils/ObservableRangeCollection<T>` (batches `ObservableCollection` mutations into a single `Reset` notification — see gotchas) are both reused wherever a screen needs to mutate a bound list in bulk without a per-item UI thrash.

### MAUI gotchas already hit in this codebase (avoid regressing them)
- **`RefreshView.Command` re-fires whenever `IsRefreshing` flips to `true`**, including when your own `LoadAsync` sets it — guard every such command with `if (IsBusy) return;` before setting `IsBusy = true`, or you get concurrent reentrant loads (this previously corrupted the local SQLite cache mid-transaction).
- **A computed property that reads another `[ObservableProperty]` needs `[NotifyPropertyChangedFor(nameof(ComputedProp))]` on that property**, or the UI never refreshes it even though the underlying value changed (e.g. `VaultUnlockViewModel.IsSetup` derived from `SetupMode`).
- **Shell absolute navigation (`"//Route"`) only works for routes that are an actual `ShellContent` in `AppShell.xaml`** (currently just `LoginPage`). Every other page is a "global route" registered via `Routing.RegisterRoute` — navigate to those with a relative route, not a leading `//`.
- **Every `OnAppearing` that fires a command via `.Execute(null)` (fire-and-forget) must have real `try/catch` inside the command**, not just `try/finally` — an unhandled exception there crashes the whole process rather than surfacing an error message.
- **API write calls (`ApiClient` PUT/DELETE/POST-without-body) return `bool`/`Task<bool>` for success** — always check it before mutating `LocalCacheDb` or `VaultSession`, or a server-rejected write (e.g. 403 from a read-only role) gets silently treated as applied.
- **Never nest a `CollectionView` inside a `ScrollView`.** MAUI has to realize every item to measure a "natural" height for the outer scroll container, which disables virtualization — the list grows very tall and mutating the bound collection gets visibly slow once it's larger than a couple of items. Give the `CollectionView` a bounded `Grid` row (`*`) instead, as in `VaultTreePage`'s right pane.
- **Bulk-mutating an `ObservableCollection<T>`** (clearing + re-adding many items, or inserting/removing several at once) **one item at a time triggers one UI layout pass per item.** Use `Utils/ObservableRangeCollection<T>` (`ReplaceAll`/`InsertRange`/`RemoveRange`) to batch it into a single `Reset` notification instead — this is what fixed both slow tree-reloads and slow folder-expansion.
- **`ToolbarItem` and `MenuFlyoutItem` don't support an `IsVisible` binding** (they're `MenuItem`, not `VisualElement`). Gate their visibility from code-behind (e.g. removing a `ToolbarItem` from `Page.ToolbarItems` in `OnAppearing` based on a ViewModel flag) instead of XAML binding.
- **`MenuFlyoutItem.Command` execution is unreliable when the flyout is attached inside a `CollectionView` `DataTemplate`** (see dotnet/maui#15616) — wire `Clicked` event handlers in code-behind instead, which always fires; still pass data via `CommandParameter` (plain property binding, not command execution, so it isn't affected).
- **`FlyoutBase.ContextFlyout` (right-click menus) only works on Windows and Mac Catalyst** — there's no Android/iOS equivalent, so any action reachable *only* via right-click has no touch-platform affordance. `VaultTreePage` currently accepts this gap for add/rename/delete; don't assume parity across platforms when adding new context-menu actions.
