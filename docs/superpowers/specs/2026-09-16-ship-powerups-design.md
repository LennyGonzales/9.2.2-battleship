# Ship Power-Ups — Design

## Status and date

Approved — 2026-09-16

## Context

Each ship in the fleet gets exactly one power-up, usable once per game, only while that ship is
alive. Activating a power-up consumes the player's whole turn (no regular shot that turn). The
mapping (fixed, derived from `GameOptions.DefaultFleet`) is:

| Ship | Power-up | Effect |
|---|---|---|
| Porte-avions (5) | **Recon** | Scans a row or column; reveals only whether *something* (ship or island) is present, never where. |
| Croiseur (4) | **DoubleStrike** | Two independent normal shots anywhere on the opponent board, in one turn. |
| Contre-torpilleur (3) | **Decoy** | Places a fake ship cell adjacent to one of its own cells, on the owner's own board. |
| Sous-marin (3) | **TwinStrike** | Two independent normal shots on two orthogonally-adjacent opponent cells. |
| Torpilleur (2) | **Torpedo** | Fires along a row/column from a chosen edge; resolves the first non-empty cell hit (ship or obstacle) as a normal shot; a fully-empty line is a no-op miss. |

Scope: implemented in **both** the real backend (`BattleShip.Models`/`BattleShip.API`, the
authoritative implementation) and the front-end mock (`MockGameApiClient`), matching the existing
pattern where fleet placement, shot resolution, and difficulty are duplicated between the two.
Usable by **both** the human player and the computer opponent — the computer uses a simple,
difficulty-aware heuristic (not a new AI subsystem).

Out of scope: PvP-specific power-up interactions beyond what already falls out of the shared
`GameEngine` turn model (PvP already alternates `Player1Turn`/`Player2Turn`; power-ups plug into
that the same way a shot does — no new PvP-only rules).

## Domain model changes (`BattleShip.Models`)

```csharp
public enum PowerUpType { Recon, Torpedo, TwinStrike, Decoy, DoubleStrike }

// GameOptions
public static readonly IReadOnlyDictionary<string, PowerUpType> PowerUpByShipName = new Dictionary<string, PowerUpType>
{
    ["Porte-avions"] = PowerUpType.Recon,
    ["Croiseur"] = PowerUpType.DoubleStrike,
    ["Contre-torpilleur"] = PowerUpType.Decoy,
    ["Sous-marin"] = PowerUpType.TwinStrike,
    ["Torpilleur"] = PowerUpType.Torpedo,
};
```

`Ship` gains:
```csharp
public PowerUpType PowerUpType { get; init; }   // set from GameOptions.PowerUpByShipName at creation
public bool PowerUpUsed { get; private set; }
internal void MarkPowerUpUsed() => PowerUpUsed = true;
public bool CanUsePowerUp => !IsSunk && !PowerUpUsed;
```

`CellState` gains `Decoy` and `DecoyHit`, mirroring the existing `Obstacle`/`ObstacleHit` pair.
`Board.IsAlreadyTargeted` includes `DecoyHit` alongside `Miss`/`Hit`/`Sunk`/`ObstacleHit`.
`Board.ResolveShot`: a `Decoy` cell resolves exactly like a ship hit (`ShotOutcome.Hit`, no ship
name), cell becomes `DecoyHit`, and it is **never** added to `_ships`, so `AreAllShipsSunk()` is
unaffected. Like any other cell, it absorbs exactly one shot — `IsAlreadyTargeted` then blocks
re-targeting it — but that one shot costs the opponent a full turn on a false lead, exactly as if
they had hit a real ship, without ever sinking or contributing to a win.

`Board` gains:
```csharp
public bool TryPlaceDecoy(int x, int y);                                   // must be Empty and orthogonally adjacent to a ship cell
public (bool HasContact,) ScanLine(Orientation orientation, int index);    // Recon — read-only
public ShotResolution? FireTorpedo(Orientation orientation, int index, Edge entryEdge); // Torpedo — null if line was all empty
```
`ResolveShot` itself is reused unchanged for TwinStrike/DoubleStrike (called twice per activation).

## Engine / turn flow (`IGameEngine`, `GameEngine`)

New method on `IGameEngine`:
```csharp
Task<PowerUpTurnResult> UsePowerUpAsync(Guid gameId, Participant? caller, UsePowerUpRequest request, CancellationToken ct = default);
```

Validation mirrors `FireShotAsync`: game exists, not finished, not `Waiting`/`PlacingFleet`,
correct turn (`ValidateCaller`), then power-up-specific checks: ship exists in the caller's fleet
by `ShipName`, `ship.CanUsePowerUp`, and the request shape matches `ship.PowerUpType` (e.g.
`Cells.Count == 2` and adjacent for TwinStrike, one `Cells` entry adjacent to an own ship cell for
Decoy). On success: `ship.MarkPowerUpUsed()`, apply the effect, then reuse
`ResolveStatusAfterShot`/`ResolveWinStatus` (renamed/shared, not duplicated) to compute the next
`GameStatus` exactly as a shot would — a power-up-use counts as this turn's action.

Decoy is the only power-up that touches the **owner's own board** and never produces an
opponent-facing result — `AreAllShipsSunk`/win detection do not change for it.

## Contracts (`BattleShip.Models/Contracts`)

Single endpoint: `POST /api/games/{id}/powerups`.

```csharp
public enum Orientation { Row, Column }
public enum Edge { Low, High }
public record CellTarget(int X, int Y);

public record UsePowerUpRequest(
    string ShipName,
    Orientation? Orientation,     // Recon, Torpedo
    int? Index,                   // Recon, Torpedo — row/column index
    Edge? EntryEdge,              // Torpedo only
    IReadOnlyList<CellTarget>? Cells // TwinStrike (2, adjacent), DoubleStrike (2, anywhere), Decoy (1, own board)
);

public record ReconResultDto(Orientation Orientation, int Index, bool HasContact);

public record PowerUpResultDto(
    string ShipName,
    PowerUpType Type,
    PlayerSide Actor,
    GameStatus Status,
    ReconResultDto? Recon,
    ShotOutcomeDto? Torpedo,                    // null Cell = line was empty
    IReadOnlyList<ShotOutcomeDto>? Cells         // TwinStrike / DoubleStrike, 2 entries
    // Decoy: no revealing field is populated — the owner already knows where they placed it
);
```

`PlaceFleetRequestValidator`-style conditional validation in a new
`UsePowerUpRequestValidator`, `When(x => x.ShipName == ...)` per power-up shape, called explicitly
in the endpoint per the project's no-implicit-validation rule.

**Masking rule preserved**: `Recon.HasContact` and the single-cell `Torpedo` result never reveal
more about un-hit opponent ship positions than a normal shot already does — this mapping lives in
the API's DTO-mapping layer (`GameMapper`/`ShotMapper`-equivalent), not in the domain.

## Computer AI (`DifficultyComputerOpponent`, mock equivalent)

Before choosing a normal target, roll a per-difficulty chance to use an eligible power-up instead:

- **Easy/Normal**: low flat probability per turn; power-up and target chosen uniformly at random
  among eligible ships/valid targets.
- **Hard**: reuses the existing hunt-queue state — prefers `Torpedo`/`TwinStrike`/`DoubleStrike`
  aimed at/around a pending hunt target once a hit is unresolved; prefers `Recon` early while the
  opponent board is still mostly `Unknown`.
- **Decoy** is placed once, early (first few turns), adjacent to a random one of its own alive
  ships — reuses the same adjacency validation as the player path, no AI-only placement rule.

This is a thin decision layer added to the existing opponent classes, not a new AI subsystem.

## Mock client & front-end (`BattleShip.App`)

- `IGameApiClient` gains `Task<PowerUpResultDto> UsePowerUpAsync(Guid id, UsePowerUpRequest request, string? playerToken, CancellationToken ct = default)`.
- `HttpGameApiClient`: 1:1 call to the new route.
- `MockGameApiClient`: duplicates engine behavior against its own `Board`, reading the same
  `GameOptions.PowerUpByShipName` map from `BattleShip.Models` (shared source of truth, not
  redefined).
- **UI**: each alive, unused-power-up ship shows a button in the player's ship list (`Battle.razor`
  / a new `PowerUpBar` component). Clicking one switches the relevant `RadarGrid` into a
  power-up-specific targeting mode:
  - Recon / Torpedo: row/column picker overlay + (Torpedo only) an edge toggle.
  - TwinStrike: click two adjacent opponent cells.
  - DoubleStrike: click any two opponent cells.
  - Decoy: click one empty cell adjacent to an own ship, on the **player's own board**.
  Results render through the existing `AlertBanner`/`StatusConsole`/`ShotLog` pattern — no new
  result-display mechanism.

## Testing / verification

- `BattleShip.Tests/Domain`: `Board`/`Ship` unit tests per power-up resolution (Decoy absorbs a hit
  without sinking or affecting `AreAllShipsSunk`; Torpedo stops at first obstacle/ship from each
  edge; Recon reveals presence only).
- `BattleShip.Tests/Domain`: `GameEngine` turn/validation tests — wrong turn, already-used
  power-up, sunk ship, malformed target shape per type, turn advances exactly like a shot.
- `BattleShip.Tests/Api`: integration tests for `POST /api/games/{id}/powerups` — 400 on bad shape,
  409 on reuse/wrong-turn/sunk-ship, 200 + masking assertions (Recon/Torpedo responses never leak
  un-hit opponent ship positions).
- Manual verification through `docker compose up --build`, confirming each power-up end-to-end in
  the browser for both PvE and the computer's own use of power-ups.

```bash
./scripts/dotnet.sh test --filter "FullyQualifiedName~PowerUp"
docker compose up --build
# http://localhost:8081 — place fleet, use each power-up once, confirm:
#  - a used/sunk ship's power-up button disappears
#  - Decoy absorbs one opposing shot without ending the game
#  - Recon/Torpedo never reveal un-hit opponent ship cells beyond the single resolved cell
```

## Commit plan (atomic, in order)

1. `BattleShip.Models`: `PowerUpType`, `GameOptions.PowerUpByShipName`, `Ship` power-up fields,
   `CellState.Decoy`/`DecoyHit`, `Board` decoy/recon/torpedo methods, new contracts.
2. `BattleShip.API`: `UsePowerUpRequestValidator`, `GameEngine.UsePowerUpAsync`, `PowerUpEndpoints`,
   masking in the mapping layer.
3. `BattleShip.API`: computer-opponent power-up heuristic in `DifficultyComputerOpponent`.
4. `BattleShip.Tests`: domain + API tests for the above (can interleave with 1–3, TDD-style).
5. `BattleShip.App`: `IGameApiClient`/`HttpGameApiClient`/`MockGameApiClient` power-up support,
   sharing `GameOptions.PowerUpByShipName`.
6. `BattleShip.App`: mock-side computer power-up heuristic.
7. `BattleShip.App`: UI — power-up buttons, per-type targeting modes, result display.
8. `swagger.yaml` updated with the new route/DTOs; `docs/adr/0007-power-ups-navires.md`.

Each commit must leave `docker compose --profile tools run --rm sdk dotnet build` (and `test`)
green.

## References

- `PROMPT-INIT.md` — layering rules (`Models` has no external deps, masking belongs in the API
  mapping layer)
- `swagger.yaml` — REST contract to extend
- `BattleShip.Models/Domain/Board.cs`, `BattleShip.Models/Domain/Ship.cs` — existing shot
  resolution reused by TwinStrike/DoubleStrike/Torpedo
- `BattleShip.API/Services/GameEngine.cs` — existing turn/status resolution reused by
  `UsePowerUpAsync`
- `BattleShip.App/Services/MockGameApiClient.cs` — existing mock duplication pattern
- `docs/adr/0006-modes-de-jeu.md` — PvE/PvP turn model this plugs into
