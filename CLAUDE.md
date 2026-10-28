# Church Attendance — standards du dépôt

Application de présences d'église : .NET 10, Blazor Server, PostgreSQL (EF Core), hébergée sur
Fly.io (staging puis production). Ce fichier est lu par Claude à chaque session et par l'agent
de revue des PR (`.github/workflows/claude-code-review.yml`) : chaque règle ci-dessous doit
être respectée par le code qu'on merge.

## Architecture

- Couches (`src/`) : `Domain` ← `Application` ← `Infrastructure` ← `Api`. `SharedKernel`
  est utilisable par toutes et ne dépend d'aucune.
  - `Domain` : entités et règles, sans EF Core ni ASP.NET Core, et sans journalisation.
  - `Application` : cas d'usage (`*Service`), `IAppDbContext`, catalogues d'erreurs. Ne
    dépend ni de Npgsql, ni d'ASP.NET Core, ni d'`Infrastructure`.
  - `Infrastructure` : `AppDbContext`, migrations, e-mail, hachage, images.
  - `Api` : pages Blazor et endpoints par fonctionnalité (`Features/<Fonctionnalité>/`) ;
    seul `Program.cs` et `Startup/` connaissent `Infrastructure`.
- Une seule base, un seul `AppDbContext`. Les pages Blazor passent par un service qui utilise
  `IAppDbContextFactory` (un contexte court par opération), jamais un contexte de longue durée.
- Ces règles sont vérifiées par `tests/Architecture.Tests` : on n'affaiblit pas un test
  d'architecture pour faire passer un changement.

## Erreurs (détail : `docs/gestion-des-erreurs.md`)

- Un échec métier attendu renvoie un `Result` / `Result<T>` avec une `Error` du catalogue de
  la fonctionnalité (`MemberErrors`, `UserErrors`…). Pas d'exception pour ça.
- Pas de `catch` vide ; pas de `catch (Exception)` hors des frontières documentées
  (`AppComponentBase`, envoi d'e-mail). On rattrape une exception précise, là où on sait quoi
  en faire.
- Une page interactive (`@rendermode InteractiveServer`) hérite d'`AppComponentBase` et
  exécute ses actions via `TryRunAsync`, pour qu'une panne affiche un message au lieu de
  tuer la connexion.
- Les erreurs d'API sont des `ProblemDetails` au titre en français.

## Sécurité et rôles

- Qui peut faire quoi est défini **une seule fois**, dans `AuthorizationPolicies`
  (`src/Api/Startup/SecuritySetup.cs`). Endpoints : `RequireAuthorization(policy)`.
  Pages : `<AuthorizeView Policy="...">` pour les boutons **et** `IsAuthorizedAsync(policy)`
  dans le gestionnaire (un bouton masqué peut quand même être déclenché). Pas de liste de
  rôles recopiée (`Roles="..."`, `IsInRole`) pour une règle qui a sa politique.
- Tout endpoint qui lit ou modifie des données exige une authentification, sauf ce qui est
  public par nature (carte de membre, formulaire visiteur, `/health`).
- Un bouton qui crée quelque chose ignore les clics pendant l'enregistrement (drapeau posé
  avant le premier `await`, bouton désactivé) : sinon une rafale de clics crée des doublons.

## Journalisation (détail : `docs/journalisation.md`)

- `ILogger` uniquement, jamais `Console.Write`. Messages en modèles (`"... {MemberId}"`),
  jamais en chaîne interpolée.
- Pas de données personnelles en clair : identifiants plutôt que noms, courriels masqués
  (`LogRedaction.MaskEmail`), jamais de mot de passe ni de jeton de carte.
- Une erreur est journalisée une seule fois, à la frontière qui la rattrape.

## Code

- Textes affichés à l'utilisateur en français ; noms de code et commentaires techniques en
  anglais, comme le code existant.
- Commentaires : expliquer le pourquoi (contrainte, piège), pas paraphraser le code.
- Dates : passer par `ChurchClock` (heure de l'église, America/Toronto), jamais
  `DateTime.Now` / `DateTime.Today`.
- URLs publiques (carte, formulaire visiteur) : `PublicUrls`.
- Recherche par nom : `TextSearch` (insensible à la casse, aux accents et à la ponctuation).
- Paquets NuGet : version dans `Directory.Packages.props` uniquement, jamais dans un `.csproj`.
- Schéma de base : une migration EF dans `src/Infrastructure/Persistence/Migrations`,
  générée par `dotnet ef migrations add`, jamais modifiée à la main après merge.

## Tests

- Tout changement de comportement vient avec un test : service ou endpoint dans
  `tests/ChurchAttendance.Tests` (vraie base PostgreSQL via Testcontainers), parcours
  navigateur dans `tests/ChurchAttendance.E2ETests` quand le bug ne se voit qu'avec Blazor
  (rôles à l'écran, clics multiples).
- Un test de correction doit échouer sans la correction.
- On ne désactive ni ne saute un test pour faire passer la CI.

## Commandes

```bash
dotnet build ChurchAttendance.sln
dotnet test tests/ChurchAttendance.Tests        # nécessite Docker (Testcontainers)
dotnet test tests/Architecture.Tests
dotnet test tests/ChurchAttendance.E2ETests     # Docker + `playwright.ps1 install chromium`
docker compose up -d                            # Postgres local pour lancer l'app
dotnet run --project src/Api                    # admin / dev-only-password en développement
```
