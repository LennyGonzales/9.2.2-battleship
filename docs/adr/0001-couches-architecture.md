# ADR 0001 : Organisation en couches et graphe de dépendances

## Statut et date

Accepté — 2026-03-15

## Contexte

Le projet impose quatre projets (`BattleShip.Models`, `BattleShip.API`, `BattleShip.App`, `BattleShip.Tests`)
et une contrainte forte du référentiel (diapo 28-29) : le moteur de jeu doit rester vérifiable indépendamment
des transports (HTTP, gRPC) et de l'interface. Il faut définir le sens des dépendances entre projets et où
vit la logique métier, avant d'écrire la première règle du jeu.

## Options envisagées

1. **Logique métier dupliquée dans l'API et le front** — Chaque projet réimplémente ses propres règles.
   Rapide à démarrer, mais duplique la logique de jeu, rend la cohérence impossible à garantir et empêche de
   tester le moteur indépendamment du transport.
2. **`BattleShip.Models` comme bibliothèque de DTOs uniquement, moteur de jeu dans `BattleShip.API`** — Sépare
   contrats et implémentation, mais le moteur reste couplé à ASP.NET Core (impossible à tester sans démarrer
   l'hôte web) et invisible depuis `BattleShip.App`.
3. **`BattleShip.Models` comme cœur du domaine (entités + interfaces de service), sans dépendance vers
   aucun autre projet ; `BattleShip.API` et `BattleShip.App` en dépendent tous les deux** — Le moteur de jeu
   (`Game`, `Board`, `Ship`, règles de tir et de pose) et les contrats DTO vivent dans `Models` ; l'API
   référence `Models` et implémente les interfaces (`IGameRepository`, `IGameEngine`) avec ASP.NET Core,
   FluentValidation et gRPC ; le front référence `Models` uniquement pour les types partagés et n'a aucune
   règle de jeu serveur.

## Décision

Option 3 retenue : graphe de dépendances à sens unique.

```
BattleShip.Models   (aucune dépendance : POCOs, interfaces, DTOs)
        ^                      ^
        |                      |
BattleShip.API           BattleShip.App
(implémente les            (Blazor WASM, consomme
 interfaces, HTTP,          les contrats via HTTP
 FluentValidation, gRPC)     et gRPC-Web)

BattleShip.Tests -> API + Models (pilote l'API via WebApplicationFactory<Program>)
```

- `BattleShip.Models` ne référence aucun package ASP.NET Core, HTTP, JSON ou gRPC : il contient uniquement
  les entités du domaine (`Models/Domain/`), les interfaces de service (`Models/Services/IGameEngine`,
  `IGameRepository`, `IComputerOpponent`) et les DTOs de contrat (`Models/Contracts/`).
- `BattleShip.API` référence `Models`, implémente les interfaces (`GameEngine`, `InMemoryGameRepository`,
  `DifficultyComputerOpponent`) et possède seule les préoccupations de transport (endpoints Minimal API,
  validateurs FluentValidation appelés explicitement, service gRPC).
- `BattleShip.App` référence `Models` pour les types partagés (DTOs, enums) mais ne référence jamais `API` :
  tous les échanges passent par HTTP/JSON ou gRPC-Web via `IGameApiClient`, jamais par un appel direct au
  moteur de jeu.
- Un mode mock côté front (`MockGameApiClient`, voir `CLAUDE.md`) réimplique volontairement des règles côté
  client pour fonctionner hors-ligne ; ce n'est pas une violation de cette ADR car `BattleShip.API` reste la
  seule implémentation faisant autorité côté serveur.

## Conséquences

- Le moteur de jeu est testable sans hôte web : `BattleShip.Tests` instancie `GameEngine` directement avec
  un `InMemoryGameRepository` en mémoire (voir `BattleShip.Tests/Domain/`).
- Toute règle de jeu nouvelle s'ajoute dans `Models/Domain` ou dans `GameEngine`, jamais dans un composant
  Razor ni dans un endpoint.
- Le front ne peut pas contourner l'API pour accéder à l'état serveur : toute divergence entre le mock et le
  moteur réel est un risque connu et documenté (voir `MockGameApiClient` dans `BattleShip.App/Services/`),
  pas une fuite de dépendance.

## Vérification et réexamen

```bash
# BattleShip.Models ne doit référencer aucun package ASP.NET Core / HTTP / gRPC
grep -c "PackageReference" BattleShip.Models/BattleShip.Models.csproj   # attendu : 0
# BattleShip.App ne doit jamais référencer BattleShip.API
grep "ProjectReference" BattleShip.App/BattleShip.App.csproj           # attendu : Models uniquement
./scripts/dotnet.sh test --filter "FullyQualifiedName~GameEngine"
```

À revoir si un composant du front doit un jour accéder à une règle de jeu sans passer par `IGameApiClient` —
signe que la frontière domaine/transport ne tient plus.

## Références

- Référentiel C# / ASP.NET Core, diapo 28-29 (« Les responsabilités à organiser »)
- [`BattleShip.Models/Services/IGameEngine.cs`](../../BattleShip.Models/Services/IGameEngine.cs)
- [`BattleShip.API/Services/GameEngine.cs`](../../BattleShip.API/Services/GameEngine.cs)
- [`CLAUDE.md`](../../CLAUDE.md) — section « Front-end mock mode »
