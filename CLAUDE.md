# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

School project (binôme, graded on code quality, decisions, and verifications): a Battleship ("Bataille Navale")
game in C# / ASP.NET Core on .NET 10, split into a Minimal API backend and a Blazor WebAssembly frontend.

**Current state**: `BattleShip.App` now has a full, playable front end (Blazor WebAssembly) built against a
client-side mock — see "Front-end mock mode" below. `BattleShip.API` is still unmodified `dotnet new`
template code (the default weather-forecast endpoint) — the real game endpoints, FluentValidation, and
gRPC-Web service described in `PROMPT-INIT.md` are not yet implemented. `BattleShip.Models` now has real
shared contracts under `Models/Contracts/` (mirroring `swagger.yaml`) alongside the still-empty domain
layer. `PROMPT-INIT.md` at the repo root is the authoritative (French) spec for the target architecture,
layering rules, API/DTO contracts, gRPC-Web contract, and required ADRs — read it before adding backend
domain logic. Only `docs/adr/0005-conteneurisation-docker.md` exists so far; ADRs 0001–0004 (layering, game
storage, REST contract, gRPC operation) described in `PROMPT-INIT.md` are still to be written.

`swagger.yaml` at the repo root already documents the target REST contract (routes, DTOs, HTTP codes) ahead of
the implementation — treat it as the contract to implement against, not as documentation of existing behavior.

## Docker-only workflow (hard constraint)

No .NET SDK is installed locally, and none should be assumed. Every `dotnet` command runs through the `sdk`
service in `docker-compose.yml` — never suggest or run `dotnet` directly on the host.

```bash
# Preferred shortcut (wraps `docker compose --profile tools run --rm sdk dotnet "$@"`)
./scripts/dotnet.sh build
./scripts/dotnet.sh test

# Equivalent explicit form, also used for scaffolding (dotnet new, dotnet sln add, etc.)
docker compose --profile tools run --rm sdk dotnet build
docker compose --profile tools run --rm sdk dotnet test
docker compose --profile tools run --rm sdk dotnet --version   # sanity-check the SDK version (10.x)

# Run a single test (standard dotnet test filter syntax)
./scripts/dotnet.sh test --filter "FullyQualifiedName~BattleShip.Tests.SomeClass.SomeMethod"

# Run the full stack
docker compose up --build
```

| Service | URL |
|---|---|
| Blazor front (`app`) | http://localhost:8081 |
| API (`api`) | http://localhost:8080 |
| OpenAPI spec (dev only) | http://localhost:8080/openapi/v1.json |

The NuGet package cache is a named Docker volume (`nuget`), so restores persist across `sdk` container runs.
The repo lives under iCloud Drive — if builds fail intermittently, it's a known bind-mount risk noted in ADR 0005.

## Architecture

Four projects in `BattleShip.slnx`, with a strict, one-directional dependency graph:

- **`BattleShip.Models`** — domain entities, shared interfaces, and DTOs. References **no other project** and
  must stay free of ASP.NET, HTTP, JSON, and gRPC dependencies, so the game engine stays independent of any
  transport concern.
- **`BattleShip.API`** — ASP.NET Core Minimal API. References `Models`. Owns endpoints, FluentValidation
  validators, the gRPC service, and DI/CORS/OpenAPI configuration. `Program.cs` ends with
  `public partial class Program { }` so `BattleShip.Tests` can drive it via `WebApplicationFactory<Program>`.
- **`BattleShip.App`** — Blazor WebAssembly frontend. References `Models`. Holds no game rules; talks to the
  API exclusively over HTTP and gRPC-Web. The API base URL is injected at Docker build time (`ApiBaseUrl` build
  arg → `wwwroot/appsettings.json`), never hardcoded.
  Never `dotnet run`/`dotnet watch` this locally — it's always served through its Docker/nginx image.
- **`BattleShip.Tests`** — xUnit. References `API` and `Models`.

Key contract rules to preserve when implementing the domain (see `PROMPT-INIT.md` for full detail):

- DTOs sent to a player must never reveal an opponent's undiscovered ship positions — masking belongs in the
  API's mapping layer, not the domain model.
- FluentValidation validators are called **explicitly** (`ValidateAsync`) inside endpoints — no implicit/magic
  validation pipeline.
- At least one gRPC-Web exchange must work end-to-end between the Blazor front and the API (contract lives in
  `Protos/battleship.proto` once created), demonstrating both a success and an expected error path.
- .NET 10 has no Swagger UI: API docs are `AddOpenApi()`/`MapOpenApi()` (dev-only) plus the versioned
  `BattleShip.API/BattleShip.API.http` file for manual calls — don't reintroduce Swagger UI.

### Front-end mock mode

`BattleShip.App/Services/IGameApiClient.cs` has two implementations, selected by the `UseMockApi` flag in
`wwwroot/appsettings.json` (default `true`) via a DI switch in `Program.cs`:
- `MockGameApiClient` — a full in-memory Battleship engine (fleet placement, shot resolution, a computer
  opponent) that makes the UI playable today without a real backend. This deliberately holds client-side
  game rules behind the `IGameApiClient` interface — not a `PROMPT-INIT.md` layering violation, since the
  real `BattleShip.API` remains the only server-authoritative implementation once it exists.
- `HttpGameApiClient` — the real implementation, calling the routes in `swagger.yaml`.

Flip `UseMockApi` to `false` once `BattleShip.API`'s real endpoints exist. In Docker, the app's
`wwwroot/appsettings.json` is generated at build time by `BattleShip.App/Dockerfile`'s `ARG UseMockApi`
(default `true`, plumbed through `docker-compose.yml`'s `app.build.args`) — set that build arg, not the
checked-in file, when building the production image.

## Documentation map

- `PROMPT-INIT.md` — full architecture spec/constraints for this project (French); the source of truth for what
  to build next.
- `swagger.yaml` — target REST API contract.
- `docs/adr/` — architecture decision records (gabarit/template is in `PROMPT-INIT.md`).
- `README.md` — Docker workflow, ports, project layout (French).
- `../csharp-school/Ressources Bataille Navale/` (sibling directory, outside this git repo) — course materials:
  `Referentiel.md` is the grading rubric, `CONTEXTE-IA.md` gives project context, `PROMPTS.md`/`REVUE-IA.md` are
  templates this repo's own `PROMPTS.md`/`REVUE-IA.md` deliverables should follow once created.
