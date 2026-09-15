# Échanges décisifs avec l'IA

## Date et sujet

**2026-03-15 — Implémentation POST /api/games avec placement des flottes**

- Outil / modèle si connu : Cursor (Composer)
- Contexte : TP Bataille Navale ASP.NET (.NET 10, Minimal API, Docker-only). Contrat REST défini dans `swagger.yaml`. Première route métier à implémenter ; le template weather de l'API doit être remplacé.
- Prompt réellement utilisé : « build le plan stp » en pointant le plan `post_api_games_fdd0a934.plan.md` — implémenter `POST /api/games` conforme à `swagger.yaml` : domaine minimal dans `BattleShip.Models`, placement aléatoire des flottes joueur et ordinateur, FluentValidation, persistance InMemory, tests xUnit, documentation (ADR 0003, `api.http`, README, REVUE-IA 1/3).
- Réponse et hypothèses résumées : l'IA propose un flux Endpoint → Validator → GameEngine → Repository → GameMapper. Hypothèse : les deux flottes sont placées aléatoirement dès la création (pas de stub). Le `GameDto` ne contient aucune position de navire. Body absent ou `{}` → défauts (`boardSize` 10, `difficulty` Normal). Enums JSON en PascalCase (`Easy`, `Normal`, `Hard`). Échec de placement après 100 tentatives → `500` (cas extrême).
- Décision et justification : placement complet des flottes au `POST` (aligné swagger et spec cours) ; `GameDto` minimal sans grilles (règle de visibilité) ; validation FluentValidation côté API, pas dans le domaine ; `InMemoryGameRepository` en singleton suffisant pour le TP.
- Scénario ou commande de vérification :
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
- Résultat attendu, puis résultat observé :
  - Tests : 13/13 passés (`CreateGameRequestValidatorTests`, `GameEnginePlacementTests`, `CreateGameEndpointTests`).
  - `POST` valide : `201 Created`, header `Location: /api/games/{id}`, corps `GameDto` avec `status: PlayerTurn`, `shotCount: 0`, sans positions de navires.
  - `POST {"boardSize":4}` : `400 Bad Request`, `application/problem+json`, erreur sur `BoardSize`.
  - OpenAPI : route `POST /api/games` présente dans `/openapi/v1.json`.
- Erreur que ce contrôle pourrait détecter : validateur non enregistré en DI (400 absent → 500 ou 201 avec données invalides) ; flottes non placées ou chevauchement ; fuite de positions adverses dans le `GameDto` ; enums non sérialisés en chaîne (échec binding ou désérialisation).
- Preuves reproductibles et limites : code dans `BattleShip.API/Endpoints/GameEndpoints.cs`, `BattleShip.API/Services/GameEngine.cs`, `BattleShip.API/Validation/CreateGameRequestValidator.cs` ; ADR [`docs/adr/0003-contrat-api.md`](docs/adr/0003-contrat-api.md) ; essais manuels [`api.http`](api.http). Commit de cette implémentation non encore créé au moment de la rédaction (dernier commit connu : `3a857af` — ajout `swagger.yaml`). Limite : persistance en mémoire (partie perdue au redémarrage du conteneur) ; placement aléatoire non déterministe en production (seed fixe uniquement dans les tests).
