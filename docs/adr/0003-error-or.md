# ADR 0003 — Erreurs métier : `ErrorOr` (2.1.1) plutôt qu'un `Result` maison ou des exceptions

- **Statut** : Accepté (2026-07-12)
- **Contexte** : les erreurs métier (règle violée, ressource introuvable, conflit) étaient
  portées par des exceptions custom (`NotFoundException`, `DomainException`) levées dans les
  handlers et converties en `ProblemDetails` par le `GlobalExceptionHandler`. Le flot de
  contrôle par exception pour un cas métier attendu (pas exceptionnel) coûte en lisibilité
  (le retour d'un handler ne dit rien de ses échecs possibles), en performance (stack unwinding),
  et complique l'agrégation de **plusieurs** erreurs (ex. plusieurs champs invalides d'un coup).

## Décision

Adopter **[ErrorOr](https://github.com/amantinband/error-or) 2.1.1** comme type de retour
des handlers Mediator pour toute erreur métier : `ICommand<ErrorOr<T>>` / `IQuery<ErrorOr<T>>`.

Retenu pour :
- **Standard communautaire** établi (plutôt qu'un `Result<T>` maison) — principe retenu :
  « standards d'abord » plutôt que réinventer un type de résultat.
- **Erreurs multiples** natives (`List<Error>`) — utile pour agréger, pas seulement retourner
  la première erreur trouvée.
- **Conversions implicites** `Error → ErrorOr<T>` et `T → ErrorOr<T>` — un handler retourne
  `ProductErrors.NotFound;` ou `product.Id.Value;` sans wrapper explicite, le code reste lisible.
- **Sentinels** pour le cas void : `Result.Success` / `Result.Updated` / `Result.Deleted`
  (types `Success`/`Updated`/`Deleted`) — un handler d'update retourne `ErrorOr<Updated>`
  plutôt qu'un `ErrorOr<Unit>` ou un `bool` peu explicite.

**Alternatives écartées** :
- **`Result<T>` maison** : envisagé en premier, écarté — préférer
  un standard maintenu et déjà connu de l'écosystème .NET (issues, docs, exemples) plutôt que
  du code interne à maintenir et documenter soi-même sans bénéfice fonctionnel réel.
- **[FluentResults](https://github.com/altmann/FluentResults)** : alternative sérieuse et
  populaire, mais modèle d'erreur plus générique (`IError` libre, pas de `ErrorType` structuré
  par défaut) — `ErrorOr.ErrorType` donne directement l'enum (`Validation`/`NotFound`/
  `Conflict`/`Forbidden`/`Unauthorized`/`Failure`/`Unexpected`) sur lequel le mapping HTTP
  (`ErrorOrHttpExtensions`) s'appuie sans couche de traduction supplémentaire.

## Frontière forme / métier : pourquoi `ValidationException` survit

`ErrorOr` remplace les exceptions **métier** (règle de domaine violée, ressource absente,
conflit) — il ne remplace pas la validation de **forme** de la requête. Le pipeline
`ValidationBehavior<,>` (FluentValidation) continue de lever `ValidationException`, convertie
par le `GlobalExceptionHandler` en `ValidationProblemDetails` avec un dictionnaire
`{ champ → [messages] }`. Ce format **par champ** n'a pas d'équivalent naturel dans
`ErrorOr<T>` (qui porte une liste d'`Error` globales, pas indexées par propriété) : le
reconstruire dans `ErrorOr` demanderait de dupliquer ce que FluentValidation fait déjà bien.
Les deux mécanismes coexistent donc, chacun sur sa frontière :

| Couche | Mécanisme | Quand |
|---|---|---|
| Forme (validation d'entrée) | `ValidationException` (pipeline) | Champ manquant/mal formé — erreurs Identity field-tied |
| Métier (règle de domaine) | `ErrorOr<T>` | Ressource absente, règle violée, conflit d'état |
| Infrastructure (contrainte DB) | `DbUpdateException`/`DbUpdateConcurrencyException` | Race condition au-delà du check applicatif (ex. 23505 en défense en profondeur) |

## Classes d'erreurs centralisées (`XxxErrors`)

Pattern recommandé par ErrorOr lui-même : chaque erreur métier est un membre statique d'une
classe `XxxErrors` (`ProductErrors`, `OrganizationErrors`, `InvitationErrors`, `AuthErrors`)
— jamais un `Error.X(...)` construit inline dans un handler. Ça donne un **registre
grep-able** (`grep -rn 'static Error ' src/ --include='*Errors.cs'`), un point unique pour
relire les messages exposés à l'utilisateur, et un test de contrat (`DomainErrorsTests`)
qui vérifie que chaque erreur a le bon `ErrorType`, un `Code` stable et une `Description`
correcte. Détail complet du registre (16 entrées) : `docs/error-codes.md`.

**Contrat HTTP touché par cette décision** : `InvitationErrors.AlreadyPending` (« invitation déjà
en attente ») est passée de **400** (`Validation`) à **409** (`Conflict`) — alignée avec la
branche `DbUpdateException` (contrainte d'unicité Postgres 23505) du `GlobalExceptionHandler`,
qui protège la même règle en cas de race entre le check applicatif et l'insert. Les deux
chemins (check applicatif `ErrorOr` et contrainte DB en filet de sécurité) renvoient
maintenant le même code HTTP pour la même situation métier.

## Dépendance `ErrorOr` dans `Domain`

Les classes `XxxErrors` vivent dans `Domain` (aux côtés des agrégats qui lèvent ces erreurs
dans leurs factories) et `Application` (erreurs transverses comme `AuthErrors`). `Domain`
référence donc le package `ErrorOr`. C'est une exception assumée à la règle « `Domain` pur,
zéro dépendance framework » — mais `ErrorOr` n'est **pas** un framework applicatif : c'est
une librairie pure (aucune dépendance ASP.NET/EF, pas d'IO, pas de DI requise), au même titre
que `Vogen` (strongly-typed IDs, déjà dans `Domain`) ou `Mediator.Abstractions`
(marker interfaces `ICommand<T>`/`IQuery<T>`, déjà dans `Application`). Le test d'architecture
`DependencyRuleTests` continue de bloquer toute dépendance EF/ASP.NET dans `Domain` ;
`ErrorOrConventionTests.Aucune_exception_metier_custom_dans_Domain` bloque le retour en
arrière vers des exceptions custom.

## Asymétrie des factories : `ErrorOr` pour les règles métier, `ArgumentException` pour les gardes techniques

Une factory d'agrégat peut échouer pour deux raisons de nature différente :

- **Règle métier** (fait partie du domaine, doit remonter proprement à l'appelant/l'API) :
  `Invitation.Create(...)` retourne `ErrorOr<Invitation>` — ex. un rôle `Owner` n'est pas
  invitable (`InvitationErrors.OwnerRole`). L'appelant (handler) est censé gérer ce cas.
- **Garde technique** (contrat de programmation violé, ne devrait jamais arriver en usage
  normal — bug appelant) : `Product.Rename(name)` lève `ArgumentException` si `name` est
  vide. Ce n'est pas une règle métier à faire remonter à l'utilisateur final ; c'est un
  invariant de contrat, au même titre qu'un `ArgumentNullException.ThrowIfNull`. Le
  `GlobalExceptionHandler` le convertit en 500 (bug à corriger côté appelant, pas une
  réponse attendue de l'API).

Cette asymétrie est volontaire : elle évite de faire porter à `ErrorOr` des cas qui ne sont
jamais censés se produire en usage normal (ce qui alourdirait chaque appelant d'un `switch`
sur des erreurs impossibles), tout en gardant les vraies règles métier dans le chemin
`ErrorOr` explicite et testé.

## Conséquences

- Le contrat de retour d'un handler dit explicitement quelles erreurs il peut produire
  (`ErrorOr<T>`) — plus lisible qu'un `throw` caché dans le corps de la méthode.
- Un seul point de mapping HTTP (`ErrorOrHttpExtensions.ToHttpResult`/`ToNoContentHttpResult`/
  `ToCreatedHttpResult`) pour tous les `ErrorType` — cohérent par construction.
- Deux mécanismes d'erreur coexistent (`ErrorOr` métier, exceptions forme/infra) — documenté
  ci-dessus pour éviter la confusion ; vérifié par `ErrorOrConventionTests`.
- Contrat HTTP changé pour l'invitation déjà en attente (400 → 409) — un client de cet
  endpoint doit traiter le 409.

## Alternatives écartées

- **`Result<T>` maison** : voir « Décision » — écarté en faveur d'un standard.
- **FluentResults** : voir « Décision » — `ErrorType` moins structuré nativement pour le
  mapping HTTP direct.
- **Garder les exceptions custom** (`NotFoundException`/`DomainException`) : conservait le
  flot de contrôle par exception pour des cas attendus, et rendait le contrat de retour des
  handlers muet sur leurs échecs possibles. Écarté.
