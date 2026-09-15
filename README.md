# BattleShip — Bataille Navale

Projet scolaire C# / ASP.NET Core (.NET 10).

## Prérequis

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (ou Docker Engine + Docker Compose v2)
- Aucun SDK .NET local requis

## Lancer l'application

```bash
docker compose up --build
```

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
- Échanges IA : [`PROMPTS.md`](PROMPTS.md)
- Revues IA : [`REVUE-IA.md`](REVUE-IA.md)

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

Les statistiques de partie passent par gRPC-Web (non implémenté à ce stade).
