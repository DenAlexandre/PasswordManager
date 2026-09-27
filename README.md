# Password Manager

Gestionnaire de mots de passe interne (organisation en groupes de sites clients, dossiers imbriqués
et entrées façon KeePass), avec chiffrement **zero-knowledge** : le serveur ne stocke et ne voit
jamais que du texte chiffré (voir `CLAUDE.md` pour le détail du modèle de chiffrement).

## Structure

- `src/PasswordManager.Api` — API ASP.NET Core (.NET 10) + EF Core / PostgreSQL.
- `src/PasswordManager.Maui` — client .NET MAUI (Windows, Android, iOS/MacCatalyst).
- `docker-compose.yml` — `db` (Postgres) + `back` (API), build via `src/PasswordManager.Api/Dockerfile`.
- `deploy/` — scripts et instructions de publication du client par plateforme (Windows, Android, iOS).

## Démarrage avec Docker (back-end)

```bash
cp .env.example .env
# éditer JWT_SECRET / POSTGRES_PASSWORD dans .env

docker compose up -d --build

# créer le premier compte admin (une seule fois, tant qu'aucun utilisateur n'existe)
curl -X POST http://localhost:8080/api/auth/setup -H "Content-Type: application/json" \
  -d '{"email":"admin@example.com","password":"..."}'
```

API : http://localhost:8080/api — Swagger : http://localhost:8080/swagger

Pour reconstruire l'image après un changement de code : `docker compose up -d --build back`.
Pour tout arrêter : `docker compose down` (ajouter `-v` pour aussi supprimer les données Postgres).

## Déploiement en production (Portainer, geekinfo-server / 192.168.1.27)

Le back-end est exposé via le tunnel Cloudflare (`geekinfo-server`) sur
`passwordmanager.geekinfo.org`, et déployé comme une **stack Portainer basée sur ce dépôt Git**
(build à la volée sur le serveur, pas de registry d'images) — même principe que la stack
`puissance4`. Seul le `back` (+ sa base Postgres) est concerné : le client MAUI n'est pas un
service web, il se distribue via `deploy/` (voir plus bas).

Mise en place (une fois), dans Portainer :

1. **Stacks → Add stack**
   - Name : `passwordmanager`
   - Build method : **Repository**
   - Repository URL : `https://github.com/DenAlexandre/PasswordManager.git`
   - Repository reference : `refs/heads/main`
   - Compose path : `docker-compose.yml`
   - **Automatic updates : désactivé** (redeploy manuel voulu — pas de webhook/polling GitOps).
2. **Environment variables** (section du formulaire de la stack, pas de fichier `.env` à
   committer — `docker-compose.yml` utilise déjà `${JWT_SECRET}`, `${POSTGRES_PASSWORD}`, etc.) :
   - `JWT_SECRET` — long secret aléatoire (obligatoire, pas de valeur par défaut en prod)
   - `POSTGRES_PASSWORD` — mot de passe Postgres (ne pas laisser la valeur par défaut de dev)
   - `POSTGRES_USER` / `POSTGRES_DB` — optionnels, défauts `postgres` / `passwordmanager`
   - `API_PORT` — optionnel, défaut `8080` (port loopback local, voir plus bas). **Sur
     `geekinfo-server`, le port `8080` est déjà pris par la stack `puissance4` (son frontend) —
     utiliser `API_PORT=8081` sur ce serveur précis.**
3. **Deploy the stack.**
4. **Créer le premier compte admin AVANT d'exposer le hostname public** (voir encadré
   sécurité ci-dessous) : Containers → `passwordmanager-back-1` → **Console** → `/bin/sh` →
   Connect, puis :
   ```
   curl -X POST http://localhost:8080/api/auth/setup -H "Content-Type: application/json" \
     -d '{"email":"admin@...","password":"..."}'
   ```
   (toujours `localhost:8080` ici : c'est le port **interne** au conteneur, indépendant du
   port publié sur l'hôte via `API_PORT`)
5. **Cloudflare Zero Trust → Networks → Tunnels → `geekinfo-server` → Public Hostname** :
   ajouter `passwordmanager.geekinfo.org` → service `HTTP` → `localhost:8081` (le port publié
   par le conteneur `back` sur l'hôte — `8081` sur `geekinfo-server`, cf. note `API_PORT` ci-dessus).

> **⚠️ Ordre important.** `POST /api/auth/setup` crée le premier compte **en admin, sans
> authentification**, et se désactive tout seul dès qu'un utilisateur existe (voir
> `AuthController.SetupAdmin`). Créez ce compte via la console du conteneur (étape 4) **avant**
> d'ajouter le hostname public Cloudflare (étape 5) : sinon, n'importe qui devinant l'URL avant
> vous peut créer ce premier compte admin à votre place.

Pour mettre à jour après un push sur `main` : dans la stack `passwordmanager`, bouton
**Pull and redeploy** (ou **Editor → Update the stack**, qui refait le `git clone` +
`docker compose up -d --build`).

Le port publié par `back` (`8080` par défaut, `8081` sur `geekinfo-server`) est bindé sur
`127.0.0.1` : seul le tunnel Cloudflare (en local sur la même machine) peut l'atteindre, rien
n'est exposé directement sur Internet. Postgres n'a lui-même aucun port publié (uniquement
accessible depuis le réseau Docker interne).

## Client MAUI — build de production

Les builds Release du client (celles utilisées pour `deploy/`) pointent par défaut sur
`https://passwordmanager.geekinfo.org` (voir `Services/AppSettings.cs`) ; les builds Debug
pointent sur `localhost`/`10.0.2.2` pour le développement local. Voir `deploy/Windows/`,
`deploy/Android/` et `deploy/iOS/` pour publier le client par plateforme.
