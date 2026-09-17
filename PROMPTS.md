# Échanges décisifs avec l'IA

## Entrée 1 — 2026-03-15 : POST /api/games

- **Outil / modèle** : Cursor (Composer)
- **Contexte** : TP Bataille Navale ASP.NET Core (.NET 10), environnement Docker-only. Première route métier à livrer, conforme au contrat [`swagger.yaml`](swagger.yaml). Remplacement du template `weatherforecast` de l'API.
- **Prompt réellement utilisé** : Demande d'exécution du plan d'implémentation [`docs/plans/post_api_games_fdd0a934.plan.md`](docs/plans/post_api_games_fdd0a934.plan.md) — mise en place de `POST /api/games` avec domaine minimal (`BattleShip.Models`), placement aléatoire des flottes, validation FluentValidation, persistance InMemory, tests xUnit et documentation associée (ADR 0003, `api.http`, README, revue IA 1/3).
- **Réponse et hypothèses résumées** : Architecture retenue : Endpoint → Validator → GameEngine → Repository → GameMapper. Les deux flottes sont placées aléatoirement à la création. Le `GameDto` exclut toute position de navire. Corps absent ou `{}` : valeurs par défaut (`boardSize` 10, `difficulty` Normal). Sérialisation JSON des énumérations en PascalCase.
- **Décision et justification** : Placement complet des flottes dès le `POST` (conformité swagger et cahier des charges) — **évolution ultérieure** : voir entrée 4 (placement manuel joueur). `GameDto` minimal sans grilles (règle de visibilité). Validation FluentValidation en couche API. `InMemoryGameRepository` en singleton, suffisant pour le périmètre du TP.
- **Scénario ou commande de vérification** :
  ```bash
  ./scripts/dotnet.sh test --filter "FullyQualifiedName~CreateGame"
  docker compose up --build -d api
  curl -i -X POST http://localhost:8080/api/games \
    -H "Content-Type: application/json" \
    -d '{"boardSize":10,"difficulty":"Normal"}'
  curl -i -X POST http://localhost:8080/api/games \
    -H "Content-Type: application/json" \
    -d '{"boardSize":4}'
  curl -s http://localhost:8080/openapi/v1.json | grep -F '"/api/games"'
  ```
- **Résultat attendu, puis résultat observé** :
  - Attendu : 13 tests passants ; `201 Created` avec `Location` et `GameDto` ; `400` sur entrée invalide.
  - Observé : 13/13 tests (`CreateGameRequestValidatorTests`, `GameEnginePlacementTests`, `CreateGameEndpointTests`) ; `POST` valide → `201`, `status: PlayerTurn`, `shotCount: 0` ; `POST {"boardSize":4}` → `400`, `application/problem+json` ; route présente dans `/openapi/v1.json`.
- **Erreur que ce contrôle pourrait détecter** : Validateur absent du conteneur DI ; flottes non placées ou en chevauchement ; fuite de positions adverses dans le `GameDto` ; échec de sérialisation des énumérations.
- **Preuves reproductibles et limites** : [`BattleShip.API/Endpoints/GameEndpoints.cs`](BattleShip.API/Endpoints/GameEndpoints.cs), [`BattleShip.API/Services/GameEngine.cs`](BattleShip.API/Services/GameEngine.cs), [`BattleShip.API/Validation/CreateGameRequestValidator.cs`](BattleShip.API/Validation/CreateGameRequestValidator.cs), [`docs/adr/0003-contrat-api.md`](docs/adr/0003-contrat-api.md), [`api.http`](api.http). *Limite* : persistance volatile (redémarrage du conteneur) ; placement non déterministe hors tests.

---

## Entrée 2 — 2026-03-15 : GET /api/games/{id}

- **Outil / modèle** : Cursor (Composer)
- **Contexte** : Deuxième route REST du projet. Réutilisation de `IGameRepository`, `GameMapper` et `GameDto` mis en place avec le `POST`.
- **Prompt réellement utilisé** : Demande d'exécution du plan d'implémentation [`docs/plans/get_api_games_id_3550a52c.plan.md`](docs/plans/get_api_games_id_3550a52c.plan.md) — mise en place de `GET /api/games/{id}` : lecture InMemory, réponses `200` / `404`, tests d'intégration et mise à jour de la documentation (ADR 0003, `api.http`, README, revue IA 2/3).
- **Réponse et hypothèses résumées** : Lecture directe via `IGameRepository.GetByIdAsync` sans extension de `IGameEngine`. Mapping identique au `POST` via `GameMapper.ToDto`. Identifiant inconnu → `404 ProblemDetails` avec `detail: "Partie inconnue"`.
- **Décision et justification** : Aucune règle métier sur un GET de consultation ; le repository et le mapper existants suffisent. Cohérence POST/GET assurée par un mapper unique.
- **Scénario ou commande de vérification** :
  ```bash
  ./scripts/dotnet.sh test --filter "FullyQualifiedName~GetGame"
  docker compose up --build -d api
  ID=$(curl -s -X POST http://localhost:8080/api/games \
    -H "Content-Type: application/json" -d '{}' | jq -r .id)
  curl -i http://localhost:8080/api/games/$ID
  curl -i http://localhost:8080/api/games/00000000-0000-0000-0000-000000000000
  ```
- **Résultat attendu, puis résultat observé** :
  - Attendu : 2 tests passants ; `200` pour une partie existante ; `404` pour un identifiant inconnu.
  - Observé : 2/2 tests (`GetGameEndpointTests`) ; `GET` après `POST` → `200`, `GameDto` cohérent ; `GET` sur UUID inconnu → `404`, `detail: "Partie inconnue"`.
- **Erreur que ce contrôle pourrait détecter** : Repository non consulté ; mapper divergent du `POST` ; code HTTP incorrect sur ressource absente.
- **Preuves reproductibles et limites** : [`BattleShip.API/Endpoints/GameEndpoints.cs`](BattleShip.API/Endpoints/GameEndpoints.cs), [`BattleShip.Tests/Api/GetGameEndpointTests.cs`](BattleShip.Tests/Api/GetGameEndpointTests.cs), [`api.http`](api.http). *Limite* : UUID mal formé géré par le framework (`400`, hors périmètre swagger).

---

## Entrée 3 — 2026-09-15 : POST /api/games/{id}/shots

- **Outil / modèle** : Cursor (Composer)
- **Contexte** : Boucle de jeu backend — première route qui fait évoluer une partie en cours. Le contrat initial du `swagger.yaml` prévoyait un tir joueur + un tir ordinateur dans une seule requête ; le front Blazor (déjà en mock) enchaîne deux appels distincts. PvE et PvP (tokens) étaient en place via l'ADR 0006.
- **Prompt réellement utilisé** : Demande d'exécution du plan [`docs/plans/post_shots_b91bed69.plan.md`](docs/plans/post_shots_b91bed69.plan.md) — implémenter `POST /api/games/{id}/shots` avec **un tir par requête**, résolution Miss/Hit/Sunk côté domaine, Computer via `IComputerOpponent`, validation FluentValidation, tests xUnit et mise à jour de `swagger.yaml`, `api.http` et ADR 0003.
- **Réponse et hypothèses résumées** : Refonte du contrat (`ShotResultDto` : un seul `shot` + `shooter` + `status`). PvE : `{x,y}` en `PlayerTurn`, corps vide en `ComputerTurn` (coordonnées choisies par `RandomComputerOpponent`). PvP : `{x,y}` + header `X-Player-Token`. `ShotCount` incrémenté uniquement sur tir humain valide. Coup refusé (case déjà ciblée, hors limites, mauvais tour) → `409` ou `400` sans mutation de la partie.
- **Décision et justification** : **refusée** — la proposition initiale du `swagger.yaml` (double tir atomique : joueur + ordinateur dans une seule requête). **Adaptée** — **1 requête = 1 tir** retenu (breaking change assumé) aligné sur l'alternance `PlayerTurn` / `ComputerTurn` du front et plus simple à tester. Règles dans `GameEngine.FireShotAsync` + `Board.ResolveShot`, pas dans l'endpoint. L'IA reste injectable (`IComputerOpponent`) pour les tests et l'évolution de la difficulté. *Apprentissage* : un contrat swagger initial n'est pas figé si le front et les tests gagnent en clarté avec un découpage plus fin.
- **Scénario ou commande de vérification** :
  ```bash
  ./scripts/dotnet.sh test --filter "FullyQualifiedName~Shot"
  docker compose up --build -d api
  ID=$(curl -s -X POST http://localhost:8080/api/games -H "Content-Type: application/json" -d '{}' | jq -r .id)
  curl -s -X POST http://localhost:8080/api/games/$ID/fleet \
    -H "Content-Type: application/json" \
    -d '{"ships":[{"name":"Porte-avions","x":0,"y":0,"horizontal":true},{"name":"Croiseur","x":0,"y":1,"horizontal":true},{"name":"Contre-torpilleur","x":0,"y":2,"horizontal":true},{"name":"Sous-marin","x":0,"y":3,"horizontal":true},{"name":"Torpilleur","x":0,"y":4,"horizontal":true}]}'
  curl -i -X POST http://localhost:8080/api/games/$ID/shots \
    -H "Content-Type: application/json" -d '{"x":4,"y":7}'
  curl -i -X POST http://localhost:8080/api/games/$ID/shots \
    -H "Content-Type: application/json" -d '{}'
  curl -i -X POST http://localhost:8080/api/games/$ID/shots \
    -H "Content-Type: application/json" -d '{"x":4,"y":7}'
  ```
- **Résultat attendu, puis résultat observé** :
  - Attendu : tests `BoardShot*`, `GameEngineShot*`, `FireShot*`, `ShotRequestValidator*` passants ; PvE joueur → `200`, `status: ComputerTurn` ; PvE ordinateur (body vide) → `200`, `status: PlayerTurn` ou `ComputerWon` ; rejeu sur la même case → `409`.
  - Observé : suite de tests verte au fil des évolutions (`FullyQualifiedName~Shot` : 44 tests au 2026-09-17) ; scénarios `api.http` « PvE — tir joueur / ordinateur / case déjà jouée » conformes ; commit initial `4ab2eaf` (PR #3 `feat/shots_route`).
- **Erreur que ce contrôle pourrait détecter** : double tir dans une seule requête ; `ShotCount` incrémenté sur le tour ordinateur ; case déjà ciblée qui modifie quand même l'état ; coordonnées acceptées en `ComputerTurn` ; PvP sans token qui tire quand même.
- **Preuves reproductibles et limites** : [`BattleShip.API/Endpoints/ShotEndpoints.cs`](BattleShip.API/Endpoints/ShotEndpoints.cs), [`BattleShip.API/Services/GameEngine.cs`](BattleShip.API/Services/GameEngine.cs), [`BattleShip.Models/Domain/Board.cs`](BattleShip.Models/Domain/Board.cs), [`BattleShip.Tests/Api/FireShotEndpointTests.cs`](BattleShip.Tests/Api/FireShotEndpointTests.cs), [`docs/adr/0003-contrat-api.md`](docs/adr/0003-contrat-api.md), [`api.http`](api.http). *Limite* : à la livraison initiale, `RandomComputerOpponent` seul (stratégie par difficulté ajoutée ensuite) ; power-ups et garde « fin de partie » documentés dans des passes ultérieures.

---

## Entrée 4 — 2026-09-16 : Placement manuel de la flotte joueur

- **Outil / modèle** : Cursor (Composer)
- **Contexte** : Le référentiel (`csharp-school`, diapo 36) et le premier plan [`docs/plans/post_api_games_fdd0a934.plan.md`](docs/plans/post_api_games_fdd0a934.plan.md) prévoyaient de placer **les deux flottes aléatoirement** dès la création de partie. L'entrée 1 ci-dessus reflète encore ce choix initial ; le flux a été revu ensuite pour améliorer l'expérience de jeu.
- **Prompt réellement utilisé** : arbitrage produit lors de l'évolution du contrat — « le joueur place sa flotte manuellement (avec une option "placement aléatoire" dans l'UI), l'ordinateur reste sur un placement aléatoire ».
- **Réponse et hypothèses résumées** : l'IA / le plan initial proposait `POST /api/games` → `PlayerTurn` avec les deux grilles déjà remplies. Alternative : `POST /api/games` → `PlacingFleet`, puis `POST /api/games/{id}/fleet` pour le joueur, placement automatique de la flotte adverse à ce moment-là.
- **Décision et justification** : **refusée** — placement 100 % aléatoire des deux flottes à la création. **Retenu** — placement **manuel** pour le joueur (écran `FleetDeployment`, option « placement aléatoire » côté UI) + placement **aléatoire** pour l'ordinateur uniquement (`FleetPlacer.PlaceFleetRandomly` après validation de la flotte joueur). Meilleure expérience de jeu et phase de briefing plus engageante ; écart assumé au socle minimal, documenté dans le README (tableau arbitrages) et l'ADR 0003. *Apprentissage* : une spec de cours peut être enrichie si l'écart est explicite, justifié et traçable.
- **Scénario ou commande de vérification** :
  ```bash
  ./scripts/dotnet.sh test --filter "FullyQualifiedName~Placement"
  docker compose up --build -d
  # Créer une partie → status PlacingFleet, pas PlayerTurn
  curl -s -X POST http://localhost:8080/api/games -H "Content-Type: application/json" -d '{}' | jq .status
  ```
- **Résultat attendu, puis résultat observé** :
  - Attendu : `POST /api/games` → `PlacingFleet` ; `POST /fleet` valide → `PlayerTurn` ; flotte ordinateur placée sans exposer ses positions.
  - Observé : `GameEnginePlacementTests.CreateGameAsync_PvE_DoesNotPlaceFleets` ; `PlaceFleetAsync_PvE_PlacesValidFleetsWithoutOverlap` ; README arbitrage « Placement des flottes ».
- **Preuves reproductibles et limites** : [`BattleShip.API/Services/GameEngine.cs`](BattleShip.API/Services/GameEngine.cs), [`BattleShip.App/Shared/FleetDeployment.razor`](BattleShip.App/Shared/FleetDeployment.razor), [`docs/adr/0003-contrat-api.md`](docs/adr/0003-contrat-api.md), [`README.md`](README.md). *Limite* : en PvP, chaque joueur place aussi manuellement sa flotte.

---

## Entrée 5 — 2026-09-15 : Environnement Docker-only (refus du SDK local)

- **Outil / modèle** : Cursor (Composer)
- **Contexte** : Initialisation du projet via [`PROMPT-INIT.md`](PROMPT-INIT.md). Le template `dotnet new` et la doc Microsoft supposent souvent `dotnet run` / `dotnet watch` en local.
- **Prompt réellement utilisé** (modifications successives du prompt d'init) :
  1. « Nous sommes plusieurs à travailler sur ce même projet et nous n'avons pas envie de polluer notre ordinateur avec des dépendances. Ainsi, je souhaite passer par Docker (execution de commandes Dotnet, execution des services) pour ce projet. »
- **Réponse et hypothèses résumées** : proposition IA standard — README avec prérequis SDK .NET, `launchSettings.json`, `dotnet dev-certs https`. Alternative retenue : service `sdk` dans `docker-compose.yml`, script [`scripts/dotnet.sh`](scripts/dotnet.sh), README limité à Docker Desktop comme seul prérequis.
- **Décision et justification** : **refusée** — workflow SDK local (`dotnet run`, `dotnet watch`, `dotnet dev-certs` sur l'hôte). **Retenu** — Docker-only : build, tests et lancement via `docker compose` et `./scripts/dotnet.sh`. Reproductibilité binôme (même .NET 10, pas de dérive de version) ; aligné avec ADR 0005. *Apprentissage* : imposer les contraintes d'environnement **dans le prompt** avant la génération évite de devoir refactorer le README et la CI ensuite.
- **Scénario ou commande de vérification** :
  ```bash
  docker compose up --build
  ./scripts/dotnet.sh build
  ./scripts/dotnet.sh test
  ```
- **Résultat attendu, puis résultat observé** :
  - Attendu : application accessible sur `:8081` / `:8080` ; tests passants sans `dotnet` installé sur l'hôte.
  - Observé : README « Prérequis : Docker Desktop uniquement » ; workflow CI GitHub Actions Docker-only ; ADR 0005 accepté.
- **Erreur que ce contrôle pourrait détecter** : instructions `dotnet run` dans le README ; absence du service `sdk` dans `docker-compose.yml`.
- **Preuves reproductibles et limites** : [`PROMPT-INIT.md`](PROMPT-INIT.md), [`docker-compose.yml`](docker-compose.yml), [`docs/adr/0005-conteneurisation-docker.md`](docs/adr/0005-conteneurisation-docker.md), [`.github/workflows/ci.yml`](.github/workflows/ci.yml). *Limite* : le développement dans l'IDE peut encore utiliser l'analyse statique locale ; seules build/test/run officiels passent par Docker.
