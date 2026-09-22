# Contexte du projet

## Contraintes du cours

- .NET 10 stable ; SDK conteneurisé via `global.json` et service Docker `sdk` (pas de SDK local).
- Quatre projets : `BattleShip.Models`, `BattleShip.API` (Minimal API), `BattleShip.App` (Blazor WASM), `BattleShip.Tests` (xUnit).
- FluentValidation sur les entrées serveur, avec appel explicite à `ValidateAsync` dans les endpoints.
- Au moins un échange gRPC-Web fonctionnel depuis le navigateur (Fait ; hors mode `UseMockApi`).
- Règles vérifiées côté serveur ; positions adverses non découvertes jamais exposées dans les DTO.
- Livrables : `README.md`, `PROMPTS.md`, `REVUE-IA.md` (3 revues minimum), `docs/adr/`, historique Git relu.
- Projet évalué au-delà du socle : ambition, pertinence et qualité des extensions retenues.

## Vision du projet, règles et expérience visée

Jeu de **bataille navale** en **PvE** (solo contre l'ordinateur) ou **PvP** (deux joueurs via lien + tokens) : le joueur crée une partie, place sa flotte, consulte l'état, tire sur la grille adverse ; l'ordinateur ou l'autre joueur répond selon l'alternance définie. Grille 5–20 (défaut 10×10), flotte standard (5 navires). Règles serveur : placement sans chevauchement, tirs Miss/Hit/Sunk, fin de partie, coup refusé sans modification de l'état (409). Expérience visée : partie jouable dans le navigateur (Blazor), feedback clair sur les tirs, power-ups et fin de partie.

**Hors périmètre volontaire** : matchmaking en ligne, sauvegarde persistante, historique avancé / classements globaux — évoqués comme évolutions possibles dans le backlog, non planifiés pour cette version.

### Backlog, priorités et périmètre retenu

| Priorité | Fonctionnalité | Statut |
|----------|----------------|--------|
| P0 — Socle | Docker-only, solution 4 projets, contrat `swagger.yaml` | Fait |
| P0 | `POST /api/games` (création → `PlacingFleet`) | Fait |
| P0 | `GET /api/games/{id}` | Fait |
| P0 | `POST /api/games/{id}/fleet` (placement manuel joueur + ordi auto) | Fait |
| P0 | `POST /api/games/{id}/shots` (un tir par requête, PvE et PvP) | Fait |
| P0 | `GET /api/games/{id}/board/player` et `/board/opponent` | Fait |
| P0 | Front Blazor (briefing, déploiement, grilles, tir, fin de partie) | Fait |
| P0 | gRPC-Web `GameStats` (API + client Blazor + barre de recherche header) | Fait |
| P1 | Stratégie de l'ordinateur selon `Difficulty` (`DifficultyComputerOpponent`) | Fait |
| P1 | PvP (`GameMode`, join, tokens, shots/boards avec `X-Player-Token`) — voir ADR 0006 | Fait |
| P1 | Power-ups par navire (`POST /powerups`) — voir ADR 0007 | Fait |
| P1 | Obstacles (îlots) sur la grille (`ObstacleGenerationOptions`) | Fait |
| P1 | CI GitHub Actions Docker-only (build, tests, images) | Fait |
| P2 | Sauvegarde fichier/DB, stats avancées / historique global | Backlog |

### Organisation du code et contrats

- **`BattleShip.Models`** : domaine (`Game`, `Board`, `Ship`, …), contrats DTO (`GameDto`, `CreateGameRequest`, …), interfaces (`IGameEngine`, `IGameRepository`).
- **`BattleShip.API`** : endpoints Minimal API, services (`GameEngine`, `InMemoryGameRepository`, `GameMapper`), validation FluentValidation, gRPC `GameStatsGrpcService`.
- **`BattleShip.App`** : Blazor WASM, consomme REST + gRPC-Web (`GrpcGameStatsClient`, panneaux `StatusConsole` et `MissionStatsPanel`).
- **`BattleShip.Tests`** : validateurs, domaine, intégration API (`WebApplicationFactory`).
- Contrat REST source de vérité : [`swagger.yaml`](swagger.yaml). OpenAPI généré : `/openapi/v1.json` (contrôle croisé, pas de remplacement du swagger versionné).

### Commandes, ports et environnement

```bash
docker compose up --build          # front :8081, API :8080
./scripts/dotnet.sh build
./scripts/dotnet.sh test
docker compose --profile tools run --rm sdk dotnet <commande>
```

| Service | Port | Rôle |
|---------|------|------|
| `api` | 8080 | API ASP.NET Core |
| `app` | 8081 | Blazor WASM (nginx) |
| `sdk` | — | Build/test (profil `tools`) |

CORS : origine `http://localhost:8081` autorisée vers l'API.

### Conventions et méthode de collaboration

- **Git** : commits atomiques par fonctionnalité ; messages en anglais ou français, impératif (« feat(api): … »).
- **Assistant IA (développement)** : prompts décisifs tracés dans [`PROMPTS.md`](PROMPTS.md) ; revues argumentées dans [`REVUE-IA.md`](REVUE-IA.md).
- **Plans d'implémentation** : [`docs/plans/`](docs/plans/) avant chaque route majeure.
- **Tests** : exécutés via Docker (`./scripts/dotnet.sh test`) ; nommage `FullyQualifiedName~<Feature>` pour les filtres.
- **CI** : GitHub Actions Docker-only sur `main` (build, tests, build images `api`/`app`) — voir [`.github/workflows/ci.yml`](.github/workflows/ci.yml).
- **Binôme** : Lenny Gonzales, Nils Saadi.

### Décisions structurantes et références des ADR

| ADR | Sujet | Statut |
|-----|-------|--------|
| [0001](docs/adr/0001-couches-architecture.md) | Découpage couches Models / API / App | Accepté |
| [0002](docs/adr/0002-stockage-partie.md) | Stockage InMemory (alternatives : fichier, DB) | Accepté |
| [0003](docs/adr/0003-contrat-api.md) | Contrat API REST (`GameDto` minimal, codes HTTP) | Accepté |
| [0004](docs/adr/0004-echange-grpc.md) | Opération gRPC `GameStats` | Accepté |
| [0005](docs/adr/0005-conteneurisation-docker.md) | Environnement Docker-only | Accepté |
| [0006](docs/adr/0006-modes-de-jeu.md) | Modes PvE / PvP, tokens, join | Accepté |
| [0007](docs/adr/0007-power-ups-navires.md) | Power-ups par navire | Accepté |

Décisions clés déjà actées : persistance InMemory en singleton ; validation FluentValidation côté API ; dual-mode **VsComputer** (défaut) / **VsPlayer** ; `GameDto` sans positions de navires ni tokens ; placement manuel joueur via `POST /fleet`.

### Vérifications réalisées et limites connues

**Réalisé et vérifié** (2026-09-22) :
- `POST /api/games` : 201/400, tests `CreateGame*`.
- `GET /api/games/{id}` : 200/404, tests `GetGame*`.
- `POST /fleet`, `POST /shots`, `GET /board/*`, `POST /join`, `POST /powerups` : tests domaine et intégration API.
- gRPC-Web `GameStats` : tests `GameStatsGrpc*` ; HUD Blazor + barre de recherche header.
- Front Blazor jouable de bout en bout via `docker compose up --build`.
- Build et tests via conteneur SDK ; CI GitHub Actions sur `main`.

**Limites connues** :
- Parties perdues au redémarrage du conteneur API (InMemory).
- `Difficulty` exploité par `DifficultyComputerOpponent` (Easy / Normal / Hard) ; non exposée dans `GameDto`.
- Stats gRPC affichées dans `StatusConsole` et `MissionStatsPanel` en mode API réelle ; pas de stats gRPC en `UseMockApi`.

### Arbitrages et évolution du périmètre

| Choix | Retenu | Écarté / reporté | Justification |
|-------|--------|------------------|---------------|
| Mode de jeu | PvE + PvP (join, tokens, shots/boards) | Matchmaking en ligne | PvP local par lien suffisant pour le TP (ADR 0006). |
| Placement des flottes | Joueur manuel (+ option aléatoire UI) ; ordinateur aléatoire | Placement 100 % aléatoire à la création | Meilleure expérience de jeu (ADR 0003). |
| Stockage | InMemory | Base de données | Suffisant pour le TP (ADR 0002). |
| Contrat création | `GameDto` sans grilles | Grilles dans le POST | Règle de visibilité + routes `/board/*` dédiées. |
| SDK | Docker uniquement | SDK local | Reproductibilité binôme (ADR 0005). |
| Stratégie de l'ordinateur | `DifficultyComputerOpponent` (Easy / Normal / Hard) | — | Tir aléatoire, damier et chasse selon difficulté (ADR 0003). |
| Stats avancées | Compteurs gRPC par partie | Historique global, classements | Hors REST, exposé via gRPC uniquement (ADR 0004). |
