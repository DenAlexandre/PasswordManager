# Déploiement iOS

## Prérequis (non disponibles sur cette machine Windows)

La publication iOS **doit** se faire sur un **Mac avec Xcode installé** — .NET MAUI ne peut ni
compiler ni signer une app iOS depuis Windows ou Linux (limitation d'Apple, pas de .NET). Il faut
en plus :

- Un **compte Apple Developer Program** (payant, ~99 $/an) — indispensable pour signer l'app,
  que ce soit pour un test interne (TestFlight, install ad-hoc) ou l'App Store.
- Un **certificat de signature** (Distribution ou Development) généré via ce compte, installé
  dans le trousseau macOS.
- Un **profil de provisionnement** (Provisioning Profile) associé à l'identifiant d'app
  (`ApplicationId` du `.csproj`, actuellement `com.companyname.passwordmanager.maui` — à changer
  pour un identifiant propre à l'organisation avant toute publication réelle) et, pour un usage
  interne, à la liste des appareils autorisés (UDID) si distribution ad-hoc plutôt que TestFlight.
- Le SDK .NET 10 avec le workload `ios` installé sur ce Mac (`dotnet workload install ios`).

## Publier (sur le Mac, une fois les prérequis en place)

```bash
chmod +x publish.sh
./publish.sh Release "iPhone Distribution: Votre Nom (TEAMID)" "Nom du profil de provisionnement"
```

Le nom exact du certificat (`CodesignKey`) et du profil (`CodesignProvision`) s'obtiennent dans
Xcode (Settings → Accounts) ou via `security find-identity -v -p codesigning`.

Produit une archive `.ipa` dans `deploy/iOS/output/`, à distribuer via TestFlight (App Store
Connect) ou en installation ad-hoc directe sur les appareils enregistrés dans le profil.

## Points d'attention

- Le build pointe par défaut sur `https://passwordmanager.geekinfo.org` (builds `Release`, voir
  `Services/AppSettings.cs`).
- Sans compte Apple Developer, seul un déploiement sur simulateur iOS (depuis un Mac) est
  possible — pas d'installation sur un appareil physique ni de distribution.
