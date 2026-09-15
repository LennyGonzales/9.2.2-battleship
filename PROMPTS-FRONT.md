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
