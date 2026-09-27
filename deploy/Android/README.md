# Déploiement Android

## Prérequis

- SDK .NET 10 avec le workload `android` installé (`dotnet workload list` doit le lister).
- Un **keystore** de signature (à créer une seule fois, puis à conserver précieusement — le
  perdre empêche de publier une mise à jour de la même app).

## Créer un keystore (une seule fois)

```powershell
keytool -genkeypair -v -keystore passwordmanager.keystore -alias passwordmanager `
  -keyalg RSA -keysize 2048 -validity 10000
```

`keytool` fait partie du JDK (installé avec le workload Android de .NET MAUI, généralement sous
`%ProgramFiles%\Microsoft\jdk-*\bin\keytool.exe`, ou celui d'un JDK système). Notez bien le mot
de passe choisi et l'alias (`passwordmanager` ci-dessus) — ils sont requis à chaque publication.
**Ne pas committer le fichier `.keystore` dans Git.**

## Publier

```powershell
./publish.ps1 -KeystorePath "C:\chemin\vers\passwordmanager.keystore" `
              -KeystoreAlias "passwordmanager" `
              -KeystorePassword (Read-Host -AsSecureString)
```

Produit un APK signé dans `deploy/Android/output/` (fichier `*-Signed.apk`), installable
directement sur un appareil (`adb install ...-Signed.apk`) ou distribuable en dehors du Play
Store. Pour une publication sur le Play Store, un `.aab` (Android App Bundle) est requis à la
place — ajouter `-p:AndroidPackageFormat=aab` à la commande `dotnet publish`.

## Points d'attention

- Le build pointe par défaut sur `https://passwordmanager.geekinfo.org` (builds `Release`, voir
  `Services/AppSettings.cs`).
- Toujours utiliser le **même keystore** pour publier une mise à jour de l'app — Android refuse
  d'installer une mise à jour signée avec une clé différente.
