# Revues de propositions IA

Quatre revues argumentées (trois minimum demandé). Aucune erreur n'est exigée ; chaque conclusion est étayée par une référence de commit vérifiable.

## Revue : masquage des positions adverses non découvertes (MockGameApiClient)

- Proposition et référence dans le dépôt : `MockGameApiClient.GetOpponentBoardAsync`, `BattleShip.App/Services/MockGameApiClient.cs` (commit `ecc7d85`).
- Hypothèse à vérifier : une cellule de la grille adverse non encore ciblée par le joueur ne doit jamais révéler l'état `VisibleCellState.Ship`, quelle que soit la position réelle de la flotte ennemie (règle explicite de `swagger.yaml`).
- Scénario, données ou commande : relecture du corps de `GetOpponentBoardAsync` : pour chaque cellule, si `PlayerShotsAtComputer` ne la contient pas, la méthode retourne `Unknown` et passe à la cellule suivante (`continue`) sans jamais consulter la flotte adverse.
- Résultat attendu avant exécution : aucun chemin de code ne doit pouvoir atteindre `VisibleCellState.Ship` pour une cellule non ciblée.
- Erreur que ce contrôle pourrait détecter : une fuite d'information — une réécriture future pourrait naturellement calculer l'état puis le filtrer « après coup », et oublier ce filtre dans un cas limite réintroduirait la fuite.
- Résultat réellement observé : confirmé par lecture directe du code — la vérification `PlayerShotsAtComputer.Contains(cell)` intervient avant toute recherche dans la flotte, rendant l'état `Ship` structurellement inatteignable pour les cellules non ciblées, plutôt que dépendant d'un filtrage a posteriori.
- Décision et justification : acceptée sans modification — la construction structurelle (garde avant recherche) est plus robuste qu'un filtrage a posteriori et a été jugée suffisante.
- Preuves reproductibles et liens vers les commits : commit `ecc7d85` (`BattleShip.App/Services/MockGameApiClient.cs`, méthode `GetOpponentBoardAsync`).
- Après correction éventuelle : résultat avant / après : sans objet, aucune correction nécessaire.
- Limites et points non vérifiés : vérification faite par lecture de code, pas par exécution réelle (Docker indisponible dans cet environnement) ; aucun test automatisé n'a pu être ajouté, car `BattleShip.Tests` ne peut pas référencer `BattleShip.App` sans violer une contrainte non négociable de `PROMPT-INIT.md`.

## Revue : compteur de tirs joués (StatusConsole / GameSession)

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

## Revue : conservation de UseMockApi dans l'image Docker de production

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

## Revue : style du bandeau de navigation (portée CSS de Blazor)

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
