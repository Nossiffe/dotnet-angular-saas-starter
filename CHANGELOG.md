# Changelog

Format : [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/) + Conventional Commits.
Convention spécifique : chaque entrée de release comporte une rubrique **Migrations DB**
(même vide : « aucune ») — impossible d'oublier un `database update` au déploiement.

## [Unreleased]

### Added
- Timeouts de requête globaux (`RequestTimeouts:DefaultSeconds`, 30 s par défaut) et output cache câblé opt-in strict (aucune policy par défaut — activation explicite par endpoint via `.CacheOutput(...)`).
- Cache déclaratif `[CachedQuery]` : le `CachingBehavior` du pipeline Mediator cache les queries opt-in dans HybridCache avec clé et tags scopés au tenant (invalidation logique par tag sur write) ; la slice Products l'adopte.

### Changed
- Stack de test basculée sur le runner natif **Microsoft Testing Platform** (MTP) : `global.json` §test + `UseMicrosoftTestingPlatformRunner` sur xunit.v3, retrait de VSTest (`Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`) et de `coverlet.collector`. Couverture désormais produite par `Microsoft.Testing.Extensions.CodeCoverage` (`dotnet test --coverage --coverage-output-format cobertura`) ; step CI et résumé de couverture adaptés (fichiers `<guid>.cobertura.xml`).

### Fixed
- `OutboxProcessor` utilise `IDateTimeProvider` (testabilité).

### Migrations DB
- (aucune)
