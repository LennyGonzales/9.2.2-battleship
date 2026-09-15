# BattleShip.App — Radar/WW2 Front-End Design

## Status and date

Approved — 2026-09-15

## Context

`BattleShip.App` (Blazor WebAssembly) currently contains only the default `dotnet new` template
(`Home`/`Counter`/`Weather` pages). `BattleShip.API` also still only exposes the default
weather-forecast endpoint — the real game endpoints described in `swagger.yaml` and
`PROMPT-INIT.md` do not exist yet.

Scope of this work is **front-end only**. The goal is a fully playable, visually themed
("armée / radar / bateau / WW2") Battleship UI that:

- Implements the real HTTP contract from `swagger.yaml` exactly (DTOs, routes, status codes),
  so it is ready to talk to the real API the moment it exists.
- Is playable *today*, before the backend is implemented, via a swappable in-memory mock behind
  the same interface.
- Fully replaces the default template pages/nav.

Backend implementation (`BattleShip.API` real endpoints, FluentValidation, gRPC-Web) is explicitly
out of scope for this work.

## Contracts (source of truth: `swagger.yaml`)

DTOs live in `BattleShip.Models/Contracts` (new folder) as C# records, shared by both the mock and
HTTP implementations so swapping one for the other requires zero UI changes.

```csharp
public enum GameStatus { Waiting, PlayerTurn, ComputerTurn, PlayerWon, ComputerWon }
public enum Player { Player, Computer }
public enum ShotOutcome { Miss, Hit, Sunk }
public enum VisibleCellState { Unknown, Empty, Ship, Miss, Hit, Sunk }
public enum BoardOwner { Player, Opponent }
public enum Difficulty { Easy, Normal, Hard }

public record CreateGameRequest(int? BoardSize, Difficulty? Difficulty);
public record GameDto(Guid Id, GameStatus Status, Player? CurrentTurn, int BoardSize, int ShotCount, DateTimeOffset CreatedAt);
public record CellDto(int X, int Y, VisibleCellState State);
public record BoardDto(BoardOwner Owner, int Size, IReadOnlyList<CellDto> Cells);
public record ShotRequest(int X, int Y);
public record ShotOutcomeDto(int X, int Y, ShotOutcome Outcome, string? SunkShipName);
public record ShotResultDto(ShotOutcomeDto PlayerShot, ShotOutcomeDto? ComputerShot, GameStatus Status);
public record ProblemDetailsDto(string? Type, string? Title, int? Status, string? Detail, string? Instance);
public record ValidationProblemDetailsDto(string? Type, string? Title, int? Status, string? Detail,
    IReadOnlyDictionary<string, string[]>? Errors);
```

`GameApiException(ProblemDetailsDto Problem, int StatusCode)` — thrown by both client
implementations on 400/404/409, caught by `GameSession` and turned into an `AlertBanner` message.

## Service layer

```
BattleShip.App/Services/
  IGameApiClient.cs
  HttpGameApiClient.cs
  MockGameApiClient.cs
  GameSession.cs
```

```csharp
public interface IGameApiClient
{
    Task<GameDto> CreateGameAsync(CreateGameRequest request, CancellationToken ct = default);
    Task<GameDto> GetGameAsync(Guid id, CancellationToken ct = default);
    Task<BoardDto> GetPlayerBoardAsync(Guid id, CancellationToken ct = default);
    Task<BoardDto> GetOpponentBoardAsync(Guid id, CancellationToken ct = default);
    Task<ShotResultDto> FireShotAsync(Guid id, ShotRequest shot, CancellationToken ct = default);
}
```

**`HttpGameApiClient`** — injected `HttpClient` (base address already configured in
`Program.cs` from `ApiBaseUrl`). Maps 1:1 to the five `swagger.yaml` operations
(`createGame`, `getGame`, `getPlayerBoard`, `getOpponentBoard`, `fireShot`). On non-2xx,
deserializes `application/problem+json` into `ProblemDetailsDto`/`ValidationProblemDetailsDto`
and throws `GameApiException`.

**`MockGameApiClient`** — in-memory `Dictionary<Guid, MockGame>`:
- `CreateGameAsync`: builds a square board (`boardSize` clamped 5–20, default 10), places the
  classic fleet (Carrier 5, Cruiser 4, Destroyer 3, Submarine 3, Torpedo boat 2) at random
  non-overlapping horizontal/vertical positions for both player and computer, status `PlayerTurn`.
- `FireShotAsync`: validates coordinates in range and not already targeted (else throws
  `GameApiException` mapped to 400/409 exactly like the real API would); resolves player shot,
  then — if the game continues — an easy computer opponent (`Easy`/`Normal`: random untried cell,
  `Hard`: hunts adjacent cells after a hit) fires back; updates `GameStatus`
  (`PlayerWon`/`ComputerWon` when a fleet is fully sunk).
- Board reads apply the same visibility masking as the real contract: opponent board never
  exposes `Ship` state, only `Unknown`/`Miss`/`Hit`/`Sunk`.

**DI switch** in `Program.cs`:
```csharp
var useMock = builder.Configuration.GetValue("UseMockApi", true);
if (useMock)
    builder.Services.AddSingleton<IGameApiClient, MockGameApiClient>();
else
    builder.Services.AddScoped<IGameApiClient, HttpGameApiClient>();
```
`wwwroot/appsettings.json` gets `"UseMockApi": true` as the default so `docker compose up` is
playable immediately; flipping to `false` (or overriding via the Docker-injected config) is the
only change needed once the real API exists.

**`GameSession`** (scoped) — holds current `GameDto` + `PlayerBoard`/`OpponentBoard`, exposes
`event Action? Changed`, wraps every `IGameApiClient` call, catches `GameApiException` and
surfaces it as a `string? AlertMessage` for `AlertBanner` to render, clears it on the next
successful action.

## Visual theme — "War Room / Radar"

CSS custom properties in `wwwroot/css/theme.css`, loaded once:

```css
:root {
  --br-bg: #12140f;
  --br-panel: #1c1f16;
  --br-olive: #3a4025;
  --br-brass: #c9a24b;
  --br-radar: #39ff6a;
  --br-alert: #d1453b;
  --font-display: "Black Ops One", system-ui, sans-serif;
  --font-hud: "Share Tech Mono", ui-monospace, monospace;
}
```

Fonts loaded via `<link>` to `fonts.googleapis.com` in `wwwroot/index.html` (allowed external
stylesheet host), with a system fallback stack per the artifact/host font rules — not blocking if
offline.

- Opponent `RadarGrid` gets a rotating `conic-gradient` sweep (`@keyframes radar-sweep`,
  `animation: radar-sweep 4s linear infinite`, pure CSS, `prefers-reduced-motion` respected) and a
  repeating-linear-gradient scanline overlay at low opacity.
- Hit cells get a `sonar-ping` keyframe: an expanding, fading ring (`box-shadow`/`::after`
  pseudo-element), red for hit, brass for sunk.
- Player board ship cells render simple CSS/SVG silhouettes (no image assets) — a hull shape
  scaled by ship length, not per-ship art.
- Status bar styled as a dispatch console (dark panel, brass borders, HUD font for turn/status/shot
  count); shot log styled as a scrolling teletype panel, newest entry typed in with a brief
  fade/slide-in.
- Layout: CSS grid, two `RadarGrid`s side by side ≥900px, stacked below that; safe-area-aware,
  responsive down to ~360px per existing Blazor CSS isolation conventions.

## Pages & components

```
BattleShip.App/
  Pages/
    Briefing.razor        route "/"
    Battle.razor           route "/battle/{GameId:guid}"
  Shared/
    RadarGrid.razor        + RadarGrid.razor.css
    StatusConsole.razor
    ShotLog.razor
    AlertBanner.razor
  Layout/
    MainLayout.razor       restyled
    NavMenu.razor           restyled, game-only links
```

- **`Briefing.razor`**: board size (5–20, default 10) and difficulty (`Easy`/`Normal`/`Hard`)
  controls, "Deploy Fleet" button → `GameSession.CreateGameAsync` → `NavigationManager.NavigateTo($"/battle/{id}")`.
- **`Battle.razor`**: on init, loads game + both boards via `GameSession`; renders `StatusConsole`,
  two `RadarGrid`s ("YOUR FLEET" = player board, read-only; "ENEMY WATERS" = opponent board,
  clickable cells fire shots through `GameSession.FireShotAsync`), `ShotLog` (newest-first list
  built from each `ShotResultDto`), and a full-screen victory/defeat overlay when `GameStatus` is
  `PlayerWon`/`ComputerWon`.
- **`RadarGrid.razor`**: `[Parameter] BoardDto Board`, `[Parameter] bool Interactive`,
  `[Parameter] EventCallback<(int X,int Y)> OnCellClick`; renders a `Board.Size × Board.Size` CSS
  grid, one `<button>`/`<div>` per `CellDto` styled by `VisibleCellState`; owns the sweep/scanline
  overlay only when `Board.Owner == Opponent`.
- **`AlertBanner.razor`**: renders `GameSession.AlertMessage` as a dismissible radar-alert strip
  (red for 409/400, amber for 404), auto-clears on next successful action.
- `MainLayout`/`NavMenu`: minimal radar-corner-bracket chrome, links to Briefing + (once a game is
  active) Battle; Counter/Weather pages, their routes, and nav entries deleted.

## Error handling

Every `GameSession` method wraps its `IGameApiClient` call in `try/catch (GameApiException)`,
sets `AlertMessage` from `Problem.Detail ?? Problem.Title`, and re-renders via `Changed`. No
silent failures; no exceptions escape into the Blazor render pipeline uncaught.

## Testing / verification

No new automated front-end test project is introduced by this design (out of scope; can be a
follow-up). Verification is manual, through the existing Docker workflow, run after each
functionally-complete commit and definitely before the final commit:

```bash
docker compose up --build
# http://localhost:8081 — create a game, fire shots until win/loss, confirm:
#  - opponent board never shows an un-hit Ship cell
#  - 409 on re-clicking an already-targeted cell shows in AlertBanner
#  - responsive layout holds at ~360px width
```

## Commit plan (atomic, in order)

1. Theme foundation: `theme.css`, Google Fonts link, restyled `MainLayout`/`NavMenu`, delete
   `Counter`/`Weather` pages and their nav entries/routes.
2. Shared contract DTOs in `BattleShip.Models/Contracts` matching `swagger.yaml`.
3. `IGameApiClient` + `MockGameApiClient` (mock fleet placement, shot resolution, computer
   opponent, visibility masking) — game is playable, still using unstyled/basic markup.
4. `HttpGameApiClient` + DI switch (`UseMockApi`) in `Program.cs`/`wwwroot/appsettings.json`.
5. `GameSession` scoped state service + error-to-`AlertMessage` translation.
6. `RadarGrid` component + per-cell state styling.
7. `Briefing.razor` (start screen) wired to `GameSession.CreateGameAsync`.
8. `Battle.razor` + `StatusConsole`/`ShotLog`/`AlertBanner`, full game loop wired end to end.
9. Sweep/scanline/sonar-ping animation polish pass.

Each commit must leave `docker compose --profile tools run --rm sdk dotnet build` green.

## References

- `PROMPT-INIT.md` — project architecture constraints (Models has no dependencies, App talks only
  to the API, configurable `ApiBaseUrl`, etc.)
- `swagger.yaml` — REST contract this design implements against
- `CLAUDE.md` — repo conventions (Docker-only workflow, layering rules)
