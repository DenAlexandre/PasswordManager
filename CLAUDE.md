# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

A password manager for internal client-site credentials, with a zero-knowledge encryption model:
- **`src/PasswordManager.Api`** — ASP.NET Core (.NET 10) Web API + EF Core / PostgreSQL. Stores only ciphertext for anything sensitive; never sees plaintext credentials, master passwords, or vault keys.
- **`src/PasswordManager.Maui`** — .NET MAUI client (Windows, Android, iOS/MacCatalyst — no Linux target, MAUI doesn't support it). Does all encryption/decryption locally and caches encrypted blobs in a local SQLite file for offline access.

Both projects target **`net10.0`** (Maui: `net10.0-windows10.0.19041.0`, `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst`).

## Commands

### Backend
```bash
# Run full stack (Postgres + API) via Docker
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

### Production deployment
The backend is deployed as a **Git-repository-based Portainer stack** (`https://github.com/DenAlexandre/PasswordManager.git`, manual redeploy only, no GitOps polling) on the `geekinfo-server` host, reachable at `https://passwordmanager.geekinfo.org` through a Cloudflare Tunnel. See the README's "Déploiement en production" section for the exact Portainer steps — in particular, the first-admin bootstrap (`POST /api/auth/setup`) must be done via the container console **before** the Cloudflare public hostname is added, since that endpoint is unauthenticated by design (it disables itself once any user exists).

The MAUI client is not a hosted service — it's distributed as platform installers via `deploy/{Windows,Android,iOS}/` (publish scripts + prerequisites per platform). `Services/AppSettings.cs` defaults `ApiBaseUrl` to the production URL above in `Release` builds and to `localhost`/`10.0.2.2` in `Debug` builds.

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
This creates the first user as admin and disables itself once any user exists (`AuthController.SetupAdmin`). All subsequent users are created via `AdminUsersController` (`POST /api/admin/users`).

## Architecture

### Zero-knowledge crypto model — the core design constraint
Every feature must preserve this: **the server never has the means to decrypt vault contents.**

- **Login password** vs **master password** are two different secrets. The login password authenticates to the API (Argon2id-hashed server-side, JWT issued). The master password never leaves the device — it's only used to derive a local key (`Crypto/KeyDerivation.cs`, Argon2id) that decrypts the user's RSA private key.
- Each user has an RSA keypair; the private key is stored server-side only as `EncryptedPrivateKey` (AES-GCM, encrypted with the master-password-derived key). The server stores/serves this blob but can never open it.
- Each `SiteGroup` (a client/tenant grouping of sites) has one AES-256-GCM symmetric key. That key is never stored in the clear — it's wrapped (RSA-OAEP) per-member in `UserSiteGroupAccess.EncryptedGroupKey`, once per user who has access.
- **Granting access to a group requires an already-authorized member's client to do the wrapping**: fetch the group key (unwrap with your own private key), re-wrap it with the *target* user's RSA public key, then `POST /api/admin/sitegroups/{id}/access`. This only works once the target user has logged in at least once and completed vault setup (i.e. has a public key) — there is no way around this while staying zero-knowledge. See `AdminSiteGroupsViewModel.GrantAccessAsync` / `AddGroupAsync` for the client-side pattern, and `AdminUsersViewModel.AddUserAsync` for the "create user + dedicated group, finalize access later" flow.
- `Site.Name/Url/Notes` are stored in the clear (they're metadata, not secrets). `Credential.Encrypted*` fields (Label, Username, Password, Url, Notes) and `UserSiteGroupAccess.EncryptedGroupKey` and `User.EncryptedPrivateKey` are the only ciphertext fields — they're all self-contained base64 blobs (`AesGcmCipher.Encrypt`/`Decrypt`: nonce + ciphertext + tag).
- When adding any new field that could contain a client secret, encrypt it client-side with the owning SiteGroup's key before it ever reaches an API DTO.

### Data model / access control (`PasswordManager.Api`)
`User` → (via `UserSiteGroupAccess`, role `None`/`Read`/`Write`) → `SiteGroup` → `Site` → `Credential`. `AccessControlService` resolves a user's effective role for a group or (indirectly, via the site's group) a site; every Sites/Credentials controller action checks this before reading or writing. Admin-only endpoints (`AdminUsersController`, `AdminSiteGroupsController`) are gated by the `AdminOnly` policy (`is_admin` JWT claim), independent of per-group roles — an admin is not automatically a member of every group's key (see zero-knowledge note above).

`GET /api/sync?since=` is the one endpoint the MAUI client uses to pull everything it's allowed to see in one shot (accessible SiteGroups + their Sites + their Credentials, including soft-deleted rows so the client can prune its cache). Regular CRUD endpoints under `/api/sitegroups/{id}/sites` and `/api/sites/{id}/credentials` are used for individual writes.

### MAUI client structure
MVVM via `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`), DI wired in `MauiProgram.cs`, navigation via `.NET MAUI Shell` (routes registered in `AppShell.xaml.cs`).

- `Services/VaultSession` — in-memory only (never persisted): holds the unwrapped RSA private key while the vault is unlocked, and a cache of unwrapped per-group AES keys.
- `Services/AuthService` — login, session restore, vault setup/unlock. `TryRestoreSessionAsync()` re-validates the stored JWT against the server (distinguishing 401 "session expired" from "offline" from "vault genuinely not set up yet") rather than trusting a cached flag — get this wrong and users get routed to "create a vault" over an existing one.
- `Data/LocalCacheDb` (SQLite, `sqlite-net-pcl`) — stores exactly what `/api/sync` returns, i.e. already-encrypted blobs. This is what makes offline viewing of credentials possible; it is not additionally at-rest encrypted as a file (relies entirely on the field-level crypto above).
- `Services/SyncService` — pulls `/api/sync` into `LocalCacheDb`. Screens read from the cache, not directly from the API, so they work offline; writes (create/update credential or site) go straight to the API and are mirrored into the cache on success.
- `ViewModels/VaultTreeViewModel` — the main post-unlock screen: a flattened, manually expand/collapsed `ObservableCollection<VaultTreeRow>` (SiteGroup rows + their Site children inserted/removed on toggle) since MAUI has no native TreeView control.
- `ViewModels/CredentialEditViewModel` — single modal for create/edit of a credential (label/username/password/url/notes together), navigated to with `Shell.Current.GoToAsync(route, IDictionary<string,object>)` passing the actual `CredentialItem` object (not query-string params) for the edit case.

### MAUI gotchas already hit in this codebase (avoid regressing them)
- **`RefreshView.Command` re-fires whenever `IsRefreshing` flips to `true`**, including when your own `LoadAsync` sets it — guard every such command with `if (IsBusy) return;` before setting `IsBusy = true`, or you get concurrent reentrant loads (this previously corrupted the local SQLite cache mid-transaction).
- **A computed property that reads another `[ObservableProperty]` needs `[NotifyPropertyChangedFor(nameof(ComputedProp))]` on that property**, or the UI never refreshes it even though the underlying value changed (bit `VaultUnlockViewModel.IsSetup` derived from `SetupMode`).
- **Shell absolute navigation (`"//Route"`) only works for routes that are an actual `ShellContent` in `AppShell.xaml`** (currently just `LoginPage`). Every other page is a "global route" registered via `Routing.RegisterRoute` — navigate to those with a relative route, not a leading `//`.
- **Every `OnAppearing` that fires a command via `.Execute(null)` (fire-and-forget) must have real `try/catch` inside the command**, not just `try/finally` — an unhandled exception there crashes the whole process rather than surfacing an error message.
- **API write calls (`ApiClient` PUT/DELETE/POST-without-body) return `bool`/`Task<bool>` for success** — always check it before mutating `LocalCacheDb` or `VaultSession`, or a server-rejected write (e.g. 403 from a read-only role) gets silently treated as applied.
