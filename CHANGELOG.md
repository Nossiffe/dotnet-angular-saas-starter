# Changelog

Format : [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/) + Conventional Commits.
Convention spécifique : chaque entrée de release comporte une rubrique **Migrations DB**
(même vide : « aucune ») — impossible d'oublier un `database update` au déploiement.

## [Unreleased]

### Added
- Timeouts de requête globaux (`RequestTimeouts:DefaultSeconds`, 30 s par défaut) et output cache câblé opt-in strict (aucune policy par défaut — activation explicite par endpoint via `.CacheOutput(...)`).

### Fixed
- `OutboxProcessor` utilise `IDateTimeProvider` (testabilité).

### Migrations DB
- (aucune)
