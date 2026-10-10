# Changelog

Format : [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/) + Conventional Commits.
Convention spécifique : chaque entrée de release comporte une rubrique **Migrations DB**
(même vide : « aucune ») — impossible d'oublier un `database update` au déploiement.

## [Unreleased]

### Added
- Timeouts de requête globaux (`RequestTimeouts:DefaultSeconds`, 30 s par défaut).
- Cache HybridCache de la lecture d'un produit, avec clé et tag propres au tenant, invalidé à la modification et à la suppression.
- Tests Playwright de la page de connexion, API simulée, avec un contrôle axe des règles WCAG 2.2 A et AA : toute violation détectée fait échouer le test (axe ne vérifie qu'une partie des critères WCAG).
- Job Hangfire quotidien `purge-processed-outbox-messages` : supprime les messages de l'outbox traités depuis plus de 7 jours.
- Mutation testing Stryker 5 (runner MTP) : seuil bloquant sur le Domain, score mesuré sur l'Application.
- Quickstart rejouable de bout en bout : API lancée par `dotnet run`, clé JWT en user-secrets, API conteneurisée derrière le profil Compose `full`.
- Contrat OpenAPI écrit au build dans `web-angular/openapi.json` et comparé par la CI au fichier versionné.
- Tests unitaires des validateurs et des handlers qui portent une logique ; `ErrorRegistryTests` compare `docs/error-codes.md` au code.
- `SECURITY.md` : signalement privé des vulnérabilités.
- Page d'acceptation d'une invitation (`/accept-invitation`), où mène le lien de l'e-mail : premier appel sans mot de passe, puis demande d'un mot de passe si aucun compte n'existe pour l'adresse invitée. Message neutre pour une invitation invalide, expirée, déjà acceptée ou révoquée. Testée par Vitest, et par Playwright avec l'API simulée et un contrôle axe.

### Changed
- Tests sur le runner natif Microsoft Testing Platform (xunit.v3) ; couverture produite par `Microsoft.Testing.Extensions.CodeCoverage` (format Cobertura).
- Angular : retrait de `withFetch()`, de `provideAnimationsAsync()` et du package `@angular/animations`, inutiles en v22.
- Actions GitHub mises à jour, épinglées par SHA avec leur version exacte en commentaire.
- Dependabot : mises à jour groupées, délai de 7 jours avant de proposer une version, montées majeures d'Angular et de TypeScript écartées (TypeScript suit la version exigée par Angular).
- Workflows déclenchés sur `main` uniquement.
- Domain sans framework : `ApplicationUser` et `RefreshToken` passent dans l'Infrastructure, `IDomainEvent` ne dépend plus de Mediator. Les handlers de domain events implémentent `IDomainEventHandler<TEvent>`.
- Outbox : une transaction par message, lu avec `FOR UPDATE SKIP LOCKED` ; chaque handler dans son propre scope ; handlers déjà passés non rappelés lors d'un nouvel essai (`OutboxMessageConsumers`) ; nouvel essai après 1, 4, 16 puis 64 minutes, puis abandon journalisé en erreur ; un arrêt de l'application ne compte pas de tentative et ne journalise pas d'erreur. L'e-mail de bienvenue n'avale plus les erreurs SMTP : il est retenté.
- Refus métier (e-mail non confirmé, lien de réinitialisation invalide, changement de son propre rôle, mot de passe manquant à l'acceptation) renvoyés en erreurs ErrorOr au lieu de `ValidationException`.
- Contrat HTTP : contrats de requête dédiés au lieu des commandes de l'Application ; Bearer exigé seulement sur les routes authentifiées ; pas de `Location` vers une route inexistante.
- Empreintes de tokens en hexadécimal minuscule partout : les refresh tokens existants ne sont plus reconnus, une reconnexion est nécessaire.
- Docker Compose utilise son réseau par défaut : le réseau externe `devnet`, à créer à la main avant le premier `docker compose up`, disparaît du Quickstart, du smoke test et de `scripts/check.sh`.
- Les avis de sécurité NuGet (NU1901 à NU1904) restent des warnings au lieu de faire échouer `dotnet build` : un avis publié du jour au lendemain n'empêche plus de compiler un clone neuf. Une source d'avis injoignable (NU1900) ne bloque plus le build non plus. `deps-audit.yml` reste la barrière bloquante pour High/Critical.
- Résumé de couverture de la CI produit par ReportGenerator (outil local) : les rapports des tests unitaires et d'intégration sont réunis ligne par ligne, hors migrations EF et code généré. Le résumé précédent additionnait les deux rapports et affichait 47,5 %.
- Analyseurs Meziantou, SonarAnalyzer et Roslynator en dernière version : les mises à jour NuGet de Dependabot échouaient faute d'espace disque, en téléchargeant une à une les versions intermédiaires.
- En Development, les commandes SQL d'EF ne sont plus journalisées (catégorie `Microsoft.EntityFrameworkCore.Database.Command` en Warning) : le premier lancement affichait environ 240 lignes de SQL avant « Now listening ». Une commande en échec reste journalisée.
- Runners CI fixés sur `ubuntu-24.04` au lieu de `ubuntu-latest` : une nouvelle image ne change plus l'environnement de la CI sans commit.
- E-mails d'invitation envoyés par l'outbox (`SendInvitationEmailHandler`, sur `MemberInvitedDomainEvent` et `InvitationResentDomainEvent`) au lieu d'un job Hangfire enfilé après le commit : le jeton du lien est créé à l'envoi et son hash enregistré avant que l'e-mail parte. Livraison au moins une fois : un doublon remplace le lien précédent. Un renvoi rend l'ancien lien caduc aussitôt (ADR 0005).
- Acceptation d'une invitation : statut et expiration vérifiés avant de demander un mot de passe à un invité sans compte.
- `Program.cs` découpé en méthodes d'extension nommées (`src/Api/Hosting/`) : observabilité, limitation de débit, base de données au démarrage, serveur Hangfire, pipeline HTTP. Les explications longues passent dans `ARCHITECTURE.md` et `DEPLOYMENT.md`.
- Création d'un produit par `ToCreatedHttpResult`, comme celle d'une invitation. Le `Location` des deux réponses 201 est calculé depuis une route nommée au lieu d'une URL écrite à la main : `/api/v1/...` comme avant, et il reprend désormais la version écrite dans la requête (`/api/v1.0/...` pour une requête en `v1.0`).
- Réponses d'erreur métier (ErrorOr) : l'extension `errors` liste toutes les erreurs du résultat, par code, comme le prévoit l'ADR 0003 ; le statut, `title` et `code` restent ceux de la première erreur. La page d'acceptation d'une invitation distingue ces erreurs, qui portent un `code`, des erreurs de saisie.

### Fixed
- gitleaks : la configuration du dépôt remplaçait les règles par défaut et ne détectait plus rien ; elle les étend maintenant. Le hook pre-commit bloque dès que gitleaks est installé.
- Job gitleaks en échec sur les pull requests (permission `pull-requests: read` manquante).
- `.env.example` : nom de variable SMTP corrigé (`Email__UseSslOnConnect`), variables inutilisées retirées.
- `OutboxProcessor` utilise `IDateTimeProvider` (testabilité).
- Une erreur de validation produisait trois logs d'erreur, dont un faux 500 ; elle est maintenant journalisée une fois, en 400.
- Les logs n'atteignaient pas le provider OpenTelemetry : Serilog les transmet maintenant aux providers enregistrés.
- Premier lancement sur une base vide : la migration automatique de développement ne journalise plus d'erreur EF (`Failed executing DbCommand`).
- Sur un clone neuf, `npm start` échouait (`Cannot find module '../../api/generated'`) : `npm start`, `npm test`, `npm run test:coverage` et `npm run watch` génèrent maintenant les types de l'API avant de démarrer.
- Le build ne démarre plus de serveur Hangfire pendant la génération OpenAPI : une dizaine de lignes de log, dont deux avertissements, à chaque build.
- Les erreurs SMTP des e-mails d'invitation et de réinitialisation étaient avalées, et l'e-mail perdu : l'invitation est maintenant retentée par l'outbox, la réinitialisation par Hangfire (trois nouvelles tentatives, puis l'état Deleted, qui expire après 24 h).
- Liste des produits triée par nom puis par identifiant : des produits de même nom pouvaient être sautés ou répétés d'une page à l'autre.
- La page produits refusait un prix de 0, que l'API accepte : un produit gratuit peut maintenant y être créé.
- En Development, un corps de requête illisible (JSON mal formé, identifiant qui n'est pas un GUID) renvoyait 500 avec un log d'erreur : `GlobalExceptionHandler` reconnaît `BadHttpRequestException` et répond son statut (400 pour un corps illisible) en problem+json, sans log d'erreur. Hors Development, ASP.NET Core répondait déjà 400, sans corps.

### Removed
- Package `Microsoft.Extensions.Http.Resilience`, référencé mais jamais utilisé.
- `IInvitationEmailEnqueuer`, `HangfireInvitationEmailEnqueuer` et `ProcessInvitationEmailJob`, remplacés par l'outbox. **Au déploiement** : laisser la file Hangfire se vider avant de mettre à jour, ou supprimer depuis le dashboard les jobs `ProcessInvitationEmailJob` restants et renvoyer les invitations concernées. Sinon, ces jobs ne peuvent plus être chargés et restent en échec, avec le jeton brut et l'adresse en arguments.
- Réglages `RateLimiting:DisableAuthPolicy` et `RateLimiting:DisableGlobalLimiter`, qui n'existaient que pour les tests : la factory des tests d'intégration remplace elle-même les limites de débit.

### Security
- Connexion : un e-mail inconnu ou un compte verrouillé coûte le même calcul de hash qu'un mauvais mot de passe, qui fait l'essentiel de la durée de la réponse ; un échec sur un compte existant enregistre encore la tentative en base, un écart bien plus faible. L'inscription, elle, signale toujours un e-mail déjà pris (voir `ARCHITECTURE.md`).
- Un jeton de confirmation ou de réinitialisation mal formé renvoie 400 au lieu de 500.
- Un utilisateur retiré d'une organisation perd ses sessions sur celle-ci (le rafraîchissement lui accordait le rôle Member) ; ses sessions sur ses autres organisations continuent. Un refresh refusé supprime le cookie.
- `SmtpEmailSender` signale ses échecs par une `EmailDeliveryException` sans le texte de l'erreur SMTP, qui peut citer l'adresse du destinataire : le type de l'erreur et ses codes (code et statut d'un refus du serveur, code d'une erreur réseau). L'adresse ne parvient plus à `OutboxMessages.Error`, au log d'échec de l'outbox ni au log d'échec de l'e-mail de confirmation à l'inscription. Le job de réinitialisation journalise cette raison au lieu du seul type de l'erreur.
- Détection de réutilisation limitée aux refresh tokens déjà remplacés par une rotation : un token révoqué par un logout, une réinitialisation ou un retrait d'organisation est refusé sans fermer les autres sessions.
- Une réinitialisation de mot de passe révoque toutes les sessions ouvertes.
- Organisation par défaut à la connexion : la plus ancienne adhésion, au lieu d'un ordre SQL indéterminé.
- `TenantId`, l'audit et la suppression logique n'ont plus de setter public ; l'intercepteur couvre aussi `SaveChanges` synchrone et refuse un changement de tenant.
- Routes `/organizations/{orgId}` : contrôle de portée centralisé dans `OrganizationScopeFilter` ; test d'architecture qui exige le filtre `"TenantId" = @TenantId` dans chaque requête SQL écrite en raw string literal (`"""`) qui nomme une table d'entité tenant.
- Le jeton brut d'une invitation n'est plus stocké en clair dans les tables Hangfire, comme argument du job : seul son hash est en base. Le job de réinitialisation, passé à l'état Deleted après ses tentatives, n'y garde pas l'adresse e-mail plus de 24 h ; son log d'échec et l'exception que Hangfire conserve ne contiennent que l'identifiant de l'utilisateur.
- Rôle Admin effectif (invitations, suppression de produits) ; changement de rôle réservé à l'Owner. Les tests appliquent les vraies policies au lieu d'une copie.
- Front : vitest et `@vitest/coverage-v8` passent en 4.1.11, qui corrige GHSA-82fw-gwwq-j7x9 (sévérité modérée), une traversée de chemin dans les redirections de mocks de `@vitest/mocker`. Dépendances de développement : rien n'en part dans l'application livrée.
- Front : un `overrides` npm impose js-yaml 4.3.2 à `@hey-api/json-schema-ref-parser` 1.4.4, utilisé par la génération des types de l'API, qui exige encore exactement 4.2.0. 4.3.2 corrige GHSA-52cp-r559-cp3m, GHSA-5p4m-2wfm-xmqj et GHSA-2883-xcg3-v3hh (sévérité élevée, consommation CPU sur un YAML piégé). Dépendance de développement elle aussi ; raison et condition de retrait dans `security/README.md`.

### Migrations DB
- `RemoveUserDisplayName` : supprime la colonne inutilisée `AspNetUsers.DisplayName`.
- `OutboxConsumersAndRetries` : crée la table `OutboxMessageConsumers`, ajoute `OutboxMessages.NextAttemptOn` et l'index partiel des messages à traiter.
- `InvitationTokenIssuedOnSend` : `Invitations.TokenHash` devient nullable (aucun lien valable avant le premier envoi, ni après un renvoi jusqu'au nouvel envoi).
