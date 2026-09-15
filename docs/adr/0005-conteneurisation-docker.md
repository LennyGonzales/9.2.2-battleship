# ADR 0005 : Environnement de développement Docker-only

## Statut et date

Accepté — 2026-03-15

## Contexte

Le projet Bataille Navale doit être développé en binôme sur cinq jours. L'équipe souhaite éviter d'installer le SDK .NET localement et standardiser l'environnement via Docker.

## Options envisagées

1. **SDK .NET installé localement** — Simple, IDE natif, mais dépend de la version installée sur chaque machine et diverge du workflow Docker de production.
2. **Dev Container (VS Code / Rider)** — Environnement reproductible dans l'IDE, mais configuration plus lourde et dépendante de l'éditeur.
3. **Docker Compose avec service SDK** — Toutes les commandes `dotnet` passent par un conteneur ; les runtimes API et App seront ajoutés aux étapes 5 et 7.

## Décision

Option 3 retenue : service `sdk` dans `docker-compose.yml`, script `scripts/dotnet.sh` comme raccourci, cache NuGet persistant via volume nommé `nuget`.

Raisons :
- Aucune installation locale du SDK requise
- Même version .NET 10 pour les deux membres du binôme
- Compatible avec l'ajout progressif des services `api` et `app`

## Conséquences

- Le README documente uniquement le workflow Docker
- `docker compose run --rm sdk dotnet build|test` remplace les commandes locales
- Risque iCloud Drive : les dossiers `obj/` et `bin/` doivent rester dans `.gitignore` et `.dockerignore`
- Les runtimes API (étape 5) et App (étape 7) compléteront ce fichier

## Vérification et réexamen

```bash
docker compose run --rm sdk dotnet --version   # doit afficher 10.x
docker compose run --rm sdk dotnet build
docker compose run --rm sdk dotnet test
```

Revoir cette décision si les bind-mounts sur iCloud provoquent des erreurs de build intermittentes (déplacer le dépôt hors iCloud).

## Références

- [Referentiel.md](../../../csharp-school/Ressources%20Bataille%20Navale/Referentiel.md) — diapo 28
- [docker-compose.yml](../../docker-compose.yml)
- [scripts/dotnet.sh](../../scripts/dotnet.sh)
