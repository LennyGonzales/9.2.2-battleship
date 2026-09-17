# ADR 0002 : Stockage de l'état de partie

## Statut et date

Accepté — 2026-03-15

## Contexte

L'API doit conserver l'état de chaque partie (grilles, flottes, tour courant, tokens joueur) entre les
requêtes HTTP, qui sont sans état par nature. Le référentiel n'impose pas de technologie de persistance ; le
projet est un TP noté sur une durée courte (remise jour 5), sans exigence de durabilité au-delà du process en
cours d'exécution. `BattleShip.Models` définit l'interface `IGameRepository` (`SaveAsync`, `GetByIdAsync`)
pour que le choix de stockage reste substituable sans toucher au moteur de jeu (voir [ADR 0001](0001-couches-architecture.md)).

## Options envisagées

1. **Base de données relationnelle (SQLite / PostgreSQL) via EF Core** — Persistance durable entre
   redémarrages, mais ajoute un schéma, des migrations et une dépendance externe pour un besoin
   (parties en cours, jamais consultées après coup) qui ne le justifie pas dans le périmètre du TP.
2. **Fichier JSON sur disque** — Simple à inspecter, mais introduit des problèmes de concurrence d'écriture
   (plusieurs requêtes PvP simultanées sur la même partie) sans bénéfice réel puisque rien n'exige que les
   parties survivent à un redémarrage du conteneur.
3. **Dictionnaire concurrent en mémoire (`ConcurrentDictionary<Guid, Game>`), un seul process API** — Aucune
   dépendance externe, écritures thread-safe par clé, latence nulle ; les parties sont perdues au redémarrage
   du conteneur `api`, ce qui est acceptable car une partie non terminée peut être recréée par les joueurs.

## Décision

Option 3 retenue : `InMemoryGameRepository` (`BattleShip.API/Services/InMemoryGameRepository.cs`) implémente
`IGameRepository` avec un `ConcurrentDictionary<Guid, Game>` interne, enregistré en `Singleton` dans le
conteneur de dépendances (une seule instance pour la durée de vie de l'application, cohérente avec l'état
partagé entre toutes les requêtes d'une même partie).

- `SaveAsync` écrase l'entrée existante par `Id` (upsert), `GetByIdAsync` retourne `null` si la partie est
  inconnue — traduit en `404 Not Found` par les endpoints.
- Aucune éviction ni TTL : le volume de parties d'un TP reste négligeable, et le process est redémarré entre
  les sessions de test/démo.
- Le choix reste isolé derrière `IGameRepository` : remplacer l'implémentation (ex. Redis en cas de scale-out
  multi-instance) ne toucherait ni `GameEngine`, ni les endpoints, ni les tests du moteur.

## Conséquences

- Aucune migration, aucun conteneur de base de données supplémentaire dans `docker-compose.yml`.
- Les parties ne survivent pas à un redémarrage ou à un déploiement multi-instance (pas de scale-out
  horizontal possible sans changer l'implémentation) : acceptable pour le périmètre évalué, à documenter
  comme limite connue dans le README si le sujet est posé en soutenance.
- Les tests d'intégration (`WebApplicationFactory<Program>`) obtiennent un repository propre à chaque
  factory, ce qui simplifie l'isolation des tests sans nettoyage explicite.

## Vérification et réexamen

```bash
./scripts/dotnet.sh test --filter "FullyQualifiedName~InMemoryGameRepository"
./scripts/dotnet.sh test --filter "FullyQualifiedName~GetGame"
# Partie inconnue après redémarrage du conteneur api : comportement attendu, pas un bug
docker compose restart api
curl -i http://localhost:8080/api/games/<id-cree-avant-redemarrage>   # attendu : 404
```

À revoir si le backlog ajoute une exigence de persistance entre redémarrages (sauvegarde/historique de
parties, cf. diapo 60 du référentiel) : remplacer `InMemoryGameRepository` par une implémentation durable de
`IGameRepository`, sans changer son contrat.

## Références

- Référentiel C# / ASP.NET Core, diapo 36 (« Spécification 1 — Le moteur de jeu ») et diapo 60 (backlog)
- [`BattleShip.Models/Services/IGameRepository.cs`](../../BattleShip.Models/Services/IGameRepository.cs)
- [`BattleShip.API/Services/InMemoryGameRepository.cs`](../../BattleShip.API/Services/InMemoryGameRepository.cs)
- [ADR 0001](0001-couches-architecture.md) — graphe de dépendances justifiant l'interface substituable
