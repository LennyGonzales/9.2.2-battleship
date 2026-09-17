# BattleShip — Bataille Navale

![CI](https://github.com/LennyGonzales/9.2.2-battleship/actions/workflows/ci.yml/badge.svg)

Projet scolaire C# / ASP.NET Core (.NET 10).

## Prérequis

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (ou Docker Engine + Docker Compose v2)
- Aucun SDK .NET local requis

## Lancer l'application

```bash
docker compose up --build
```

Sur Mac Apple Silicon, les services `api`, `app` et `sdk` sont configurés en `linux/amd64` dans `docker-compose.yml` (contournement d'un crash `protoc` gRPC en ARM dans Docker).

| Service | URL |
|---------|-----|
| Front Blazor | http://localhost:8081 |
| API | http://localhost:8080 |
| OpenAPI (dev) | http://localhost:8080/openapi/v1.json |

## Développement (build / test)

```bash
# Raccourci
./scripts/dotnet.sh build
./scripts/dotnet.sh test

# Ou directement
docker compose --profile tools run --rm sdk dotnet build
docker compose --profile tools run --rm sdk dotnet test
```

## CI (GitHub Actions)

Workflow Docker-only [`.github/workflows/ci.yml`](.github/workflows/ci.yml), déclenché sur **push** et **pull request** vers `main` :

1. `./scripts/dotnet.sh build`
2. `./scripts/dotnet.sh test --no-build`
3. `docker compose build api app`

Résultats : onglet **Actions** du dépôt [github.com/LennyGonzales/9.2.2-battleship](https://github.com/LennyGonzales/9.2.2-battleship).

## Structure

| Projet | Rôle |
|--------|------|
| `BattleShip.Models` | Domaine et contrats partagés |
| `BattleShip.API` | API ASP.NET Core Minimal API |
| `BattleShip.App` | Front Blazor WebAssembly |
| `BattleShip.Tests` | Tests xUnit |

## Documentation

- Contrat REST : [`swagger.yaml`](swagger.yaml)
- Essais manuels : [`api.http`](api.http)
- Décisions d'architecture : [`docs/adr/`](docs/adr/)
- Contexte projet : [`CONTEXTE-IA.md`](CONTEXTE-IA.md)
- Échanges IA : [`PROMPTS.md`](PROMPTS.md) (backend) et [`PROMPTS-FRONT.md`](PROMPTS-FRONT.md) (frontend)
- Revues IA : [`REVUE-IA.md`](REVUE-IA.md) (backend) et [`REVUE-IA-FRONT.md`](REVUE-IA-FRONT.md) (frontend)

## API — POST /api/games

Endpoint implémenté : création d'une partie contre l'ordinateur (placement aléatoire des flottes).

```bash
# Partie avec options
curl -i -X POST http://localhost:8080/api/games \
  -H "Content-Type: application/json" \
  -d '{"boardSize":10,"difficulty":"Normal"}'

# Validation echouee (boardSize < 5)
curl -i -X POST http://localhost:8080/api/games \
  -H "Content-Type: application/json" \
  -d '{"boardSize":4}'
```

Réponse attendue en succès : `201 Created`, header `Location: /api/games/{id}`, corps `GameDto` JSON (sans positions de navires).

## API — GET /api/games/{id}

Endpoint implémenté : lecture de l'état visible d'une partie existante.

```bash
# Creer une partie puis lire son etat
ID=$(curl -s -X POST http://localhost:8080/api/games -H "Content-Type: application/json" -d '{}' | jq -r .id)
curl -i http://localhost:8080/api/games/$ID

# Partie inconnue
curl -i http://localhost:8080/api/games/00000000-0000-0000-0000-000000000000
```

Réponse attendue en succès : `200 OK`, corps `GameDto` JSON. Partie introuvable : `404 Not Found`, `application/problem+json`.

## API — PvP (joueur vs joueur)

Fondation dual-mode : création en `Waiting`, join place les flottes et retourne un token joueur 2.

```bash
# Creer une partie PvP
CREATE=$(curl -s -X POST http://localhost:8080/api/games \
  -H "Content-Type: application/json" \
  -d '{"mode":"VsPlayer","boardSize":10}')
ID=$(echo "$CREATE" | jq -r .id)
TOKEN_P1=$(echo "$CREATE" | jq -r .playerToken)

# Second joueur rejoint
curl -i -X POST http://localhost:8080/api/games/$ID/join

# Lire l'etat (sans token dans la reponse)
curl -i http://localhost:8080/api/games/$ID
```

Les routes `shots` et `board/*` utilisent le header `X-Player-Token` en PvP.

## Stats gRPC — `GameStats.GetGameStats`

Les statistiques de partie sont exposées via gRPC-Web (hors REST). Contrat : [`Protos/battleship.proto`](Protos/battleship.proto).

```bash
# Lancer l'API seule
docker compose up --build -d api

# Créer une partie et placer la flotte
ID=$(curl -s -X POST http://localhost:8080/api/games -H "Content-Type: application/json" -d '{}' | jq -r .id)
curl -s -X POST http://localhost:8080/api/games/$ID/fleet \
  -H "Content-Type: application/json" \
  -d '{"ships":[{"name":"Porte-avions","x":0,"y":0,"horizontal":true},{"name":"Croiseur","x":0,"y":1,"horizontal":true},{"name":"Contre-torpilleur","x":0,"y":2,"horizontal":true},{"name":"Sous-marin","x":0,"y":3,"horizontal":true},{"name":"Torpilleur","x":0,"y":4,"horizontal":true}]}'

# Interroger les stats (grpcurl requis)
grpcurl -plaintext -d "{\"game_id\":\"$ID\"}" \
  localhost:8080 battleship.GameStats/GetGameStats
```

Erreurs attendues : `InvalidArgument` (GUID invalide), `NotFound` (partie inconnue).

### Depuis le navigateur (Blazor)

```bash
docker compose up --build
```

1. Ouvrir http://localhost:8081
2. Créer une partie PvE, placer la flotte, tirer sur la grille adverse
3. Le panneau HUD affiche les compteurs gRPC (tirs/touches des deux côtés)
4. DevTools → Network : requête gRPC-Web vers `localhost:8080`

**Barre de recherche (header)** — consultation stats via gRPC uniquement :

1. Copier l'identifiant depuis l'URL (`/battle/{id}`) et le coller dans le champ **Mission** → panneau stats sous le header
2. Saisir `test` → `InvalidArgument`
3. Saisir un UUID valide mais inconnu → `NotFound`

La barre de recherche est désactivée en mode mock (`UseMockApi: true`).
