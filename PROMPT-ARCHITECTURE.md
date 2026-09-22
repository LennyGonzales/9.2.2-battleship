# Prompt d'initialisation — Bataille Navale (architecture)

> **Note** : document d'initialisation (étape squelette). L'état livré du projet est décrit dans [`CONTEXTE-IA.md`](CONTEXTE-IA.md) et [`README.md`](README.md).

Tu es un architecte logiciel C# / ASP.NET Core. Tu m'aides à initialiser le projet scolaire « Bataille Navale » en binôme (5 jours, évaluation sur la qualité du code, des décisions et des vérifications).

## Environnement Docker uniquement (contrainte forte)

Je ne veux PAS installer ni exécuter le SDK .NET localement sur ma machine. Tout passe par Docker :
- création de la solution (`dotnet new`, `dotnet sln add`, etc.) via un conteneur SDK
- compilation (`dotnet build`) via Docker
- tests (`dotnet test`) via Docker
- lancement de l'API et du front via `docker compose up`

Le README ne doit documenter QUE le workflow Docker. Prérequis unique côté développeur : Docker Desktop (ou Docker Engine + Docker Compose v2).

Fournir un service `sdk` (ou équivalent) dans `docker-compose.yml` pour exécuter les commandes dotnet sans installation locale :

```bash
docker compose run --rm sdk dotnet --version
docker compose run --rm sdk dotnet build
docker compose run --rm sdk dotnet test
docker compose up --build
```

## Périmètre de cette étape (strict)

Cette étape couvre UNIQUEMENT l'architecture et le squelette compilable. Tu ne dois PAS implémenter les règles métier du jeu :
- pas de placement aléatoire des flottes
- pas de résolution des tirs (touché / manqué / coulé)
- pas d'IA adverse
- pas de boucle de jeu complète

Tu fournis : la solution, le découpage en couches, les interfaces, les contrats (DTO, endpoints, .proto), la configuration (DI, CORS, gRPC-Web, OpenAPI), la conteneurisation Docker (Dockerfile, docker-compose), les tests qui compilent, les livrables documentaires et les ADR des choix structurants.

Les implémentations métier restent des stubs (NotImplementedException, retours factices documentés, ou tests marqués Skip avec un message explicite).

## Contraintes non négociables du cours

- .NET 10 stable (LTS), C# 14, exécuté dans l'image `mcr.microsoft.com/dotnet/sdk:10.0`. Copier `global.json` à la racine AVANT toute commande `dotnet new` :
  ```json
  {
    "sdk": {
      "version": "10.0.100",
      "rollForward": "latestFeature",
      "allowPrerelease": false
    }
  }
  ```
  Vérifier avec `docker compose run --rm sdk dotnet --version` (pas de `dotnet` local).

- Quatre projets dans une solution `BattleShip` :
  - `BattleShip.API` — ASP.NET Core Minimal API
  - `BattleShip.App` — Blazor WebAssembly
  - `BattleShip.Models` — bibliothèque de classes (modèles partagés + domaine)
  - `BattleShip.Tests` — xUnit

- Références de projets :
  - `BattleShip.Models` ne référence AUCUN autre projet (le moteur reste indépendant de HTTP, JSON et gRPC)
  - `BattleShip.API` référence `BattleShip.Models`
  - `BattleShip.App` référence `BattleShip.Models`
  - `BattleShip.Tests` référence `BattleShip.API` et `BattleShip.Models`

- FluentValidation sur les entrées serveur, avec appel EXPLICITE à `ValidateAsync` dans les endpoints (pas de validation implicite magique).

- Au moins un échange gRPC-Web fonctionnel entre le front Blazor et l'API, avec une réponse de succès ET une erreur attendue démontrables.

- Règles validées côté serveur ; les positions adverses non découvertes doivent rester secrètes dans les DTO (à prévoir dans les contrats, pas à implémenter maintenant).

- .NET 10 n'embarque plus Swagger UI : utiliser `AddOpenApi()` + `MapOpenApi()` en développement, et un fichier `api.http` versionné pour les essais manuels.

- Livrables documentaires à créer dès cette étape :
  - `README.md` (noms du binôme, prérequis Docker uniquement, commandes build/test/run, ports, structure)
  - `PROMPTS.md` (gabarit pour tracer les échanges décisifs avec l'IA)
  - `docs/adr/` (au moins 3 ADR pour les choix structurants)
  - `REVUE-IA.md` (gabarit pour 3 revues argumentées minimum)

- `.gitignore` .NET à la racine (`dotnet new gitignore`).

- Docker est le SEUL moyen d'exécuter le projet : build, tests et lancement. Aucune commande `dotnet` locale dans le README.

## Arborescence cible

```
BattleShip/
├── global.json
├── .gitignore
├── .dockerignore
├── docker-compose.yml                    # services sdk, api, app (+ test optionnel)
├── BattleShip.slnx
├── README.md
├── PROMPTS.md
├── REVUE-IA.md
├── api.http
├── docs/
│   └── adr/
│       ├── 0001-decoupage-couches.md
│       ├── 0002-stockage-parties.md
│       ├── 0003-contrat-api.md
│       ├── 0004-echange-grpc.md
│       └── 0005-conteneurisation-docker.md
├── Protos/
│   └── battleship.proto
├── BattleShip.Models/
│   ├── Domain/           # entités et règles (interfaces + stubs)
│   ├── Contracts/        # DTO partagés (HTTP + visibilité joueur)
│   └── Services/         # interfaces du moteur (IGameEngine, IGameRepository, etc.)
├── BattleShip.API/
│   ├── Dockerfile        # build multi-étapes, image runtime
│   ├── Program.cs
│   ├── Endpoints/        # Minimal API groupées par responsabilité
│   ├── Services/         # adaptateurs (implémentations des interfaces)
│   ├── Validation/       # validateurs FluentValidation
│   └── Grpc/             # service gRPC
├── BattleShip.App/
│   ├── Dockerfile        # build WASM + nginx pour servir les fichiers statiques
│   ├── nginx.conf        # reverse proxy / fallback SPA si nécessaire
│   ├── Program.cs
│   ├── Pages/            # composants Blazor
│   ├── Services/         # clients HTTP et gRPC
│   └── wwwroot/
└── BattleShip.Tests/
    ├── Domain/           # tests du moteur (Skip tant que non implémenté)
    ├── Api/              # tests d'intégration (WebApplicationFactory)
    └── Validation/       # tests des validateurs
```

## Découpage des responsabilités

### BattleShip.Models (cœur, sans dépendance externe)

Contient :
- Entités du domaine : `Game`, `Board`, `Cell`, `Ship`, `Shot`, `GameStatus`, `CellState`, etc.
- Interfaces : `IGameEngine`, `IGameRepository`, `IComputerOpponent` (stub)
- DTO partagés entre API et App : `GameDto`, `BoardDto`, `ShotRequest`, `ShotResultDto`, `CreateGameRequest`, etc.
- Séparation stricte : les DTO ne révèlent JAMAIS les positions adverses non découvertes (masquage à prévoir dans la couche de mapping, pas dans le domaine)

Ne contient PAS : HttpClient, références ASP.NET, gRPC, FluentValidation.

### BattleShip.API (frontière serveur)

Contient :
- Endpoints Minimal API mappant vers les services
- Implémentations des interfaces (`GameEngine` stub, `InMemoryGameRepository`, etc.)
- Validateurs FluentValidation pour chaque entrée
- Service gRPC héritant de la classe de base générée
- Configuration : DI, CORS, gRPC-Web, OpenAPI
- `public partial class Program { }` en fin de `Program.cs` pour les tests d'intégration

Ne contient PAS : logique de rendu, composants Blazor.

### BattleShip.App (frontière client)

Contient :
- Composants Blazor (pages, grilles, boutons)
- `HttpClient` configuré avec `BaseAddress` pointant vers l'API
- Client gRPC-Web généré depuis `Protos/battleship.proto`
- Gestion des états UI : chargement, succès, erreur, refus de coup

Ne contient PAS : règles métier (tout passe par l'API).

### BattleShip.Tests

Contient :
- Tests unitaires du domaine (marqués `[Fact(Skip = "Moteur non implémenté")]` ou équivalent)
- Tests des validateurs FluentValidation (exécutables dès maintenant)
- Tests d'intégration API avec `WebApplicationFactory<Program>` et `Microsoft.AspNetCore.Mvc.Testing`
- Au moins un test qui vérifie qu'un endpoint retourne 400 sur une entrée invalide

## Contrats à définir (sans implémenter la logique)

### API REST (Minimal API)

Proposer et documenter ces opérations (ajuster si tu as une meilleure alternative, mais justifier dans l'ADR 0003) :

| Méthode | Route | Entrée | Réponse | Erreurs |
|---------|-------|--------|---------|---------|
| POST | `/api/games` | `CreateGameRequest` (optionnel : taille grille, difficulté) | `201` + `GameDto` | `400` validation |
| GET | `/api/games/{id}` | — | `200` + `GameDto` (état visible joueur) | `404` |
| POST | `/api/games/{id}/shots` | `ShotRequest` (x, y) | `200` + `ShotResultDto` | `400` validation, `404`, `409` coup invalide ou partie terminée |
| GET | `/api/games/{id}/board/player` | — | `200` + `BoardDto` (grille joueur) | `404` |
| GET | `/api/games/{id}/board/opponent` | — | `200` + `BoardDto` (grille adverse masquée) | `404` |

Codes HTTP à respecter :
- `200` succès, `201` créé, `204` succès sans contenu
- `400` requête invalide (ValidationProblem FluentValidation)
- `404` ressource introuvable
- `409` conflit avec l'état courant (coup refusé, partie terminée)

### DTO et visibilité

- `GameDto` : id, statut (`Waiting`, `PlayerTurn`, `ComputerTurn`, `PlayerWon`, `ComputerWon`), tour courant, nombre de coups
- `BoardDto` : taille, cellules avec état visible (`Unknown`, `Miss`, `Hit`, `Sunk` pour l'adversaire ; `Empty`, `Ship`, `Hit`, `Sunk` pour le joueur)
- `ShotResultDto` : résultat (`Miss`, `Hit`, `Sunk`), coordonnées, nom du navire si coulé
- Les DTO adverses ne contiennent JAMAIS la position des navires non touchés

### gRPC-Web (au moins un échange)

Choisir une opération pertinente pour gRPC (ex. : récupérer les statistiques d'une partie, vérifier l'état, ou obtenir l'historique des coups). Proposer le contrat dans `Protos/battleship.proto` :

```protobuf
syntax = "proto3";
option csharp_namespace = "BattleShip.Grpc";
package battleship;

service GameStats {
  rpc GetGameStats (GameStatsQuery) returns (GameStatsReply);
}

message GameStatsQuery {
  string game_id = 1;
}

message GameStatsReply {
  string game_id = 1;
  int32 player_shots = 2;
  int32 computer_shots = 3;
  int32 player_hits = 4;
  int32 computer_hits = 5;
  string status = 6;
}
```

Le service gRPC doit :
- Valider l'entrée avec FluentValidation (`GameStatsQueryValidator`)
- Retourner une réponse sur un `game_id` valide
- Lever `RpcException` avec `StatusCode.NotFound` si la partie n'existe pas
- Lever `RpcException` avec `StatusCode.InvalidArgument` si l'entrée est invalide

Justifier le choix de l'opération gRPC dans l'ADR 0004.

## Configuration à mettre en place

### Packages NuGet (vérifier les versions compatibles .NET 10)

API :
- `FluentValidation`
- `Grpc.AspNetCore`
- `Grpc.AspNetCore.Web`

App :
- `Grpc.Net.Client`
- `Grpc.Net.Client.Web`
- `Google.Protobuf`
- `Grpc.Tools` (PrivateAssets="all")

Tests :
- `Microsoft.AspNetCore.Mvc.Testing`
- `FluentValidation` (pour tester les validateurs)

### DI et durées de vie (justifier dans l'ADR 0001)

Proposition de départ :
- `IGameRepository` → `InMemoryGameRepository` : **Singleton** (état partagé en mémoire)
- `IGameEngine` → `GameEngine` : **Scoped** (une instance par requête)
- Validateurs FluentValidation : **Scoped**
- `TimeProvider.System` : **Singleton** (pour tests déterministes ultérieurs)

### CORS

Configurer une politique autorisant l'origine du front Blazor telle qu'exposée par Docker (port mappé dans `docker-compose.yml`, ex. `http://localhost:8081`). Ne pas dépendre de `launchSettings.json` pour le workflow principal.

### gRPC-Web

```csharp
builder.Services.AddGrpc();
// après builder.Build()
app.UseGrpcWeb();
app.MapGrpcService<GameStatsGrpcService>().EnableGrpcWeb();
```

### Blazor HttpClient

```csharp
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) // ou configurer via appsettings
});
```

L'adresse de l'API doit être configurable (appsettings ou variable d'environnement), pas codée en dur.

### OpenAPI (développement uniquement)

```csharp
builder.Services.AddOpenApi();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
```

### HTTPS

Privilégier HTTP en Docker (pas de `dotnet dev-certs` : le SDK n'est pas installé localement). Si HTTPS est requis plus tard, expliquer la stratégie (proxy inverse, certificat) dans l'ADR 0005.

## Docker

### Objectif

Permettre à un autre binôme de travailler sur le projet avec uniquement Docker installé. Le README doit permettre de tout faire sans jamais lancer `dotnet` sur l'hôte :

```bash
# Vérifier l'environnement
docker compose run --rm sdk dotnet --version

# Compiler et tester
docker compose run --rm sdk dotnet build
docker compose run --rm sdk dotnet test

# Lancer l'application
docker compose up --build
```

L'API et le front Blazor doivent démarrer, communiquer (HTTP + gRPC-Web) et rester accessibles depuis le navigateur de l'hôte.

### Fichiers à fournir

- `.dockerignore` à la racine (exclure `bin/`, `obj/`, `.git`, etc.)
- `BattleShip.API/Dockerfile` — build multi-étapes basé sur l'image officielle `mcr.microsoft.com/dotnet/sdk:10.0` puis `mcr.microsoft.com/dotnet/aspnet:10.0`
- `BattleShip.App/Dockerfile` — build WASM (`dotnet publish`) puis image `nginx:alpine` pour servir les fichiers statiques
- `BattleShip.App/nginx.conf` — configuration nginx (fallback `index.html`, headers utiles, proxy optionnel)
- `docker-compose.yml` — service SDK (build/test), API et App

### Service SDK (obligatoire)

Conteneur utilitaire pour toutes les commandes `dotnet` sans installation locale :

```yaml
services:
  sdk:
    image: mcr.microsoft.com/dotnet/sdk:10.0
    working_dir: /src
    volumes:
      - .:/src
    profiles: ["tools"]   # optionnel : n'empêche pas `docker compose up` de lancer api+app
```

Les commandes de scaffolding initial peuvent aussi passer par ce conteneur :

```bash
docker compose run --rm sdk dotnet new sln -n BattleShip
docker compose run --rm sdk dotnet new webapi -n BattleShip.API
# etc.
```

Documenter ces commandes dans le README. Un script `scripts/dotnet.sh` (ou `Makefile`) qui encapsule `docker compose run --rm sdk dotnet "$@"` est un plus.

### docker-compose.yml (structure attendue)

```yaml
services:
  sdk:
    image: mcr.microsoft.com/dotnet/sdk:10.0
    working_dir: /src
    volumes:
      - .:/src
    profiles: ["tools"]

  api:
    build:
      context: .
      dockerfile: BattleShip.API/Dockerfile
    ports:
      - "8080:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ASPNETCORE_URLS: http://+:8080
      Cors__AllowedOrigins__0: http://localhost:8081

  app:
    build:
      context: .
      dockerfile: BattleShip.App/Dockerfile
      args:
        ApiBaseUrl: http://localhost:8080/
    ports:
      - "8081:80"
    depends_on:
      - api
```

Adapter les ports, les noms de services et les variables selon la configuration réelle. Documenter les URLs d'accès dans le README :
- Front : `http://localhost:8081`
- API : `http://localhost:8080`
- OpenAPI (dev) : `http://localhost:8080/openapi/v1.json`

### Configuration Blazor WASM dans Docker

L'adresse de l'API ne doit pas être codée en dur. Utiliser une configuration injectée au build Docker :

- `wwwroot/appsettings.json` avec valeur par défaut
- variable de build `ApiBaseUrl` passée au `Dockerfile` du front (ex. `http://localhost:8080/`)
- lecture via `builder.Configuration` dans `Program.cs` du client

Le client HTTP et le canal gRPC-Web doivent utiliser la même base URL configurable.

### CORS et réseau Docker

- L'API doit autoriser l'origine du front tel qu'elle est vue par le navigateur (`http://localhost:8081`), pas seulement le nom du service Docker interne
- Configurer CORS via `appsettings.json` + variables d'environnement (`Cors__AllowedOrigins__0`, etc.) dans `docker-compose.yml`
- gRPC-Web doit fonctionner depuis le navigateur vers l'API exposée sur le port hôte

### Bonnes pratiques Docker

- Builds multi-étapes pour limiter la taille des images finales
- Ne pas inclure le SDK dans l'image runtime de l'API
- Utiliser un utilisateur non-root dans les images si possible
- Exécuter `dotnet test` dans le conteneur SDK (pas sur l'hôte)
- Le README documente un seul workflow : Docker

### Vérification Docker attendue

Après génération, ces commandes doivent fonctionner sans aucun SDK .NET installé sur l'hôte :

```bash
docker compose run --rm sdk dotnet build
docker compose run --rm sdk dotnet test
docker compose build
docker compose up -d
curl -f http://localhost:8080/openapi/v1.json   # ou endpoint de santé si ajouté
curl -f -o /dev/null -w "%{http_code}" http://localhost:8081/   # doit retourner 200
docker compose down
```

## Décisions de jeu (libres, proposer des défauts argumentés)

Le cours n'impose pas les règles exactes. Proposer des valeurs par défaut paramétrables :

| Paramètre | Valeur par défaut proposée | Justification |
|-----------|---------------------------|---------------|
| Taille de grille | 10×10 | Standard bataille navale |
| Flotte | Porte-avions (5), Croiseur (4), Contre-torpilleur (3), Sous-marin (3), Torpilleur (2) | Flotte classique |
| Placement | Aléatoire, horizontal ou vertical | À implémenter plus tard |
| Alternance | Joueur puis ordinateur | Spec boucle de jeu |
| Fin de partie | Tous les navires adverses coulés | Spec moteur |

Ces valeurs doivent être configurables (constantes ou options), pas codées en dur dans la logique.

## ADR à produire (format gabarit cours)

Chaque ADR suit ce modèle :

```markdown
# ADR NNNN : Titre

## Statut et date
Proposé — [date]

## Contexte
...

## Options envisagées
1. Option A — avantages / limites
2. Option B — avantages / limites

## Décision
...

## Conséquences
...

## Vérification et réexamen
...

## Références
...
```

ADR minimum :
- **0001** : découpage en couches (Models / API / App / Tests, dépendances)
- **0002** : stockage des parties (InMemory pour le socle, alternatives : fichier, base de données)
- **0003** : forme du contrat API REST (routes, DTO, codes HTTP)
- **0004** : opération gRPC choisie et articulation avec REST
- **0005** : conteneurisation Docker (SDK conteneurisé, 2 conteneurs vs 1, HTTP vs HTTPS, injection URL WASM, CORS, absence de SDK local)

## Tests à fournir (compilent, certains en Skip)

Exécutables dès maintenant :
- Validateurs : entrée vide, coordonnées hors grille, game_id invalide
- Intégration : `POST /api/games` retourne 201, `GET /api/games/{id}` retourne 404 sur id inconnu
- Intégration : entrée invalide retourne 400 avec ValidationProblem

En Skip (en attente du moteur) :
- Placement sans chevauchement
- Tir sur case déjà jouée refusé
- Détection fin de partie
- Masquage des positions adverses dans le DTO

## Garde-fous anti-hallucination

- Vérifier les options des modèles avec `docker compose run --rm sdk dotnet new <modèle> --help` avant de les utiliser
- Ne pas inventer d'API ou de package : citer la documentation .NET 10 / ASP.NET Core 10
- Si une option CLI ou un package est incertain, le signaler explicitement au lieu de l'affirmer
- Les ports 7001 et 7043 du support sont des EXEMPLES : utiliser les ports définis dans `docker-compose.yml`
- Ne pas copier les exemples génériques du cours (catalogue de produits) : adapter au domaine bataille navale
- Vérifier les tags d'images Docker officielles .NET 10 sur Microsoft Container Registry avant de les utiliser

## Format de ta réponse

1. **Plan d'architecture** (1 page) : couches, flux de données, alternatives écartées. Attendre ma validation avant de générer le code.

2. Après validation, fournir dans l'ordre :
   - Commandes CLI exactes, TOUTES préfixées par `docker compose run --rm sdk` (`dotnet new`, `dotnet sln add`, etc.)
   - Contenu de chaque fichier créé ou modifié
   - Fichiers Docker : `.dockerignore`, `Dockerfile` (API + App), `nginx.conf`, `docker-compose.yml` (avec service `sdk`)
   - Script utilitaire optionnel (`scripts/dotnet.sh` ou `Makefile`) pour simplifier les commandes
   - Contenu des 5 ADR
   - Contenu de `api.http` avec les URLs Docker (`http://localhost:8080`, etc.)
   - Gabarits `PROMPTS.md` et `REVUE-IA.md` remplis avec une première entrée exemple

3. Terminer par ces commandes (aucune exécution `dotnet` locale) :
   ```bash
   docker compose run --rm sdk dotnet build
   docker compose run --rm sdk dotnet test
   docker compose build
   docker compose up -d
   docker compose ps
   docker compose down
   ```
   `dotnet build` et `dotnet test` dans le conteneur SDK doivent réussir (les tests Skip ne comptent pas comme échec).
   `docker compose build` doit réussir et les conteneurs api/app doivent démarrer sans erreur.

## Ce que je ne veux PAS

- Une application jouable (c'est l'étape suivante)
- Du code copié depuis les exemples « catalogue de produits » du cours
- Des règles métier inventées et implémentées sans mon accord
- Des packages ou APIs non vérifiés
- Du Swagger UI (remplacé par OpenAPI + api.http en .NET 10)
- Des secrets ou données personnelles dans les exemples
- Des instructions nécessitant un SDK .NET installé localement (`dotnet run`, `dotnet watch`, `dotnet dev-certs`, etc.)

## Références du cours

- Référentiel complet : `csharp-school/Ressources Bataille Navale/Referentiel.md`
- Contexte projet : `csharp-school/Ressources Bataille Navale/CONTEXTE-IA.md`
- Gabarits livrables : `PROMPTS.md`, `REVUE-IA.md`, `docs/adr/0001-modele.md`
- Exemples gRPC (à adapter, ne pas copier) : `Exemples/catalogue.proto`, `Exemples/CatalogueGrpcService.cs.txt`
