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
