# Revues de propositions IA

Trois revues argumentées minimum. Aucune erreur n'est exigée ; chaque conclusion doit être étayée.

---

## Back

### Revue 1 : FluentValidation sur CreateGameRequest

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
  - Commit de l'implémentation : livré sur `main` (commit 418d96f).
- Après correction éventuelle : résultat avant / après : aucune correction nécessaire — le comportement observé correspond au résultat attendu dès la première implémentation.
- Limites et points non vérifiés : seul `boardSize: 4` a été testé manuellement via curl ; `boardSize: 21` et `difficulty` invalide sont couverts par les tests unitaires du validateur mais pas re-testés manuellement sur l'API dockerisée ; le cas body totalement absent (sans `Content-Type`) n'a pas été vérifié séparément (couvert indirectement par `PostEmptyBody_Returns201WithGameDto`).

---

### Revue 2 : GET /api/games/{id} — partie inconnue

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
  - Commit de l'implémentation : livré sur `main` (commit 4ffde0a).
- Après correction éventuelle : résultat avant / après : aucune correction nécessaire — comportement conforme dès la première implémentation.
- Limites et points non vérifiés : UUID mal formé dans l'URL (comportement framework `400`, non documenté dans swagger) ; cas `200` après POST couvert par test d'intégration uniquement.

---

### Revue 3 : Aucun tir après fin de partie

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
- Résultat réellement observé : les tests listés passent — le moteur lève `GameConflictException` dès `IsFinished()`, l'endpoint renvoie `409` sans toucher à l'état persisté.
- Décision et justification : la règle du référentiel (« aucun nouveau coup après la fin de partie ») est validée côté domaine et API ; on conserve la vérification dans `GameEngine` plutôt que dans l'endpoint seul, pour que toute voie d'appel (y compris `PlayComputerTurnAsync`) respecte la même invariante.
- Preuves reproductibles et liens vers les commits :
  - `./scripts/dotnet.sh test --filter "FullyQualifiedName~FireShot"`
  - [`BattleShip.API/Services/GameEngine.cs`](BattleShip.API/Services/GameEngine.cs) lignes 136–137
  - Commit de l'implémentation : livré sur `main` (commit 27f3630).
- Après correction éventuelle : résultat avant / après : tests ajoutés après revue de conformité — ils échoueraient si la garde `IsFinished()` était retirée ou déplacée après `ResolveShot` ou `ResolvePowerUpForShooterAsync`.
- Limites et points non vérifiés : les essais manuels `api.http` supposent une partie déjà terminée (non reproductibles en curl pur sans jouer jusqu'à la fin).

---

## Front

### Revue 4 : masquage des positions adverses non découvertes (MockGameApiClient)

- Proposition et référence dans le dépôt : `MockGameApiClient.GetOpponentBoardAsync`, `BattleShip.App/Services/MockGameApiClient.cs` (commit `ecc7d85`).
- Hypothèse à vérifier : une cellule de la grille adverse non encore ciblée par le joueur ne doit jamais révéler l'état `VisibleCellState.Ship`, quelle que soit la position réelle de la flotte ennemie (règle explicite de `swagger.yaml`).
- Scénario, données ou commande : relecture du corps de `GetOpponentBoardAsync` : pour chaque cellule, si `PlayerShotsAtComputer` ne la contient pas, la méthode retourne `Unknown` et passe à la cellule suivante (`continue`) sans jamais consulter la flotte adverse.
- Résultat attendu avant exécution : aucun chemin de code ne doit pouvoir atteindre `VisibleCellState.Ship` pour une cellule non ciblée.
- Erreur que ce contrôle pourrait détecter : une fuite d'information — une réécriture future pourrait naturellement calculer l'état puis le filtrer « après coup », et oublier ce filtre dans un cas limite réintroduirait la fuite.
- Résultat réellement observé : confirmé par lecture directe du code — la vérification `PlayerShotsAtComputer.Contains(cell)` intervient avant toute recherche dans la flotte, rendant l'état `Ship` structurellement inatteignable pour les cellules non ciblées, plutôt que dépendant d'un filtrage a posteriori.
- Décision et justification : acceptée sans modification — la construction structurelle (garde avant recherche) est plus robuste qu'un filtrage a posteriori et a été jugée suffisante.
- Preuves reproductibles et liens vers les commits : commit `ecc7d85` (`BattleShip.App/Services/MockGameApiClient.cs`, méthode `GetOpponentBoardAsync`).
- Après correction éventuelle : résultat avant / après : sans objet, aucune correction nécessaire.
- Limites et points non vérifiés : vérification faite par lecture de code, pas par exécution réelle (Docker indisponible dans cet environnement) ; aucun test automatisé n'a pu être ajouté, car `BattleShip.Tests` ne peut pas référencer `BattleShip.App` sans violer une contrainte non négociable de `PROMPT-ARCHITECTURE.md`.

### Revue 5 : compteur de tirs joués (StatusConsole / GameSession)

- Proposition et référence dans le dépôt : `GameSession.FireShotAsync`, `BattleShip.App/Services/GameSession.cs` (introduit au commit `b6a8ad6`, affiché par `StatusConsole.razor` au commit `94caa69`).
- Hypothèse à vérifier : après chaque tir valide, `Game.ShotCount` (affiché comme « Tirs joués » dans `StatusConsole`) augmente de 1.
- Scénario, données ou commande : lecture du code de `FireShotAsync` : `Game = Game with { Status = result.Status };` après chaque tir résolu.
- Résultat attendu avant exécution : `ShotCount` devrait refléter le nombre de tirs réellement joués par le joueur au fil de la partie.
- Erreur que ce contrôle pourrait détecter : un champ affiché à l'utilisateur qui reste figé malgré des actions répétées — une régression silencieuse, invisible tant qu'on ne regarde pas spécifiquement ce champ pendant une partie.
- Résultat réellement observé : le `with` expression ne met à jour que `Status`, jamais `ShotCount` ; `RefreshBoardsAsync` ne rafraîchit que les deux grilles, jamais l'objet `Game` lui-même. Le compteur reste donc bloqué à 0 pendant toute la partie, quel que soit le nombre de tirs joués — détecté lors de la revue finale de branche.
- Décision et justification : proposition initiale rejetée en l'état, adaptée : ajout de `ShotCount = Game.ShotCount + 1` dans le même `with` expression, cohérent avec la sémantique du champ (« nombre de coups valides joués par le joueur », documentée sur `GameDto`).
- Preuves reproductibles et liens vers les commits : défaut introduit au commit `b6a8ad6`, détecté lors de la revue finale de branche (diff `3a857af..9ba7bf6`), corrigé au commit `1d8dddb`.
- Après correction éventuelle : résultat avant / après : avant (`b6a8ad6`..`9ba7bf6`) — `ShotCount` reste à 0 quel que soit le nombre de tirs ; après (`1d8dddb`) — `ShotCount` s'incrémente de 1 à chaque tir résolu, cohérent avec `Session.History.Count`.
- Limites et points non vérifiés : la correction n'a été vérifiée que par relecture du diff, pas par exécution réelle dans le navigateur (Docker indisponible) — à confirmer visuellement en jouant une partie complète avant remise.

---

### Revue 6 : conservation de UseMockApi dans l'image Docker de production

- Proposition et référence dans le dépôt : `BattleShip.App/Dockerfile`, étape `RUN printf ... > BattleShip.App/wwwroot/appsettings.json` (introduite au commit `a76a365`).
- Hypothèse à vérifier : construire l'image Docker de `BattleShip.App` ne doit pas altérer la configuration `UseMockApi` de `wwwroot/appsettings.json` — l'objectif affiché de cette configuration est qu'il suffise de la basculer à `false` pour pointer vers la vraie API, y compris en déploiement Docker.
- Scénario, données ou commande : lecture du `Dockerfile` et comparaison entre le contenu de `wwwroot/appsettings.json` versionné dans le dépôt (`{"ApiBaseUrl": "...", "UseMockApi": true}`) et le contenu réellement généré par l'étape `RUN printf` au moment du build.
- Résultat attendu avant exécution : le fichier généré dans l'image devrait contenir les deux clés, comme le fichier versionné.
- Erreur que ce contrôle pourrait détecter : une configuration documentée comme « le seul changement nécessaire » qui ne survit pas en pratique au pipeline de build — un décalage entre la documentation et le comportement réel du système.
- Résultat réellement observé : l'instruction `printf '%s\n' "{\"ApiBaseUrl\":\"${ApiBaseUrl}\"}" > ...` n'écrit que la clé `ApiBaseUrl` ; la clé `UseMockApi` est absente de toute image construite via Docker, quel que soit le contenu du fichier versionné. Le comportement observable (le mock continue de fonctionner) masquait le défaut, car `GetValue("UseMockApi", true)` retombe sur `true` par défaut en l'absence de la clé.
- Décision et justification : proposition initiale rejetée en l'état, adaptée : ajout d'un `ARG UseMockApi=true` et modification du `printf` pour écrire les deux clés (valeur booléenne JSON, non entre guillemets), puis ajout de l'argument correspondant dans `docker-compose.yml` (`app.build.args`).
- Preuves reproductibles et liens vers les commits : défaut présent de `a76a365` à `9ba7bf6` ; corrigé au commit `d04ddb2`. Reproductible en comparant la sortie de l'étape `RUN printf` du `Dockerfile` avant et après ce commit.
- Après correction éventuelle : résultat avant / après : avant — `wwwroot/appsettings.json` généré ne contient que `ApiBaseUrl` ; après — il contient `ApiBaseUrl` et `UseMockApi` (valeur pilotée par l'argument de build, `true` par défaut).
- Limites et points non vérifiés : correction vérifiée par relecture du `Dockerfile` et de `docker-compose.yml`, pas par une construction Docker réelle (indisponible dans cet environnement) — à valider avec `docker compose build app` avant remise.

---

### Revue 7 : style du bandeau de navigation (portée CSS de Blazor)

- Proposition et référence dans le dépôt : `BattleShip.App/Layout/NavMenu.razor.css`, règle `.war-room__brand-link` (introduite au commit `cfa983e`).
- Hypothèse à vérifier : les déclarations CSS de `.war-room__brand-link` (police, couleur, alignement) s'appliquent bien au lien de marque affiché dans l'en-tête, toujours visible.
- Scénario, données ou commande : relecture croisée de `NavMenu.razor` (qui applique `class="war-room__brand-link"` sur le composant `<NavLink>`) et du mécanisme d'isolation CSS de Blazor (chaque fichier `.razor.css` ne porte que sur les éléments rendus directement dans le fichier `.razor` associé).
- Résultat attendu avant exécution : la règle devrait s'appliquer à l'élément `<a>` visible dans le navigateur.
- Erreur que ce contrôle pourrait détecter : une classe CSS définie mais jamais appliquée en pratique à cause d'un mécanisme de portée spécifique au framework — un défaut invisible dans le code source, visible seulement à l'écran.
- Résultat réellement observé : `<NavLink>` génère lui-même son propre élément `<a>` racine ; l'attribut de portée injecté par l'isolation CSS de Blazor ne s'y propage pas sans le combinateur `::deep`. La règle `.war-room__brand-link` ne correspond donc à rien une fois compilée : le lien de marque s'affiche avec le style par défaut du navigateur au lieu du style « brass », alors que `.war-room__brand-mark` (un `<span>` écrit directement dans `NavMenu.razor`) est bien stylé, lui.
- Décision et justification : proposition initiale rejetée en l'état, adaptée : sélecteur changé en `.war-room__header ::deep .war-room__brand-link`, seule modification apportée (aucune propriété du bloc de déclaration n'a changé).
- Preuves reproductibles et liens vers les commits : défaut présent de `cfa983e` à `9ba7bf6` ; corrigé au commit `5cee62d`. Reproductible en inspectant le CSS compilé (`BattleShip.App.styles.css`) avant/après ce commit pour confirmer la présence du sélecteur `::deep`.
- Après correction éventuelle : résultat avant / après : avant — la règle ne correspond à aucun élément rendu ; après — la règle cible correctement l'élément `<a>` rendu par `<NavLink>`.
- Limites et points non vérifiés : correction vérifiée par relecture du CSS, pas par un rendu réel dans un navigateur (Docker indisponible dans cet environnement) — à confirmer visuellement avant remise.
