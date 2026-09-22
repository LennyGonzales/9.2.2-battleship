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
- **PvE** : création → `PlacingFleet` (grille joueur vide) ; `POST /fleet` place joueur + ordi auto → `PlayerTurn`, pas de token
- **PvP** : création → `Waiting` + grille J1 vide + token J1 ; `POST /join` → `PlacingFleet` + grille J2 vide + token J2 ; chaque joueur `POST /fleet` → `Player1Turn` quand les deux flottes sont placées
- Token retourné uniquement à la création (PvP) et au join ; **absent** de `GET /api/games/{id}`
- Header `X-Player-Token` requis sur `shots`, `board/*` et `fleet` en PvP
- `IComputerOpponent` pour le tir ordinateur sur `ComputerTurn`
- `POST /shots` : un tir par requête ; le front PvE enchaîne joueur puis ordinateur

## Conséquences

- `CreateGameRequest` accepte `mode` (défaut `VsComputer`) — rétrocompatible
- `GameDto` expose `mode` ; statuts PvP `Player1Turn` / `Player2Turn` distincts de PvE
- `difficulty` ignorée en validation si `mode = VsPlayer`
- PvE et PvP partagent la même route `POST /shots` avec sémantique selon `status` courant

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
