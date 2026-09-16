# Ship Power-Ups Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give each of the 5 fleet ships a one-time-per-game power-up (Recon, Torpedo, TwinStrike, Decoy, DoubleStrike), usable by both the human player and the computer opponent, in both the real backend and the front-end mock, consuming the player's turn exactly like a shot.

**Architecture:** Domain logic (ship/board state, resolution) lives in `BattleShip.Models`, reused unchanged by both `BattleShip.API`'s authoritative `GameEngine` and `BattleShip.App`'s `MockGameApiClient`. A single new REST endpoint (`POST /api/games/{id}/powerups`) handles player-initiated activation with a request DTO whose optional fields are validated conditionally per ship type (mirrors `ShotRequest`/`PlaceFleetRequest`). A second new endpoint (`POST /api/games/{id}/computer-turn`) resolves the computer's whole turn (shot or power-up, decided by a heuristic in `IComputerOpponent`), replacing the front's previous habit of calling `POST /shots` with an empty body to trigger the computer — that old call path is untouched and still works for its existing tests.

**Tech Stack:** .NET 10, ASP.NET Core Minimal API, FluentValidation, Blazor WebAssembly, xUnit — all via the Docker `sdk` service (`./scripts/dotnet.sh build|test`), never local `dotnet`.

**Spec:** `docs/superpowers/specs/2026-09-16-ship-powerups-design.md` (see also `docs/adr/0007-power-ups-navires.md`)

## Global Constraints

- Every `dotnet` command runs through `./scripts/dotnet.sh` (wraps `docker compose --profile tools run --rm sdk dotnet "$@"`) — never invoke `dotnet` on the host.
- `BattleShip.Models` references no other project and stays free of ASP.NET/HTTP/JSON/gRPC dependencies.
- FluentValidation validators are called explicitly (`ValidateAsync`/`ValidateAsResultAsync`) inside endpoints — no implicit validation pipeline.
- DTOs sent to a player must never reveal an opponent's undiscovered ship positions; masking lives in the API's mapping layer (`GameMapper`/`ShotMapper`/`PowerUpMapper`), never in the domain.
- A power-up is usable once per game, only while its ship is alive (`!IsSunk && !PowerUpUsed`), and activating one consumes the whole turn — no shot that turn.
- Fixed mapping: Porte-avions→Recon, Croiseur→DoubleStrike, Contre-torpilleur→Decoy, Sous-marin→TwinStrike, Torpilleur→Torpedo.
- Existing endpoints/tests (`/shots`, `FireShotEndpointTests`, etc.) must keep passing unmodified — new capability is additive.
- Every commit must leave `./scripts/dotnet.sh build` and `./scripts/dotnet.sh test` green.

---

### Task 1: Domain — power-up enums, fleet map, `Ship` power-up state

**Files:**
- Create: `BattleShip.Models/Domain/PowerUpType.cs`
- Modify: `BattleShip.Models/Domain/GameOptions.cs`
- Modify: `BattleShip.Models/Domain/Ship.cs`
- Test: `BattleShip.Tests/Domain/ShipPowerUpTests.cs`

**Interfaces:**
- Produces: `PowerUpType` enum (`Recon`, `Torpedo`, `TwinStrike`, `Decoy`, `DoubleStrike`), `Orientation` enum (`Row`, `Column`), `Edge` enum (`Low`, `High`); `GameOptions.PowerUpByShipName` (`IReadOnlyDictionary<string, PowerUpType>`); `Ship.PowerUpType` (computed `get`), `Ship.PowerUpUsed` (`bool`, `get`), `Ship.CanUsePowerUp` (`bool`, `get`), `Ship.MarkPowerUpUsed()` (public — unlike `MarkSunk`/`SetCells`, this must be callable from `BattleShip.API`'s `GameEngine`, a different project, so it cannot be `internal`).

- [ ] **Step 1: Write the failing test**

```csharp
// BattleShip.Tests/Domain/ShipPowerUpTests.cs
using BattleShip.Models.Domain;

namespace BattleShip.Tests.Domain;

public class ShipPowerUpTests
{
    [Theory]
    [InlineData("Porte-avions", PowerUpType.Recon)]
    [InlineData("Croiseur", PowerUpType.DoubleStrike)]
    [InlineData("Contre-torpilleur", PowerUpType.Decoy)]
    [InlineData("Sous-marin", PowerUpType.TwinStrike)]
    [InlineData("Torpilleur", PowerUpType.Torpedo)]
    public void PowerUpType_MatchesShipName(string name, PowerUpType expected)
    {
        var ship = new Ship { Name = name, Length = 2 };

        Assert.Equal(expected, ship.PowerUpType);
    }

    [Fact]
    public void CanUsePowerUp_NewShip_IsTrue()
    {
        var ship = new Ship { Name = "Torpilleur", Length = 2 };

        Assert.True(ship.CanUsePowerUp);
    }

    [Fact]
    public void CanUsePowerUp_AfterMarkPowerUpUsed_IsFalse()
    {
        var ship = new Ship { Name = "Torpilleur", Length = 2 };

        ship.MarkPowerUpUsed();

        Assert.False(ship.CanUsePowerUp);
        Assert.True(ship.PowerUpUsed);
    }

    [Fact]
    public void CanUsePowerUp_OnSunkShip_IsFalse()
    {
        var board = new Board(10);
        var ship = new Ship { Name = "Torpilleur", Length = 1 };
        board.PlaceShip(ship, 0, 0, horizontal: true);

        board.ResolveShot(0, 0);

        Assert.True(ship.IsSunk);
        Assert.False(ship.CanUsePowerUp);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `./scripts/dotnet.sh test --filter "FullyQualifiedName~ShipPowerUpTests"`
Expected: FAIL (compile error — `PowerUpType`, `CanUsePowerUp`, `MarkPowerUpUsed` don't exist yet)

- [ ] **Step 3: Create `PowerUpType.cs`**

```csharp
// BattleShip.Models/Domain/PowerUpType.cs
namespace BattleShip.Models.Domain;

public enum PowerUpType
{
    Recon,
    Torpedo,
    TwinStrike,
    Decoy,
    DoubleStrike
}

public enum Orientation
{
    Row,
    Column
}

public enum Edge
{
    Low,
    High
}
```

- [ ] **Step 4: Add the fleet→power-up map to `GameOptions.cs`**

Add this member to the existing `GameOptions` class (`BattleShip.Models/Domain/GameOptions.cs`), after `DefaultFleet`:

```csharp
    public static readonly IReadOnlyDictionary<string, PowerUpType> PowerUpByShipName = new Dictionary<string, PowerUpType>
    {
        ["Porte-avions"] = PowerUpType.Recon,
        ["Croiseur"] = PowerUpType.DoubleStrike,
        ["Contre-torpilleur"] = PowerUpType.Decoy,
        ["Sous-marin"] = PowerUpType.TwinStrike,
        ["Torpilleur"] = PowerUpType.Torpedo,
    };
```

- [ ] **Step 5: Add power-up state to `Ship.cs`**

Replace the full contents of `BattleShip.Models/Domain/Ship.cs` with:

```csharp
namespace BattleShip.Models.Domain;

public sealed class Ship
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
    public required int Length { get; init; }
    public IReadOnlyList<(int X, int Y)> Cells { get; private set; } = [];
    public bool IsSunk { get; private set; }
    public bool PowerUpUsed { get; private set; }

    public PowerUpType PowerUpType => GameOptions.PowerUpByShipName[Name];
    public bool CanUsePowerUp => !IsSunk && !PowerUpUsed;

    internal void SetCells(IReadOnlyList<(int X, int Y)> cells) => Cells = cells;

    internal void MarkSunk() => IsSunk = true;

    public void MarkPowerUpUsed() => PowerUpUsed = true;
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `./scripts/dotnet.sh test --filter "FullyQualifiedName~ShipPowerUpTests"`
Expected: PASS (8 tests)

- [ ] **Step 7: Commit**

```bash
git add BattleShip.Models/Domain/PowerUpType.cs BattleShip.Models/Domain/GameOptions.cs BattleShip.Models/Domain/Ship.cs BattleShip.Tests/Domain/ShipPowerUpTests.cs
git commit -m "feat(domain): add power-up type and per-ship power-up state"
```

---

### Task 2: Domain — `Board` decoy/recon/torpedo resolution

**Files:**
- Modify: `BattleShip.Models/Domain/CellState.cs`
- Modify: `BattleShip.Models/Domain/Board.cs`
- Test: `BattleShip.Tests/Domain/BoardPowerUpTests.cs`

**Interfaces:**
- Consumes: `Orientation`, `Edge` (Task 1)
- Produces: `Board.TryPlaceDecoy(int x, int y) : bool`, `Board.ScanLine(Orientation orientation, int index) : bool`, `Board.FireTorpedo(Orientation orientation, int index, Edge entryEdge) : ShotResolution?` (null = the whole line was empty/already-resolved, nothing new hit). `CellState.Decoy`/`CellState.DecoyHit` added.

- [ ] **Step 1: Write the failing test**

```csharp
// BattleShip.Tests/Domain/BoardPowerUpTests.cs
using BattleShip.Models.Domain;

namespace BattleShip.Tests.Domain;

public class BoardPowerUpTests
{
    [Fact]
    public void TryPlaceDecoy_AdjacentToShip_Succeeds()
    {
        var board = new Board(10);
        var ship = new Ship { Name = "Contre-torpilleur", Length = 3 };
        board.PlaceShip(ship, 0, 0, horizontal: true);

        var placed = board.TryPlaceDecoy(0, 1);

        Assert.True(placed);
        Assert.Equal(CellState.Decoy, board.GetCell(0, 1));
    }

    [Fact]
    public void TryPlaceDecoy_NotAdjacentToAnyShip_Fails()
    {
        var board = new Board(10);
        var ship = new Ship { Name = "Contre-torpilleur", Length = 3 };
        board.PlaceShip(ship, 0, 0, horizontal: true);

        var placed = board.TryPlaceDecoy(5, 5);

        Assert.False(placed);
        Assert.Equal(CellState.Empty, board.GetCell(5, 5));
    }

    [Fact]
    public void TryPlaceDecoy_OnOccupiedCell_Fails()
    {
        var board = new Board(10);
        var ship = new Ship { Name = "Contre-torpilleur", Length = 3 };
        board.PlaceShip(ship, 0, 0, horizontal: true);

        var placed = board.TryPlaceDecoy(1, 0);

        Assert.False(placed);
    }

    [Fact]
    public void ResolveShot_OnDecoy_ReturnsHitAndNeverSinks()
    {
        var board = new Board(10);
        var ship = new Ship { Name = "Contre-torpilleur", Length = 3 };
        board.PlaceShip(ship, 0, 0, horizontal: true);
        board.TryPlaceDecoy(0, 1);

        var result = board.ResolveShot(0, 1);

        Assert.Equal(ShotOutcome.Hit, result.Outcome);
        Assert.Null(result.SunkShipName);
        Assert.Equal(CellState.DecoyHit, board.GetCell(0, 1));
        Assert.False(board.AreAllShipsSunk());
    }

    [Fact]
    public void IsAlreadyTargeted_OnDecoyHit_ReturnsTrue()
    {
        var board = new Board(10);
        var ship = new Ship { Name = "Contre-torpilleur", Length = 3 };
        board.PlaceShip(ship, 0, 0, horizontal: true);
        board.TryPlaceDecoy(0, 1);
        board.ResolveShot(0, 1);

        Assert.True(board.IsAlreadyTargeted(0, 1));
    }

    [Fact]
    public void ScanLine_RowWithShip_ReturnsTrue()
    {
        var board = new Board(10);
        var ship = new Ship { Name = "Torpilleur", Length = 2 };
        board.PlaceShip(ship, 3, 4, horizontal: true);

        Assert.True(board.ScanLine(Orientation.Row, 4));
        Assert.False(board.ScanLine(Orientation.Row, 5));
    }

    [Fact]
    public void ScanLine_ColumnWithObstacle_ReturnsTrue()
    {
        var board = new Board(10);
        board.ApplyObstacles([(2, 7)]);

        Assert.True(board.ScanLine(Orientation.Column, 2));
    }

    [Fact]
    public void FireTorpedo_FromLowEdge_HitsFirstShipCell()
    {
        var board = new Board(10);
        var ship = new Ship { Name = "Torpilleur", Length = 2 };
        board.PlaceShip(ship, 5, 3, horizontal: true);

        var result = board.FireTorpedo(Orientation.Row, 3, Edge.Low);

        Assert.NotNull(result);
        Assert.Equal(5, result!.X);
        Assert.Equal(3, result.Y);
        Assert.Equal(ShotOutcome.Hit, result.Outcome);
    }

    [Fact]
    public void FireTorpedo_FromHighEdge_HitsFirstShipCellFromTheOtherSide()
    {
        var board = new Board(10);
        var ship = new Ship { Name = "Torpilleur", Length = 2 };
        board.PlaceShip(ship, 5, 3, horizontal: true);

        var result = board.FireTorpedo(Orientation.Row, 3, Edge.High);

        Assert.NotNull(result);
        Assert.Equal(6, result!.X);
        Assert.Equal(3, result.Y);
        Assert.Equal(ShotOutcome.Hit, result.Outcome);
    }

    [Fact]
    public void FireTorpedo_OnEmptyLine_ReturnsNull()
    {
        var board = new Board(10);

        var result = board.FireTorpedo(Orientation.Row, 0, Edge.Low);

        Assert.Null(result);
    }

    [Fact]
    public void FireTorpedo_StopsAtObstacleBeforeShip()
    {
        var board = new Board(10);
        var ship = new Ship { Name = "Torpilleur", Length = 1 };
        board.PlaceShip(ship, 8, 0, horizontal: true);
        board.ApplyObstacles([(3, 0)]);

        var result = board.FireTorpedo(Orientation.Row, 0, Edge.Low);

        Assert.NotNull(result);
        Assert.Equal(3, result!.X);
        Assert.Equal(ShotOutcome.Obstacle, result.Outcome);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `./scripts/dotnet.sh test --filter "FullyQualifiedName~BoardPowerUpTests"`
Expected: FAIL (compile error — `TryPlaceDecoy`/`ScanLine`/`FireTorpedo`/`CellState.Decoy` don't exist yet)

- [ ] **Step 3: Add `Decoy`/`DecoyHit` to `CellState.cs`**

```csharp
// BattleShip.Models/Domain/CellState.cs
namespace BattleShip.Models.Domain;

public enum CellState
{
    Empty,
    Ship,
    Miss,
    Hit,
    Sunk,
    Unknown,
    Obstacle,
    ObstacleHit,
    Decoy,
    DecoyHit
}
```

- [ ] **Step 4: Extend `Board.cs`**

In `BattleShip.Models/Domain/Board.cs`, change `IsAlreadyTargeted` (around line 26) to also treat `DecoyHit` as targeted:

```csharp
    public bool IsAlreadyTargeted(int x, int y)
    {
        var state = _cells[x, y];
        return state is CellState.Miss or CellState.Hit or CellState.Sunk or CellState.ObstacleHit or CellState.DecoyHit;
    }
```

In `ResolveShot` (around line 57), add a `Decoy` branch before the `Obstacle` check:

```csharp
    public ShotResolution ResolveShot(int x, int y)
    {
        if (!IsWithinBounds(x, y))
            throw new ArgumentOutOfRangeException(nameof(x), $"Coordonnees hors grille : ({x},{y}).");

        if (IsAlreadyTargeted(x, y))
            throw new InvalidOperationException($"La case ({x},{y}) a deja ete ciblee.");

        if (_cells[x, y] == CellState.Decoy)
        {
            _cells[x, y] = CellState.DecoyHit;
            return new ShotResolution(x, y, ShotOutcome.Hit, null);
        }

        if (_cells[x, y] == CellState.Obstacle)
        {
            _cells[x, y] = CellState.ObstacleHit;
            return new ShotResolution(x, y, ShotOutcome.Obstacle, null);
        }
        // ...rest unchanged
```

Add these new public methods and private helpers anywhere after `ApplyObstacles` (they don't depend on obstacle-generation internals):

```csharp
    public bool TryPlaceDecoy(int x, int y)
    {
        if (!IsWithinBounds(x, y) || _cells[x, y] != CellState.Empty)
            return false;

        if (!GetOrthogonalNeighbors(x, y).Any(n => _cells[n.X, n.Y] == CellState.Ship))
            return false;

        _cells[x, y] = CellState.Decoy;
        return true;
    }

    public bool ScanLine(Orientation orientation, int index)
    {
        if (index < 0 || index >= Size)
            throw new ArgumentOutOfRangeException(nameof(index));

        for (var i = 0; i < Size; i++)
        {
            var (x, y) = orientation == Orientation.Row ? (i, index) : (index, i);
            if (_cells[x, y] is CellState.Ship or CellState.Hit or CellState.Sunk
                or CellState.Obstacle or CellState.ObstacleHit)
            {
                return true;
            }
        }

        return false;
    }

    public ShotResolution? FireTorpedo(Orientation orientation, int index, Edge entryEdge)
    {
        if (index < 0 || index >= Size)
            throw new ArgumentOutOfRangeException(nameof(index));

        var indices = Enumerable.Range(0, Size);
        if (entryEdge == Edge.High)
            indices = indices.Reverse();

        foreach (var i in indices)
        {
            var (x, y) = orientation == Orientation.Row ? (i, index) : (index, i);
            if (_cells[x, y] is CellState.Ship or CellState.Obstacle or CellState.Decoy)
                return ResolveShot(x, y);
        }

        return null;
    }

    private IEnumerable<(int X, int Y)> GetOrthogonalNeighbors(int x, int y)
    {
        if (IsWithinBounds(x + 1, y)) yield return (x + 1, y);
        if (IsWithinBounds(x - 1, y)) yield return (x - 1, y);
        if (IsWithinBounds(x, y + 1)) yield return (x, y + 1);
        if (IsWithinBounds(x, y - 1)) yield return (x, y - 1);
    }
```

- [ ] **Step 5: Run test to verify it passes**

Run: `./scripts/dotnet.sh test --filter "FullyQualifiedName~BoardPowerUpTests"`
Expected: PASS (11 tests)

- [ ] **Step 6: Run the full Domain test suite to confirm no regressions**

Run: `./scripts/dotnet.sh test --filter "FullyQualifiedName~BattleShip.Tests.Domain"`
Expected: PASS (all existing + new tests, including `BoardShotTests`)

- [ ] **Step 7: Commit**

```bash
git add BattleShip.Models/Domain/CellState.cs BattleShip.Models/Domain/Board.cs BattleShip.Tests/Domain/BoardPowerUpTests.cs
git commit -m "feat(domain): add decoy, recon and torpedo resolution to Board"
```

---

### Task 3: Contracts + `UsePowerUpRequestValidator`

**Files:**
- Create: `BattleShip.Models/Contracts/PowerUpDtos.cs`
- Create: `BattleShip.API/Validation/UsePowerUpRequestValidator.cs`
- Test: `BattleShip.Tests/Validation/UsePowerUpRequestValidatorTests.cs`

**Interfaces:**
- Consumes: `PowerUpType`, `Orientation`, `Edge` (Task 1), `GameOptions.DefaultFleet` (existing)
- Produces: `CellTarget(int X, int Y)`, `UsePowerUpRequest(string ShipName, Orientation? Orientation, int? Index, Edge? EntryEdge, IReadOnlyList<CellTarget>? Cells)`, `ReconResultDto(Orientation Orientation, int Index, bool HasContact)`, `PowerUpResultDto(string ShipName, PowerUpType Type, PlayerSide Actor, GameStatus Status, ReconResultDto? Recon, ShotOutcomeDto? Torpedo, IReadOnlyList<ShotOutcomeDto>? Cells)`, `ComputerTurnResultDto(bool UsedPowerUp, ShotResultDto? Shot, PowerUpResultDto? PowerUp)`, `UsePowerUpRequestValidator`.

- [ ] **Step 1: Write the failing test**

```csharp
// BattleShip.Tests/Validation/UsePowerUpRequestValidatorTests.cs
using BattleShip.API.Validation;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using FluentValidation.TestHelper;

namespace BattleShip.Tests.Validation;

public class UsePowerUpRequestValidatorTests
{
    private readonly UsePowerUpRequestValidator _validator = new();

    [Fact]
    public void UnknownShipName_HasError()
    {
        var result = _validator.TestValidate(new UsePowerUpRequest("Fregate", null, null, null, null));
        result.ShouldHaveValidationErrorFor(x => x.ShipName);
    }

    [Fact]
    public void Recon_WithoutOrientation_HasError()
    {
        var result = _validator.TestValidate(new UsePowerUpRequest("Porte-avions", null, 3, null, null));
        result.ShouldHaveValidationErrorFor(x => x.Orientation);
    }

    [Fact]
    public void Recon_WithOrientationAndIndex_IsValid()
    {
        var result = _validator.TestValidate(new UsePowerUpRequest("Porte-avions", Orientation.Row, 3, null, null));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Torpedo_WithoutEntryEdge_HasError()
    {
        var result = _validator.TestValidate(new UsePowerUpRequest("Torpilleur", Orientation.Column, 2, null, null));
        result.ShouldHaveValidationErrorFor(x => x.EntryEdge);
    }

    [Fact]
    public void Torpedo_Complete_IsValid()
    {
        var result = _validator.TestValidate(new UsePowerUpRequest("Torpilleur", Orientation.Column, 2, Edge.High, null));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void TwinStrike_WithOneCell_HasError()
    {
        var result = _validator.TestValidate(new UsePowerUpRequest(
            "Sous-marin", null, null, null, [new CellTarget(0, 0)]));
        result.ShouldHaveValidationErrorFor(x => x.Cells);
    }

    [Fact]
    public void TwinStrike_WithTwoCells_IsValid()
    {
        var result = _validator.TestValidate(new UsePowerUpRequest(
            "Sous-marin", null, null, null, [new CellTarget(0, 0), new CellTarget(0, 1)]));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Decoy_WithTwoCells_HasError()
    {
        var result = _validator.TestValidate(new UsePowerUpRequest(
            "Contre-torpilleur", null, null, null, [new CellTarget(0, 0), new CellTarget(0, 1)]));
        result.ShouldHaveValidationErrorFor(x => x.Cells);
    }

    [Fact]
    public void Decoy_WithOneCell_IsValid()
    {
        var result = _validator.TestValidate(new UsePowerUpRequest(
            "Contre-torpilleur", null, null, null, [new CellTarget(0, 0)]));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void NegativeCellCoordinate_HasError()
    {
        var result = _validator.TestValidate(new UsePowerUpRequest(
            "Croiseur", null, null, null, [new CellTarget(-1, 0), new CellTarget(0, 1)]));
        result.ShouldHaveValidationErrorFor("Cells[0].X");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `./scripts/dotnet.sh test --filter "FullyQualifiedName~UsePowerUpRequestValidatorTests"`
Expected: FAIL (compile error — types don't exist yet)

- [ ] **Step 3: Create the Contracts DTOs**

```csharp
// BattleShip.Models/Contracts/PowerUpDtos.cs
using BattleShip.Models.Domain;

namespace BattleShip.Models.Contracts;

public record CellTarget(int X, int Y);

public record UsePowerUpRequest(
    string ShipName,
    Orientation? Orientation,
    int? Index,
    Edge? EntryEdge,
    IReadOnlyList<CellTarget>? Cells);

public record ReconResultDto(Orientation Orientation, int Index, bool HasContact);

public record PowerUpResultDto(
    string ShipName,
    PowerUpType Type,
    PlayerSide Actor,
    GameStatus Status,
    ReconResultDto? Recon,
    ShotOutcomeDto? Torpedo,
    IReadOnlyList<ShotOutcomeDto>? Cells);

public record ComputerTurnResultDto(
    bool UsedPowerUp,
    ShotResultDto? Shot,
    PowerUpResultDto? PowerUp);
```

- [ ] **Step 4: Create the validator**

```csharp
// BattleShip.API/Validation/UsePowerUpRequestValidator.cs
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using FluentValidation;

namespace BattleShip.API.Validation;

public sealed class UsePowerUpRequestValidator : AbstractValidator<UsePowerUpRequest>
{
    private static readonly HashSet<string> AllowedShipNames =
        GameOptions.DefaultFleet.Select(s => s.Name).ToHashSet();

    public UsePowerUpRequestValidator()
    {
        RuleFor(x => x.ShipName)
            .NotEmpty()
            .Must(name => AllowedShipNames.Contains(name))
            .WithMessage("Nom de navire invalide.");

        When(x => x.ShipName is "Porte-avions" or "Torpilleur", () =>
        {
            RuleFor(x => x.Orientation).NotNull().WithMessage("Orientation requise.");
            RuleFor(x => x.Index).NotNull().GreaterThanOrEqualTo(0).WithMessage("Index de ligne/colonne requis.");
        });

        When(x => x.ShipName == "Torpilleur", () =>
        {
            RuleFor(x => x.EntryEdge).NotNull().WithMessage("Bord d'entree requis pour la torpille.");
        });

        When(x => x.ShipName == "Sous-marin", () =>
        {
            RuleFor(x => x.Cells)
                .Must(cells => cells is { Count: 2 })
                .WithMessage("Le sous-marin cible exactement deux cases.");
        });

        When(x => x.ShipName == "Croiseur", () =>
        {
            RuleFor(x => x.Cells)
                .Must(cells => cells is { Count: 2 })
                .WithMessage("Le croiseur cible exactement deux cases.");
        });

        When(x => x.ShipName == "Contre-torpilleur", () =>
        {
            RuleFor(x => x.Cells)
                .Must(cells => cells is { Count: 1 })
                .WithMessage("Le leurre cible exactement une case.");
        });

        RuleForEach(x => x.Cells).ChildRules(cell =>
        {
            cell.RuleFor(c => c.X).GreaterThanOrEqualTo(0).WithMessage("La colonne doit etre positive.");
            cell.RuleFor(c => c.Y).GreaterThanOrEqualTo(0).WithMessage("La ligne doit etre positive.");
        });
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `./scripts/dotnet.sh test --filter "FullyQualifiedName~UsePowerUpRequestValidatorTests"`
Expected: PASS (10 tests)

- [ ] **Step 6: Commit**

```bash
git add BattleShip.Models/Contracts/PowerUpDtos.cs BattleShip.API/Validation/UsePowerUpRequestValidator.cs BattleShip.Tests/Validation/UsePowerUpRequestValidatorTests.cs
git commit -m "feat(contracts): add power-up request/result DTOs and validator"
```

---

### Task 4: Engine — `GameEngine.UsePowerUpAsync`

**Files:**
- Modify: `BattleShip.Models/Domain/TurnResolution.cs`
- Modify: `BattleShip.Models/Services/IGameEngine.cs`
- Modify: `BattleShip.API/Services/GameEngine.cs`
- Test: `BattleShip.Tests/Domain/GameEnginePowerUpTests.cs`

**Interfaces:**
- Consumes: `Board.TryPlaceDecoy/ScanLine/FireTorpedo` (Task 2), `Ship.PowerUpType/CanUsePowerUp/MarkPowerUpUsed` (Task 1), `UsePowerUpRequest`/`CellTarget` (Task 3), existing `GameEngine` private helpers `GetExpectedShooter`, `ValidateCaller`, `ResolveStatusAfterShot`, `GetActiveParticipantForStatus`.
- Produces: `ReconOutcome(Orientation Orientation, int Index, bool HasContact)`, `PowerUpTurnResult(string ShipName, PowerUpType Type, Participant Actor, GameStatus Status, ReconOutcome? Recon, ShotResolution? Torpedo, IReadOnlyList<ShotResolution>? Cells)`, `IGameEngine.UsePowerUpAsync(Guid gameId, Participant? caller, UsePowerUpRequest request, CancellationToken ct = default) : Task<PowerUpTurnResult>`.

- [ ] **Step 1: Write the failing test**

```csharp
// BattleShip.Tests/Domain/GameEnginePowerUpTests.cs
using BattleShip.API.Services;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Models.Exceptions;
using BattleShip.Models.Services;
using BattleShip.Tests.TestHelpers;

namespace BattleShip.Tests.Domain;

public class GameEnginePowerUpTests
{
    [Fact]
    public async Task UsePowerUpAsync_Recon_ReturnsContactWithoutRevealingCell()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(1));
        var game = await CreateReadyPveGameAsync(engine);

        var saved = await repository.GetByIdAsync(game.Id);
        Assert.NotNull(saved);
        saved.Player2Board = CreateBoardWithShip(10, "Torpilleur", 2, x: 3, y: 4, horizontal: true);
        await repository.SaveAsync(saved);

        var request = new UsePowerUpRequest("Porte-avions", Orientation.Row, 4, null, null);
        var result = await engine.UsePowerUpAsync(game.Id, Participant.Player1, request);

        Assert.NotNull(result.Recon);
        Assert.True(result.Recon!.HasContact);
        Assert.Equal(GameStatus.ComputerTurn, result.Status);
    }

    [Fact]
    public async Task UsePowerUpAsync_Torpedo_ResolvesFirstCellFromChosenEdge()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(1));
        var game = await CreateReadyPveGameAsync(engine);

        var saved = await repository.GetByIdAsync(game.Id);
        Assert.NotNull(saved);
        saved.Player2Board = CreateBoardWithShip(10, "Torpilleur", 2, x: 5, y: 3, horizontal: true);
        await repository.SaveAsync(saved);

        var request = new UsePowerUpRequest("Torpilleur", Orientation.Row, 3, Edge.Low, null);
        var result = await engine.UsePowerUpAsync(game.Id, Participant.Player1, request);

        Assert.NotNull(result.Torpedo);
        Assert.Equal(5, result.Torpedo!.X);
        Assert.Equal(ShotOutcome.Hit, result.Torpedo.Outcome);
    }

    [Fact]
    public async Task UsePowerUpAsync_TwinStrike_ResolvesBothCells()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(1));
        var game = await CreateReadyPveGameAsync(engine);

        var request = new UsePowerUpRequest(
            "Sous-marin", null, null, null, [new CellTarget(0, 0), new CellTarget(1, 0)]);
        var result = await engine.UsePowerUpAsync(game.Id, Participant.Player1, request);

        Assert.NotNull(result.Cells);
        Assert.Equal(2, result.Cells!.Count);
    }

    [Fact]
    public async Task UsePowerUpAsync_TwinStrike_NonAdjacentCells_ThrowsConflict()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(1));
        var game = await CreateReadyPveGameAsync(engine);

        var request = new UsePowerUpRequest(
            "Sous-marin", null, null, null, [new CellTarget(0, 0), new CellTarget(5, 5)]);

        await Assert.ThrowsAsync<GameConflictException>(() =>
            engine.UsePowerUpAsync(game.Id, Participant.Player1, request));
    }

    [Fact]
    public async Task UsePowerUpAsync_DoubleStrike_ResolvesBothCellsAnywhere()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(1));
        var game = await CreateReadyPveGameAsync(engine);

        var request = new UsePowerUpRequest(
            "Croiseur", null, null, null, [new CellTarget(0, 0), new CellTarget(9, 9)]);
        var result = await engine.UsePowerUpAsync(game.Id, Participant.Player1, request);

        Assert.NotNull(result.Cells);
        Assert.Equal(2, result.Cells!.Count);
    }

    [Fact]
    public async Task UsePowerUpAsync_Decoy_PlacesDecoyOnOwnBoard()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(1));
        var game = await CreateReadyPveGameAsync(engine);

        var saved = await repository.GetByIdAsync(game.Id);
        Assert.NotNull(saved);
        var ownShip = saved.Player1Board!.Ships.First(s => s.Name == "Contre-torpilleur");
        var (shipX, shipY) = ownShip.Cells[^1];
        var decoyTarget = new CellTarget(shipX + 1, shipY);

        var request = new UsePowerUpRequest("Contre-torpilleur", null, null, null, [decoyTarget]);
        var result = await engine.UsePowerUpAsync(game.Id, Participant.Player1, request);

        Assert.Null(result.Recon);
        Assert.Null(result.Torpedo);
        Assert.Null(result.Cells);
        Assert.Equal(CellState.Decoy, saved.Player1Board.GetCell(decoyTarget.X, decoyTarget.Y));
    }

    [Fact]
    public async Task UsePowerUpAsync_AlreadyUsed_ThrowsConflict()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(1));
        var game = await CreateReadyPveGameAsync(engine);

        var request = new UsePowerUpRequest("Porte-avions", Orientation.Row, 0, null, null);
        await engine.UsePowerUpAsync(game.Id, Participant.Player1, request);

        var saved = await repository.GetByIdAsync(game.Id);
        Assert.NotNull(saved);
        saved.Status = GameStatus.PlayerTurn;
        await repository.SaveAsync(saved);

        await Assert.ThrowsAsync<GameConflictException>(() =>
            engine.UsePowerUpAsync(game.Id, Participant.Player1, request));
    }

    private static async Task<Game> CreateReadyPveGameAsync(GameEngine engine)
    {
        var game = await engine.CreateGameAsync(null);
        return await engine.PlaceFleetAsync(game.Id, Participant.Player1, FleetTestData.ValidFleet);
    }

    private static GameEngine CreateEngine(InMemoryGameRepository repository, Random random) =>
        new(repository, new FleetPlacer(random), new PlayerTokenService(), new DifficultyComputerOpponent(random), random, ObstacleGenerationOptions.None);

    private static Board CreateBoardWithShip(int size, string name, int length, int x, int y, bool horizontal)
    {
        var board = new Board(size);
        var ship = new Ship { Name = name, Length = length };
        board.PlaceShip(ship, x, y, horizontal);
        return board;
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `./scripts/dotnet.sh test --filter "FullyQualifiedName~GameEnginePowerUpTests"`
Expected: FAIL (compile error — `UsePowerUpAsync` doesn't exist yet)

- [ ] **Step 3: Add result types to `TurnResolution.cs`**

Add to the end of `BattleShip.Models/Domain/TurnResolution.cs` (keep the existing `ShotTurnResult`/`ShotResolution`/`ShotOutcome` untouched):

```csharp
public sealed record ReconOutcome(Orientation Orientation, int Index, bool HasContact);

public sealed record PowerUpTurnResult(
    string ShipName,
    PowerUpType Type,
    Participant Actor,
    GameStatus Status,
    ReconOutcome? Recon,
    ShotResolution? Torpedo,
    IReadOnlyList<ShotResolution>? Cells);
```

- [ ] **Step 4: Add the method signature to `IGameEngine.cs`**

Add this to the `IGameEngine` interface, after `FireShotAsync`:

```csharp
    Task<PowerUpTurnResult> UsePowerUpAsync(
        Guid gameId,
        Participant? caller,
        UsePowerUpRequest request,
        CancellationToken cancellationToken = default);
```

- [ ] **Step 5: Implement `UsePowerUpAsync` in `GameEngine.cs`**

Add this method to `GameEngine` (in `BattleShip.API/Services/GameEngine.cs`), after `FireShotAsync`:

```csharp
    public async Task<PowerUpTurnResult> UsePowerUpAsync(
        Guid gameId,
        Participant? caller,
        UsePowerUpRequest request,
        CancellationToken cancellationToken = default)
    {
        var game = await repository.GetByIdAsync(gameId, cancellationToken)
            ?? throw new GameNotFoundException();

        if (game.IsFinished() || game.Status is GameStatus.Waiting or GameStatus.PlacingFleet)
            throw new GameConflictException();

        var expectedShooter = GetExpectedShooter(game);
        ValidateCaller(game, caller, expectedShooter);

        var ownBoard = game.GetBoard(expectedShooter);
        var ship = ownBoard.Ships.FirstOrDefault(s => s.Name == request.ShipName)
            ?? throw new GameConflictException("Navire inconnu.");

        if (!ship.CanUsePowerUp)
            throw new GameConflictException("Power-up indisponible pour ce navire.");

        var opponentBoard = game.GetOpponentBoard(expectedShooter);
        var content = ship.PowerUpType switch
        {
            PowerUpType.Recon => ResolveRecon(opponentBoard, request),
            PowerUpType.Torpedo => ResolveTorpedo(opponentBoard, request),
            PowerUpType.TwinStrike => ResolveMultiStrike(opponentBoard, request, requireAdjacent: true),
            PowerUpType.DoubleStrike => ResolveMultiStrike(opponentBoard, request, requireAdjacent: false),
            PowerUpType.Decoy => ResolveDecoy(ownBoard, ship, request),
            _ => throw new InvalidOperationException("Power-up inconnu.")
        };

        ship.MarkPowerUpUsed();
        game.Status = ResolveStatusAfterShot(game, expectedShooter, opponentBoard);
        game.ActiveParticipant = GetActiveParticipantForStatus(game.Status);

        await repository.SaveAsync(game, cancellationToken);

        return new PowerUpTurnResult(
            ship.Name, ship.PowerUpType, expectedShooter, game.Status,
            content.Recon, content.Torpedo, content.Cells);
    }

    private readonly record struct PowerUpContent(
        ReconOutcome? Recon,
        ShotResolution? Torpedo,
        IReadOnlyList<ShotResolution>? Cells);

    private static (Orientation Orientation, int Index) RequireLine(UsePowerUpRequest request)
    {
        if (request.Orientation is not { } orientation || request.Index is not { } index)
            throw new GameConflictException("Ligne ou colonne requise pour ce power-up.");
        return (orientation, index);
    }

    private static PowerUpContent ResolveRecon(Board opponentBoard, UsePowerUpRequest request)
    {
        var (orientation, index) = RequireLine(request);
        var hasContact = opponentBoard.ScanLine(orientation, index);
        return new PowerUpContent(new ReconOutcome(orientation, index, hasContact), null, null);
    }

    private static PowerUpContent ResolveTorpedo(Board opponentBoard, UsePowerUpRequest request)
    {
        var (orientation, index) = RequireLine(request);
        var entryEdge = request.EntryEdge ?? throw new GameConflictException("Bord d'entree requis pour la torpille.");
        var resolution = opponentBoard.FireTorpedo(orientation, index, entryEdge);
        return new PowerUpContent(null, resolution, null);
    }

    private static PowerUpContent ResolveMultiStrike(Board opponentBoard, UsePowerUpRequest request, bool requireAdjacent)
    {
        var cells = request.Cells;
        if (cells is not { Count: 2 })
            throw new GameConflictException("Deux cases sont requises pour ce power-up.");

        var (a, b) = (cells[0], cells[1]);
        if (a.X == b.X && a.Y == b.Y)
            throw new GameConflictException("Les deux cases doivent etre distinctes.");

        if (requireAdjacent && Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) != 1)
            throw new GameConflictException("Les deux cases doivent etre adjacentes.");

        foreach (var cell in cells)
        {
            if (!opponentBoard.IsWithinBounds(cell.X, cell.Y))
                throw new ShotOutOfBoundsException(cell.X, cell.Y);
            if (opponentBoard.IsAlreadyTargeted(cell.X, cell.Y))
                throw new GameConflictException("Cette case a deja ete ciblee.");
        }

        var results = cells.Select(c => opponentBoard.ResolveShot(c.X, c.Y)).ToList();
        return new PowerUpContent(null, null, results);
    }

    private static PowerUpContent ResolveDecoy(Board ownBoard, Ship ship, UsePowerUpRequest request)
    {
        if (request.Cells is not { Count: 1 })
            throw new GameConflictException("Une case est requise pour le leurre.");

        var target = request.Cells[0];
        if (!ship.Cells.Any(c => Math.Abs(c.X - target.X) + Math.Abs(c.Y - target.Y) == 1))
            throw new GameConflictException("Le leurre doit etre adjacent au contre-torpilleur.");

        if (!ownBoard.TryPlaceDecoy(target.X, target.Y))
            throw new GameConflictException("Impossible de placer le leurre ici.");

        return new PowerUpContent(null, null, null);
    }
```

- [ ] **Step 6: Run test to verify it passes**

Run: `./scripts/dotnet.sh test --filter "FullyQualifiedName~GameEnginePowerUpTests"`
Expected: PASS (7 tests)

- [ ] **Step 7: Run the full test suite to confirm no regressions**

Run: `./scripts/dotnet.sh test`
Expected: PASS (all existing tests + new ones)

- [ ] **Step 8: Commit**

```bash
git add BattleShip.Models/Domain/TurnResolution.cs BattleShip.Models/Services/IGameEngine.cs BattleShip.API/Services/GameEngine.cs BattleShip.Tests/Domain/GameEnginePowerUpTests.cs
git commit -m "feat(engine): add GameEngine.UsePowerUpAsync"
```

---

### Task 5: Computer AI — `IComputerOpponent.ChoosePowerUp` + `PlayComputerTurnAsync`

**Files:**
- Modify: `BattleShip.Models/Domain/TurnResolution.cs`
- Modify: `BattleShip.Models/Services/IComputerOpponent.cs`
- Modify: `BattleShip.Models/Services/IGameEngine.cs`
- Modify: `BattleShip.API/Services/GameEngine.cs`
- Modify: `BattleShip.API/Services/DifficultyComputerOpponent.cs`
- Modify: `BattleShip.Tests/Domain/DifficultyComputerOpponentTests.cs`
- Test: `BattleShip.Tests/Domain/GameEngineComputerTurnTests.cs`

**Interfaces:**
- Consumes: `UsePowerUpAsync`, `FireShotAsync` (Task 4 / existing), `UsePowerUpRequest`/`CellTarget` (Task 3), `Ship.PowerUpType/CanUsePowerUp` (Task 1), `Board.ScanLine`/`GetCell`/`IsWithinBounds`/`IsAlreadyTargeted` (existing/Task 2).
- Produces: `IComputerOpponent.ChoosePowerUp(Game game) : UsePowerUpRequest?` (null = fire a normal shot instead), `ComputerTurnResult(bool UsedPowerUp, ShotTurnResult? Shot, PowerUpTurnResult? PowerUp)`, `IGameEngine.PlayComputerTurnAsync(Guid gameId, CancellationToken ct = default) : Task<ComputerTurnResult>`.

- [ ] **Step 1: Write the failing tests**

Add these methods to the end of the existing `DifficultyComputerOpponentTests` class in `BattleShip.Tests/Domain/DifficultyComputerOpponentTests.cs` (keep everything else in the file unchanged):

```csharp
    [Fact]
    public void ChoosePowerUp_NoEligibleShips_ReturnsNull()
    {
        var playerBoard = new Board(10);
        var game = CreateGameWithComputerFleet(Difficulty.Hard, playerBoard, allPowerUpsUsed: true);

        var request = _opponent.ChoosePowerUp(game);

        Assert.Null(request);
    }

    [Fact]
    public void ChoosePowerUp_WhenReturningRequest_TargetsAnEligibleShipWithMatchingShape()
    {
        var playerBoard = new Board(10);
        var game = CreateGameWithComputerFleet(Difficulty.Hard, playerBoard, allPowerUpsUsed: false);

        UsePowerUpRequest? request = null;
        for (var seed = 0; seed < 200 && request is null; seed++)
        {
            var opponent = new DifficultyComputerOpponent(new Random(seed));
            request = opponent.ChoosePowerUp(game);
        }

        Assert.NotNull(request);
        var ship = game.Player2Board!.Ships.First(s => s.Name == request!.ShipName);
        Assert.True(ship.CanUsePowerUp);
        AssertShapeMatchesType(ship.PowerUpType, request!);
    }

    private static void AssertShapeMatchesType(PowerUpType type, UsePowerUpRequest request)
    {
        switch (type)
        {
            case PowerUpType.Recon:
                Assert.NotNull(request.Orientation);
                Assert.NotNull(request.Index);
                break;
            case PowerUpType.Torpedo:
                Assert.NotNull(request.Orientation);
                Assert.NotNull(request.Index);
                Assert.NotNull(request.EntryEdge);
                break;
            case PowerUpType.TwinStrike:
            case PowerUpType.DoubleStrike:
                Assert.NotNull(request.Cells);
                Assert.Equal(2, request.Cells!.Count);
                break;
            case PowerUpType.Decoy:
                Assert.NotNull(request.Cells);
                Assert.Single(request.Cells!);
                break;
        }
    }

    private static Game CreateGameWithComputerFleet(Difficulty difficulty, Board playerBoard, bool allPowerUpsUsed)
    {
        var computerBoard = new Board(playerBoard.Size);
        new FleetPlacer(new Random(123)).PlaceFleetRandomly(computerBoard);

        if (allPowerUpsUsed)
        {
            foreach (var ship in computerBoard.Ships)
                ship.MarkPowerUpUsed();
        }

        return new Game
        {
            Mode = GameMode.VsComputer,
            Difficulty = difficulty,
            BoardSize = playerBoard.Size,
            Player1Board = playerBoard,
            Player2Board = computerBoard,
            Status = GameStatus.ComputerTurn,
        };
    }
```

This file needs `using BattleShip.Models.Contracts;` added to its `using` block (for `UsePowerUpRequest`/`CellTarget`).

```csharp
// BattleShip.Tests/Domain/GameEngineComputerTurnTests.cs
using BattleShip.API.Services;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Models.Exceptions;
using BattleShip.Models.Services;
using BattleShip.Tests.TestHelpers;

namespace BattleShip.Tests.Domain;

public class GameEngineComputerTurnTests
{
    [Fact]
    public async Task PlayComputerTurnAsync_NoPowerUpChosen_FiresNormalShot()
    {
        var repository = new InMemoryGameRepository();
        var opponent = new NeverUsesPowerUpOpponent(1, 1);
        var engine = new GameEngine(
            repository, new FleetPlacer(new Random(1)), new PlayerTokenService(),
            opponent, new Random(1), ObstacleGenerationOptions.None);

        var game = await engine.CreateGameAsync(null);
        await engine.PlaceFleetAsync(game.Id, Participant.Player1, FleetTestData.ValidFleet);
        await engine.FireShotAsync(game.Id, Participant.Player1, 9, 9);

        var result = await engine.PlayComputerTurnAsync(game.Id);

        Assert.False(result.UsedPowerUp);
        Assert.NotNull(result.Shot);
        Assert.Null(result.PowerUp);
    }

    [Fact]
    public async Task PlayComputerTurnAsync_PowerUpChosen_UsesPowerUpInsteadOfShot()
    {
        var repository = new InMemoryGameRepository();
        var opponent = new AlwaysUsesReconOpponent();
        var engine = new GameEngine(
            repository, new FleetPlacer(new Random(1)), new PlayerTokenService(),
            opponent, new Random(1), ObstacleGenerationOptions.None);

        var game = await engine.CreateGameAsync(null);
        await engine.PlaceFleetAsync(game.Id, Participant.Player1, FleetTestData.ValidFleet);
        await engine.FireShotAsync(game.Id, Participant.Player1, 9, 9);

        var result = await engine.PlayComputerTurnAsync(game.Id);

        Assert.True(result.UsedPowerUp);
        Assert.Null(result.Shot);
        Assert.NotNull(result.PowerUp);
        Assert.Equal(PowerUpType.Recon, result.PowerUp!.Type);
    }

    [Fact]
    public async Task PlayComputerTurnAsync_WrongStatus_ThrowsConflict()
    {
        var repository = new InMemoryGameRepository();
        var opponent = new NeverUsesPowerUpOpponent(0, 0);
        var engine = new GameEngine(
            repository, new FleetPlacer(new Random(1)), new PlayerTokenService(),
            opponent, new Random(1), ObstacleGenerationOptions.None);

        var game = await engine.CreateGameAsync(null);
        await engine.PlaceFleetAsync(game.Id, Participant.Player1, FleetTestData.ValidFleet);

        await Assert.ThrowsAsync<GameConflictException>(() => engine.PlayComputerTurnAsync(game.Id));
    }

    private sealed class NeverUsesPowerUpOpponent(int x, int y) : IComputerOpponent
    {
        public (int X, int Y) ChooseShot(Game game) => (x, y);
        public UsePowerUpRequest? ChoosePowerUp(Game game) => null;
    }

    private sealed class AlwaysUsesReconOpponent : IComputerOpponent
    {
        public (int X, int Y) ChooseShot(Game game) => (0, 0);
        public UsePowerUpRequest? ChoosePowerUp(Game game) =>
            new("Porte-avions", Orientation.Row, 0, null, null);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `./scripts/dotnet.sh test --filter "FullyQualifiedName~GameEngineComputerTurnTests|FullyQualifiedName~ChoosePowerUp"`
Expected: FAIL (compile errors — `ChoosePowerUp`/`PlayComputerTurnAsync` don't exist yet)

- [ ] **Step 3: Add `ComputerTurnResult` to `TurnResolution.cs`**

Add to the end of `BattleShip.Models/Domain/TurnResolution.cs`:

```csharp
public sealed record ComputerTurnResult(bool UsedPowerUp, ShotTurnResult? Shot, PowerUpTurnResult? PowerUp);
```

- [ ] **Step 4: Extend `IComputerOpponent.cs`**

```csharp
// BattleShip.Models/Services/IComputerOpponent.cs
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;

namespace BattleShip.Models.Services;

public interface IComputerOpponent
{
    (int X, int Y) ChooseShot(Game game);
    UsePowerUpRequest? ChoosePowerUp(Game game);
}
```

- [ ] **Step 5: Add `PlayComputerTurnAsync` to `IGameEngine.cs`**

```csharp
    Task<ComputerTurnResult> PlayComputerTurnAsync(Guid gameId, CancellationToken cancellationToken = default);
```

- [ ] **Step 6: Implement `PlayComputerTurnAsync` in `GameEngine.cs`**

Add after `UsePowerUpAsync`:

```csharp
    public async Task<ComputerTurnResult> PlayComputerTurnAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var game = await repository.GetByIdAsync(gameId, cancellationToken)
            ?? throw new GameNotFoundException();

        if (game.Mode != GameMode.VsComputer || game.Status != GameStatus.ComputerTurn)
            throw new GameConflictException();

        var request = computerOpponent.ChoosePowerUp(game);
        if (request is not null)
        {
            var powerUpResult = await UsePowerUpAsync(gameId, Participant.Player2, request, cancellationToken);
            return new ComputerTurnResult(true, null, powerUpResult);
        }

        var shotResult = await FireShotAsync(gameId, null, null, null, cancellationToken);
        return new ComputerTurnResult(false, shotResult, null);
    }
```

- [ ] **Step 7: Implement the heuristic in `DifficultyComputerOpponent.cs`**

Add `using BattleShip.Models.Contracts;` to the top of the file, then add these members to the class:

```csharp
    private const double CasualPowerUpChance = 0.15;
    private const double HardPowerUpChance = 0.35;

    public UsePowerUpRequest? ChoosePowerUp(Game game)
    {
        var ownBoard = game.GetBoard(Participant.Player2);
        var opponentBoard = game.GetOpponentBoard(Participant.Player2);
        var eligible = ownBoard.Ships.Where(s => s.CanUsePowerUp).ToList();
        if (eligible.Count == 0)
            return null;

        var chance = game.Difficulty == Difficulty.Hard ? HardPowerUpChance : CasualPowerUpChance;
        if (random.NextDouble() >= chance)
            return null;

        var decoy = eligible.FirstOrDefault(s => s.PowerUpType == PowerUpType.Decoy);
        if (decoy is not null && random.Next(3) == 0)
        {
            var decoyRequest = BuildDecoyRequest(decoy, ownBoard);
            if (decoyRequest is not null)
                return decoyRequest;
        }

        var attackers = eligible.Where(s => s.PowerUpType != PowerUpType.Decoy).ToList();
        if (attackers.Count == 0)
            return null;

        var ship = attackers[random.Next(attackers.Count)];
        return ship.PowerUpType switch
        {
            PowerUpType.Recon => BuildReconRequest(ship, opponentBoard, random),
            PowerUpType.Torpedo => BuildTorpedoRequest(ship, opponentBoard, random),
            PowerUpType.TwinStrike => BuildTwinStrikeRequest(ship, opponentBoard, random),
            PowerUpType.DoubleStrike => BuildDoubleStrikeRequest(ship, opponentBoard, random),
            _ => null,
        };
    }

    private static UsePowerUpRequest? BuildDecoyRequest(Ship ship, Board ownBoard)
    {
        foreach (var (x, y) in ship.Cells)
        {
            foreach (var (nx, ny) in GetNeighbors(x, y))
            {
                if (ownBoard.IsWithinBounds(nx, ny) && ownBoard.GetCell(nx, ny) == CellState.Empty)
                    return new UsePowerUpRequest(ship.Name, null, null, null, [new CellTarget(nx, ny)]);
            }
        }
        return null;
    }

    private static UsePowerUpRequest BuildReconRequest(Ship ship, Board opponentBoard, Random random)
    {
        var orientation = random.Next(2) == 0 ? Orientation.Row : Orientation.Column;
        var index = random.Next(opponentBoard.Size);
        return new UsePowerUpRequest(ship.Name, orientation, index, null, null);
    }

    private static UsePowerUpRequest BuildTorpedoRequest(Ship ship, Board opponentBoard, Random random)
    {
        var orientation = random.Next(2) == 0 ? Orientation.Row : Orientation.Column;
        var index = random.Next(opponentBoard.Size);
        var edge = random.Next(2) == 0 ? Edge.Low : Edge.High;
        return new UsePowerUpRequest(ship.Name, orientation, index, edge, null);
    }

    private static UsePowerUpRequest? BuildTwinStrikeRequest(Ship ship, Board opponentBoard, Random random)
    {
        var pair = PickAdjacentUntargetedPair(opponentBoard, random);
        return pair is null
            ? null
            : new UsePowerUpRequest(ship.Name, null, null, null, [pair.Value.A, pair.Value.B]);
    }

    private static UsePowerUpRequest? BuildDoubleStrikeRequest(Ship ship, Board opponentBoard, Random random)
    {
        var candidates = EnumerateUntargeted(opponentBoard);
        if (candidates.Count < 2)
            return null;

        var first = candidates[random.Next(candidates.Count)];
        candidates.Remove(first);
        var second = candidates[random.Next(candidates.Count)];

        return new UsePowerUpRequest(
            ship.Name, null, null, null,
            [new CellTarget(first.X, first.Y), new CellTarget(second.X, second.Y)]);
    }

    private static (CellTarget A, CellTarget B)? PickAdjacentUntargetedPair(Board board, Random random)
    {
        var candidates = new List<(CellTarget A, CellTarget B)>();
        for (var x = 0; x < board.Size; x++)
        {
            for (var y = 0; y < board.Size; y++)
            {
                if (board.IsAlreadyTargeted(x, y))
                    continue;

                if (board.IsWithinBounds(x + 1, y) && !board.IsAlreadyTargeted(x + 1, y))
                    candidates.Add((new CellTarget(x, y), new CellTarget(x + 1, y)));

                if (board.IsWithinBounds(x, y + 1) && !board.IsAlreadyTargeted(x, y + 1))
                    candidates.Add((new CellTarget(x, y), new CellTarget(x, y + 1)));
            }
        }

        return candidates.Count == 0 ? null : candidates[random.Next(candidates.Count)];
    }
```

`BuildDecoyRequest` reuses the existing private `GetNeighbors(int x, int y)` helper already defined in this class. `BuildDoubleStrikeRequest` reuses the existing private `EnumerateUntargeted(Board board)` helper (its `List<(int X, int Y)>.Remove(first)` works because tuples have structural equality).

- [ ] **Step 8: Run tests to verify they pass**

Run: `./scripts/dotnet.sh test --filter "FullyQualifiedName~GameEngineComputerTurnTests|FullyQualifiedName~DifficultyComputerOpponentTests"`
Expected: PASS

- [ ] **Step 9: Run the full test suite to confirm no regressions**

Run: `./scripts/dotnet.sh test`
Expected: PASS

- [ ] **Step 10: Commit**

```bash
git add BattleShip.Models/Domain/TurnResolution.cs BattleShip.Models/Services/IComputerOpponent.cs BattleShip.Models/Services/IGameEngine.cs BattleShip.API/Services/GameEngine.cs BattleShip.API/Services/DifficultyComputerOpponent.cs BattleShip.Tests/Domain/DifficultyComputerOpponentTests.cs BattleShip.Tests/Domain/GameEngineComputerTurnTests.cs
git commit -m "feat(engine): computer opponent can choose to use a power-up on its turn"
```

---

### Task 6: API — mapper, endpoints, `Program.cs`, fleet status on `BoardDto`

**Files:**
- Modify: `BattleShip.Models/Contracts/BoardDto.cs`
- Modify: `BattleShip.API/Services/GameMapper.cs`
- Create: `BattleShip.API/Services/PowerUpMapper.cs`
- Create: `BattleShip.API/Endpoints/PowerUpEndpoints.cs`
- Modify: `BattleShip.API/Program.cs`
- Test: `BattleShip.Tests/Api/PowerUpEndpointTests.cs`

**Interfaces:**
- Consumes: `UsePowerUpAsync`/`PlayComputerTurnAsync` (Tasks 4-5), `PowerUpResultDto`/`ComputerTurnResultDto`/`UsePowerUpRequestValidator` (Task 3), existing `ShotMapper.ToDto`, `ParticipantResolver.Resolve`.
- Produces: `ShipStatusDto(string Name, int Length, PowerUpType PowerUpType, bool IsSunk, bool PowerUpUsed)`, `BoardDto` gains optional `Ships` property, `PowerUpMapper.ToDto(Game, PowerUpTurnResult) : PowerUpResultDto` and `PowerUpMapper.ToDto(Game, ComputerTurnResult) : ComputerTurnResultDto`, routes `POST /api/games/{id}/powerups` and `POST /api/games/{id}/computer-turn`.

- [ ] **Step 1: Write the failing test**

```csharp
// BattleShip.Tests/Api/PowerUpEndpointTests.cs
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestHelpers;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

public class PowerUpEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;

    public PowerUpEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClientWithoutObstacles();
    }

    [Fact]
    public async Task PostRecon_Returns200AndHidesUndiscoveredPositions()
    {
        var gameId = await CreatePveGameAsync();

        var response = await _client.PostAsJsonAsync($"/api/games/{gameId}/powerups", new
        {
            shipName = "Porte-avions",
            orientation = "Row",
            index = 0,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PowerUpResultDto>(JsonOptions);
        Assert.NotNull(result);
        Assert.NotNull(result.Recon);
        Assert.Equal(PowerUpType.Recon, result.Type);
        Assert.Null(result.Torpedo);
        Assert.Null(result.Cells);
    }

    [Fact]
    public async Task PostPowerUp_UnknownShip_Returns400()
    {
        var gameId = await CreatePveGameAsync();

        var response = await _client.PostAsJsonAsync($"/api/games/{gameId}/powerups", new { shipName = "Fregate" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostPowerUp_OnUnknownGame_Returns404()
    {
        var response = await _client.PostAsJsonAsync($"/api/games/{Guid.Empty}/powerups", new
        {
            shipName = "Porte-avions",
            orientation = "Row",
            index = 0,
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostPowerUp_UsedTwice_Returns409()
    {
        var gameId = await CreatePveGameAsync();
        var body = new { shipName = "Porte-avions", orientation = "Row", index = 0 };

        var first = await _client.PostAsJsonAsync($"/api/games/{gameId}/powerups", body);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        await _client.PostAsync($"/api/games/{gameId}/computer-turn", null);

        var second = await _client.PostAsJsonAsync($"/api/games/{gameId}/powerups", body);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task PostComputerTurn_OnComputerTurn_Returns200()
    {
        var gameId = await CreatePveGameAsync();
        await _client.PostAsJsonAsync($"/api/games/{gameId}/shots", new { x = 0, y = 0 });

        var response = await _client.PostAsync($"/api/games/{gameId}/computer-turn", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ComputerTurnResultDto>(JsonOptions);
        Assert.NotNull(result);
        Assert.True(result.Shot is not null || result.PowerUp is not null);
    }

    [Fact]
    public async Task GetPlayerBoard_ExposesOwnFleetPowerUpStatus()
    {
        var gameId = await CreatePveGameAsync();

        var response = await _client.GetAsync($"/api/games/{gameId}/board/player");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var board = await response.Content.ReadFromJsonAsync<BoardDto>(JsonOptions);
        Assert.NotNull(board);
        Assert.NotNull(board.Ships);
        Assert.Equal(5, board.Ships!.Count);
        Assert.All(board.Ships, s => Assert.False(s.PowerUpUsed));
    }

    private async Task<Guid> CreatePveGameAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/games", new { });
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<GameDto>(JsonOptions);
        Assert.NotNull(dto);

        var fleetResponse = await _client.PostAsJsonAsync($"/api/games/{dto.Id}/fleet", FleetTestData.ValidFleetJson);
        fleetResponse.EnsureSuccessStatusCode();

        return dto.Id;
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `./scripts/dotnet.sh test --filter "FullyQualifiedName~PowerUpEndpointTests"`
Expected: FAIL (route doesn't exist yet — 404s where 200 expected, compile error on `BoardDto.Ships`)

- [ ] **Step 3: Extend `BoardDto.cs`**

```csharp
// BattleShip.Models/Contracts/BoardDto.cs
using BattleShip.Models.Domain;

namespace BattleShip.Models.Contracts;

public record CellDto(int X, int Y, VisibleCellState State);

public record ShipStatusDto(string Name, int Length, PowerUpType PowerUpType, bool IsSunk, bool PowerUpUsed);

public record BoardDto(BoardOwner Owner, int Size, IReadOnlyList<CellDto> Cells, IReadOnlyList<ShipStatusDto>? Ships = null);
```

- [ ] **Step 4: Populate `Ships` in `GameMapper.ToBoardDto`**

In `BattleShip.API/Services/GameMapper.cs`, add `using System.Linq;` to the top, then replace `ToBoardDto` and add a helper:

```csharp
    public static BoardDto ToBoardDto(Board board, BoardOwner owner)
    {
        var cells = new List<CellDto>(board.Size * board.Size);
        for (var y = 0; y < board.Size; y++)
        {
            for (var x = 0; x < board.Size; x++)
            {
                cells.Add(new CellDto(x, y, ToVisibleCellState(board.GetCell(x, y), owner)));
            }
        }

        var ships = owner == BoardOwner.Player ? ToShipStatusDtos(board) : null;
        return new BoardDto(owner, board.Size, cells, ships);
    }

    private static IReadOnlyList<ShipStatusDto> ToShipStatusDtos(Board board) =>
        board.Ships.Select(s => new ShipStatusDto(s.Name, s.Length, s.PowerUpType, s.IsSunk, s.PowerUpUsed)).ToList();
```

- [ ] **Step 5: Create `PowerUpMapper.cs`**

```csharp
// BattleShip.API/Services/PowerUpMapper.cs
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;

namespace BattleShip.API.Services;

public static class PowerUpMapper
{
    public static PowerUpResultDto ToDto(Game game, PowerUpTurnResult result)
    {
        var actor = game.MapToPlayerSide(result.Actor)
            ?? throw new InvalidOperationException("Impossible de mapper l'acteur.");

        return new PowerUpResultDto(
            result.ShipName,
            result.Type,
            actor,
            result.Status,
            result.Recon is { } recon ? new ReconResultDto(recon.Orientation, recon.Index, recon.HasContact) : null,
            result.Torpedo is { } torpedo ? ToOutcomeDto(torpedo) : null,
            result.Cells?.Select(ToOutcomeDto).ToList());
    }

    public static ComputerTurnResultDto ToDto(Game game, ComputerTurnResult result) =>
        new(
            result.UsedPowerUp,
            result.Shot is { } shot ? ShotMapper.ToDto(game, shot) : null,
            result.PowerUp is { } powerUp ? ToDto(game, powerUp) : null);

    private static ShotOutcomeDto ToOutcomeDto(ShotResolution resolution) =>
        new(resolution.X, resolution.Y, resolution.Outcome, resolution.SunkShipName);
}
```

- [ ] **Step 6: Create `PowerUpEndpoints.cs`**

```csharp
// BattleShip.API/Endpoints/PowerUpEndpoints.cs
using BattleShip.API.Services;
using BattleShip.API.Validation;
using BattleShip.Models.Contracts;
using BattleShip.Models.Exceptions;
using BattleShip.Models.Services;
using FluentValidation;

namespace BattleShip.API.Endpoints;

public static class PowerUpEndpoints
{
    public static RouteGroupBuilder MapPowerUpEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/games")
            .WithTags("PowerUps");

        group.MapPost("/{id:guid}/powerups", UsePowerUpAsync)
            .WithName("UsePowerUp")
            .Produces<PowerUpResultDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/computer-turn", PlayComputerTurnAsync)
            .WithName("PlayComputerTurn")
            .Produces<ComputerTurnResultDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<IResult> UsePowerUpAsync(
        Guid id,
        UsePowerUpRequest? request,
        IGameEngine engine,
        IGameRepository repository,
        ParticipantResolver participantResolver,
        IValidator<UsePowerUpRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["shipName"] = ["Corps de requete requis."],
            });
        }

        var validationProblem = await validator.ValidateAsResultAsync(request, cancellationToken);
        if (validationProblem is not null)
            return validationProblem;

        var game = await repository.GetByIdAsync(id, cancellationToken);
        if (game is null)
        {
            return TypedResults.Problem(detail: "Partie inconnue", statusCode: StatusCodes.Status404NotFound);
        }

        var token = httpContext.Request.Headers[PlayerTokenHeaders.HeaderName].FirstOrDefault();
        var caller = participantResolver.Resolve(game, token);

        try
        {
            var result = await engine.UsePowerUpAsync(id, caller, request, cancellationToken);
            return TypedResults.Ok(PowerUpMapper.ToDto(game, result));
        }
        catch (GameNotFoundException)
        {
            return TypedResults.Problem(detail: "Partie inconnue", statusCode: StatusCodes.Status404NotFound);
        }
        catch (InvalidPlayerTokenException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status401Unauthorized);
        }
        catch (GameConflictException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (ShotOutOfBoundsException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> PlayComputerTurnAsync(
        Guid id,
        IGameEngine engine,
        IGameRepository repository,
        CancellationToken cancellationToken)
    {
        var game = await repository.GetByIdAsync(id, cancellationToken);
        if (game is null)
        {
            return TypedResults.Problem(detail: "Partie inconnue", statusCode: StatusCodes.Status404NotFound);
        }

        try
        {
            var result = await engine.PlayComputerTurnAsync(id, cancellationToken);
            return TypedResults.Ok(PowerUpMapper.ToDto(game, result));
        }
        catch (GameNotFoundException)
        {
            return TypedResults.Problem(detail: "Partie inconnue", statusCode: StatusCodes.Status404NotFound);
        }
        catch (GameConflictException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }
}
```

- [ ] **Step 7: Wire up `Program.cs`**

Add this line next to the other validator registrations:

```csharp
builder.Services.AddScoped<IValidator<UsePowerUpRequest>, UsePowerUpRequestValidator>();
```

Add this line next to the other `app.Map*Endpoints()` calls:

```csharp
app.MapPowerUpEndpoints();
```

- [ ] **Step 8: Run test to verify it passes**

Run: `./scripts/dotnet.sh test --filter "FullyQualifiedName~PowerUpEndpointTests"`
Expected: PASS (6 tests)

- [ ] **Step 9: Run the full test suite to confirm no regressions**

Run: `./scripts/dotnet.sh test`
Expected: PASS (all tests, including untouched `FireShotEndpointTests`/`PlaceFleetEndpointTests`)

- [ ] **Step 10: Commit**

```bash
git add BattleShip.Models/Contracts/BoardDto.cs BattleShip.API/Services/GameMapper.cs BattleShip.API/Services/PowerUpMapper.cs BattleShip.API/Endpoints/PowerUpEndpoints.cs BattleShip.API/Program.cs BattleShip.Tests/Api/PowerUpEndpointTests.cs
git commit -m "feat(api): expose power-up and computer-turn endpoints"
```

---

### Task 7: App — `IGameApiClient`, `HttpGameApiClient`, `MockGameApiClient`

**Files:**
- Modify: `BattleShip.App/Services/IGameApiClient.cs`
- Modify: `BattleShip.App/Services/HttpGameApiClient.cs`
- Modify: `BattleShip.App/Services/MockGameApiClient.cs`

**Interfaces:**
- Consumes: `UsePowerUpRequest`/`PowerUpResultDto`/`ComputerTurnResultDto`/`ShipStatusDto` (Tasks 3/6), `Board.TryPlaceDecoy/ScanLine/FireTorpedo/ResolveShot` (Task 2/existing), `Ship.PowerUpType/CanUsePowerUp/MarkPowerUpUsed` (Task 1).
- Produces: `IGameApiClient.UsePowerUpAsync(Guid id, UsePowerUpRequest request, string? playerToken, CancellationToken ct = default) : Task<PowerUpResultDto>`, `IGameApiClient.PlayComputerTurnAsync(Guid id, string? playerToken, CancellationToken ct = default) : Task<ComputerTurnResultDto>`, implemented by both clients.

There is no automated test project covering `BattleShip.App` (per `docs/superpowers/specs/2026-09-15-radar-frontend-design.md`, front-end verification is manual through the Docker workflow). This task is verified by `./scripts/dotnet.sh build` and manually in Task 11.

- [ ] **Step 1: Extend `IGameApiClient.cs`**

```csharp
// BattleShip.App/Services/IGameApiClient.cs
using BattleShip.Models.Contracts;

namespace BattleShip.App.Services;

public interface IGameApiClient
{
    Task<GameCreatedDto> CreateGameAsync(CreateGameRequest request, CancellationToken ct = default);
    Task<JoinGameDto> JoinGameAsync(Guid id, CancellationToken ct = default);
    Task<GameDto> GetGameAsync(Guid id, CancellationToken ct = default);
    Task<GameDto> PlaceFleetAsync(Guid id, PlaceFleetRequest request, string? playerToken, CancellationToken ct = default);
    Task<BoardDto> GetPlayerBoardAsync(Guid id, string? playerToken, CancellationToken ct = default);
    Task<BoardDto> GetOpponentBoardAsync(Guid id, string? playerToken, CancellationToken ct = default);
    Task<ShotResultDto> FireShotAsync(Guid id, ShotRequest shot, string? playerToken, CancellationToken ct = default);
    Task<PowerUpResultDto> UsePowerUpAsync(Guid id, UsePowerUpRequest request, string? playerToken, CancellationToken ct = default);
    Task<ComputerTurnResultDto> PlayComputerTurnAsync(Guid id, string? playerToken, CancellationToken ct = default);
}
```

- [ ] **Step 2: Extend `HttpGameApiClient.cs`**

Add these methods to the `HttpGameApiClient` class, after `FireShotAsync`:

```csharp
    public async Task<PowerUpResultDto> UsePowerUpAsync(
        Guid id, UsePowerUpRequest request, string? playerToken, CancellationToken ct = default)
    {
        using var httpRequest = CreateRequest(HttpMethod.Post, $"api/games/{id}/powerups", playerToken);
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);
        using var response = await http.SendAsync(httpRequest, ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<PowerUpResultDto>(JsonOptions, ct))!;
    }

    public async Task<ComputerTurnResultDto> PlayComputerTurnAsync(
        Guid id, string? playerToken, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"api/games/{id}/computer-turn", playerToken);
        using var response = await http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<ComputerTurnResultDto>(JsonOptions, ct))!;
    }
```

- [ ] **Step 3: Run build to verify it compiles**

Run: `./scripts/dotnet.sh build`
Expected: FAIL (compile error — `MockGameApiClient` doesn't implement the two new interface methods yet)

- [ ] **Step 4: Extend `MockGameApiClient.cs`**

Add `using System.Linq;` if not already present (it already uses LINQ elsewhere in the file, so it should already have it via implicit usings — verify by building).

Add these public methods after `FireShotAsync`:

```csharp
    public Task<PowerUpResultDto> UsePowerUpAsync(
        Guid id, UsePowerUpRequest request, string? playerToken, CancellationToken ct = default)
    {
        var game = GetGameOrThrow(id);
        EnsureStarted(game);

        if (game.Status != GameStatus.PlayerTurn)
        {
            throw Conflict("Ce n'est pas votre tour.");
        }

        var result = ResolvePowerUp(game, game.PlayerBoard, game.ComputerBoard!, request, PlayerSide.Player);
        game.Status = game.ComputerBoard!.AreAllShipsSunk() ? GameStatus.PlayerWon : GameStatus.ComputerTurn;

        return Task.FromResult(result with { Status = game.Status });
    }

    public Task<ComputerTurnResultDto> PlayComputerTurnAsync(
        Guid id, string? playerToken, CancellationToken ct = default)
    {
        var game = GetGameOrThrow(id);
        EnsureStarted(game);

        if (game.Status != GameStatus.ComputerTurn)
        {
            throw Conflict("Ce n'est pas le tour de l'ordinateur.");
        }

        var request = ChooseComputerPowerUp(game);
        if (request is not null)
        {
            var powerUpResult = ResolvePowerUp(game, game.ComputerBoard!, game.PlayerBoard, request, PlayerSide.Computer);
            game.Status = game.PlayerBoard.AreAllShipsSunk() ? GameStatus.ComputerWon : GameStatus.PlayerTurn;
            return Task.FromResult(new ComputerTurnResultDto(true, null, powerUpResult with { Status = game.Status }));
        }

        var shotResult = FireComputerShot(game);
        return Task.FromResult(new ComputerTurnResultDto(false, shotResult, null));
    }
```

Add these private static helpers anywhere in the class (e.g. after `UpdateHuntState`):

```csharp
    private static PowerUpResultDto ResolvePowerUp(
        MockGame game, Board ownBoard, Board opponentBoard, UsePowerUpRequest request, PlayerSide actor)
    {
        var ship = ownBoard.Ships.FirstOrDefault(s => s.Name == request.ShipName)
            ?? throw ValidationError("Navire inconnu.");

        if (!ship.CanUsePowerUp)
        {
            throw Conflict("Power-up indisponible pour ce navire.");
        }

        ReconResultDto? recon = null;
        ShotOutcomeDto? torpedo = null;
        IReadOnlyList<ShotOutcomeDto>? cells = null;

        switch (ship.PowerUpType)
        {
            case PowerUpType.Recon:
            {
                var (orientation, index) = RequireLine(request);
                recon = new ReconResultDto(orientation, index, opponentBoard.ScanLine(orientation, index));
                break;
            }
            case PowerUpType.Torpedo:
            {
                var (orientation, index) = RequireLine(request);
                var entryEdge = request.EntryEdge ?? throw ValidationError("Bord d'entree requis pour la torpille.");
                var resolution = opponentBoard.FireTorpedo(orientation, index, entryEdge);
                torpedo = resolution is null ? null : ToOutcomeDto(resolution);
                break;
            }
            case PowerUpType.TwinStrike:
                cells = ResolveMultiStrike(opponentBoard, request, requireAdjacent: true);
                break;
            case PowerUpType.DoubleStrike:
                cells = ResolveMultiStrike(opponentBoard, request, requireAdjacent: false);
                break;
            case PowerUpType.Decoy:
                ResolveDecoy(ownBoard, ship, request);
                break;
        }

        ship.MarkPowerUpUsed();

        return new PowerUpResultDto(ship.Name, ship.PowerUpType, actor, game.Status, recon, torpedo, cells);
    }

    private static (Orientation Orientation, int Index) RequireLine(UsePowerUpRequest request)
    {
        if (request.Orientation is not { } orientation || request.Index is not { } index)
        {
            throw ValidationError("Ligne ou colonne requise pour ce power-up.");
        }
        return (orientation, index);
    }

    private static IReadOnlyList<ShotOutcomeDto> ResolveMultiStrike(Board opponentBoard, UsePowerUpRequest request, bool requireAdjacent)
    {
        var targetCells = request.Cells;
        if (targetCells is not { Count: 2 })
        {
            throw ValidationError("Deux cases sont requises pour ce power-up.");
        }

        var (a, b) = (targetCells[0], targetCells[1]);
        if (requireAdjacent && Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) != 1)
        {
            throw ValidationError("Les deux cases doivent etre adjacentes.");
        }

        foreach (var cell in targetCells)
        {
            if (!opponentBoard.IsWithinBounds(cell.X, cell.Y))
            {
                throw ValidationError("La cible est hors de la grille.");
            }
            if (opponentBoard.IsAlreadyTargeted(cell.X, cell.Y))
            {
                throw Conflict("Cette case a deja ete ciblee.");
            }
        }

        return targetCells.Select(c => ToOutcomeDto(opponentBoard.ResolveShot(c.X, c.Y))).ToList();
    }

    private static void ResolveDecoy(Board ownBoard, Ship ship, UsePowerUpRequest request)
    {
        if (request.Cells is not { Count: 1 })
        {
            throw ValidationError("Une case est requise pour le leurre.");
        }

        var target = request.Cells[0];
        if (!ship.Cells.Any(c => Math.Abs(c.X - target.X) + Math.Abs(c.Y - target.Y) == 1))
        {
            throw ValidationError("Le leurre doit etre adjacent au contre-torpilleur.");
        }

        if (!ownBoard.TryPlaceDecoy(target.X, target.Y))
        {
            throw Conflict("Impossible de placer le leurre ici.");
        }
    }

    private static ShotOutcomeDto ToOutcomeDto(ShotResolution resolution) =>
        new(resolution.X, resolution.Y, resolution.Outcome, resolution.SunkShipName);

    private static UsePowerUpRequest? ChooseComputerPowerUp(MockGame game)
    {
        var eligible = game.ComputerBoard!.Ships.Where(s => s.CanUsePowerUp).ToList();
        if (eligible.Count == 0)
        {
            return null;
        }

        var chance = game.Difficulty == Difficulty.Hard ? 0.35 : 0.15;
        if (Random.Shared.NextDouble() >= chance)
        {
            return null;
        }

        var decoy = eligible.FirstOrDefault(s => s.PowerUpType == PowerUpType.Decoy);
        if (decoy is not null && Random.Shared.Next(3) == 0)
        {
            var target = FindDecoySpot(decoy, game.ComputerBoard);
            if (target is not null)
            {
                return new UsePowerUpRequest(decoy.Name, null, null, null, [new CellTarget(target.Value.X, target.Value.Y)]);
            }
        }

        var attackers = eligible.Where(s => s.PowerUpType != PowerUpType.Decoy).ToList();
        if (attackers.Count == 0)
        {
            return null;
        }

        var ship = attackers[Random.Shared.Next(attackers.Count)];
        var board = game.PlayerBoard;

        return ship.PowerUpType switch
        {
            PowerUpType.Recon => new UsePowerUpRequest(
                ship.Name, RandomOrientation(), Random.Shared.Next(board.Size), null, null),
            PowerUpType.Torpedo => new UsePowerUpRequest(
                ship.Name, RandomOrientation(), Random.Shared.Next(board.Size), RandomEdge(), null),
            PowerUpType.TwinStrike => BuildAdjacentPairRequest(ship, board),
            PowerUpType.DoubleStrike => BuildAnyPairRequest(ship, board),
            _ => null,
        };
    }

    private static Orientation RandomOrientation() => Random.Shared.Next(2) == 0 ? Orientation.Row : Orientation.Column;

    private static Edge RandomEdge() => Random.Shared.Next(2) == 0 ? Edge.Low : Edge.High;

    private static (int X, int Y)? FindDecoySpot(Ship ship, Board ownBoard)
    {
        foreach (var (x, y) in ship.Cells)
        {
            foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
            {
                if (ownBoard.IsWithinBounds(nx, ny) && ownBoard.GetCell(nx, ny) == CellState.Empty)
                {
                    return (nx, ny);
                }
            }
        }
        return null;
    }

    private static UsePowerUpRequest? BuildAdjacentPairRequest(Ship ship, Board board)
    {
        for (var x = 0; x < board.Size; x++)
        {
            for (var y = 0; y < board.Size; y++)
            {
                if (board.IsAlreadyTargeted(x, y))
                {
                    continue;
                }

                if (board.IsWithinBounds(x + 1, y) && !board.IsAlreadyTargeted(x + 1, y))
                {
                    return new UsePowerUpRequest(ship.Name, null, null, null, [new CellTarget(x, y), new CellTarget(x + 1, y)]);
                }

                if (board.IsWithinBounds(x, y + 1) && !board.IsAlreadyTargeted(x, y + 1))
                {
                    return new UsePowerUpRequest(ship.Name, null, null, null, [new CellTarget(x, y), new CellTarget(x, y + 1)]);
                }
            }
        }
        return null;
    }

    private static UsePowerUpRequest? BuildAnyPairRequest(Ship ship, Board board)
    {
        var candidates = new List<(int X, int Y)>();
        for (var x = 0; x < board.Size; x++)
        {
            for (var y = 0; y < board.Size; y++)
            {
                if (!board.IsAlreadyTargeted(x, y))
                {
                    candidates.Add((x, y));
                }
            }
        }

        if (candidates.Count < 2)
        {
            return null;
        }

        var first = candidates[Random.Shared.Next(candidates.Count)];
        candidates.Remove(first);
        var second = candidates[Random.Shared.Next(candidates.Count)];

        return new UsePowerUpRequest(ship.Name, null, null, null, [new CellTarget(first.X, first.Y), new CellTarget(second.X, second.Y)]);
    }
```

Also update the existing private `ToBoardDto` method in this file (mirrors Task 6 Step 4, duplicated by design like the rest of this file):

```csharp
    private static BoardDto ToBoardDto(Board board, BoardOwner owner)
    {
        var cells = new List<CellDto>(board.Size * board.Size);
        for (var y = 0; y < board.Size; y++)
        {
            for (var x = 0; x < board.Size; x++)
            {
                cells.Add(new CellDto(x, y, ToVisibleCellState(board.GetCell(x, y), owner)));
            }
        }
        var ships = owner == BoardOwner.Player ? ToShipStatusDtos(board) : null;
        return new BoardDto(owner, board.Size, cells, ships);
    }

    private static IReadOnlyList<ShipStatusDto> ToShipStatusDtos(Board board) =>
        board.Ships.Select(s => new ShipStatusDto(s.Name, s.Length, s.PowerUpType, s.IsSunk, s.PowerUpUsed)).ToList();
```

- [ ] **Step 5: Run build to verify it compiles**

Run: `./scripts/dotnet.sh build`
Expected: PASS

- [ ] **Step 6: Run the full test suite to confirm no regressions**

Run: `./scripts/dotnet.sh test`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add BattleShip.App/Services/IGameApiClient.cs BattleShip.App/Services/HttpGameApiClient.cs BattleShip.App/Services/MockGameApiClient.cs
git commit -m "feat(app): power-up support in HTTP and mock API clients"
```

---

### Task 8: App — `GameSession` wiring

**Files:**
- Create: `BattleShip.App/Services/TurnLogEntry.cs`
- Modify: `BattleShip.App/Services/GameSession.cs`

**Interfaces:**
- Consumes: `IGameApiClient.UsePowerUpAsync`/`PlayComputerTurnAsync` (Task 7).
- Produces: `TurnLogEntry(ShotResultDto? Shot, PowerUpResultDto? PowerUp)` with `FromShot`/`FromPowerUp` factory methods; `GameSession.History` becomes `IReadOnlyList<TurnLogEntry>`; `GameSession.UsePowerUpAsync(UsePowerUpRequest request) : Task`.

- [ ] **Step 1: Create `TurnLogEntry.cs`**

```csharp
// BattleShip.App/Services/TurnLogEntry.cs
using BattleShip.Models.Contracts;

namespace BattleShip.App.Services;

public sealed record TurnLogEntry(ShotResultDto? Shot, PowerUpResultDto? PowerUp)
{
    public static TurnLogEntry FromShot(ShotResultDto shot) => new(shot, null);
    public static TurnLogEntry FromPowerUp(PowerUpResultDto powerUp) => new(null, powerUp);
}
```

- [ ] **Step 2: Update `GameSession.cs`**

Replace the `_history` field declaration and `History` property:

```csharp
    private readonly List<TurnLogEntry> _history = [];
    // ...
    public IReadOnlyList<TurnLogEntry> History => _history;
```

Replace `FireShotAsync`:

```csharp
    public Task FireShotAsync(int x, int y) => RunAsync(async () =>
    {
        if (Game is null)
        {
            return;
        }

        var result = await client.FireShotAsync(Game.Id, new ShotRequest(x, y), PlayerToken);
        _history.Add(TurnLogEntry.FromShot(result));

        if (Game.Mode == GameMode.VsComputer && result.Status is GameStatus.ComputerTurn)
        {
            await PlayComputerTurnAsync();
        }

        Game = await client.GetGameAsync(Game.Id);
        await RefreshBoardsAsync();
    });
```

Add a new `UsePowerUpAsync` method after `FireShotAsync`:

```csharp
    public Task UsePowerUpAsync(UsePowerUpRequest request) => RunAsync(async () =>
    {
        if (Game is null)
        {
            return;
        }

        var result = await client.UsePowerUpAsync(Game.Id, request, PlayerToken);
        _history.Add(TurnLogEntry.FromPowerUp(result));

        if (Game.Mode == GameMode.VsComputer && result.Status is GameStatus.ComputerTurn)
        {
            await PlayComputerTurnAsync();
        }

        Game = await client.GetGameAsync(Game.Id);
        await RefreshBoardsAsync();
    });

    private async Task PlayComputerTurnAsync()
    {
        if (Game is null)
        {
            return;
        }

        var computerResult = await client.PlayComputerTurnAsync(Game.Id, PlayerToken);
        _history.Add(computerResult.UsedPowerUp
            ? TurnLogEntry.FromPowerUp(computerResult.PowerUp!)
            : TurnLogEntry.FromShot(computerResult.Shot!));
    }
```

- [ ] **Step 3: Run build to verify it compiles**

Run: `./scripts/dotnet.sh build`
Expected: FAIL (compile error — `ShotLog.razor` still expects `IReadOnlyList<ShotResultDto>`; fixed in Task 9)

- [ ] **Step 4: Commit**

```bash
git add BattleShip.App/Services/TurnLogEntry.cs BattleShip.App/Services/GameSession.cs
git commit -m "feat(app): route computer turns and power-ups through GameSession"
```

(Build is expected to stay red until Task 9 updates `ShotLog.razor` — that's fine, this task and the next are tightly coupled; the next task's Step 1 fixes the build before its own commit.)

---

### Task 9: App — UI (power-up bar, targeting, log)

**Files:**
- Modify: `BattleShip.App/Shared/RadarGrid.razor`
- Create: `BattleShip.App/Shared/PowerUpBar.razor`
- Modify: `BattleShip.App/Shared/ShotLog.razor`
- Modify: `BattleShip.App/Pages/Battle.razor`

**Interfaces:**
- Consumes: `GameSession.UsePowerUpAsync`/`History`/`CanFire` (Task 8), `ShipStatusDto` (Task 6).
- Produces: `RadarGrid.TargetPredicate` parameter, `PowerUpBar` component, updated `ShotLog`/`Battle.razor` targeting flow.

- [ ] **Step 1: Add `TargetPredicate` to `RadarGrid.razor`**

Replace the `@code` block's `IsTargetable` and add the new parameter:

```csharp
@code {
    [Parameter, EditorRequired] public required BoardDto Board { get; set; }
    [Parameter] public bool Interactive { get; set; }
    [Parameter] public EventCallback<(int X, int Y)> OnCellClick { get; set; }
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public Func<VisibleCellState, bool>? TargetPredicate { get; set; }

    private static string CellClass(VisibleCellState state) => state switch
    {
        VisibleCellState.Unknown => "radar-cell--unknown",
        VisibleCellState.Empty => "radar-cell--empty",
        VisibleCellState.Ship => "radar-cell--ship",
        VisibleCellState.Miss => "radar-cell--miss",
        VisibleCellState.Hit => "radar-cell--hit",
        VisibleCellState.Sunk => "radar-cell--sunk",
        VisibleCellState.Obstacle => "radar-cell--obstacle",
        _ => "",
    };

    private bool IsTargetable(VisibleCellState state) => (TargetPredicate ?? DefaultTargetPredicate)(state);

    private static bool DefaultTargetPredicate(VisibleCellState state) => state == VisibleCellState.Unknown;

    private async Task HandleClickAsync(CellDto cell)
    {
        if (!Interactive || !IsTargetable(cell.State))
        {
            return;
        }

        await OnCellClick.InvokeAsync((cell.X, cell.Y));
    }
}
```

- [ ] **Step 2: Create `PowerUpBar.razor`**

```razor
@* BattleShip.App/Shared/PowerUpBar.razor *@
<div class="power-up-bar radar-panel">
    <header class="hud-label">Power-ups</header>
    <div class="power-up-bar__buttons">
        @foreach (var ship in Ships.Where(s => !s.IsSunk && !s.PowerUpUsed))
        {
            <button type="button" class="btn-brass" disabled="@(!Interactive)"
                    @onclick="() => OnSelect.InvokeAsync(ship)">
                @ship.Name (@Label(ship.PowerUpType))
            </button>
        }
    </div>
</div>

@code {
    [Parameter, EditorRequired] public required IReadOnlyList<ShipStatusDto> Ships { get; set; }
    [Parameter] public bool Interactive { get; set; }
    [Parameter] public EventCallback<ShipStatusDto> OnSelect { get; set; }

    private static string Label(PowerUpType type) => type switch
    {
        PowerUpType.Recon => "Reconnaissance",
        PowerUpType.Torpedo => "Torpille",
        PowerUpType.TwinStrike => "Tir jumele",
        PowerUpType.Decoy => "Leurre",
        PowerUpType.DoubleStrike => "Double frappe",
        _ => type.ToString(),
    };
}
```

- [ ] **Step 3: Update `ShotLog.razor`**

Replace the whole file:

```razor
<div class="shot-log radar-panel">
    <header class="hud-label">Journal des tirs</header>
    <ol class="shot-log__list">
        @foreach (var entry in History)
        {
            <li class="shot-log__entry">
                @if (entry.Shot is { } shot)
                {
                    <span class="shot-log__side @(IsMine(shot.Shooter) ? "" : "shot-log__side--enemy")">
                        @SideLabel(shot.Shooter)
                    </span>
                    @FormatOutcome(shot.Shot)
                }
                else if (entry.PowerUp is { } powerUp)
                {
                    <span class="shot-log__side @(IsMine(powerUp.Actor) ? "" : "shot-log__side--enemy")">
                        @SideLabel(powerUp.Actor)
                    </span>
                    @FormatPowerUp(powerUp)
                }
            </li>
        }
    </ol>
</div>

@code {
    [Parameter, EditorRequired] public required IReadOnlyList<TurnLogEntry> History { get; set; }

    private static bool IsMine(PlayerSide side) =>
        side is PlayerSide.Player or PlayerSide.Player1;

    private static string SideLabel(PlayerSide side) => side switch
    {
        PlayerSide.Player => "VOUS",
        PlayerSide.Computer => "ENNEMI",
        PlayerSide.Player1 => "JOUEUR 1",
        PlayerSide.Player2 => "JOUEUR 2",
        _ => "—",
    };

    private static string FormatOutcome(ShotOutcomeDto outcome)
    {
        var coord = $"{(char)('A' + outcome.X)}{outcome.Y + 1}";
        return outcome.Outcome switch
        {
            ShotOutcome.Miss => $"{coord} — manque",
            ShotOutcome.Hit => $"{coord} — touche",
            ShotOutcome.Sunk => $"{coord} — coule ({outcome.SunkShipName})",
            ShotOutcome.Obstacle => $"{coord} — île touchée",
            _ => coord,
        };
    }

    private static string FormatPowerUp(PowerUpResultDto powerUp) => powerUp.Type switch
    {
        PowerUpType.Recon when powerUp.Recon is { } recon =>
            $"{powerUp.ShipName} — reconnaissance {(recon.Orientation == Orientation.Row ? "ligne" : "colonne")} {recon.Index + 1} : {(recon.HasContact ? "contact" : "rien")}",
        PowerUpType.Torpedo when powerUp.Torpedo is { } torpedo => $"{powerUp.ShipName} — torpille : {FormatOutcome(torpedo)}",
        PowerUpType.Torpedo => $"{powerUp.ShipName} — torpille : rien touche",
        PowerUpType.TwinStrike or PowerUpType.DoubleStrike when powerUp.Cells is { } cells =>
            $"{powerUp.ShipName} — {string.Join(", ", cells.Select(FormatOutcome))}",
        PowerUpType.Decoy => $"{powerUp.ShipName} — leurre pose",
        _ => powerUp.ShipName,
    };
}
```

- [ ] **Step 4: Run build to verify it compiles**

Run: `./scripts/dotnet.sh build`
Expected: PASS (Task 8's `GameSession`/`ShotLog` mismatch is now resolved)

- [ ] **Step 5: Update `Battle.razor`**

Add these fields and methods to the existing `@code` block (after the existing `IWon` property, before `OnInitializedAsync`):

```csharp
    private ShipStatusDto? _pendingPowerUp;
    private Orientation? _pendingOrientation;
    private int? _pendingIndex;
    private Edge? _pendingEdge;
    private readonly List<(int X, int Y)> _pendingCells = [];

    private bool IsTargetingLine => _pendingPowerUp?.PowerUpType is PowerUpType.Recon or PowerUpType.Torpedo;
    private bool IsTargetingCells => _pendingPowerUp?.PowerUpType is PowerUpType.TwinStrike or PowerUpType.DoubleStrike;
    private bool IsTargetingDecoy => _pendingPowerUp?.PowerUpType == PowerUpType.Decoy;

    private void SelectPowerUp(ShipStatusDto ship)
    {
        _pendingPowerUp = ship;
        _pendingOrientation = null;
        _pendingIndex = null;
        _pendingEdge = null;
        _pendingCells.Clear();
    }

    private void CancelPowerUp()
    {
        _pendingPowerUp = null;
        _pendingCells.Clear();
    }

    private async Task HandleOpponentCellClickAsync((int X, int Y) target)
    {
        if (_pendingPowerUp is { } ship && IsTargetingCells)
        {
            _pendingCells.Add(target);
            if (_pendingCells.Count == 2)
            {
                var cells = _pendingCells.Select(c => new CellTarget(c.X, c.Y)).ToList();
                await SubmitPowerUpAsync(ship, cells: cells);
            }
            return;
        }

        await Session.FireShotAsync(target.X, target.Y);
    }

    private async Task HandleOwnCellClickAsync((int X, int Y) target)
    {
        if (_pendingPowerUp is { } ship && IsTargetingDecoy)
        {
            await SubmitPowerUpAsync(ship, cells: [new CellTarget(target.X, target.Y)]);
        }
    }

    private async Task SubmitLinePowerUpAsync()
    {
        if (_pendingPowerUp is not { } ship || _pendingOrientation is not { } orientation || _pendingIndex is not { } index)
        {
            return;
        }

        await SubmitPowerUpAsync(ship, orientation: orientation, index: index, edge: _pendingEdge);
    }

    private async Task SubmitPowerUpAsync(
        ShipStatusDto ship,
        Orientation? orientation = null,
        int? index = null,
        Edge? edge = null,
        IReadOnlyList<CellTarget>? cells = null)
    {
        var request = new UsePowerUpRequest(ship.Name, orientation, index, edge, cells);
        _pendingPowerUp = null;
        _pendingCells.Clear();
        await Session.UsePowerUpAsync(request);
    }
```

Replace the `else` branch's markup (the main battle view — everything between `<StatusConsole ...>` and the victory overlay) with:

```razor
else
{
    <StatusConsole Game="Session.Game" />

    <AlertBanner Message="@Session.AlertMessage" />

    @if (Session.PlayerBoard is { Ships: { } ships })
    {
        <PowerUpBar Ships="ships" Interactive="@(Session.CanFire && _pendingPowerUp is null)" OnSelect="SelectPowerUp" />
    }

    @if (_pendingPowerUp is { } pendingShip)
    {
        <section class="radar-panel power-up-target">
            <header class="hud-label">Ciblage — @pendingShip.Name</header>

            @if (IsTargetingLine)
            {
                <div class="power-up-target__controls">
                    <label>
                        Orientation
                        <select @bind="_pendingOrientation">
                            <option value="@Orientation.Row">Ligne</option>
                            <option value="@Orientation.Column">Colonne</option>
                        </select>
                    </label>
                    <label>
                        Index
                        <input type="number" min="0" max="@(Session.Game!.BoardSize - 1)" @bind="_pendingIndex" />
                    </label>
                    @if (pendingShip.PowerUpType == PowerUpType.Torpedo)
                    {
                        <label>
                            Bord d'entree
                            <select @bind="_pendingEdge">
                                <option value="@Edge.Low">Bas / Gauche</option>
                                <option value="@Edge.High">Haut / Droite</option>
                            </select>
                        </label>
                    }
                    <button type="button" class="btn-brass" @onclick="SubmitLinePowerUpAsync">Lancer</button>
                </div>
            }
            else if (IsTargetingCells)
            {
                <p class="hud-label">Cliquez sur deux cases sur la grille ennemie (@_pendingCells.Count/2).</p>
            }
            else if (IsTargetingDecoy)
            {
                <p class="hud-label">Cliquez sur une case vide adjacente a votre @pendingShip.Name, sur votre grille.</p>
            }

            <button type="button" class="btn-brass" @onclick="CancelPowerUp">Annuler</button>
        </section>
    }

    <div class="battle__grids">
        @if (Session.PlayerBoard is not null)
        {
            <RadarGrid Board="Session.PlayerBoard"
                       Interactive="IsTargetingDecoy"
                       Title="VOTRE FLOTTE"
                       TargetPredicate="@(s => s == VisibleCellState.Empty)"
                       OnCellClick="HandleOwnCellClickAsync" />
        }
        @if (Session.OpponentBoard is not null)
        {
            <RadarGrid Board="Session.OpponentBoard"
                       Interactive="@(Session.CanFire || IsTargetingCells)"
                       Title="EAUX ENNEMIES"
                       OnCellClick="HandleOpponentCellClickAsync" />
        }
    </div>

    <ShotLog History="Session.History" />

    @if (IsGameOver)
    {
        <div class="battle__overlay">
            <div class="radar-panel battle__overlay-panel">
                <h2>@(IWon ? "VICTOIRE" : "DEFAITE")</h2>
                <p class="hud-label">
                    @(IWon ? "La flotte ennemie a ete coulee." : "Votre flotte a ete coulee.")
                </p>
                <button type="button" class="btn-brass" @onclick="@(() => Navigation.NavigateTo("/"))">
                    Nouveau deploiement
                </button>
            </div>
        </div>
    }
}
```

Remove the old `private async Task HandleFireAsync((int X, int Y) target) => await Session.FireShotAsync(target.X, target.Y);` method — it's superseded by `HandleOpponentCellClickAsync`, which now handles both plain shots and cell-based power-up targeting.

- [ ] **Step 6: Run build to verify it compiles**

Run: `./scripts/dotnet.sh build`
Expected: PASS

- [ ] **Step 7: Run the full test suite to confirm no regressions**

Run: `./scripts/dotnet.sh test`
Expected: PASS

- [ ] **Step 8: Commit**

```bash
git add BattleShip.App/Shared/RadarGrid.razor BattleShip.App/Shared/PowerUpBar.razor BattleShip.App/Shared/ShotLog.razor BattleShip.App/Pages/Battle.razor
git commit -m "feat(app): power-up bar, targeting flow and log entries in the UI"
```

---

### Task 10: `swagger.yaml` — document the new contract

**Files:**
- Modify: `swagger.yaml`

- [ ] **Step 1: Add the two new paths**

Insert after the existing `/api/games/{id}/shots` path block (before `/api/games/{id}/board/player`):

```yaml
  /api/games/{id}/powerups:
    parameters:
      - $ref: '#/components/parameters/GameId'
      - $ref: '#/components/parameters/PlayerToken'
    post:
      tags: [PowerUps]
      summary: Utiliser le power-up d'un navire
      description: |
        Active le power-up d'un navire de la flotte de l'appelant : Recon
        (Porte-avions), Torpedo (Torpilleur), TwinStrike (Sous-marin), Decoy
        (Contre-torpilleur), DoubleStrike (Croiseur). Utilisable une seule
        fois par navire et par partie, uniquement tant qu'il n'est pas coule.
        Consomme le tour courant, comme un tir.
      operationId: usePowerUp
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/UsePowerUpRequest'
      responses:
        '200':
          description: Power-up resolu
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/PowerUpResultDto'
        '400':
          $ref: '#/components/responses/ValidationProblem'
        '404':
          $ref: '#/components/responses/NotFound'
        '409':
          $ref: '#/components/responses/Conflict'

  /api/games/{id}/computer-turn:
    parameters:
      - $ref: '#/components/parameters/GameId'
    post:
      tags: [PowerUps]
      summary: Resoudre le tour de l'ordinateur (PvE)
      description: |
        Resout le tour de l'ordinateur : il choisit lui-meme, selon la
        difficulte, entre tirer normalement ou utiliser un power-up.
        Remplace, pour le front, l'ancien appel a POST /shots avec un corps
        vide au tour de l'ordinateur.
      operationId: playComputerTurn
      responses:
        '200':
          description: Tour de l'ordinateur resolu
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/ComputerTurnResultDto'
        '404':
          $ref: '#/components/responses/NotFound'
        '409':
          $ref: '#/components/responses/Conflict'

```

- [ ] **Step 2: Add the new schemas**

Insert after the `ShotResultDto` schema (before `ProblemDetails`):

```yaml
    PowerUpType:
      type: string
      description: |
        Porte-avions -> Recon, Croiseur -> DoubleStrike,
        Contre-torpilleur -> Decoy, Sous-marin -> TwinStrike, Torpilleur -> Torpedo.
      enum: [Recon, Torpedo, TwinStrike, Decoy, DoubleStrike]

    Orientation:
      type: string
      enum: [Row, Column]

    Edge:
      type: string
      description: Bord d'entree de la torpille dans la ligne/colonne.
      enum: [Low, High]

    CellTarget:
      type: object
      required: [x, y]
      properties:
        x:
          type: integer
          minimum: 0
        y:
          type: integer
          minimum: 0

    UsePowerUpRequest:
      type: object
      description: |
        Forme variable selon le navire cible :
        Recon/Torpedo : orientation + index (Torpedo ajoute entryEdge).
        TwinStrike/DoubleStrike : deux cases dans cells (TwinStrike : adjacentes).
        Decoy : une case dans cells, adjacente a une case du contre-torpilleur, sur sa propre grille.
      required: [shipName]
      properties:
        shipName:
          type: string
          enum: [Porte-avions, Croiseur, Contre-torpilleur, Sous-marin, Torpilleur]
        orientation:
          allOf:
            - $ref: '#/components/schemas/Orientation'
          nullable: true
        index:
          type: integer
          minimum: 0
          nullable: true
        entryEdge:
          allOf:
            - $ref: '#/components/schemas/Edge'
          nullable: true
        cells:
          type: array
          nullable: true
          items:
            $ref: '#/components/schemas/CellTarget'
      example:
        shipName: Porte-avions
        orientation: Row
        index: 4

    ReconResultDto:
      type: object
      required: [orientation, index, hasContact]
      properties:
        orientation:
          $ref: '#/components/schemas/Orientation'
        index:
          type: integer
        hasContact:
          type: boolean
          description: Presence d'un navire ou d'un ilot sur la ligne/colonne, sans preciser ou.

    PowerUpResultDto:
      type: object
      description: |
        Un seul de recon/torpedo/cells est renseigne selon le type de power-up ;
        aucun ne l'est pour Decoy (aucune information cote adversaire).
      required: [shipName, type, actor, status]
      properties:
        shipName:
          type: string
        type:
          $ref: '#/components/schemas/PowerUpType'
        actor:
          $ref: '#/components/schemas/Player'
        status:
          $ref: '#/components/schemas/GameStatus'
        recon:
          allOf:
            - $ref: '#/components/schemas/ReconResultDto'
          nullable: true
        torpedo:
          allOf:
            - $ref: '#/components/schemas/ShotOutcomeDto'
          nullable: true
        cells:
          type: array
          nullable: true
          items:
            $ref: '#/components/schemas/ShotOutcomeDto'

    ComputerTurnResultDto:
      type: object
      required: [usedPowerUp]
      properties:
        usedPowerUp:
          type: boolean
        shot:
          allOf:
            - $ref: '#/components/schemas/ShotResultDto'
          nullable: true
        powerUp:
          allOf:
            - $ref: '#/components/schemas/PowerUpResultDto'
          nullable: true

```

- [ ] **Step 3: Extend `BoardDto` and add `ShipStatusDto`**

In the existing `BoardDto` schema, add a `ships` property:

```yaml
    ShipStatusDto:
      type: object
      required: [name, length, powerUpType, isSunk, powerUpUsed]
      properties:
        name:
          type: string
        length:
          type: integer
        powerUpType:
          $ref: '#/components/schemas/PowerUpType'
        isSunk:
          type: boolean
        powerUpUsed:
          type: boolean

    BoardDto:
      type: object
      description: |
        Grille et etats visibles. Le tableau cells contient size x size elements.
        Pour la grille adverse, aucune cellule n'a l'etat Ship. ships n'est
        renseigne que pour owner=Player (statut des power-ups de sa propre flotte).
      required: [owner, size, cells]
      properties:
        owner:
          $ref: '#/components/schemas/BoardOwner'
        size:
          type: integer
        cells:
          type: array
          items:
            $ref: '#/components/schemas/CellDto'
        ships:
          type: array
          nullable: true
          items:
            $ref: '#/components/schemas/ShipStatusDto'
      example:
        owner: Opponent
        size: 10
        cells:
          - { x: 0, y: 0, state: Unknown }
          - { x: 1, y: 0, state: Miss }
          - { x: 2, y: 0, state: Hit }
```

(This replaces the old `BoardDto` schema block in place — remove the old one when adding this.)

- [ ] **Step 4: Commit**

```bash
git add swagger.yaml
git commit -m "docs: document power-up and computer-turn contract in swagger.yaml"
```

---

### Task 11: Final verification

**Files:** none (verification only)

- [ ] **Step 1: Full automated test suite**

Run: `./scripts/dotnet.sh test`
Expected: PASS — every test in `BattleShip.Tests`, old and new.

- [ ] **Step 2: Full build**

Run: `./scripts/dotnet.sh build`
Expected: PASS, no warnings-as-errors.

- [ ] **Step 3: Manual walkthrough**

```bash
docker compose up --build
```

At `http://localhost:8081`:
- Start a PvE game, place the fleet, confirm the power-up bar shows all 5 ships.
- Use Recon: confirm the log shows only "contact/rien", never a specific cell.
- Use Torpedo from both edges on a line with a known ship (if reachable): confirm it stops at the first ship/obstacle cell.
- Use TwinStrike on two adjacent cells, and confirm submitting two non-adjacent cells is rejected (`AlertBanner` shows the 409/400 message).
- Use DoubleStrike on two arbitrary cells.
- Use Decoy adjacent to the Contre-torpilleur; keep firing at the opponent until (by chance, over a few games) a decoy cell is hit by the computer, and confirm the game doesn't end/sink from it.
- Confirm each ship's power-up button disappears once used, and again once that ship is sunk.
- Play several full games and confirm the computer occasionally uses a power-up (visible in the log) instead of firing.
- Confirm `POST /shots` with an empty body still works standalone against the API directly (e.g. via `BattleShip.API/BattleShip.API.http`) — it's untouched, just no longer called by the front for computer turns.

- [ ] **Step 4: Report results to the user**

Summarize pass/fail for each of the above; do not claim completion if any manual check fails or any automated test is red.
