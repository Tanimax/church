# Gestion des erreurs

Chaque type d'erreur a **une seule façon** d'être traité, à **un seul endroit**. Pas de
`try/catch` éparpillés : on rattrape une exception précise là où on sait quoi en faire, ou on
la laisse remonter jusqu'à une frontière.

## 1. Trois catégories

| Catégorie | Exemples | Comment |
|---|---|---|
| **Résultat métier attendu** | carte inconnue, non baptisé, nom d'utilisateur déjà pris, doublon de scan | **Pas d'exception.** Le cas d'usage renvoie un `Result` / `Result<T>` (SharedKernel) portant une `Error(Code, Message)` tirée du catalogue de sa fonctionnalité (`UserErrors`, `MemberErrors`…), ou un type de réponse dédié quand c'est un contrat d'API (`CheckInResponse`). Le message est en français et s'affiche tel quel. |
| **Règle du domaine violée** | créer un `User` sans nom | `DomainException` (Domain). La validation des formulaires devrait l'empêcher : c'est le signe d'un bug ou d'un chemin non validé. |
| **Panne technique** | base indisponible, délai dépassé, API d'e-mail en panne | Exception, traitée **aux frontières** (§ 3). Les pannes passagères de la base sont d'abord **retentées** (§ 2). |

### Conflits en base

Une violation de contrainte unique (code PostgreSQL `23505`) est traduite par `AppDbContext`
en `ConflictException` (Application), avec le nom de la contrainte. Application ne dépend
donc pas de Npgsql. Un cas d'usage qui s'attend au conflit la rattrape et en fait :
- un résultat métier (« ce nom d'utilisateur existe déjà »),
- ou un succès (« déjà enregistré » : le check-in simultané de deux scanners).

Sinon, elle remonte : `409` côté API, message « modifiée par quelqu'un d'autre » côté page.

Cas gérés : nom d'utilisateur pris entre la vérification et l'enregistrement
(`UserErrors.UsernameTaken`), jeton de carte déjà existant (on en tire un autre, 3 essais),
session du jour et présence déjà créées par l'autre scanner (check-in).

## 2. Résilience

- **Base de données** : les erreurs passagères (redémarrage, connexion coupée, réveil de la
  machine Fly) sont retentées 3 fois (`EnableRetryOnFailure`, `DatabaseSetup.ConfigureNpgsql`).
  ⚠️ Cette stratégie interdit les transactions explicites non encapsulées : toute
  `BeginTransaction` devra passer par `Database.CreateExecutionStrategy().ExecuteAsync(...)`.
- **HTTP sortant** : délai de 10 s (e-mail), au lieu des 100 s par défaut.
- **Au mieux** : l'envoi d'e-mail rattrape tout, journalise, et ne bloque jamais l'action.
- **Health check Fly** : `GET /health` (`fly.toml`, `[[http_service.checks]]`). Il ne
  consulte pas la base : une panne de base est déjà signalée aux utilisateurs, et faire
  échouer ce check ferait cesser le routage vers la seule machine. Un déploiement dont l'app
  ne répond pas échoue au lieu de remplacer la version en place.
- **Démarrage** : on échoue tout de suite si la configuration est fausse (`ADMIN_PASSWORD`,
  fuseau horaire, base) — mieux vaut un déploiement staging qui échoue qu'une prod qui
  plante au premier dimanche.

## 3. Les frontières (seuls endroits qui rattrapent tout)

| Frontière | Où | Ce que voit l'utilisateur |
|---|---|---|
| **API `/api/*`** | `Startup/ErrorHandlingSetup.cs` | JSON `ProblemDetails` avec un titre en français et `traceId` : `400` requête invalide / règle du domaine, `401` connexion requise, `403`, `404`, `409` conflit, `503` base momentanément indisponible, `500` erreur inattendue. Vaut aussi pour les codes d'erreur sans exception. |
| **Page (première requête)** | `Components/Pages/Error.razor` | Page « Une erreur est survenue » en français, avec la référence de l'erreur, sans détail technique. URL inconnue : `Components/Pages/NotFound.razor`. |
| **Actions d'une page interactive** | `Components/AppComponentBase.cs` | **Toute** page interactive hérite de `AppComponentBase` (vérifié par un test) et lance ses chargements et actions via `TryRunAsync(action, "Impossible de …")` : en cas d'échec, message en français (composant `<ErrorAlert>`) avec une référence, le formulaire reste ouvert et la saisie est conservée. |
| **Lien de carte inconnu** | `Features/Members/CardEndpoints.cs` | Page HTML « Carte introuvable » (404) : le lien est ouvert dans un navigateur. |
| **Dernier recours Blazor** | `#blazor-error-ui` dans les layouts | Bandeau « Une erreur inattendue est survenue et la page ne répond plus — Recharger la page ». Ne devrait apparaître que si une action n'est pas passée par `TryRunAsync`. |

> Pourquoi pas un `ErrorBoundary` ? Les layouts sont rendus en statique et chaque page devient
> interactive séparément : un `ErrorBoundary` du layout ne voit pas les erreurs des pages, et
> aucun `ErrorBoundary` ne rattrape les erreurs des gestionnaires d'événements du composant qui
> le contient.

## 4. Journalisation

- **Une seule fois, à la frontière.** Jamais de « catch → log → rethrow ».
  - API : `ApiExceptionHandler` journalise (depuis .NET 10, le middleware ne le fait plus
    pour une exception qu'un `IExceptionHandler` a traitée).
  - Pages : le middleware d'exceptions d'ASP.NET Core journalise ; on ne relogue pas.
  - Pages interactives : `TryRunAsync` journalise avec la page, l'action et la référence.
- La **référence** affichée à l'utilisateur se retrouve dans les logs :
  `fly logs | grep <référence>`.
  - API et page d'erreur : c'est le `TraceId` de la requête, présent dans chaque ligne de log
    grâce à `Logging:Console:FormatterOptions:IncludeScopes` (`appsettings.json`).
  - Actions de page interactive (`réf. xxxxxxxx`) : elle figure dans le message du log
    écrit par `TryRunAsync`.
- Le reste (niveaux, format, données personnelles) : [journalisation.md](journalisation.md).

## 5. Règles

- Pas de `catch` vide.
- Pas de `catch (Exception)` hors des frontières listées ci-dessus et de l'envoi d'e-mail
  (vérifié par `tests/Architecture.Tests/ExceptionHandlingRulesTests.cs` : ajouter le fichier à
  la liste autorisée, avec une raison, si un nouveau cas le justifie).
- On rattrape une exception **précise** (`ConflictException`, …) seulement là où l'on sait
  quoi en faire.
- Un nouveau cas d'usage dont l'échec est normal renvoie un `Result`, pas une exception.
