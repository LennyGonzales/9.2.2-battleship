# Échanges décisifs avec l'IA

## 2026-09-15 — Cadrage et lancement du front-end (radar/WW2)

- Outil / modèle si connu : Claude Code, Claude Sonnet 5
- Contexte : `BattleShip.App` ne contenait que le squelette par défaut de `dotnet new blazorwasm` (pages Home/Counter/Weather) ; `BattleShip.API` n'implémentait encore aucun endpoint métier, seul `swagger.yaml` documentait le contrat cible.
- Prompt réellement utilisé : « fait uniquement le front-end. Base toi sur PROMPT-INIT.md et swagger.yaml. Fait des commits atomic. Je veux un jolie front, avec style armée, radar, bateau, WW2 »
- Réponse et hypothèses résumées : proposition de construire le client HTTP réel contre `swagger.yaml`, complété par un moteur de jeu en mémoire (`MockGameApiClient`) derrière la même interface `IGameApiClient`, pour que l'interface soit jouable avant l'implémentation du vrai backend ; thème visuel « War Room / Radar » (palette militaire, police stencil, effets de balayage radar) ; découpage en 9 tâches avec un commit atomique par tâche.
- Décision : approche « contrat réel + repli mock » retenue via une question de clarification explicite (voir la revue « choix d'architecture » ci-dessous) ; le thème et le découpage en tâches ont été validés section par section avant écriture du plan.
- Vérification : exécution du plan `docs/superpowers/plans/2026-09-15-radar-frontend.md` tâche par tâche, avec relecture (conformité + qualité) après chaque tâche, puis revue finale de l'ensemble de la branche.
  - Résultat attendu : un front jouable de bout en bout (créer une partie, tirer, gagner/perdre) sans backend réel.
  - Résultat observé : les 9 tâches et la revue finale ont validé la chaîne complète (`RadarGrid` → `GameSession` → `IGameApiClient` → `MockGameApiClient` → DTOs) ; la revue finale a détecté et fait corriger 6 anomalies d'intégration (voir REVUE-IA-FRONT.md).
  - Erreur que ce contrôle pourrait détecter : une rupture de contrat (DTO qui ne correspond plus à `swagger.yaml`) ou une rupture de la chaîne UI → service lors d'une tâche ultérieure.
- Preuves reproductibles et limites : commits `3fde4b9`..`9ba7bf6` sur la branche `front-end` (9 tâches), puis `1d8dddb`..`de28cd7` (corrections de la revue finale). Limite : aucune commande `docker compose up --build` n'a pu être exécutée dans cet environnement (Docker absent) — la vérification est restée statique ; à rejouer manuellement avant remise.

## 2026-09-15 — Choix d'architecture : contrat réel vs repli mock local

- Outil / modèle si connu : Claude Code, Claude Sonnet 5
- Contexte : au moment de cadrer le front, `BattleShip.API` ne possède aucun endpoint métier (seulement le modèle `weatherforecast` par défaut) — un front branché uniquement sur `swagger.yaml` ne serait pas démontrable en l'état.
- Prompt réellement utilisé : question de clarification posée avant la conception — « Les vrais endpoints n'existent pas encore. Comment le front doit-il gérer ça ? », avec trois options proposées (contrat réel seul / mock local seul / contrat réel + repli mock local).
- Réponse et hypothèses résumées : l'option retenue (« Wire to real contract, add local mock fallback ») suppose qu'une interface unique `IGameApiClient` peut être implémentée à la fois par un client HTTP fidèle au contrat et par un moteur en mémoire, et qu'un seul paramètre de configuration (`UseMockApi`) suffirait à basculer de l'un à l'autre sans toucher à l'UI.
- Décision : acceptée. Elle permet une démonstration jouable dès maintenant tout en gardant le code prêt pour la vraie API, sans double implémentation de l'UI.
- Vérification : relecture du code (`MockGameApiClient.cs`, `HttpGameApiClient.cs`, `Program.cs`) pour confirmer que les deux implémentations respectent exactement les mêmes signatures et que le switch DI est bien un changement d'une ligne.
  - Résultat attendu : aucune divergence de signature entre les deux implémentations, bascule par une seule ligne de configuration.
  - Résultat observé : confirmé par la relecture de la tâche 4 — mais la revue finale a découvert que le `Dockerfile` de l'image de production écrasait `wwwroot/appsettings.json` et supprimait silencieusement la clé `UseMockApi` du build : l'hypothèse « un seul paramètre suffit » était donc fausse en pratique pour un déploiement Docker.
  - Erreur que ce contrôle pourrait détecter : une divergence de contrat entre les deux implémentations d'`IGameApiClient`, ou une configuration qui ne survit pas au build de production.
- Preuves reproductibles et limites : décision mise en œuvre aux commits `ecc7d85` (mock) et `a76a365` (HTTP + switch DI) ; défaut de configuration Docker découvert et corrigé au commit `d04ddb2` (voir REVUE-IA-FRONT.md). Limite : la bascule réelle vers `UseMockApi=false` n'a pas pu être testée contre une vraie API, puisque `BattleShip.API` ne l'implémente pas encore.

## 2026-09-15 — Revue finale de branche (prompt de vérification globale)

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

## 2026-09-16 — Refus Bootstrap : thème radar custom

- Outil / modèle si connu : Claude Code, Claude Sonnet 5
- Contexte : le squelette `dotnet new blazorwasm` embarque Bootstrap par défaut (`wwwroot/lib/bootstrap`, styles `.btn-primary`, etc.). Le plan [`docs/superpowers/plans/2026-09-15-radar-frontend.md`](docs/superpowers/plans/2026-09-15-radar-frontend.md) visait une identité visuelle « War Room / radar WW2 ».
- Prompt réellement utilisé : « je veux un jolie front, avec style armée, radar, bateau, WW2 » — puis validation section par section du plan (palette militaire, polices stencil, grilles radar).
- Réponse et hypothèses résumées : l'IA a proposé de conserver Bootstrap pour les composants de base et d'ajouter une couche thème par-dessus. Alternative : `theme.css` + `app.css` allégé (erreurs Blazor, chargement), sans lien vers Bootstrap dans `index.html`.
- Décision : **refusée** — conservation de Bootstrap comme socle UI. **Retenu** — thème entièrement custom (`wwwroot/css/theme.css`, tokens `--br-*`, composants `.btn-brass`, `.radar-panel`). *Apprentissage* : pour une identité visuelle forte, partir du template par défaut complique plus qu'un thème dédié ; le plan a explicitement demandé de retirer les règles Bootstrap-orientées de `app.css`.
- Vérification : relecture de [`BattleShip.App/wwwroot/index.html`](BattleShip.App/wwwroot/index.html) (pas de `<link>` Bootstrap) et de [`BattleShip.App/wwwroot/css/theme.css`](BattleShip.App/wwwroot/css/theme.css) (tokens et primitives partagées).
  - Résultat attendu : boutons et panneaux stylés via `btn-brass` / `radar-panel`, pas `.btn-primary`.
  - Résultat observé : thème cohérent sur Briefing, Battle, NavMenu ; `app.css` limité au mécanique Blazor (spinner, error UI).
- Preuves reproductibles et limites : plan `2026-09-15-radar-frontend.md` (étape « Trim app.css »), commits branche `front-end`. *Limite* : `lib/bootstrap` peut subsister dans `wwwroot` sans être référencé — non bloquant.

## 2026-09-16 — Refus animations et sons trop chargés

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

## 2026-09-17 — Refus boutons de test gRPC : barre de recherche Mission

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
