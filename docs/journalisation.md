# Journalisation

Comment l'app écrit ses logs, et quoi y mettre. Les erreurs elles-mêmes (qui les rattrape,
quelle référence est montrée à l'utilisateur) sont dans [gestion-des-erreurs.md](gestion-des-erreurs.md).

## 1. Où les lire

L'app écrit sur la console ; Fly.io la collecte.

- `fly logs -a church-attendance-app` : le flux en direct (`-a church-attendance-app-staging`
  pour le staging).
- Le tableau de bord Fly (onglet *Logs*, ou *Monitoring* → Grafana) permet de chercher dans
  les jours précédents. La rétention de Fly est courte : pour garder les logs plus longtemps,
  il faut les envoyer ailleurs (prévu plus tard).
- Une référence affichée à l'utilisateur : `fly logs | grep <référence>`.

## 2. Format

| Environnement | Format | Pourquoi |
|---|---|---|
| Production et staging | JSON, une ligne par entrée | Fly garde chaque entrée en une seule ligne, pile d'appels comprise ; chaque champ est cherchable. |
| Développement | Texte sur une ligne, avec heure | Lisible dans le terminal. |

Chaque entrée porte ses **scopes**, c'est-à-dire le contexte de la requête :

- `TraceId` : la référence montrée à l'utilisateur pour une erreur d'API ou de page.
- `RequestPath`, `RequestId` : ajoutés par ASP.NET Core.
- `UserName`, `Role` : le compte du personnel connecté, ou `(anonyme)`. Ils sont ajoutés par
  `LoggingSetup.UseUserLogScope`, placé en tête du pipeline : même la ligne écrite par le
  gestionnaire d'exceptions les porte.
- `Page` : la page interactive, pour ce qui est journalisé pendant une action de page.
  Ajoutée par `AppComponentBase.TryRunAsync`, parce que les événements Blazor s'exécutent
  hors de toute requête HTTP.

Les accents restent lisibles dans le JSON (`Échec`, pas `Échec`), pour que `grep` trouve
le texte français.

## 3. Niveaux

| Niveau | Quand | Exemple |
|---|---|---|
| `Error` | Une action a échoué et l'utilisateur a vu une erreur. | Base injoignable pendant un check-in. |
| `Warning` | Quelque chose d'anormal, mais l'action a abouti ou n'était pas essentielle. | Courriel de bienvenue non envoyé. |
| `Information` | Un événement métier ou de sécurité qu'on veut retrouver. | Connexion, migration appliquée. |
| `Debug` | Détail utile en développement seulement (activé pour `ChurchAttendance` en dev). | |

Configuration (`appsettings.json`) :

- `Default` est à `Information`, `Microsoft.AspNetCore` à `Warning`.
- `Microsoft.EntityFrameworkCore.Database.Command` et `.Infrastructure` sont à `Warning` :
  sinon chaque requête SQL est écrite. Les commandes en échec (`Error`) restent visibles.
- Au tout premier démarrage sur une base vide, EF écrit une erreur `Failed executing DbCommand`
  sur `__EFMigrationsHistory` avant de créer la table : elle est attendue.

## 4. Écrire un log

- Toujours par `ILogger<T>` injecté (ou `ILoggerFactory` dans un composant). Jamais
  `Console.WriteLine` : le niveau et le contexte seraient perdus.
- Un **modèle** avec des champs nommés, jamais une chaîne interpolée :

  ```csharp
  logger.LogWarning("Courriel non envoyé à {Email} : {Status}", LogRedaction.MaskEmail(email), status);  // oui
  logger.LogWarning($"Courriel non envoyé à {email} : {status}");                                     // non
  ```

  Avec le modèle, `Email` et `Status` sont des champs du JSON, et toutes les occurrences du
  message partagent le même texte.
- L'exception en premier argument (`logger.LogError(ex, "...")`), pas `ex.Message` dans le texte :
  la pile d'appels est conservée.
- Une erreur est journalisée **une seule fois**, là où elle est rattrapée (voir
  gestion-des-erreurs.md § 4).
- Le domaine (`src/Domain`) ne journalise pas : il renvoie un résultat ou lève une exception, et
  les couches autour décident.

## 5. Données personnelles

Les logs sont lus par des personnes qui n'ont pas à connaître les membres. Règle :
**des identifiants, pas de noms**.

| Interdit | À la place |
|---|---|
| Mots de passe, hachés compris | Rien. |
| Jeton de carte de membre | L'identifiant du membre (`MemberId`). |
| Nom, prénom, téléphone d'un membre ou visiteur | `MemberId` / `VisitorId`. |
| Courriel complet | `LogRedaction.MaskEmail(email)` → `m***@gmail.com`. |
| Clés d'API, chaînes de connexion | Rien. |

- Le nom du compte du personnel (`UserName`) est permis : c'est un identifiant de connexion,
  et il faut savoir qui a fait quoi.
- Pour une URL qui contient un jeton, journaliser le **modèle de route**
  (`/api/members/{token}/qr.png`), pas le chemin. `ApiExceptionHandler` le fait.
- Limite connue : le scope `RequestPath` d'ASP.NET Core contient le chemin réel, donc le jeton
  pour les pages `/card/{token}`. Seuls les administrateurs Fly lisent les logs, et ils ont déjà
  accès à la base.

## 6. Règles vérifiées par les tests

`tests/Architecture.Tests/LoggingRulesTests.cs` :

- pas de `Console.Write` dans `src/` ;
- pas de chaîne interpolée (`$"..."`) passée à `Log*` ;
- `src/Domain` ne dépend pas de `Microsoft.Extensions.Logging`.

Pour tester ce qu'un service journalise : `FakeLogger<T>`
(`Microsoft.Extensions.Diagnostics.Testing`), comme `EmailServiceLoggingTests`.
