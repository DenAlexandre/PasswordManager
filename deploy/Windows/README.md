# Déploiement Windows

## Prérequis

- SDK .NET 10 avec le workload `maui-windows` installé (`dotnet workload list` doit le lister).
- Windows 10/11 avec le SDK `10.0.19041.0` (installé avec Visual Studio ou via le workload MAUI).

## Publier

```powershell
./publish.ps1
```

Produit un build **autonome (self-contained), non empaqueté** (pas de MSIX, pas de Microsoft
Store, pas besoin d'installer le runtime .NET sur le poste cible) dans `deploy/Windows/output/`.
Exécutable à distribuer : `output/PasswordManager.Maui.exe` (+ tout le contenu du dossier, à
copier ensemble).

## Points d'attention

- Le build pointe par défaut sur `https://passwordmanager.geekinfo.org` (builds `Release`, voir
  `Services/AppSettings.cs`). Vérifier que le back-end est bien déployé et joignable avant de
  distribuer un build.
- **Non signé** : Windows SmartScreen affichera un avertissement à l'installation/premier
  lancement sur les postes des utilisateurs. Pour l'éviter, il faut un certificat de signature de
  code (Authenticode) — non requis pour un usage interne restreint, mais à prévoir si diffusion
  plus large.
- Pour reconstruire après un changement de code : relancer `./publish.ps1` (écrase `output/`).
