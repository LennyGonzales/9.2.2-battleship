# BattleShip — Bataille Navale

Projet scolaire C# / ASP.NET Core (.NET 10).

## Prérequis

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (ou Docker Engine + Docker Compose v2)
- Aucun SDK .NET local requis

## Commandes

Toutes les commandes `dotnet` passent par le conteneur SDK :

```bash
# Raccourci
./scripts/dotnet.sh build
./scripts/dotnet.sh test

# Ou directement
docker compose run --rm sdk dotnet build
docker compose run --rm sdk dotnet test
```

## Structure

| Projet | Rôle |
|--------|------|
| `BattleShip.Models` | Domaine et contrats partagés |
| `BattleShip.API` | API ASP.NET Core Minimal API |
| `BattleShip.App` | Front Blazor WebAssembly |
| `BattleShip.Tests` | Tests xUnit |

## Documentation

- Décisions d'architecture : [`docs/adr/`](docs/adr/)
- Échanges IA : [`PROMPTS.md`](PROMPTS.md) (à compléter)
