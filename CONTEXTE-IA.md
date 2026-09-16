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

Jeu de **bataille navale en solo contre l'ordinateur** : le joueur crée une partie, consulte l'état, tire sur la grille adverse, l'ordinateur répond selon l'alternance définie. Grille classique 10×10, flotte standard (5 navires). Règles serveur : placement sans chevauchement, tirs Miss/Hit/Sunk, fin de partie, coup refusé sans modification de l'état (409). Expérience visée : partie jouable dans le navigateur (Blazor), feedback clair sur les tirs et la fin de partie.

**Hors périmètre volontaire** : multijoueur humain (PvP), sauvegarde persistante, historique avancé — évoqués comme évolutions possibles dans le backlog, non planifiés pour cette version.

### Backlog, priorités et périmètre retenu

| Priorité | Fonctionnalité | Statut |
|----------|----------------|--------|
| P0 — Socle | Docker-only, solution 4 projets, contrat `swagger.yaml` | Fait |
| P0 | `POST /api/games` (création + placement flottes) | Fait |
| P0 | `GET /api/games/{id}` | Fait |
| P0 | `POST /api/games/{id}/shots` (tir joueur + réponse ordinateur) | À faire |
| P0 | `GET /api/games/{id}/board/player` et `/board/opponent` | À faire |
| P0 | Front Blazor (création partie, grilles, tir) | À faire |
| P0 | gRPC-Web `GameStats` (API + client Blazor) | Fait |
| P1 | Stratégie de l'ordinateur selon `Difficulty` (`DifficultyComputerOpponent`) | Fait |
| P1 | Fondation PvP (`GameMode`, join, tokens) — voir ADR 0006 | Fait |
| P1 | `POST /shots` et `GET /board/*` en PvE et PvP | À faire |
| P2 | Sauvegarde fichier/DB, stats avancées | Backlog |

### Organisation du code et contrats

- **`BattleShip.Models`** : domaine (`Game`, `Board`, `Ship`, …), contrats DTO (`GameDto`, `CreateGameRequest`, …), interfaces (`IGameEngine`, `IGameRepository`).
- **`BattleShip.API`** : endpoints Minimal API, services (`GameEngine`, `InMemoryGameRepository`, `GameMapper`), validation FluentValidation.
- **`BattleShip.App`** : Blazor WASM, consomme REST + gRPC-Web (`GrpcGameStatsClient`, panneau `StatusConsole`).
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
- **Binôme** : *noms à compléter*.

### Décisions structurantes et références des ADR

| ADR | Sujet | Statut |
|-----|-------|--------|
| [0003](docs/adr/0003-contrat-api.md) | Contrat API REST (`GameDto` minimal, codes HTTP) | Accepté |
| [0005](docs/adr/0005-conteneurisation-docker.md) | Environnement Docker-only | Accepté |
| [0006](docs/adr/0006-modes-de-jeu.md) | Modes PvE / PvP, tokens, join | Accepté |
| 0001 | Découpage couches Models / API / App | À rédiger |
| 0002 | Stockage InMemory (alternatives : fichier, DB) | À rédiger |
| [0004](docs/adr/0004-echange-grpc.md) | Opération gRPC `GameStats` | Accepté |

Décisions clés déjà actées : persistance InMemory en singleton ; validation FluentValidation côté API ; dual-mode **VsComputer** (défaut) / **VsPlayer** ; `GameDto` sans positions de navires ni tokens.

### Vérifications réalisées et limites connues

**Réalisé et vérifié** (2026-03-15) :
- `POST /api/games` : 201/400, 13 tests `CreateGame*`.
- `GET /api/games/{id}` : 200/404, 2 tests `GetGame*`.
- Build et tests via conteneur SDK ; API dockerisée accessible sur `:8080`.

**Limites connues** :
- Parties perdues au redémarrage du conteneur API (InMemory).
- Développement sur iCloud Drive : risque de lenteur ou fichiers manquants sur `obj/`/`bin/` (ignorés par git).
- `Difficulty` exploité par `DifficultyComputerOpponent` (Easy / Normal / Hard) ; non exposée dans `GameDto`.
- Stats gRPC affichées dans `StatusConsole` en mode API réelle ; pas de stats gRPC en `UseMockApi`.

### Arbitrages et évolution du périmètre

| Choix | Retenu | Écarté / reporté | Justification |
|-------|--------|------------------|---------------|
| Mode de jeu | PvE + fondation PvP (join, tokens) | PvP complet (shots/boards avec token) | Socle PvE d'abord ; PvP étendu via ADR 0006 sans casser le contrat existant. |
| Stockage | InMemory | Base de données | Suffisant pour le TP ; ADR 0002 documentera les alternatives. |
| Contrat création | `GameDto` sans grilles | Grilles dans le POST | Règle de visibilité + routes `/board/*` dédiées. |
| SDK | Docker uniquement | SDK local | Reproductibilité binôme (ADR 0005). |
| Stratégie de l'ordinateur | Tir aléatoire (V1) | Tir « chasse » après un touché | Livrable socle d'abord ; enrichissement backlog P1 (`IComputerOpponent`). |
