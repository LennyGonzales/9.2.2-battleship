# ADR 0004 : Statistiques de partie via gRPC-Web

## Statut et date

Accepté — 2026-09-16

## Contexte

Les statistiques agrégées (nombre de tirs, touches, statut) sont une lecture seule, sans effet sur l'état de la partie — un bon candidat pour un contrat RPC dédié plutôt qu'une route REST supplémentaire.

Le contrat est défini dans [`Protos/battleship.proto`](../../Protos/battleship.proto) (aligné sur [`PROMPT-INIT.md`](../../PROMPT-INIT.md)).

## Décision

Exposer `GameStats.GetGameStats` côté API via `Grpc.AspNetCore` + `Grpc.AspNetCore.Web` :

- **Entrée** : `game_id` (string GUID)
- **Sortie** : compteurs dérivés des grilles en mémoire + `status` textuel (`GameStatus.ToString()`)
- **Erreurs** :
  - `InvalidArgument` — `game_id` vide ou non GUID
  - `NotFound` — partie inconnue

### Mapping des champs

| Champ proto | Source |
|-------------|--------|
| `player_shots` | cases ciblées (`Miss` / `Hit` / `Sunk`) sur `Player2Board` |
| `computer_shots` | cases ciblées sur `Player1Board` |
| `player_hits` | `Hit` + `Sunk` sur `Player2Board` |
| `computer_hits` | `Hit` + `Sunk` sur `Player1Board` |
| `status` | `game.Status` |

En PvE, `player_shots` doit rester aligné avec `Game.ShotCount` (tirs humains valides uniquement). En PvP, les mêmes noms de champs désignent les tirs du joueur 1 sur la grille du joueur 2 (`player_shots`) et inversement (`computer_shots`).

Le calcul est **stateless** (`GameStatsCalculator`) : aucun champ supplémentaire sur `Game`.

Pas de token dans le proto — stats globales de la partie (spec cours).

## Conséquences

- Les stats ne figurent pas dans [`swagger.yaml`](../../swagger.yaml) ; démo via tests d'intégration et **grpcurl**
- Le front Blazor consommera ce service dans une passe ultérieure (`Grpc.Net.Client.Web`)
- `BattleShip.Models` reste sans dépendance gRPC

## Vérification

```bash
./scripts/dotnet.sh test --filter "FullyQualifiedName~GameStats"
grpcurl -plaintext -d '{"game_id":"<uuid>"}' \
  localhost:8080 battleship.GameStats/GetGameStats
```
