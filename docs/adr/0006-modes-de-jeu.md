# ADR 0006 : Modes de jeu PvE et PvP

## Statut et date

Accepté — 2026-03-15

## Contexte

Le projet démarre en mode joueur vs ordinateur (`VsComputer`). Une extension multijoueur (`VsPlayer`) est prévue sans refonte complète du domaine ni rupture du contrat PvE existant.

## Options envisagées

1. **Deux moteurs séparés** — `SoloGameEngine` et `MultiplayerGameEngine` : clair mais duplication des règles de placement et de tir.
2. **Un domaine neutre + stratégies de tour** — `Player1`/`Player2`, `GameMode`, résolution de tour selon le mode.
3. **Endpoints distincts par mode** — `/api/solo/*` et `/api/pvp/*` : explicite mais double maintenance du contrat.

## Décision

Option 2 retenue :

- `GameMode` : `VsComputer` (défaut) ou `VsPlayer`
- Grilles internes `Player1Board` / `Player2Board` (alias PvE : joueur / ordinateur)
- **PvE** : création place les 2 flottes, `Status = PlayerTurn`, pas de token
- **PvP** : création → `Waiting` sans flottes, token joueur 1 ; `POST /join` place les 2 flottes, token joueur 2, `Status = Player1Turn`
- Token retourné uniquement à la création (PvP) et au join ; **absent** de `GET /api/games/{id}`
- Header `X-Player-Token` documenté pour les routes futures `shots` et `board/*` en PvP
- Interfaces `IComputerOpponent` et `ITurnResolver` préparées pour `POST /shots`

## Conséquences

- `CreateGameRequest` accepte `mode` (défaut `VsComputer`) — rétrocompatible
- `GameDto` expose `mode` ; statuts PvP `Player1Turn` / `Player2Turn` distincts de PvE
- `difficulty` ignorée en validation si `mode = VsPlayer`
- Prochaine étape : implémenter `POST /shots` avec `ComputerTurnResolver` (PvE) et `HumanTurnResolver` (PvP)

## Vérification

```bash
./scripts/dotnet.sh test --filter "FullyQualifiedName~Game"
curl -s -X POST http://localhost:8080/api/games -H "Content-Type: application/json" -d '{"mode":"VsPlayer"}'
ID=<id>
curl -s -X POST http://localhost:8080/api/games/$ID/join
```

## Références

- [`swagger.yaml`](../../swagger.yaml)
- [`BattleShip.API/Services/GameEngine.cs`](../../BattleShip.API/Services/GameEngine.cs)
- [`BattleShip.Models/Domain/Game.Participants.cs`](../../BattleShip.Models/Domain/Game.Participants.cs)
