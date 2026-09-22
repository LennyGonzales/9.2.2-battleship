# Échanges décisifs avec l'IA

## Back

### Entrée 1 — 2026-03-15 : POST /api/games

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

### Entrée 2 — 2026-03-15 : GET /api/games/{id}

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

### Entrée 3 — 2026-09-15 : POST /api/games/{id}/shots

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

### Entrée 4 — 2026-09-16 : Placement manuel de la flotte joueur

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

### Entrée 5 — 2026-09-15 : Environnement Docker-only (refus du SDK local)

- **Outil / modèle** : Cursor (Composer)
- **Contexte** : Initialisation du projet via [`PROMPT-ARCHITECTURE.md`](PROMPT-ARCHITECTURE.md). Le template `dotnet new` et la doc Microsoft supposent souvent `dotnet run` / `dotnet watch` en local.
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
- **Preuves reproductibles et limites** : [`PROMPT-ARCHITECTURE.md`](PROMPT-ARCHITECTURE.md), [`docker-compose.yml`](docker-compose.yml), [`docs/adr/0005-conteneurisation-docker.md`](docs/adr/0005-conteneurisation-docker.md), [`.github/workflows/ci.yml`](.github/workflows/ci.yml). *Limite* : le développement dans l'IDE peut encore utiliser l'analyse statique locale ; seules build/test/run officiels passent par Docker.

---

## Front

---

### Entrée 6 — 2026-09-15 — Cadrage et lancement du front-end (radar/WW2)

- Outil / modèle si connu : Claude Code, Claude Sonnet 5
- Contexte : `BattleShip.App` ne contenait que le squelette par défaut de `dotnet new blazorwasm` (pages Home/Counter/Weather) ; `BattleShip.API` n'implémentait encore aucun endpoint métier, seul `swagger.yaml` documentait le contrat cible.
- Prompt réellement utilisé : « Développe uniquement le front-end en suivant strictement PROMPT-INIT.md et les contrats d'API de swagger.yaml. Ne touche à aucun fichier back-end. »
- Réponse et hypothèses résumées : proposition de construire le client HTTP réel contre `swagger.yaml`, complété par un moteur de jeu en mémoire (`MockGameApiClient`) derrière la même interface `IGameApiClient`, pour que l'interface soit jouable avant l'implémentation du vrai backend ; thème visuel « War Room / Radar » (palette militaire, police stencil, effets de balayage radar) ; découpage en 9 tâches avec un commit atomique par tâche.
- Décision : approche « contrat réel + repli mock » retenue via une question de clarification explicite (voir la revue « choix d'architecture » ci-dessous) ; le thème et le découpage en tâches ont été validés section par section avant écriture du plan.
- Vérification : exécution du plan `docs/superpowers/plans/2026-09-15-radar-frontend.md` tâche par tâche, avec relecture (conformité + qualité) après chaque tâche, puis revue finale de l'ensemble de la branche.
  - Résultat attendu : un front jouable de bout en bout (créer une partie, tirer, gagner/perdre) sans backend réel.
  - Résultat observé : les 9 tâches et la revue finale ont validé la chaîne complète (`RadarGrid` → `GameSession` → `IGameApiClient` → `MockGameApiClient` → DTOs) ; la revue finale a détecté et fait corriger 6 anomalies d'intégration (voir [`REVUE-IA.md`](REVUE-IA.md), section Front).
  - Erreur que ce contrôle pourrait détecter : une rupture de contrat (DTO qui ne correspond plus à `swagger.yaml`) ou une rupture de la chaîne UI → service lors d'une tâche ultérieure.
- Preuves reproductibles et limites : commits `3fde4b9`..`9ba7bf6` sur la branche `front-end` (9 tâches), puis `1d8dddb`..`de28cd7` (corrections de la revue finale). Limite : aucune commande `docker compose up --build` n'a pu être exécutée dans cet environnement (Docker absent) — la vérification est restée statique ; à rejouer manuellement avant remise.

### Entrée 7 — 2026-09-15 — Choix d'architecture : contrat réel vs repli mock local

---

- Outil / modèle si connu : Claude Code, Claude Sonnet 5
- Contexte : au moment de cadrer le front, `BattleShip.API` ne possède aucun endpoint métier (seulement le modèle `weatherforecast` par défaut) — un front branché uniquement sur `swagger.yaml` ne serait pas démontrable en l'état.
- Prompt réellement utilisé : question de clarification posée avant la conception — « Les vrais endpoints n'existent pas encore. Comment le front doit-il gérer ça ? », avec trois options proposées (contrat réel seul / mock local seul / contrat réel + repli mock local).
- Réponse et hypothèses résumées : l'option retenue (« Wire to real contract, add local mock fallback ») suppose qu'une interface unique `IGameApiClient` peut être implémentée à la fois par un client HTTP fidèle au contrat et par un moteur en mémoire, et qu'un seul paramètre de configuration (`UseMockApi`) suffirait à basculer de l'un à l'autre sans toucher à l'UI.
- Décision : acceptée. Elle permet une démonstration jouable dès maintenant tout en gardant le code prêt pour la vraie API, sans double implémentation de l'UI.
- Vérification : relecture du code (`MockGameApiClient.cs`, `HttpGameApiClient.cs`, `Program.cs`) pour confirmer que les deux implémentations respectent exactement les mêmes signatures et que le switch DI est bien un changement d'une ligne.
  - Résultat attendu : aucune divergence de signature entre les deux implémentations, bascule par une seule ligne de configuration.
  - Résultat observé : confirmé par la relecture de la tâche 4 — mais la revue finale a découvert que le `Dockerfile` de l'image de production écrasait `wwwroot/appsettings.json` et supprimait silencieusement la clé `UseMockApi` du build : l'hypothèse « un seul paramètre suffit » était donc fausse en pratique pour un déploiement Docker.
  - Erreur que ce contrôle pourrait détecter : une divergence de contrat entre les deux implémentations d'`IGameApiClient`, ou une configuration qui ne survit pas au build de production.
- Preuves reproductibles et limites : décision mise en œuvre aux commits `ecc7d85` (mock) et `a76a365` (HTTP + switch DI) ; défaut de configuration Docker découvert et corrigé au commit `d04ddb2` (voir [`REVUE-IA.md`](REVUE-IA.md), section Front). *Limite au moment de l'entrée* : la bascule vers `UseMockApi=false` n'avait pas encore été testée contre une vraie API (depuis livrée et validée).

---

### Entrée 8 — 2026-09-15 — Revue finale de branche (prompt de vérification globale)

- Outil / modèle si connu : Claude Code, Claude Sonnet 5 (revue dispatchée sur le modèle le plus capable disponible)
- Contexte : les 9 tâches du plan étaient individuellement relues et approuvées ; il restait à vérifier l'intégration de bout en bout avant de considérer la branche prête.
- Prompt réellement utilisé (résumé du prompt de revue, ~350 lignes au complet avec le contexte du projet) : « Relis l'ensemble du diff `3a857af..9ba7bf6` (12 commits). Trace au moins une action utilisateur complète à travers toutes les couches. Vérifie en particulier : la règle de visibilité de `swagger.yaml`, la cohérence du switch mock/réel, ce qui se passe sur un rafraîchissement de page en pleine partie, et le respect des contraintes de `PROMPT-INIT.md`. »
- Réponse et hypothèses résumées : le relecteur a tracé un tir complet (`RadarGrid` → `GameSession` → `IGameApiClient` → `MockGameApiClient` → DTOs) et confirmé la cohérence de bout en bout et le respect du contrat `swagger.yaml`, mais a détecté 6 anomalies d'intégration invisibles à l'échelle d'une seule tâche : compteur de tirs bloqué à 0, écran de combat bloqué sur « Chargement… » sans erreur visible en cas d'échec, style du bandeau perdu (portée `::deep` de Blazor manquante), perte de `UseMockApi` au build Docker, et une documentation `CLAUDE.md` obsolète.
- Décision : les 6 anomalies ont été corrigées dans une seule vague de correctifs (suivie d'une seule relecture ciblée), conformément au processus : elles cassaient des comportements réellement visibles par un joueur ou un correcteur, sans remettre en cause l'architecture retenue.
- Vérification : relecture ciblée du diff de correction (`git diff 9ba7bf6..de28cd7`) vérifiant que chacune des 6 anomalies était bien résolue et qu'aucune régression n'était introduite.
  - Résultat attendu : les 6 correctifs ne modifient que le comportement visé, sans effet de bord.
  - Résultat observé : confirmé par la relecture ciblée — tous les points traités, aucune régression détectée.
  - Erreur que ce contrôle pourrait détecter : une intégration correcte tâche par tâche mais incohérente une fois assemblée — le type d'erreur qu'une revue par tâche isolée ne peut pas voir.
- Preuves reproductibles et limites : commits de correction `1d8dddb`, `bbadf6a`, `5cee62d`, `d04ddb2`, `de28cd7`. Limite : aucun `dotnet build` réel n'a pu être exécuté (Docker indisponible dans cet environnement) — la vérification reste une relecture statique très soignée, pas une compilation réelle ; à rejouer avant remise.

---

### Entrée 9 — 2026-09-16 — Refus Bootstrap : thème radar custom

- Outil / modèle si connu : Claude Code, Claude Sonnet 5
- Contexte : le squelette `dotnet new blazorwasm` embarque Bootstrap par défaut (`wwwroot/lib/bootstrap`, styles `.btn-primary`, etc.). Le plan [`docs/superpowers/plans/2026-09-15-radar-frontend.md`](docs/superpowers/plans/2026-09-15-radar-frontend.md) visait une identité visuelle « War Room / radar WW2 ».
- Prompt réellement utilisé : « Développe uniquement le front-end en suivant strictement PROMPT-INIT.md et les contrats d'API de swagger.yaml. Ne touche à aucun fichier back-end. » — puis validation section par section du plan (palette militaire, polices stencil, grilles radar).
- Réponse et hypothèses résumées : l'IA a proposé de conserver Bootstrap pour les composants de base et d'ajouter une couche thème par-dessus. Alternative : `theme.css` + `app.css` allégé (erreurs Blazor, chargement), sans lien vers Bootstrap dans `index.html`.
- Décision : **refusée** — conservation de Bootstrap comme socle UI. **Retenu** — thème entièrement custom (`wwwroot/css/theme.css`, tokens `--br-*`, composants `.btn-brass`, `.radar-panel`). *Apprentissage* : pour une identité visuelle forte, partir du template par défaut complique plus qu'un thème dédié ; le plan a explicitement demandé de retirer les règles Bootstrap-orientées de `app.css`.
- Vérification : relecture de [`BattleShip.App/wwwroot/index.html`](BattleShip.App/wwwroot/index.html) (pas de `<link>` Bootstrap) et de [`BattleShip.App/wwwroot/css/theme.css`](BattleShip.App/wwwroot/css/theme.css) (tokens et primitives partagées).
  - Résultat attendu : boutons et panneaux stylés via `btn-brass` / `radar-panel`, pas `.btn-primary`.
  - Résultat observé : thème cohérent sur Briefing, Battle, NavMenu ; `app.css` limité au mécanique Blazor (spinner, error UI).
- Preuves reproductibles et limites : plan `2026-09-15-radar-frontend.md` (étape « Trim app.css »), commits branche `front-end`. *Limite* : `lib/bootstrap` peut subsister dans `wwwroot` sans être référencé — non bloquant.

---

### Entrée 10 — 2026-09-16 — Refus animations et sons trop chargés

- Outil / modèle si connu : Cursor (Composer)
- Contexte : enrichissements UX proposés par l'IA sur les tirs (`RadarGrid.razor.css`) et les effets sonores (`wwwroot/js/sfx.js`).
- Prompt réellement utilisé : itérations successives — « Maintenant, dans le côté front, je souhaite avoir des animations pour les différents type de status de tirs (son métallique lorsqu'un bateau est touché, un son d'explosion lorsqu'il coule, ...). », puis retours utilisateur sur le rendu.
- Réponse et hypothèses résumées :
  - **Sons** : proposition initiale — ping sonar sur un navire coulé. Retenu — explosion métallique distincte du son « touché ».
  - **Animation loupé / île** : éclaboussures et impact rocheux conservés mais simplifiés après relecture (pas d'effet infini).
- Décision : **refusées** — surcharge visuelle et sonore (ping sonar sur coulé, animation naufrage complexe type vortex). **Adaptées** — feedback lisible en < 1 s, cohérent avec le HUD radar, sans distraire du gameplay. *Apprentissage* : une animation « spectaculaire » peut nuire à la lisibilité d'une grille de jeu ; mieux vaut un signal clair et bref.
- Vérification : jeu manuel sur `http://localhost:8081` après `docker compose up --build` — tir loupé, touche, coulé, île.
  - Résultat attendu : le joueur distingue immédiatement miss / hit / sunk sans relire la légende.
  - Résultat observé : sons et animations distincts ; naufrage simplifié (commits sur `RadarGrid.razor.css` et `sfx.js`).
- Preuves reproductibles et limites : [`BattleShip.App/Shared/RadarGrid.razor.css`](BattleShip.App/Shared/RadarGrid.razor.css), [`BattleShip.App/wwwroot/js/sfx.js`](BattleShip.App/wwwroot/js/sfx.js). *Limite* : préférences subjectives ; le délai de 2 s avant le tir ordinateur est un choix UX additionnel (non listé comme refus).

---

### Entrée 11 — 2026-09-17 — Refus boutons de test gRPC : barre de recherche Mission

- Outil / modèle si connu : Cursor (Composer)
- Contexte : exigence cours (diapo 48) — au moins un échange gRPC-Web fonctionnel **et** une erreur attendue démontrable depuis le navigateur. Le HUD en combat (`StatusConsole`) ne peut produire `InvalidArgument` (toujours un `Guid` valide de la partie courante).
- Prompt réellement utilisé :
  1. « Comment ajouter le cas d'erreur dans l'UI ? »
  2. Réponse IA : boutons « Tester GUID invalide » / « Tester partie inconnue » dans le HUD.
  3. **Modification du prompt utilisateur** : « Je ne trouve pas que la solution proposée est adaptée dans le contexte de l'application, je cherche une erreur qui peut survenir sans que l'utilisateur veut explicitement la déclencher (entrer un mauvais guid dans une barre de recherche, …) »
  4. Puis : « une barre de recherche en haut pour pouvoir rechercher une partie et que ça nous affiche les stats de celle-ci. »
- Réponse et hypothèses résumées : première proposition — probes dédiés dans `StatusConsole` (artificiels pour le correcteur). Alternative retenue — champ **Mission** dans le header (`NavMenu.razor`) → `MissionStatsLookup` → gRPC direct (sans REST), texte brut envoyé au serveur ; panneau [`MissionStatsPanel.razor`](BattleShip.App/Shared/MissionStatsPanel.razor) sous le header.
- Décision : **refusée** — boutons de démo « Tester GUID invalide / partie inconnue ». **Retenue** — barre de recherche : coller un id de partie, une URL `/battle/{id}`, ou une saisie invalide (`test` → `InvalidArgument`, UUID inconnu → `NotFound`). Voir plan [`docs/plans/search_bar_stats_grpc_bd7e5486.plan.md`](docs/plans/search_bar_stats_grpc_bd7e5486.plan.md) et ADR 0004. *Apprentissage* : une erreur « démontrable » doit pouvoir survenir dans un usage réel. Les boutons de test masquent l'intention produit.
- Vérification :
  ```bash
  docker compose up --build
  # Header Mission : id valide → stats ; "test" → InvalidArgument ; UUID aléatoire → NotFound
  ```
  Correction CORS associée : `WithExposedHeaders(Grpc-Status, Grpc-Message, …)` dans [`BattleShip.API/Program.cs`](BattleShip.API/Program.cs) après erreur navigateur « No grpc-status found on response ».
  - Résultat attendu : erreurs gRPC lisibles dans l'UI sans DevTools ni grpcurl.
  - Résultat observé : README section « Barre de recherche (header) » ; `GameStatsLoadResult` + `GrpcGameStatsClient` ne avalent plus les `RpcException`.
- Preuves reproductibles et limites : [`BattleShip.App/Services/MissionStatsLookup.cs`](BattleShip.App/Services/MissionStatsLookup.cs), [`BattleShip.App/Layout/NavMenu.razor`](BattleShip.App/Layout/NavMenu.razor), [`docs/adr/0004-echange-grpc.md`](docs/adr/0004-echange-grpc.md). *Limite* : barre désactivée en `UseMockApi: true`.
