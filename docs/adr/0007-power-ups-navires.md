# ADR 0007 : Power-ups par navire

## Statut et date

Accepté — 2026-09-16

## Contexte

Chaque navire de la flotte doit disposer d'un power-up unique, utilisable une seule fois par
partie et uniquement tant que le navire n'est pas coulé. Activer un power-up consomme le tour du
joueur (pas de tir normal ce tour-ci). Le mécanisme doit fonctionner à la fois en PvE et en PvP, et
être utilisable aussi bien par le joueur humain que par l'ordinateur.

Le projet duplique déjà les règles de jeu entre le moteur serveur faisant autorité
(`BattleShip.API`/`BattleShip.Models`) et le moteur mock du front (`MockGameApiClient`) — voir
`docs/adr/0006-modes-de-jeu.md`. La question est où placer la logique des power-ups et comment
l'exposer côté contrat API.

## Options envisagées

1. **Power-ups uniquement côté mock (front)** — jouable immédiatement, mais viole le principe
   « les règles validées côté serveur » de `PROMPT-INIT.md` et crée une divergence durable entre
   mock et backend réel.
2. **Power-ups uniquement côté backend réel** — respecte la couche serveur-autoritative, mais rend
   la fonctionnalité injouable tant que `UseMockApi=true` reste la valeur par défaut du front.
3. **Power-ups dans les deux moteurs, avec une table de correspondance navire→power-up partagée
   depuis `BattleShip.Models`** — duplique l'effort d'implémentation (comme le reste des règles de
   jeu aujourd'hui) mais garde le front jouable immédiatement tout en gardant le backend comme
   référence.
4. **Un endpoint dédié par power-up** (`/powerups/recon`, `/powerups/torpedo`, ...) vs **un
   endpoint unique** (`POST /api/games/{id}/powerups`) avec une charge utile dont la forme varie
   selon le type de navire, validée conditionnellement par FluentValidation (comme `ShotRequest`
   gère déjà tir joueur et tir ordinateur dans un seul type).

## Décision

Option 3 retenue pour l'emplacement de la logique, et endpoint unique retenu pour l'option 4 :

- `BattleShip.Models` porte la table `GameOptions.PowerUpByShipName` (source de vérité unique,
  lue par le backend et par le mock) et les nouveaux membres de domaine (`PowerUpType`,
  `Ship.PowerUpUsed`, `CellState.Decoy`/`DecoyHit`, méthodes de résolution sur `Board`).
- `BattleShip.API` implémente le moteur faisant autorité : `IGameEngine.UsePowerUpAsync`,
  validation FluentValidation explicite, masquage des positions adverses non découvertes dans la
  couche de mapping (jamais dans le domaine).
- `MockGameApiClient` duplique le même comportement contre son propre `Board`, en lisant la même
  table de correspondance — même pattern que le placement de flotte, la résolution de tir et la
  difficulté déjà dupliqués aujourd'hui.
- Un seul endpoint REST, `POST /api/games/{id}/powerups`, avec un DTO `UsePowerUpRequest` dont les
  champs optionnels sont validés conditionnellement selon le navire ciblé — évite de multiplier les
  routes et la duplication de tests pour un contrat qui reste, malgré la variation de forme, une
  seule action de jeu (« utiliser un power-up ce tour »).
- Le leurre (contre-torpilleur) absorbe un tir comme un vrai navire (résultat `Hit`, jamais
  `Sunk`), sans jamais compter dans `AreAllShipsSunk` — implémenté via une paire d'états de cellule
  (`Decoy`/`DecoyHit`) qui réutilise le pattern déjà existant des îlots (`Obstacle`/`ObstacleHit`)
  plutôt que d'introduire un concept de « faux navire » séparé.
- L'ordinateur utilise les power-ups via une heuristique simple ajoutée à
  `DifficultyComputerOpponent` (et son équivalent mock), pas un nouveau sous-système d'IA.

## Conséquences

- `Ship` gagne `PowerUpType` et `PowerUpUsed` ; un power-up n'est utilisable que si
  `!IsSunk && !PowerUpUsed`.
- `Ship.MarkPowerUpUsed()` est `public` (contrairement à `MarkSunk()`, resté `internal`) : c'est une
  déviation délibérée, requise car `GameEngine` (assembly `BattleShip.API`) appelle cette méthode
  sur un `Ship` défini dans `BattleShip.Models`, un assembly différent — `internal` ne suffirait pas.
- `swagger.yaml` gagne la route `POST /api/games/{id}/powerups`, les DTO `UsePowerUpRequest` /
  `PowerUpResultDto` / `ReconResultDto`, et les enums `PowerUpType`/`Orientation`/`Edge`.
- `UsePowerUpAsync` réutilise la résolution de tour existante (`ResolveStatusAfterShot`,
  `ResolveWinStatus`) : activer un power-up fait avancer le tour exactement comme un tir.
- Le leurre est la seule action qui modifie le plateau du **propriétaire** plutôt que celui de
  l'adversaire ; elle ne produit aucun résultat révélateur côté adversaire.
- Tests ajoutés dans `BattleShip.Tests/Domain` (résolution par power-up, validations de tour) et
  `BattleShip.Tests/Api` (400/409/200 sur `POST /powerups`, non-fuite des positions adverses).
- Détail complet : `docs/superpowers/specs/2026-09-16-ship-powerups-design.md`.

## Vérification et réexamen

```bash
./scripts/dotnet.sh test --filter "FullyQualifiedName~PowerUp"
docker compose up --build
curl -s -X POST http://localhost:8080/api/games/$ID/powerups \
  -H "Content-Type: application/json" \
  -d '{"shipName":"Porte-avions","orientation":"Row","index":3}'
```

À réexaminer si un mode PvP compétitif nécessite un équilibrage différent (probabilité IA,
puissance du leurre) ou si un power-up supplémentaire est ajouté à une flotte non standard.

## Références

- [`docs/superpowers/specs/2026-09-16-ship-powerups-design.md`](../superpowers/specs/2026-09-16-ship-powerups-design.md)
- [`docs/adr/0006-modes-de-jeu.md`](0006-modes-de-jeu.md)
- [`PROMPT-INIT.md`](../../PROMPT-INIT.md)
- [`swagger.yaml`](../../swagger.yaml)
- [`BattleShip.API/Services/GameEngine.cs`](../../BattleShip.API/Services/GameEngine.cs)
- [`BattleShip.App/Services/MockGameApiClient.cs`](../../BattleShip.App/Services/MockGameApiClient.cs)
