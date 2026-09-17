# Revues de propositions IA

Trois revues argumentées minimum. Aucune erreur n'est exigée ; chaque conclusion doit être étayée.

---

## Revue 1/3 : FluentValidation sur CreateGameRequest

- Proposition et référence dans le dépôt : `CreateGameRequestValidator` enregistré en DI et appelé dans `BattleShip.API/Endpoints/GameEndpoints.cs` avant `GameEngine.CreateGameAsync`. Référence : [`CreateGameRequestValidator.cs`](BattleShip.API/Validation/CreateGameRequestValidator.cs), [`GameEndpoints.cs`](BattleShip.API/Endpoints/GameEndpoints.cs).
- Hypothèse à vérifier : une requête avec `boardSize` hors plage (ex. 4) renvoie `400 Bad Request` avec un corps `ValidationProblem`, et non `500 Internal Server Error` ni `201 Created`.
- Scénario, données ou commande :
  ```bash
  docker compose up --build -d api
  curl -i -X POST http://localhost:8080/api/games \
    -H "Content-Type: application/json" \
    -d '{"boardSize":4}'
  ```
  Test automatisé : `BattleShip.Tests/Api/CreateGameEndpointTests.PostInvalidBoardSize_Returns400ValidationProblem`
- Résultat attendu avant exécution :
  - HTTP `400 Bad Request`
  - `Content-Type: application/problem+json`
  - Champ `errors.BoardSize` présent dans le corps JSON
- Erreur que ce contrôle pourrait détecter : validateur absent du conteneur DI ; endpoint qui appelle le moteur sans valider ; mauvais mapping des erreurs FluentValidation vers `ValidationProblem` ; code HTTP incorrect (ex. 422 ou 500).
- Résultat réellement observé :
  - `HTTP/1.1 400 Bad Request`
  - `Content-Type: application/problem+json`
  - Corps : `{"status":400,"errors":{"BoardSize":["La taille de grille doit etre comprise entre 5 et 20."]},...}`
  - Test xUnit : passé (13/13 tests `CreateGame*` au 2026-03-15).
- Décision et justification : la proposition IA est validée — la validation est bien en place et bloque la création de partie avant tout accès au domaine. On conserve FluentValidation côté API (couche HTTP) plutôt que dans `BattleShip.Models`, conformément à l'ADR 0003.
- Preuves reproductibles et liens vers les commits :
  - `./scripts/dotnet.sh test --filter "FullyQualifiedName~CreateGame"`
  - [`api.http`](api.http) — scénario « validation echouee »
  - Commit de l'implémentation : non encore créé au moment de la rédaction (dernier commit connu : `3a857af` — `swagger.yaml` uniquement).
- Après correction éventuelle : résultat avant / après : aucune correction nécessaire — le comportement observé correspond au résultat attendu dès la première implémentation.
- Limites et points non vérifiés : seul `boardSize: 4` a été testé manuellement via curl ; `boardSize: 21` et `difficulty` invalide sont couverts par les tests unitaires du validateur mais pas re-testés manuellement sur l'API dockerisée ; le cas body totalement absent (sans `Content-Type`) n'a pas été vérifié séparément (couvert indirectement par `PostEmptyBody_Returns201WithGameDto`).

---

## Revue 2/3 : GET /api/games/{id} — partie inconnue

- Proposition et référence dans le dépôt : `GetGameAsync` dans [`GameEndpoints.cs`](BattleShip.API/Endpoints/GameEndpoints.cs) — consultation de `IGameRepository.GetByIdAsync`, retour `404 ProblemDetails` si `null`.
- Hypothèse à vérifier : un identifiant de partie inconnu renvoie `404 Not Found` avec un corps `ProblemDetails`, jamais `500` ni `200` avec un corps vide.
- Scénario, données ou commande :
  ```bash
  curl -i http://localhost:8080/api/games/00000000-0000-0000-0000-000000000000
  ```
  Test automatisé : `BattleShip.Tests/Api/GetGameEndpointTests.GetUnknownGame_Returns404`
- Résultat attendu avant exécution :
  - HTTP `404 Not Found`
  - `Content-Type: application/problem+json`
  - Champ `detail` contenant « Partie inconnue »
- Erreur que ce contrôle pourrait détecter : repository ignoré ; exception non gérée sur id absent (500) ; mauvais code HTTP (200 ou 204) ; message d'erreur absent ou générique.
- Résultat réellement observé :
  - `HTTP/1.1 404 Not Found`
  - `Content-Type: application/problem+json`
  - Corps contenant `"detail":"Partie inconnue"`
  - Test xUnit : passé (`GetUnknownGame_Returns404`).
- Décision et justification : la proposition IA est validée — le contrat swagger est respecté ; on conserve la lecture directe via le repository sans passer par `IGameEngine`.
- Preuves reproductibles et liens vers les commits :
  - `./scripts/dotnet.sh test --filter "FullyQualifiedName~GetGame"`
  - [`api.http`](api.http) — scénario « Lire une partie inconnue »
  - Commit à renseigner après validation locale.
- Après correction éventuelle : résultat avant / après : aucune correction nécessaire — comportement conforme dès la première implémentation.
- Limites et points non vérifiés : UUID mal formé dans l'URL (comportement framework `400`, non documenté dans swagger) ; cas `200` après POST couvert par test d'intégration uniquement.

---

## Revue 3/3 : Aucun tir après fin de partie

- Proposition et référence dans le dépôt : garde `game.IsFinished()` dans [`GameEngine.FireShotAsync`](BattleShip.API/Services/GameEngine.cs) avant toute mutation ; rejet `409 Conflict` côté endpoint [`ShotEndpoints.cs`](BattleShip.API/Endpoints/ShotEndpoints.cs).
- Hypothèse à vérifier : une partie au statut `PlayerWon` / `ComputerWon` refuse tout nouveau tir sans modifier `ShotCount` ni le statut.
- Scénario, données ou commande :
  ```bash
  ./scripts/dotnet.sh test --filter "FullyQualifiedName~AfterGameFinished|FullyQualifiedName~AfterPlayerWins|FullyQualifiedName~AfterComputerWins|FullyQualifiedName~AfterPvpGameFinished|FullyQualifiedName~PostShotAfter|FullyQualifiedName~PostPowerUpAfterGameFinished"
  ```
  Tests automatisés :
  - `GameEngineShotTests.FireShotAsync_AfterPlayerWins_ThrowsConflictWithoutChangingState`
  - `GameEngineShotTests.FireShotAsync_AfterComputerWins_ThrowsConflictWithoutChangingState`
  - `GameEngineShotTests.FireShotAsync_AfterPvpGameFinished_ThrowsConflictWithoutChangingState`
  - `GameEnginePowerUpTests.UsePowerUpAsync_AfterGameFinished_ThrowsConflictWithoutChangingState`
  - `FireShotEndpointTests.PostShotAfterPlayerWins_Returns409`
  - `FireShotEndpointTests.PostShotAfterComputerWins_Returns409`
  - `FireShotEndpointTests.PostPvpShotAfterGameFinished_Returns409`
  - `PowerUpEndpointTests.PostPowerUpAfterGameFinished_Returns409`
  Essais manuels : [`api.http`](api.http) — scénarios « tir / power-up après fin de partie »
- Résultat attendu avant exécution :
  - Domaine : `GameConflictException`, `ShotCount` et `Status` inchangés après la tentative.
  - API : HTTP `409 Conflict` sur `POST /api/games/{id}/shots` et `POST /api/games/{id}/powerups` après fin de partie (PvE joueur, PvE ordinateur, PvP).
- Erreur que ce contrôle pourrait détecter : garde absente ou placée après mutation ; statut gagnant encore jouable ; `ShotCount` qui s'incrémente malgré le refus ; code HTTP incorrect (200 ou 500).
- Résultat réellement observé : les deux tests passent — le moteur lève `GameConflictException` dès `IsFinished()`, l'endpoint renvoie `409` sans toucher à l'état persisté.
- Décision et justification : la règle du référentiel (« aucun nouveau coup après la fin de partie ») est validée côté domaine et API ; on conserve la vérification dans `GameEngine` plutôt que dans l'endpoint seul, pour que toute voie d'appel (y compris `PlayComputerTurnAsync`) respecte la même invariante.
- Preuves reproductibles et liens vers les commits :
  - `./scripts/dotnet.sh test --filter "FullyQualifiedName~FireShot"`
  - [`BattleShip.API/Services/GameEngine.cs`](BattleShip.API/Services/GameEngine.cs) lignes 136–137
- Après correction éventuelle : résultat avant / après : tests ajoutés après revue de conformité — ils échoueraient si la garde `IsFinished()` était retirée ou déplacée après `ResolveShot` ou `ResolvePowerUpForShooterAsync`.
- Limites et points non vérifiés : les essais manuels `api.http` supposent une partie déjà terminée (non reproductibles en curl pur sans jouer jusqu'à la fin).
