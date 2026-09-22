# ADR 0003 : Contrat API REST

## Statut et date

Accepté — 2026-03-15

## Contexte

Le front Blazor WebAssembly consomme l'API ASP.NET Core via HTTP. Le contrat REST est défini dans [`swagger.yaml`](../../swagger.yaml). `POST /api/games` crée une partie (PvE ou PvP) et retourne un `GameDto` minimal ; le placement des flottes passe par `POST /api/games/{id}/fleet`.

## Options envisagées

1. **Retourner les grilles complètes à la création** — Simplifie le front mais viole la règle de visibilité (positions adverses jamais exposées).
2. **Retourner un `GameDto` minimal** — Identifiant, statut, tour courant, taille de grille, compteur de coups ; les grilles passent par des routes dédiées (`GET .../board/*`).
3. **RPC / gRPC pour toute la logique** — Cohérent pour les stats (gRPC-Web prévu), mais le cours impose REST pour le cycle de jeu.

## Décision

Option 2 retenue : `POST /api/games` et `GET /api/games/{id}` retournent un `GameDto` sans aucune position de navire.

- Body optionnel `CreateGameRequest` (`boardSize`, `difficulty`) avec défauts 10 × 10 et `Normal`
- Réponse `201 Created` + header `Location: /api/games/{id}`
- `GET /api/games/{id}` retourne `200` + `GameDto` ou `404` ProblemDetails (`detail: "Partie inconnue"`)
- Erreurs de validation → `400` avec `ValidationProblemDetails` (FluentValidation)
- `POST /api/games` PvE retourne `PlacingFleet` avec grille joueur vide ; `POST /api/games/{id}/fleet` place la flotte joueur puis l'ordinateur automatiquement
- PvP : chaque joueur place sa flotte via `POST /fleet` avec `X-Player-Token` ; la partie démarre quand les deux flottes sont validées
- `POST /api/games/{id}/shots` : **un tir par requête** ; PvE alterne `PlayerTurn` (body `{x,y}`) et `ComputerTurn` (body vide) ; PvP exige `X-Player-Token` ; réponse `ShotResultDto` (`shot`, `shooter`, `status`) ; `409` si coup refusé sans mutation
- `difficulty` (PvE) : `Easy` = tir aléatoire ; `Normal` = damier `(x+y)` pair puis aléatoire ; `Hard` = chasse autour des `Hit` puis `Normal` (`DifficultyComputerOpponent`, stateless)

## Conséquences

- Le front devra enchaîner `POST /api/games` → placement local → `POST /api/games/{id}/fleet` → `GET /api/games/{id}/board/player`
- La validation est explicite côté API (`CreateGameRequestValidator`), pas dans le domaine
- Les statistiques de partie restent hors REST (service gRPC `GameStats`, voir [ADR 0004](0004-echange-grpc.md))
- Persistance en mémoire (`InMemoryGameRepository`) suffisante pour le TP

## Vérification et réexamen

```bash
./scripts/dotnet.sh test --filter "FullyQualifiedName~CreateGame"
./scripts/dotnet.sh test --filter "FullyQualifiedName~GetGame"
./scripts/dotnet.sh test --filter "FullyQualifiedName~Shot"
docker compose up --build -d api
curl -i -X POST http://localhost:8080/api/games -H "Content-Type: application/json" -d '{"boardSize":10,"difficulty":"Normal"}'
curl -i -X POST http://localhost:8080/api/games -H "Content-Type: application/json" -d '{"boardSize":4}'
curl -i http://localhost:8080/api/games/00000000-0000-0000-0000-000000000000
curl -s http://localhost:8080/openapi/v1.json | grep -F '"/api/games'
```

Revoir si le contrat OpenAPI généré diverge de `swagger.yaml` après modification du contrat.

## Références

- [`swagger.yaml`](../../swagger.yaml) — schémas `CreateGameRequest`, `GameDto`
- [`BattleShip.API/Endpoints/GameEndpoints.cs`](../../BattleShip.API/Endpoints/GameEndpoints.cs)
- [`BattleShip.API/Validation/CreateGameRequestValidator.cs`](../../BattleShip.API/Validation/CreateGameRequestValidator.cs)
- [`api.http`](../../api.http)
