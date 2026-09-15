# BattleShip Radar/WW2 Front-End Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a fully playable, WW2/radar-themed Blazor WebAssembly front end for Battleship (`BattleShip.App`), wired to the real `swagger.yaml` contract with a swappable in-memory mock so it's playable before `BattleShip.API` implements the real endpoints.

**Architecture:** A single `IGameApiClient` interface (implemented by `HttpGameApiClient` and `MockGameApiClient`) is selected via one DI switch. A scoped `GameSession` wraps it and holds UI-facing state. Two pages (`Briefing`, `Battle`) and four `Shared` components (`RadarGrid`, `AlertBanner`, `StatusConsole`, `ShotLog`) render that state with a custom CSS theme (no Bootstrap).

**Tech Stack:** .NET 10, Blazor WebAssembly, C# 14, System.Text.Json, plain CSS (custom properties, CSS Grid, keyframe animations), Google Fonts (Black Ops One, Share Tech Mono). No new NuGet packages.

**Spec:** `docs/superpowers/specs/2026-09-15-radar-frontend-design.md`

## Global Constraints

- **Front-end only.** Do not modify `BattleShip.API`'s endpoints/behavior or `BattleShip.Tests`. `BattleShip.Models` may only gain new files under `Contracts/` — no existing file there is touched (there are none yet besides the unused `Class1.cs`, which is deleted).
- **Docker-only.** Never run `dotnet` on the host. Every build/verification command in this plan is `./scripts/dotnet.sh build` (wraps `docker compose --profile tools run --rm sdk dotnet build`) or `docker compose up --build` / `docker compose down`.
- **No new test project.** The approved spec explicitly deferred automated front-end tests (out of scope; a follow-up). It is also not possible to unit-test `BattleShip.App` code from the existing `BattleShip.Tests` project without adding a `BattleShip.Tests → BattleShip.App` project reference, which would contradict `PROMPT-INIT.md`'s non-negotiable project-reference list (`BattleShip.Tests` references only `BattleShip.API` and `BattleShip.Models`). Verification in this plan is therefore: `./scripts/dotnet.sh build` must stay green after every task, and full manual browser verification happens via `docker compose up --build` at the milestones called out below (end of Task 8, and again in Task 9).
- **Project layering (`PROMPT-INIT.md`):** `BattleShip.Models` has zero dependencies and zero ASP.NET/HTTP references. `BattleShip.App` never contains game rules — all placement/shot-resolution logic lives in `MockGameApiClient`, behind the same `IGameApiClient` interface the real HTTP client implements, so swapping to the real API later is a one-line config change.
- **Visibility rule (`swagger.yaml`):** the opponent `BoardDto` must never expose `VisibleCellState.Ship` — only `Unknown`/`Miss`/`Hit`/`Sunk`. `MockGameApiClient.GetOpponentBoardAsync` and the real API both must uphold this; the mock implementation in Task 3 is the one place this is enforced client-side.
- **DTO shape:** every DTO in `BattleShip.Models/Contracts` mirrors `swagger.yaml` exactly (same field names in PascalCase, `System.Text.Json` handles the camelCase-on-the-wire / PascalCase-in-C# mapping — see Task 4).
- **Atomic commits.** One commit per task, in the order below, each ending with:
  ```
  Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
  ```
- **French UI copy.** All user-facing strings are French, matching `README.md`/`swagger.yaml`/`PROMPT-INIT.md`. Code identifiers stay in English.

---

### Task 1: Radar/WW2 theme foundation + template cleanup

**Files:**
- Create: `BattleShip.App/wwwroot/css/theme.css`
- Modify: `BattleShip.App/wwwroot/index.html`
- Modify: `BattleShip.App/wwwroot/css/app.css`
- Modify: `BattleShip.App/Layout/MainLayout.razor`
- Modify: `BattleShip.App/Layout/MainLayout.razor.css`
- Modify: `BattleShip.App/Layout/NavMenu.razor`
- Modify: `BattleShip.App/Layout/NavMenu.razor.css`
- Modify: `BattleShip.App/Pages/Home.razor`
- Modify: `BattleShip.App/Pages/NotFound.razor`
- Delete: `BattleShip.App/Pages/Counter.razor`
- Delete: `BattleShip.App/Pages/Weather.razor`

**Interfaces:**
- Consumes: nothing (first task).
- Produces: CSS custom properties (`--br-bg`, `--br-panel`, `--br-olive`, `--br-olive-light`, `--br-brass`, `--br-brass-dim`, `--br-radar`, `--br-radar-dim`, `--br-alert`, `--br-text`, `--br-text-dim`, `--font-display`, `--font-hud`) and utility classes (`.hud-label`, `.radar-panel`, `.btn-brass`, `.btn-brass--active`, `.alert-banner`) that every later task's markup relies on.

- [ ] **Step 1: Create the theme stylesheet**

`BattleShip.App/wwwroot/css/theme.css`:

```css
/* ===== Design tokens ===== */
:root {
    --br-bg: #12140f;
    --br-bg-elevated: #181b12;
    --br-panel: #1c1f16;
    --br-olive: #3a4025;
    --br-olive-light: #565f38;
    --br-brass: #c9a24b;
    --br-brass-dim: #8f7538;
    --br-radar: #39ff6a;
    --br-radar-dim: rgba(57, 255, 106, 0.25);
    --br-alert: #d1453b;
    --br-text: #e7e4d8;
    --br-text-dim: #a9a693;
    --font-display: "Black Ops One", "Arial Narrow", system-ui, sans-serif;
    --font-hud: "Share Tech Mono", ui-monospace, "Courier New", monospace;
}

/* ===== Reset / base ===== */
* {
    box-sizing: border-box;
}

html, body {
    margin: 0;
    min-height: 100%;
    background: var(--br-bg);
    color: var(--br-text);
    font-family: var(--font-hud);
}

h1, h2, h3, .hud-title {
    font-family: var(--font-display);
    letter-spacing: 0.08em;
    text-transform: uppercase;
    color: var(--br-brass);
    margin: 0 0 0.75rem 0;
}

a, .btn-link {
    color: var(--br-radar);
}

/* ===== Shared primitives ===== */
.hud-label {
    font-family: var(--font-hud);
    text-transform: uppercase;
    letter-spacing: 0.12em;
    font-size: 0.8rem;
    color: var(--br-text-dim);
}

.radar-panel {
    background: var(--br-panel);
    border: 1px solid var(--br-olive);
    border-radius: 4px;
    padding: 1.25rem;
    box-shadow: 0 0 0 1px rgba(0, 0, 0, 0.4), 0 8px 24px rgba(0, 0, 0, 0.35);
    position: relative;
}

.radar-panel::before,
.radar-panel::after {
    content: "";
    position: absolute;
    width: 0.75rem;
    height: 0.75rem;
    border: 2px solid var(--br-brass-dim);
}

.radar-panel::before {
    top: -1px;
    left: -1px;
    border-right: none;
    border-bottom: none;
}

.radar-panel::after {
    bottom: -1px;
    right: -1px;
    border-left: none;
    border-top: none;
}

.btn-brass {
    font-family: var(--font-hud);
    text-transform: uppercase;
    letter-spacing: 0.08em;
    font-size: 0.85rem;
    color: var(--br-bg);
    background: linear-gradient(180deg, var(--br-brass) 0%, var(--br-brass-dim) 100%);
    border: 1px solid var(--br-brass-dim);
    border-radius: 3px;
    padding: 0.6rem 1.1rem;
    cursor: pointer;
    transition: filter 0.15s ease, transform 0.05s ease;
}

.btn-brass:hover:not(:disabled) {
    filter: brightness(1.1);
}

.btn-brass:active:not(:disabled) {
    transform: translateY(1px);
}

.btn-brass:disabled {
    opacity: 0.5;
    cursor: not-allowed;
}

.btn-brass--active {
    outline: 2px solid var(--br-radar);
    outline-offset: 2px;
}

.alert-banner {
    display: flex;
    align-items: center;
    gap: 0.6rem;
    background: rgba(209, 69, 59, 0.15);
    border: 1px solid var(--br-alert);
    color: #ffb3ad;
    border-radius: 3px;
    padding: 0.6rem 0.9rem;
    font-family: var(--font-hud);
    font-size: 0.85rem;
    margin: 0.75rem 0;
}

.alert-banner__icon {
    font-size: 1rem;
}
```

- [ ] **Step 2: Rewrite `wwwroot/index.html`** — drop Bootstrap, add the Google Fonts + theme link, rename the title

```html
<!DOCTYPE html>
<html lang="fr">

<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>BattleShip — War Room</title>
    <base href="/" />
    <link rel="preload" id="webassembly" />
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link href="https://fonts.googleapis.com/css2?family=Black+Ops+One&family=Share+Tech+Mono&display=swap" rel="stylesheet" />
    <link rel="stylesheet" href="css/theme.css" />
    <link rel="stylesheet" href="css/app.css" />
    <link rel="icon" type="image/png" href="favicon.png" />
    <link href="BattleShip.App.styles.css" rel="stylesheet" />
    <script type="importmap"></script>
</head>

<body>
    <div id="app">
        <svg class="loading-progress">
            <circle r="40%" cx="50%" cy="50%" />
            <circle r="40%" cx="50%" cy="50%" />
        </svg>
        <div class="loading-progress-text"></div>
    </div>

    <div id="blazor-error-ui">
        An unhandled error has occurred.
        <a href="." class="reload">Reload</a>
        <span class="dismiss">🗙</span>
    </div>
    <script src="_framework/blazor.webassembly#[.{fingerprint}].js"></script>
</body>

</html>
```

- [ ] **Step 3: Trim `wwwroot/css/app.css`** to only the Blazor mechanical styles (error UI, loading spinner), recolored to the theme — remove every Bootstrap-oriented rule (`.btn-primary`, `.valid.modified`, `.form-floating`, etc., since Bootstrap is no longer linked)

```css
#blazor-error-ui {
    color-scheme: dark;
    background: var(--br-panel);
    border-top: 1px solid var(--br-alert);
    color: var(--br-text);
    bottom: 0;
    box-shadow: 0 -1px 2px rgba(0, 0, 0, 0.4);
    box-sizing: border-box;
    display: none;
    left: 0;
    padding: 0.6rem 1.25rem 0.7rem 1.25rem;
    position: fixed;
    width: 100%;
    z-index: 1000;
    font-family: var(--font-hud);
}

    #blazor-error-ui .dismiss {
        cursor: pointer;
        position: absolute;
        right: 0.75rem;
        top: 0.5rem;
        color: var(--br-text-dim);
    }

    #blazor-error-ui a.reload {
        color: var(--br-radar);
    }

.blazor-error-boundary {
    background: var(--br-alert);
    padding: 1rem 1rem 1rem 1.25rem;
    color: white;
    border-radius: 4px;
    font-family: var(--font-hud);
}

    .blazor-error-boundary::after {
        content: "An error has occurred.";
    }

.loading-progress {
    position: absolute;
    display: block;
    width: 8rem;
    height: 8rem;
    inset: 20vh 0 auto 0;
    margin: 0 auto 0 auto;
}

    .loading-progress circle {
        fill: none;
        stroke: var(--br-olive);
        stroke-width: 0.6rem;
        transform-origin: 50% 50%;
        transform: rotate(-90deg);
    }

        .loading-progress circle:last-child {
            stroke: var(--br-radar);
            stroke-dasharray: calc(3.141 * var(--blazor-load-percentage, 0%) * 0.8), 500%;
            transition: stroke-dasharray 0.05s ease-in-out;
        }

.loading-progress-text {
    position: absolute;
    text-align: center;
    font-weight: bold;
    color: var(--br-text);
    font-family: var(--font-hud);
    inset: calc(20vh + 3.25rem) 0 auto 0.2rem;
}

    .loading-progress-text:after {
        content: var(--blazor-load-percentage-text, "Loading");
    }
```

- [ ] **Step 4: Restyle `Layout/MainLayout.razor`** — drop the sidebar, keep a slim vertical shell (`NavMenu` owns its own header markup/css from Step 5)

```razor
@inherits LayoutComponentBase
<div class="war-room">
    <NavMenu />
    <main class="war-room__main">
        @Body
    </main>
</div>
```

- [ ] **Step 5: Replace `Layout/MainLayout.razor.css`**

```css
.war-room {
    min-height: 100vh;
    display: flex;
    flex-direction: column;
}

.war-room__main {
    flex: 1;
    padding: 1.5rem;
    max-width: 1100px;
    width: 100%;
    margin: 0 auto;
}

@media (max-width: 640px) {
    .war-room__main {
        padding: 1rem;
    }
}
```

- [ ] **Step 6: Replace `Layout/NavMenu.razor`** — single brand link, no hamburger/collapse logic (only one link, nothing to collapse)

```razor
<header class="war-room__header">
    <NavLink href="" Match="NavLinkMatch.All" class="war-room__brand-link">
        <span class="war-room__brand-mark" aria-hidden="true">⌖</span>
        BATTLESHIP <span class="hud-label">// COMMAND</span>
    </NavLink>
</header>
```

- [ ] **Step 7: Replace `Layout/NavMenu.razor.css`**

```css
.war-room__header {
    background: var(--br-bg-elevated);
    border-bottom: 1px solid var(--br-olive);
    padding: 0.85rem 1.5rem;
}

.war-room__brand-link {
    display: flex;
    align-items: center;
    gap: 0.6rem;
    font-family: var(--font-display);
    text-transform: uppercase;
    letter-spacing: 0.1em;
    color: var(--br-brass);
    text-decoration: none;
    font-size: 1.1rem;
}

.war-room__brand-mark {
    color: var(--br-radar);
    font-size: 1.3rem;
}
```

- [ ] **Step 8: Strip `Pages/Home.razor` to a themed placeholder** (replaced by `Briefing.razor` in Task 7 — this keeps `/` rendering something sensible in the meantime)

```razor
@page "/"

<PageTitle>BattleShip — War Room</PageTitle>

<section class="radar-panel">
    <h1>Battleship Command</h1>
    <p class="hud-label">Fleet operations console loading…</p>
</section>
```

- [ ] **Step 9: Restyle `Pages/NotFound.razor`**

```razor
@page "/not-found"
@layout MainLayout

<section class="radar-panel">
    <h1>Signal perdu</h1>
    <p class="hud-label">Coordonnees inconnues. Retour au poste de commandement.</p>
    <a class="btn-brass" href="/">Retour a la base</a>
</section>
```

- [ ] **Step 10: Delete the unused template pages**

```bash
rm BattleShip.App/Pages/Counter.razor BattleShip.App/Pages/Weather.razor
```

- [ ] **Step 11: Build**

Run: `./scripts/dotnet.sh build`
Expected: Build succeeded, no errors.

- [ ] **Step 12: Commit**

```bash
git add BattleShip.App/wwwroot/css/theme.css BattleShip.App/wwwroot/css/app.css \
  BattleShip.App/wwwroot/index.html BattleShip.App/Layout BattleShip.App/Pages
git commit -m "$(cat <<'EOF'
feat(app): apply radar/WW2 theme and remove template pages

Replace the default Blazor template look with a custom war-room/radar
theme (CSS custom properties, Black Ops One + Share Tech Mono fonts,
no Bootstrap) and drop the Counter/Weather template pages ahead of the
real game screens.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Shared game contracts in `BattleShip.Models`

**Files:**
- Create: `BattleShip.Models/Contracts/GameEnums.cs`
- Create: `BattleShip.Models/Contracts/GameDto.cs`
- Create: `BattleShip.Models/Contracts/BoardDto.cs`
- Create: `BattleShip.Models/Contracts/ShotDtos.cs`
- Create: `BattleShip.Models/Contracts/ProblemDetailsDto.cs`
- Create: `BattleShip.Models/Contracts/GameApiException.cs`
- Delete: `BattleShip.Models/Class1.cs`

**Interfaces:**
- Consumes: nothing.
- Produces (namespace `BattleShip.Models.Contracts`, consumed by every later task):
  - `enum GameStatus { Waiting, PlayerTurn, ComputerTurn, PlayerWon, ComputerWon }`
  - `enum Player { Player, Computer }`
  - `enum ShotOutcome { Miss, Hit, Sunk }`
  - `enum VisibleCellState { Unknown, Empty, Ship, Miss, Hit, Sunk }`
  - `enum BoardOwner { Player, Opponent }`
  - `enum Difficulty { Easy, Normal, Hard }`
  - `record CreateGameRequest(int? BoardSize, Difficulty? Difficulty)`
  - `record GameDto(Guid Id, GameStatus Status, Player? CurrentTurn, int BoardSize, int ShotCount, DateTimeOffset CreatedAt)`
  - `record CellDto(int X, int Y, VisibleCellState State)`
  - `record BoardDto(BoardOwner Owner, int Size, IReadOnlyList<CellDto> Cells)`
  - `record ShotRequest(int X, int Y)`
  - `record ShotOutcomeDto(int X, int Y, ShotOutcome Outcome, string? SunkShipName)`
  - `record ShotResultDto(ShotOutcomeDto PlayerShot, ShotOutcomeDto? ComputerShot, GameStatus Status)`
  - `record ProblemDetailsDto(string? Type, string? Title, int? Status, string? Detail, string? Instance)`
  - `record ValidationProblemDetailsDto(string? Type, string? Title, int? Status, string? Detail, IReadOnlyDictionary<string, string[]>? Errors)`
  - `class GameApiException(ProblemDetailsDto problem, int statusCode) : Exception` with `Problem` and `StatusCode` properties

- [ ] **Step 1: Create the enums**

`BattleShip.Models/Contracts/GameEnums.cs`:

```csharp
namespace BattleShip.Models.Contracts;

public enum GameStatus
{
    Waiting,
    PlayerTurn,
    ComputerTurn,
    PlayerWon,
    ComputerWon,
}

public enum Player
{
    Player,
    Computer,
}

public enum ShotOutcome
{
    Miss,
    Hit,
    Sunk,
}

public enum VisibleCellState
{
    Unknown,
    Empty,
    Ship,
    Miss,
    Hit,
    Sunk,
}

public enum BoardOwner
{
    Player,
    Opponent,
}

public enum Difficulty
{
    Easy,
    Normal,
    Hard,
}
```

- [ ] **Step 2: Create the game DTOs**

`BattleShip.Models/Contracts/GameDto.cs`:

```csharp
namespace BattleShip.Models.Contracts;

public record CreateGameRequest(int? BoardSize, Difficulty? Difficulty);

public record GameDto(
    Guid Id,
    GameStatus Status,
    Player? CurrentTurn,
    int BoardSize,
    int ShotCount,
    DateTimeOffset CreatedAt);
```

`BattleShip.Models/Contracts/BoardDto.cs`:

```csharp
namespace BattleShip.Models.Contracts;

public record CellDto(int X, int Y, VisibleCellState State);

public record BoardDto(BoardOwner Owner, int Size, IReadOnlyList<CellDto> Cells);
```

`BattleShip.Models/Contracts/ShotDtos.cs`:

```csharp
namespace BattleShip.Models.Contracts;

public record ShotRequest(int X, int Y);

public record ShotOutcomeDto(int X, int Y, ShotOutcome Outcome, string? SunkShipName);

public record ShotResultDto(ShotOutcomeDto PlayerShot, ShotOutcomeDto? ComputerShot, GameStatus Status);
```

- [ ] **Step 3: Create the error DTOs and exception**

`BattleShip.Models/Contracts/ProblemDetailsDto.cs`:

```csharp
namespace BattleShip.Models.Contracts;

public record ProblemDetailsDto(string? Type, string? Title, int? Status, string? Detail, string? Instance);

public record ValidationProblemDetailsDto(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    IReadOnlyDictionary<string, string[]>? Errors);
```

`BattleShip.Models/Contracts/GameApiException.cs`:

```csharp
namespace BattleShip.Models.Contracts;

public sealed class GameApiException(ProblemDetailsDto problem, int statusCode)
    : Exception(problem.Detail ?? problem.Title ?? $"API error ({statusCode})")
{
    public ProblemDetailsDto Problem { get; } = problem;
    public int StatusCode { get; } = statusCode;
}
```

- [ ] **Step 4: Delete the unused template class**

```bash
rm BattleShip.Models/Class1.cs
```

- [ ] **Step 5: Build**

Run: `./scripts/dotnet.sh build`
Expected: Build succeeded, no errors.

- [ ] **Step 6: Commit**

```bash
git add BattleShip.Models/Contracts BattleShip.Models/Class1.cs
git commit -m "$(cat <<'EOF'
feat(models): add shared game contracts

Add the DTOs, enums, and GameApiException matching swagger.yaml,
shared between the App's HTTP and mock API clients. Remove the
unused Class1.cs template stub.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: `IGameApiClient` + in-memory `MockGameApiClient`

**Files:**
- Create: `BattleShip.App/Services/IGameApiClient.cs`
- Create: `BattleShip.App/Services/MockGameApiClient.cs`
- Modify: `BattleShip.App/_Imports.razor`

**Interfaces:**
- Consumes: all DTOs/enums/`GameApiException` from Task 2 (`BattleShip.Models.Contracts`).
- Produces:
  - `interface IGameApiClient` with `CreateGameAsync`, `GetGameAsync`, `GetPlayerBoardAsync`, `GetOpponentBoardAsync`, `FireShotAsync` (signatures below) — this is what `GameSession` (Task 5), `HttpGameApiClient` (Task 4), and `Program.cs`'s DI registration depend on.
  - `class MockGameApiClient : IGameApiClient` — a working, in-memory implementation.

- [ ] **Step 1: Add global usings for the new namespaces**

Append to `BattleShip.App/_Imports.razor`:

```razor
@using BattleShip.App.Services
@using BattleShip.Models.Contracts
```

- [ ] **Step 2: Define the client interface**

`BattleShip.App/Services/IGameApiClient.cs`:

```csharp
using BattleShip.Models.Contracts;

namespace BattleShip.App.Services;

public interface IGameApiClient
{
    Task<GameDto> CreateGameAsync(CreateGameRequest request, CancellationToken ct = default);
    Task<GameDto> GetGameAsync(Guid id, CancellationToken ct = default);
    Task<BoardDto> GetPlayerBoardAsync(Guid id, CancellationToken ct = default);
    Task<BoardDto> GetOpponentBoardAsync(Guid id, CancellationToken ct = default);
    Task<ShotResultDto> FireShotAsync(Guid id, ShotRequest shot, CancellationToken ct = default);
}
```

- [ ] **Step 3: Implement the mock game engine**

`BattleShip.App/Services/MockGameApiClient.cs`:

```csharp
using System.Collections.Concurrent;
using BattleShip.Models.Contracts;

namespace BattleShip.App.Services;

public sealed class MockGameApiClient : IGameApiClient
{
    private static readonly (string Name, int Length)[] FleetSpec =
    [
        ("Porte-avions", 5),
        ("Croiseur", 4),
        ("Contre-torpilleur", 3),
        ("Sous-marin", 3),
        ("Torpilleur", 2),
    ];

    private readonly ConcurrentDictionary<Guid, MockGame> _games = new();

    public Task<GameDto> CreateGameAsync(CreateGameRequest request, CancellationToken ct = default)
    {
        var boardSize = Math.Clamp(request.BoardSize ?? 10, 5, 20);
        var difficulty = request.Difficulty ?? Difficulty.Normal;
        var rng = Random.Shared;

        var game = new MockGame
        {
            Id = Guid.NewGuid(),
            BoardSize = boardSize,
            Difficulty = difficulty,
            PlayerFleet = PlaceFleet(boardSize, rng),
            ComputerFleet = PlaceFleet(boardSize, rng),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _games[game.Id] = game;
        return Task.FromResult(ToGameDto(game));
    }

    public Task<GameDto> GetGameAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(ToGameDto(GetGameOrThrow(id)));

    public Task<BoardDto> GetPlayerBoardAsync(Guid id, CancellationToken ct = default)
    {
        var game = GetGameOrThrow(id);
        var cells = new List<CellDto>();
        for (var y = 0; y < game.BoardSize; y++)
        {
            for (var x = 0; x < game.BoardSize; x++)
            {
                var cell = (X: x, Y: y);
                var ship = game.PlayerFleet.FirstOrDefault(s => s.Cells.Contains(cell));
                var state = ship switch
                {
                    null => VisibleCellState.Empty,
                    _ when ship.Hits.Contains(cell) && ship.IsSunk => VisibleCellState.Sunk,
                    _ when ship.Hits.Contains(cell) => VisibleCellState.Hit,
                    _ => VisibleCellState.Ship,
                };
                cells.Add(new CellDto(x, y, state));
            }
        }
        return Task.FromResult(new BoardDto(BoardOwner.Player, game.BoardSize, cells));
    }

    public Task<BoardDto> GetOpponentBoardAsync(Guid id, CancellationToken ct = default)
    {
        var game = GetGameOrThrow(id);
        var cells = new List<CellDto>();
        for (var y = 0; y < game.BoardSize; y++)
        {
            for (var x = 0; x < game.BoardSize; x++)
            {
                var cell = (X: x, Y: y);
                if (!game.PlayerShotsAtComputer.Contains(cell))
                {
                    cells.Add(new CellDto(x, y, VisibleCellState.Unknown));
                    continue;
                }

                var ship = game.ComputerFleet.FirstOrDefault(s => s.Cells.Contains(cell));
                var state = ship switch
                {
                    { IsSunk: true } => VisibleCellState.Sunk,
                    not null => VisibleCellState.Hit,
                    null => VisibleCellState.Miss,
                };
                cells.Add(new CellDto(x, y, state));
            }
        }
        return Task.FromResult(new BoardDto(BoardOwner.Opponent, game.BoardSize, cells));
    }

    public Task<ShotResultDto> FireShotAsync(Guid id, ShotRequest shot, CancellationToken ct = default)
    {
        var game = GetGameOrThrow(id);
        if (game.Status is GameStatus.PlayerWon or GameStatus.ComputerWon)
        {
            throw Conflict("La partie est deja terminee.");
        }

        if (shot.X < 0 || shot.X >= game.BoardSize || shot.Y < 0 || shot.Y >= game.BoardSize)
        {
            throw ValidationError("La cible est hors de la grille.");
        }

        var target = (X: shot.X, Y: shot.Y);
        if (!game.PlayerShotsAtComputer.Add(target))
        {
            throw Conflict("Cette case a deja ete ciblee.");
        }

        var playerShot = ResolveShot(game.ComputerFleet, target);
        game.ShotCount++;

        ShotOutcomeDto? computerShot = null;
        if (AllSunk(game.ComputerFleet))
        {
            game.Status = GameStatus.PlayerWon;
        }
        else
        {
            var computerTarget = PickComputerTarget(game);
            game.ComputerShotsAtPlayer.Add(computerTarget);
            computerShot = ResolveShot(game.PlayerFleet, computerTarget);
            UpdateHuntState(game, computerShot);
            game.Status = AllSunk(game.PlayerFleet) ? GameStatus.ComputerWon : GameStatus.PlayerTurn;
        }

        return Task.FromResult(new ShotResultDto(playerShot, computerShot, game.Status));
    }

    private MockGame GetGameOrThrow(Guid id)
    {
        if (!_games.TryGetValue(id, out var game))
        {
            throw NotFound("Partie introuvable.");
        }
        return game;
    }

    private static List<ShipInstance> PlaceFleet(int boardSize, Random rng)
    {
        var occupied = new HashSet<(int X, int Y)>();
        var fleet = new List<ShipInstance>();

        foreach (var (name, length) in FleetSpec)
        {
            const int maxAttempts = 5000;
            var placed = false;

            for (var attempt = 0; attempt < maxAttempts && !placed; attempt++)
            {
                var horizontal = rng.Next(2) == 0;
                var maxX = horizontal ? boardSize - length : boardSize - 1;
                var maxY = horizontal ? boardSize - 1 : boardSize - length;
                var startX = rng.Next(maxX + 1);
                var startY = rng.Next(maxY + 1);

                var cells = Enumerable.Range(0, length)
                    .Select(i => horizontal
                        ? (X: startX + i, Y: startY)
                        : (X: startX, Y: startY + i))
                    .ToList();

                if (cells.Any(occupied.Contains))
                {
                    continue;
                }

                foreach (var cell in cells)
                {
                    occupied.Add(cell);
                }

                fleet.Add(new ShipInstance { Name = name, Length = length, Cells = cells });
                placed = true;
            }

            if (!placed)
            {
                throw new InvalidOperationException(
                    $"Impossible de placer le navire '{name}' sur une grille de taille {boardSize}.");
            }
        }

        return fleet;
    }

    private static ShotOutcomeDto ResolveShot(List<ShipInstance> fleet, (int X, int Y) target)
    {
        var ship = fleet.FirstOrDefault(s => s.Cells.Contains(target));
        if (ship is null)
        {
            return new ShotOutcomeDto(target.X, target.Y, ShotOutcome.Miss, null);
        }

        ship.Hits.Add(target);
        return ship.IsSunk
            ? new ShotOutcomeDto(target.X, target.Y, ShotOutcome.Sunk, ship.Name)
            : new ShotOutcomeDto(target.X, target.Y, ShotOutcome.Hit, null);
    }

    private static (int X, int Y) PickComputerTarget(MockGame game)
    {
        if (game.Difficulty == Difficulty.Hard)
        {
            while (game.HuntQueue.Count > 0)
            {
                var candidate = game.HuntQueue.Dequeue();
                if (IsInBounds(candidate, game.BoardSize) && !game.ComputerShotsAtPlayer.Contains(candidate))
                {
                    return candidate;
                }
            }
        }

        return RandomUntried(game);
    }

    private static (int X, int Y) RandomUntried(MockGame game)
    {
        var rng = Random.Shared;
        (int X, int Y) candidate;
        do
        {
            candidate = (rng.Next(game.BoardSize), rng.Next(game.BoardSize));
        } while (game.ComputerShotsAtPlayer.Contains(candidate));
        return candidate;
    }

    private static bool IsInBounds((int X, int Y) cell, int size) =>
        cell.X >= 0 && cell.X < size && cell.Y >= 0 && cell.Y < size;

    private static void UpdateHuntState(MockGame game, ShotOutcomeDto outcome)
    {
        if (game.Difficulty != Difficulty.Hard)
        {
            return;
        }

        if (outcome.Outcome == ShotOutcome.Sunk)
        {
            game.HuntQueue.Clear();
        }
        else if (outcome.Outcome == ShotOutcome.Hit)
        {
            var (x, y) = (outcome.X, outcome.Y);
            foreach (var neighbor in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
            {
                game.HuntQueue.Enqueue(neighbor);
            }
        }
    }

    private static bool AllSunk(IEnumerable<ShipInstance> fleet) => fleet.All(s => s.IsSunk);

    private static GameDto ToGameDto(MockGame game) => new(
        game.Id,
        game.Status,
        game.Status is GameStatus.PlayerWon or GameStatus.ComputerWon ? null : Player.Player,
        game.BoardSize,
        game.ShotCount,
        game.CreatedAt);

    private static GameApiException NotFound(string detail) =>
        new(new ProblemDetailsDto("about:blank", "Not Found", 404, detail, null), 404);

    private static GameApiException Conflict(string detail) =>
        new(new ProblemDetailsDto("about:blank", "Conflict", 409, detail, null), 409);

    private static GameApiException ValidationError(string detail) =>
        new(new ProblemDetailsDto("about:blank", "Bad Request", 400, detail, null), 400);

    private sealed class ShipInstance
    {
        public required string Name { get; init; }
        public required int Length { get; init; }
        public required List<(int X, int Y)> Cells { get; init; }
        public HashSet<(int X, int Y)> Hits { get; } = [];
        public bool IsSunk => Hits.Count == Cells.Count;
    }

    private sealed class MockGame
    {
        public required Guid Id { get; init; }
        public required int BoardSize { get; init; }
        public required Difficulty Difficulty { get; init; }
        public required List<ShipInstance> PlayerFleet { get; init; }
        public required List<ShipInstance> ComputerFleet { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public HashSet<(int X, int Y)> PlayerShotsAtComputer { get; } = [];
        public HashSet<(int X, int Y)> ComputerShotsAtPlayer { get; } = [];
        public Queue<(int X, int Y)> HuntQueue { get; } = [];
        public GameStatus Status { get; set; } = GameStatus.PlayerTurn;
        public int ShotCount { get; set; }
    }
}
```

- [ ] **Step 4: Build**

Run: `./scripts/dotnet.sh build`
Expected: Build succeeded, no errors. (The mock isn't wired to any UI yet — it's exercised end-to-end once `GameSession` and the pages exist in Tasks 5–8.)

- [ ] **Step 5: Commit**

```bash
git add BattleShip.App/Services BattleShip.App/_Imports.razor
git commit -m "$(cat <<'EOF'
feat(app): add game API client interface and in-memory mock

Add IGameApiClient and MockGameApiClient: random non-overlapping
fleet placement, shot resolution, a computer opponent (random on
Easy/Normal, adjacent-hunt on Hard), and opponent-board visibility
masking per swagger.yaml. Makes the game playable without a real
backend.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: `HttpGameApiClient` + mock/real DI switch

**Files:**
- Create: `BattleShip.App/Services/HttpGameApiClient.cs`
- Modify: `BattleShip.App/Program.cs`
- Modify: `BattleShip.App/wwwroot/appsettings.json`

**Interfaces:**
- Consumes: `IGameApiClient` (Task 3), all DTOs (Task 2).
- Produces: `class HttpGameApiClient(HttpClient http) : IGameApiClient`; the `UseMockApi` config switch in `Program.cs` that Task 5 appends `GameSession` registration next to.

- [ ] **Step 1: Implement the real HTTP client**

`BattleShip.App/Services/HttpGameApiClient.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BattleShip.Models.Contracts;

namespace BattleShip.App.Services;

public sealed class HttpGameApiClient(HttpClient http) : IGameApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<GameDto> CreateGameAsync(CreateGameRequest request, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("api/games", request, JsonOptions, ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<GameDto>(JsonOptions, ct))!;
    }

    public async Task<GameDto> GetGameAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"api/games/{id}", ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<GameDto>(JsonOptions, ct))!;
    }

    public async Task<BoardDto> GetPlayerBoardAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"api/games/{id}/board/player", ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<BoardDto>(JsonOptions, ct))!;
    }

    public async Task<BoardDto> GetOpponentBoardAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"api/games/{id}/board/opponent", ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<BoardDto>(JsonOptions, ct))!;
    }

    public async Task<ShotResultDto> FireShotAsync(Guid id, ShotRequest shot, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync($"api/games/{id}/shots", shot, JsonOptions, ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<ShotResultDto>(JsonOptions, ct))!;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var statusCode = (int)response.StatusCode;
        ProblemDetailsDto? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions, ct);
        }
        catch (JsonException)
        {
            // Body wasn't problem+json; fall back to a generic problem below.
        }

        problem ??= new ProblemDetailsDto(null, response.ReasonPhrase, statusCode, null, null);
        throw new GameApiException(problem, statusCode);
    }
}
```

- [ ] **Step 2: Wire the DI switch in `Program.cs`**

Replace the full file:

```csharp
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using BattleShip.App;
using BattleShip.App.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

var useMockApi = builder.Configuration.GetValue("UseMockApi", true);
if (useMockApi)
{
    builder.Services.AddSingleton<IGameApiClient, MockGameApiClient>();
}
else
{
    builder.Services.AddScoped<IGameApiClient, HttpGameApiClient>();
}

await builder.Build().RunAsync();
```

- [ ] **Step 3: Add the config flag**

`BattleShip.App/wwwroot/appsettings.json`:

```json
{
  "ApiBaseUrl": "http://localhost:8080/",
  "UseMockApi": true
}
```

- [ ] **Step 4: Build**

Run: `./scripts/dotnet.sh build`
Expected: Build succeeded, no errors.

- [ ] **Step 5: Commit**

```bash
git add BattleShip.App/Services/HttpGameApiClient.cs BattleShip.App/Program.cs \
  BattleShip.App/wwwroot/appsettings.json
git commit -m "$(cat <<'EOF'
feat(app): add HTTP game API client with mock/real DI switch

HttpGameApiClient implements IGameApiClient against the real
swagger.yaml routes, parsing problem+json errors into
GameApiException. UseMockApi in appsettings.json (default true)
picks Mock vs Http in Program.cs — flip it once BattleShip.API
implements the real endpoints.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: `GameSession` state service

**Files:**
- Create: `BattleShip.App/Services/GameSession.cs`
- Modify: `BattleShip.App/Program.cs`

**Interfaces:**
- Consumes: `IGameApiClient` (Task 3/4), all DTOs (Task 2).
- Produces: `class GameSession(IGameApiClient client)` with:
  - `GameDto? Game`, `BoardDto? PlayerBoard`, `BoardDto? OpponentBoard`, `string? AlertMessage`, `bool IsBusy`, `IReadOnlyList<ShotResultDto> History`
  - `event Action? Changed`
  - `Task StartGameAsync(int boardSize, Difficulty difficulty)`
  - `Task LoadGameAsync(Guid id)`
  - `Task FireShotAsync(int x, int y)`

  These exact members are what `Briefing.razor` (Task 7) and `Battle.razor`/`StatusConsole`/`ShotLog` (Task 8) bind to.

- [ ] **Step 1: Implement the session**

`BattleShip.App/Services/GameSession.cs`:

```csharp
using BattleShip.Models.Contracts;

namespace BattleShip.App.Services;

public sealed class GameSession(IGameApiClient client)
{
    private readonly List<ShotResultDto> _history = [];

    public GameDto? Game { get; private set; }
    public BoardDto? PlayerBoard { get; private set; }
    public BoardDto? OpponentBoard { get; private set; }
    public string? AlertMessage { get; private set; }
    public bool IsBusy { get; private set; }
    public IReadOnlyList<ShotResultDto> History => _history;

    public event Action? Changed;

    public Task StartGameAsync(int boardSize, Difficulty difficulty) => RunAsync(async () =>
    {
        _history.Clear();
        Game = await client.CreateGameAsync(new CreateGameRequest(boardSize, difficulty));
        await RefreshBoardsAsync();
    });

    public Task LoadGameAsync(Guid id) => RunAsync(async () =>
    {
        Game = await client.GetGameAsync(id);
        await RefreshBoardsAsync();
    });

    public Task FireShotAsync(int x, int y) => RunAsync(async () =>
    {
        if (Game is null)
        {
            return;
        }

        var result = await client.FireShotAsync(Game.Id, new ShotRequest(x, y));
        _history.Add(result);
        Game = Game with { Status = result.Status };
        await RefreshBoardsAsync();
    });

    private async Task RefreshBoardsAsync()
    {
        if (Game is null)
        {
            return;
        }

        PlayerBoard = await client.GetPlayerBoardAsync(Game.Id);
        OpponentBoard = await client.GetOpponentBoardAsync(Game.Id);
    }

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        AlertMessage = null;
        Changed?.Invoke();
        try
        {
            await action();
        }
        catch (GameApiException ex)
        {
            AlertMessage = ex.Problem.Detail ?? ex.Problem.Title ?? "Une erreur inattendue est survenue.";
        }
        finally
        {
            IsBusy = false;
            Changed?.Invoke();
        }
    }
}
```

- [ ] **Step 2: Register it in `Program.cs`**

In `BattleShip.App/Program.cs`, add this line right after the `IGameApiClient` if/else block (before `await builder.Build().RunAsync();`):

```csharp
builder.Services.AddScoped<GameSession>();
```

- [ ] **Step 3: Build**

Run: `./scripts/dotnet.sh build`
Expected: Build succeeded, no errors.

- [ ] **Step 4: Commit**

```bash
git add BattleShip.App/Services/GameSession.cs BattleShip.App/Program.cs
git commit -m "$(cat <<'EOF'
feat(app): add GameSession state service

Scoped service wrapping IGameApiClient: holds the current game and
both boards, tracks shot history, and turns GameApiException into a
UI-facing AlertMessage. Pages will bind to this instead of calling
IGameApiClient directly.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: `RadarGrid` and `AlertBanner` shared components

**Files:**
- Create: `BattleShip.App/Shared/RadarGrid.razor`
- Create: `BattleShip.App/Shared/RadarGrid.razor.css`
- Create: `BattleShip.App/Shared/AlertBanner.razor`
- Modify: `BattleShip.App/_Imports.razor`

**Interfaces:**
- Consumes: `BoardDto`, `CellDto`, `BoardOwner`, `VisibleCellState` (Task 2).
- Produces:
  - `<RadarGrid Board="BoardDto" Interactive="bool" Title="string" OnCellClick="EventCallback<(int X, int Y)>">` — used by `Battle.razor` (Task 8).
  - `<AlertBanner Message="string?">` — used by `Briefing.razor` (Task 7) and `Battle.razor` (Task 8).

- [ ] **Step 1: Add the `Shared` namespace to global usings**

Append to `BattleShip.App/_Imports.razor`:

```razor
@using BattleShip.App.Shared
```

- [ ] **Step 2: Create the radar grid component**

`BattleShip.App/Shared/RadarGrid.razor`:

```razor
<section class="radar-panel radar-grid-panel">
    <header class="hud-label radar-grid-panel__title">@Title</header>
    <div class="radar-grid" style="--grid-size: @Board.Size">
        @if (Board.Owner == BoardOwner.Opponent)
        {
            <div class="radar-sweep" aria-hidden="true"></div>
            <div class="radar-scanlines" aria-hidden="true"></div>
        }
        @foreach (var cell in Board.Cells)
        {
            <button type="button"
                    class="radar-cell @CellClass(cell.State)"
                    disabled="@(!Interactive || !IsTargetable(cell.State))"
                    @onclick="() => HandleClickAsync(cell)"
                    aria-label="@($"{(char)('A' + cell.X)}{cell.Y + 1}")">
                @if (cell.State is VisibleCellState.Hit or VisibleCellState.Sunk)
                {
                    <span class="radar-cell__ping" aria-hidden="true"></span>
                }
            </button>
        }
    </div>
</section>

@code {
    [Parameter, EditorRequired] public required BoardDto Board { get; set; }
    [Parameter] public bool Interactive { get; set; }
    [Parameter] public EventCallback<(int X, int Y)> OnCellClick { get; set; }
    [Parameter] public string Title { get; set; } = "";

    private static string CellClass(VisibleCellState state) => state switch
    {
        VisibleCellState.Unknown => "radar-cell--unknown",
        VisibleCellState.Empty => "radar-cell--empty",
        VisibleCellState.Ship => "radar-cell--ship",
        VisibleCellState.Miss => "radar-cell--miss",
        VisibleCellState.Hit => "radar-cell--hit",
        VisibleCellState.Sunk => "radar-cell--sunk",
        _ => "",
    };

    private static bool IsTargetable(VisibleCellState state) => state == VisibleCellState.Unknown;

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

- [ ] **Step 3: Style the grid, including the sweep/scanline/sonar-ping effects**

`BattleShip.App/Shared/RadarGrid.razor.css`:

```css
.radar-grid-panel {
    display: flex;
    flex-direction: column;
    gap: 0.6rem;
}

.radar-grid-panel__title {
    text-align: center;
}

.radar-grid {
    position: relative;
    display: grid;
    grid-template-columns: repeat(var(--grid-size), 1fr);
    gap: 2px;
    aspect-ratio: 1;
    background: #0a0b07;
    border: 1px solid var(--br-olive);
    padding: 4px;
    overflow: hidden;
}

.radar-cell {
    position: relative;
    aspect-ratio: 1;
    border: none;
    border-radius: 2px;
    padding: 0;
    cursor: pointer;
    background: var(--br-panel);
}

.radar-cell:disabled {
    cursor: default;
}

.radar-cell--unknown {
    background: var(--br-panel);
}

.radar-cell--empty {
    background: #0f1a12;
}

.radar-cell--ship {
    background: var(--br-olive-light);
}

.radar-cell--miss {
    background: #0f1a12;
}

.radar-cell--miss::before {
    content: "•";
    position: absolute;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    color: var(--br-text-dim);
}

.radar-cell--hit,
.radar-cell--sunk {
    background: var(--br-alert);
}

.radar-cell__ping {
    position: absolute;
    inset: 15%;
    border-radius: 50%;
    border: 2px solid var(--br-alert);
}

.radar-sweep {
    position: absolute;
    inset: 0;
    pointer-events: none;
    background: conic-gradient(from 0deg, var(--br-radar-dim), transparent 35%);
}

@media (prefers-reduced-motion: no-preference) {
    .radar-sweep {
        animation: radar-sweep 4s linear infinite;
    }

    .radar-cell__ping {
        animation: sonar-ping 1.1s ease-out infinite;
    }

    @keyframes radar-sweep {
        to {
            transform: rotate(360deg);
        }
    }

    @keyframes sonar-ping {
        from {
            transform: scale(0.6);
            opacity: 1;
        }
        to {
            transform: scale(1.6);
            opacity: 0;
        }
    }
}

.radar-scanlines {
    position: absolute;
    inset: 0;
    pointer-events: none;
    background: repeating-linear-gradient(
        0deg,
        rgba(57, 255, 106, 0.05) 0px,
        rgba(57, 255, 106, 0.05) 1px,
        transparent 1px,
        transparent 3px
    );
}
```

- [ ] **Step 4: Create the alert banner component**

`BattleShip.App/Shared/AlertBanner.razor`:

```razor
@if (!string.IsNullOrWhiteSpace(Message))
{
    <div class="alert-banner" role="alert">
        <span class="alert-banner__icon" aria-hidden="true">⚠</span>
        <span class="alert-banner__text">@Message</span>
    </div>
}

@code {
    [Parameter] public string? Message { get; set; }
}
```

- [ ] **Step 5: Build**

Run: `./scripts/dotnet.sh build`
Expected: Build succeeded, no errors.

- [ ] **Step 6: Commit**

```bash
git add BattleShip.App/Shared BattleShip.App/_Imports.razor
git commit -m "$(cat <<'EOF'
feat(app): add RadarGrid and AlertBanner components

RadarGrid renders a BoardDto as a clickable grid, with a rotating
radar sweep, CRT scanlines, and a sonar-ping pulse on hit/sunk cells
for the opponent board (motion-reduced when prefers-reduced-motion is
set). AlertBanner renders GameSession's error messages.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 7: `Briefing.razor` — start screen

**Files:**
- Delete: `BattleShip.App/Pages/Home.razor`
- Create: `BattleShip.App/Pages/Briefing.razor`
- Create: `BattleShip.App/Pages/Briefing.razor.css`

**Interfaces:**
- Consumes: `GameSession.StartGameAsync(int, Difficulty)`, `GameSession.Game`, `GameSession.AlertMessage`, `GameSession.IsBusy` (Task 5); `<AlertBanner>` (Task 6); `Difficulty` (Task 2).
- Produces: route `"/"`.

- [ ] **Step 1: Remove the placeholder home page**

```bash
rm BattleShip.App/Pages/Home.razor
```

- [ ] **Step 2: Create the briefing screen**

`BattleShip.App/Pages/Briefing.razor`:

```razor
@page "/"
@inject GameSession Session
@inject NavigationManager Navigation

<PageTitle>BattleShip — Briefing</PageTitle>

<section class="radar-panel briefing">
    <h1>Operation Battleship</h1>
    <p class="hud-label">Etat-major — preparez le deploiement de la flotte</p>

    <div class="briefing__field">
        <label class="hud-label" for="board-size">Taille de la grille</label>
        <input id="board-size" type="range" min="5" max="20" step="1" @bind="boardSize" @bind:event="oninput" />
        <span class="hud-label">@boardSize x @boardSize</span>
    </div>

    <div class="briefing__field">
        <span class="hud-label">Difficulte de l'adversaire</span>
        <div class="briefing__difficulty">
            @foreach (var option in DifficultyOptions)
            {
                <button type="button"
                        class="btn-brass @(difficulty == option ? "btn-brass--active" : "")"
                        @onclick="() => difficulty = option">
                    @option
                </button>
            }
        </div>
    </div>

    <AlertBanner Message="@Session.AlertMessage" />

    <button type="button" class="btn-brass btn-brass--deploy" disabled="@Session.IsBusy" @onclick="DeployAsync">
        @(Session.IsBusy ? "Deploiement en cours..." : "Deployer la flotte")
    </button>
</section>

@code {
    private static readonly Difficulty[] DifficultyOptions =
    [
        Difficulty.Easy, Difficulty.Normal, Difficulty.Hard,
    ];

    private int boardSize = 10;
    private Difficulty difficulty = Difficulty.Normal;

    private async Task DeployAsync()
    {
        await Session.StartGameAsync(boardSize, difficulty);
        if (Session.Game is not null)
        {
            Navigation.NavigateTo($"/battle/{Session.Game.Id}");
        }
    }
}
```

- [ ] **Step 3: Style it**

`BattleShip.App/Pages/Briefing.razor.css`:

```css
.briefing {
    max-width: 32rem;
    margin: 2rem auto;
    display: flex;
    flex-direction: column;
    gap: 1.25rem;
}

.briefing__field {
    display: flex;
    flex-direction: column;
    gap: 0.4rem;
}

.briefing__field input[type="range"] {
    accent-color: var(--br-radar);
}

.briefing__difficulty {
    display: flex;
    gap: 0.6rem;
}

.btn-brass--deploy {
    align-self: flex-start;
    font-size: 1rem;
    padding: 0.8rem 1.5rem;
}
```

- [ ] **Step 4: Build**

Run: `./scripts/dotnet.sh build`
Expected: Build succeeded, no errors.

- [ ] **Step 5: Manual check**

Run: `docker compose up --build` then open `http://localhost:8081`.
Expected: the briefing screen renders (board-size slider, three difficulty buttons, "Deployer la flotte"); clicking deploy navigates to `/battle/{guid}` (a 404/blank page is expected here — `Battle.razor` doesn't exist until Task 8). Stop with `docker compose down`.

- [ ] **Step 6: Commit**

```bash
git add BattleShip.App/Pages/Briefing.razor BattleShip.App/Pages/Briefing.razor.css \
  BattleShip.App/Pages/Home.razor
git commit -m "$(cat <<'EOF'
feat(app): add briefing screen to deploy a new game

Briefing.razor (route "/") replaces the placeholder Home page: board
size and difficulty selection, wired to GameSession.StartGameAsync,
navigates to /battle/{id} on success.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 8: `Battle.razor` + `StatusConsole` + `ShotLog`

**Files:**
- Create: `BattleShip.App/Shared/StatusConsole.razor`
- Create: `BattleShip.App/Shared/StatusConsole.razor.css`
- Create: `BattleShip.App/Shared/ShotLog.razor`
- Create: `BattleShip.App/Shared/ShotLog.razor.css`
- Create: `BattleShip.App/Pages/Battle.razor`
- Create: `BattleShip.App/Pages/Battle.razor.css`

**Interfaces:**
- Consumes: `GameSession` (Task 5) in full — `Game`, `PlayerBoard`, `OpponentBoard`, `History`, `AlertMessage`, `IsBusy`, `Changed`, `LoadGameAsync`, `FireShotAsync`; `<RadarGrid>`/`<AlertBanner>` (Task 6); `GameDto`, `GameStatus`, `Player`, `ShotResultDto`, `ShotOutcomeDto`, `ShotOutcome` (Task 2).
- Produces: route `"/battle/{GameId:guid}"`; `<StatusConsole Game="GameDto">`; `<ShotLog History="IReadOnlyList<ShotResultDto>">`.

- [ ] **Step 1: Create the status console**

`BattleShip.App/Shared/StatusConsole.razor`:

```razor
<div class="status-console radar-panel">
    <div class="status-console__item">
        <span class="hud-label">Statut</span>
        <strong>@StatusLabel(Game.Status)</strong>
    </div>
    <div class="status-console__item">
        <span class="hud-label">Tour</span>
        <strong>
            @(Game.CurrentTurn switch
            {
                Player.Player => "JOUEUR",
                Player.Computer => "ORDINATEUR",
                _ => "—",
            })
        </strong>
    </div>
    <div class="status-console__item">
        <span class="hud-label">Grille</span>
        <strong>@Game.BoardSize x @Game.BoardSize</strong>
    </div>
    <div class="status-console__item">
        <span class="hud-label">Tirs joues</span>
        <strong>@Game.ShotCount</strong>
    </div>
</div>

@code {
    [Parameter, EditorRequired] public required GameDto Game { get; set; }

    private static string StatusLabel(GameStatus status) => status switch
    {
        GameStatus.Waiting => "EN ATTENTE",
        GameStatus.PlayerTurn => "A VOUS DE JOUER",
        GameStatus.ComputerTurn => "L'ADVERSAIRE VISE",
        GameStatus.PlayerWon => "VICTOIRE",
        GameStatus.ComputerWon => "DEFAITE",
        _ => status.ToString(),
    };
}
```

`BattleShip.App/Shared/StatusConsole.razor.css`:

```css
.status-console {
    display: flex;
    flex-wrap: wrap;
    gap: 1.5rem;
    margin-bottom: 1.25rem;
}

.status-console__item {
    display: flex;
    flex-direction: column;
    gap: 0.2rem;
    font-family: var(--font-hud);
}

.status-console__item strong {
    color: var(--br-brass);
    font-size: 1.05rem;
    letter-spacing: 0.04em;
}
```

- [ ] **Step 2: Create the shot log**

`BattleShip.App/Shared/ShotLog.razor`:

```razor
<div class="shot-log radar-panel">
    <header class="hud-label">Journal des tirs</header>
    <ol class="shot-log__list">
        @foreach (var entry in History)
        {
            <li class="shot-log__entry">
                <span class="shot-log__side">VOUS</span>
                @FormatOutcome(entry.PlayerShot)
                @if (entry.ComputerShot is { } computerShot)
                {
                    <br />
                    <span class="shot-log__side shot-log__side--enemy">ENNEMI</span>
                    @FormatOutcome(computerShot)
                }
            </li>
        }
    </ol>
</div>

@code {
    [Parameter, EditorRequired] public required IReadOnlyList<ShotResultDto> History { get; set; }

    private static string FormatOutcome(ShotOutcomeDto outcome)
    {
        var coord = $"{(char)('A' + outcome.X)}{outcome.Y + 1}";
        return outcome.Outcome switch
        {
            ShotOutcome.Miss => $"{coord} — manque",
            ShotOutcome.Hit => $"{coord} — touche",
            ShotOutcome.Sunk => $"{coord} — coule ({outcome.SunkShipName})",
            _ => coord,
        };
    }
}
```

`BattleShip.App/Shared/ShotLog.razor.css`:

```css
.shot-log__list {
    list-style: none;
    margin: 0.75rem 0 0 0;
    padding: 0;
    max-height: 16rem;
    overflow-y: auto;
    display: flex;
    flex-direction: column-reverse;
    gap: 0.5rem;
}

.shot-log__entry {
    font-family: var(--font-hud);
    font-size: 0.85rem;
    border-left: 2px solid var(--br-olive-light);
    padding-left: 0.6rem;
}

.shot-log__side {
    color: var(--br-radar);
    font-weight: bold;
    margin-right: 0.4rem;
}

.shot-log__side--enemy {
    color: var(--br-alert);
}

@media (prefers-reduced-motion: no-preference) {
    .shot-log__entry {
        animation: shot-log-in 0.2s ease-out;
    }

    @keyframes shot-log-in {
        from {
            opacity: 0;
            transform: translateX(-6px);
        }
        to {
            opacity: 1;
            transform: translateX(0);
        }
    }
}
```

Note: `History` is appended oldest-first; `flex-direction: column-reverse` puts the newest entry (last DOM child) visually on top without needing to reverse the list in C#.

- [ ] **Step 3: Create the battle screen**

`BattleShip.App/Pages/Battle.razor`:

```razor
@page "/battle/{GameId:guid}"
@implements IDisposable
@inject GameSession Session
@inject NavigationManager Navigation

<PageTitle>BattleShip — Battle</PageTitle>

@if (Session.Game is null)
{
    <p class="hud-label">Chargement de la partie…</p>
}
else
{
    <StatusConsole Game="Session.Game" />

    <AlertBanner Message="@Session.AlertMessage" />

    <div class="battle__grids">
        @if (Session.PlayerBoard is not null)
        {
            <RadarGrid Board="Session.PlayerBoard" Interactive="false" Title="VOTRE FLOTTE" />
        }
        @if (Session.OpponentBoard is not null)
        {
            <RadarGrid Board="Session.OpponentBoard"
                       Interactive="CanFire"
                       Title="EAUX ENNEMIES"
                       OnCellClick="HandleFireAsync" />
        }
    </div>

    <ShotLog History="Session.History" />

    @if (Session.Game.Status is GameStatus.PlayerWon or GameStatus.ComputerWon)
    {
        <div class="battle__overlay">
            <div class="radar-panel battle__overlay-panel">
                <h2>@(Session.Game.Status == GameStatus.PlayerWon ? "VICTOIRE" : "DEFAITE")</h2>
                <p class="hud-label">
                    @(Session.Game.Status == GameStatus.PlayerWon
                        ? "La flotte ennemie a ete coulee."
                        : "Votre flotte a ete coulee.")
                </p>
                <button type="button" class="btn-brass" @onclick="@(() => Navigation.NavigateTo("/"))">
                    Nouveau deploiement
                </button>
            </div>
        </div>
    }
}

@code {
    [Parameter] public Guid GameId { get; set; }

    private bool CanFire => !Session.IsBusy && Session.Game?.Status == GameStatus.PlayerTurn;

    protected override async Task OnInitializedAsync()
    {
        Session.Changed += StateHasChanged;
        if (Session.Game?.Id != GameId)
        {
            await Session.LoadGameAsync(GameId);
        }
    }

    private async Task HandleFireAsync((int X, int Y) target)
    {
        await Session.FireShotAsync(target.X, target.Y);
    }

    public void Dispose()
    {
        Session.Changed -= StateHasChanged;
    }
}
```

- [ ] **Step 4: Style the battle screen**

`BattleShip.App/Pages/Battle.razor.css`:

```css
.battle__grids {
    display: grid;
    grid-template-columns: 1fr;
    gap: 1.5rem;
    margin: 1.25rem 0;
}

@media (min-width: 900px) {
    .battle__grids {
        grid-template-columns: 1fr 1fr;
    }
}

.battle__overlay {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.75);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 10;
}

.battle__overlay-panel {
    text-align: center;
    max-width: 24rem;
}

.battle__overlay-panel h2 {
    font-size: 2rem;
}

@media (prefers-reduced-motion: no-preference) {
    .battle__overlay {
        animation: overlay-in 0.3s ease-out;
    }

    @keyframes overlay-in {
        from {
            opacity: 0;
        }
        to {
            opacity: 1;
        }
    }
}
```

- [ ] **Step 5: Build**

Run: `./scripts/dotnet.sh build`
Expected: Build succeeded, no errors.

- [ ] **Step 6: Manual milestone verification**

Run: `docker compose up --build`, open `http://localhost:8081`.
Check, per the spec's verification checklist:
- Deploy a fleet from the briefing screen and land on `/battle/{id}` with both grids, status console, and shot log rendered.
- Fire shots on "EAUX ENNEMIES" until the game ends; confirm the victory/defeat overlay appears and "Nouveau deploiement" returns to `/`.
- Click an already-targeted cell (or refire fast) and confirm a 409 raises the red `AlertBanner` with a readable French message.
- The opponent board never shows a `Ship`-colored cell for an un-hit ship (only after Task 9's ship glyph exists on the *player* board, which is expected to differ).
- Resize the browser to ~360px width and confirm no horizontal scroll and both grids remain usable stacked.

Stop with `docker compose down`.

- [ ] **Step 7: Commit**

```bash
git add BattleShip.App/Shared/StatusConsole.razor BattleShip.App/Shared/StatusConsole.razor.css \
  BattleShip.App/Shared/ShotLog.razor BattleShip.App/Shared/ShotLog.razor.css \
  BattleShip.App/Pages/Battle.razor BattleShip.App/Pages/Battle.razor.css
git commit -m "$(cat <<'EOF'
feat(app): add battle screen with live radar grids and shot log

Battle.razor (route /battle/{id}) wires GameSession to two RadarGrid
instances, StatusConsole, ShotLog, and a victory/defeat overlay,
completing the end-to-end playable loop.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 9: Final polish + full manual QA pass

**Files:**
- Modify: `BattleShip.App/Shared/RadarGrid.razor.css`

**Interfaces:**
- Consumes: everything from Tasks 1–8. No new public members produced (visual-only + verification task).

- [ ] **Step 1: Give player-fleet cells a distinct glyph**

The `swagger.yaml` `BoardDto`/`CellDto` contract only carries a per-cell `VisibleCellState`, not ship groupings — so a true multi-cell hull silhouette isn't derivable from the DTO. Add a simple per-cell glyph instead, in `BattleShip.App/Shared/RadarGrid.razor.css`, right after the existing `.radar-cell--ship { ... }` rule:

```css
.radar-cell--ship::before {
    content: "▲";
    position: absolute;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 0.7em;
    color: var(--br-bg);
}
```

- [ ] **Step 2: Build**

Run: `./scripts/dotnet.sh build`
Expected: Build succeeded, no errors.

- [ ] **Step 3: Full manual QA pass**

Run: `docker compose up --build`, open `http://localhost:8081`, and walk the whole spec verification checklist end to end:

```bash
docker compose up --build
# http://localhost:8081 — create a game, fire shots until win/loss, confirm:
#  - opponent board never shows an un-hit Ship cell
#  - player board ship cells show the ▲ glyph
#  - 409 on re-clicking an already-targeted cell shows in AlertBanner
#  - radar sweep / scanlines / sonar-ping render on the opponent grid
#  - responsive layout holds at ~360px width, no horizontal scroll
#  - the browser's reduced-motion setting (OS accessibility setting) removes
#    the sweep/ping/overlay/shot-log animations without breaking layout
docker compose down
```

Fix anything that fails before committing (the fix belongs in whichever file governs the broken behavior — this step's only *planned* file change is the CSS in Step 1).

- [ ] **Step 4: Commit**

```bash
git add BattleShip.App/Shared/RadarGrid.razor.css
git commit -m "$(cat <<'EOF'
feat(app): polish radar animations and finish manual QA pass

Add a per-cell ship glyph to the player board and complete the full
manual verification pass from the design spec (visibility rule,
error handling, animations, responsive layout, reduced motion).

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Self-Review Notes

- **Spec coverage:** every section of `docs/superpowers/specs/2026-09-15-radar-frontend-design.md` maps to a task — contracts (Task 2), service layer + DI switch (Tasks 3–5), theme (Task 1), components (Tasks 6, 8), pages (Tasks 7–8), error handling (Task 5), commit plan (all tasks), testing/verification (Tasks 8–9).
- **Deviations from the spec, disclosed:** `AlertBanner` was moved from the spec's Task 8 into Task 6, since `Briefing.razor` (Task 7) needs it before Task 8 exists — this is a pure reordering, not a scope change. The spec's loose "ship silhouette" language is narrowed to a per-cell glyph in Task 9, since `BoardDto`/`CellDto` (matching `swagger.yaml`) carry no ship-shape data to draw a real hull from.
- **Type consistency:** `GameSession.FireShotAsync(int x, int y)` ↔ `Battle.razor`'s `HandleFireAsync((int X, int Y) target)` ↔ `RadarGrid`'s `EventCallback<(int X, int Y)> OnCellClick` all agree. `GameSession.History` (`IReadOnlyList<ShotResultDto>`) matches `ShotLog.History`. `GameDto`/`BoardDto`/`CellDto` field names are identical across Tasks 2–8 — verified by re-reading each task's code blocks against Task 2's definitions.
