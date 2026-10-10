# ADR 0005 — E-mails transactionnels : outbox après une écriture, Hangfire sinon

- **Statut** : Proposé (2026-10-10)
- **Contexte** : l'e-mail d'invitation partait par un job Hangfire enfilé après le commit de
  l'invitation (`InviteMemberHandler`, `ResendInvitationHandler`) : une double écriture, sans
  garantie que les deux aient lieu. Le jeton brut du lien était un argument du job, donc stocké en
  clair dans les tables Hangfire, alors que l'application n'en garde que le hash. Enfin, les jobs
  d'envoi (`ProcessInvitationEmailJob`, `ProcessPasswordResetJob`) avalaient les erreurs SMTP :
  Hangfire ne réessayait jamais, et l'e-mail était perdu.

## Décision

**Un e-mail qui suit une écriture en base part par l'outbox.** Un domain event, écrit dans la
même transaction que l'agrégat, déclenche l'envoi depuis un handler de l'outbox
(`SendInvitationEmailHandler`, sur `MemberInvitedDomainEvent` et `InvitationResentDomainEvent`) :

1. le handler charge l'invitation ; si elle n'existe plus, n'est plus en attente ou a expiré, il
   ne fait rien ;
2. il crée le jeton brut, enregistre son hash (`Invitation.IssueToken`) et valide ;
3. seulement ensuite, il envoie l'e-mail.

Le jeton brut n'existe qu'en mémoire du handler et dans l'e-mail : ni l'outbox, ni Hangfire, ni
l'invitation ne le stockent. Un renvoi efface le hash (`Invitation.Refresh`) : l'ancien lien est
refusé dès le renvoi, et le nouveau part avec `InvitationResentDomainEvent`. Une erreur SMTP ou un
conflit de concurrence fait échouer le handler, que l'outbox reprend (1, 4, 16 puis 64 minutes).

**Un e-mail sans écriture en base part par Hangfire.** La demande de réinitialisation du mot de
passe n'écrit rien : il n'y a pas de transaction où accrocher l'outbox, et la réponse HTTP est la
même que l'e-mail existe ou non. `ProcessPasswordResetJob` reçoit l'adresse, génère le jeton
Identity et envoie l'e-mail. Une erreur SMTP fait échouer le job ; Hangfire le reprend trois fois
au plus, puis le passe à l'état Deleted (`AttemptsExceededAction.Delete`), qui expire après 24 h
comme un job réussi : l'adresse, argument du job, ne reste pas indéfiniment dans ses tables, comme
elle le ferait dans un job en échec. Le message d'une erreur SMTP pouvant citer l'adresse, ni
notre log ni l'exception que Hangfire journalise et conserve ne le reprennent : ils portent
l'identifiant de l'utilisateur et la raison de l'échec, celle d'une `EmailDeliveryException`.

Dans les deux cas, **aucune erreur d'envoi n'est avalée**. `SmtpEmailSender` signale un échec par
une `EmailDeliveryException`, dont la raison donne le type de l'erreur et ses codes (code et
statut d'un refus du serveur, code d'une erreur réseau), jamais son texte, qui peut citer
l'adresse. La colonne `OutboxMessages.Error` et les logs, qui reprennent cette exception, ne
contiennent donc pas l'adresse non plus.

## Conséquences

- **Livraison au moins une fois.** Un nouvel essai après un e-mail parti (le serveur SMTP l'a
  accepté mais a répondu en erreur, ou l'application s'est arrêtée avant de valider le message)
  pose un nouveau jeton : le destinataire reçoit deux e-mails, et seul le second lien est
  valable. Un doublon remplace le lien précédent.
- **Ordre des e-mails.** Si deux envois se chevauchent (une invitation suivie aussitôt d'un
  renvoi), le dernier e-mail reçu n'est pas forcément celui dont le lien est valable. L'éviter
  demanderait un verrou tenu pendant l'envoi SMTP, que ce starter ne prend pas.
- **Abandon.** Après cinq tentatives, le message de l'outbox est abandonné et journalisé en
  erreur ; l'invitation reste sans lien valable jusqu'à un renvoi (`DEPLOYMENT.md` explique aussi
  comment relancer un message abandonné).
- **Délai.** L'e-mail d'invitation part au lot suivant de l'outbox, dans les 5 secondes, et non
  pendant la requête. Les tests d'intégration lancent les lots eux-mêmes.

## Option écartée : jetons auto-validés par Data Protection

Un jeton chiffré et signé par ASP.NET Core Data Protection, comme ceux d'Identity pour la
confirmation d'e-mail et la réinitialisation, porterait lui-même l'identifiant de l'invitation et
son échéance : rien à stocker, et le lien pourrait être créé dans la requête.

Non retenu, pour deux raisons :

- **Le modèle de jeton change.** Le hash stocké disparaît, et avec lui le moyen simple de rendre un
  lien caduc : un renvoi ou une révocation devrait passer par un tampon de version à comparer, un
  mécanisme à part entière.
- **Les clés sont éphémères en conteneur.** Sans `DataProtection__KeysPath` sur un volume
  persistant, les clés vivent en mémoire et disparaissent à chaque redéploiement, ce qui invalide
  tous les liens émis avant (`DEPLOYMENT.md` § Data Protection). Les invitations, valables 7 jours,
  y seraient bien plus exposées que les liens d'Identity, valables un jour par défaut.
