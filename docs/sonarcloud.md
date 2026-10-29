# SonarCloud et la couverture des tests

SonarCloud analyse chaque PR et chaque push sur `main`. Pour afficher la **couverture des
tests**, l'analyse doit tourner dans GitHub Actions (`.github/workflows/sonar.yml`) : l'analyse
automatique de SonarCloud ne lit pas la couverture d'un projet .NET.

Le workflow compile la solution, lance `tests/ChurchAttendance.Tests` avec la couverture
(coverlet, format OpenCover) et envoie le tout à SonarCloud. Les migrations et `wwwroot` sont
exclus du calcul de couverture.

## Mise en place (une seule fois, dans cet ordre)

1. **Désactiver l'analyse automatique** : sur sonarcloud.io, projet *Tanimax_church* →
   *Administration* → *Analysis Method* → décocher *Automatic Analysis*.
   Si elle reste active, l'analyse du workflow échoue : SonarCloud refuse deux méthodes
   d'analyse à la fois.
2. **Créer un jeton** : sur sonarcloud.io, *My Account* → *Security* → *Generate Tokens*.
   Donnez-lui un nom (par exemple `github-actions`) et copiez-le.
3. **L'enregistrer dans GitHub** : dépôt → *Settings* → *Secrets and variables* → *Actions*
   → *New repository secret*, nom `SONAR_TOKEN`, valeur : le jeton. Ne le collez nulle part
   ailleurs (ni dans un fichier, ni dans une conversation).
4. **Vérifier l'organisation** : le workflow utilise l'organisation `tanimax` et le projet
   `Tanimax_church`. Si la clé d'organisation affichée sur sonarcloud.io est différente,
   corrigez `/o:` dans `sonar.yml`.

Tant que le secret `SONAR_TOKEN` n'existe pas, le job SonarCloud ne fait rien et le signale
par un avertissement : il ne bloque pas la PR.

## En local

Pour voir la couverture sans SonarCloud :

```bash
dotnet test tests/ChurchAttendance.Tests --collect:"XPlat Code Coverage;Format=opencover"
```

Le rapport est écrit dans `tests/ChurchAttendance.Tests/TestResults/<id>/coverage.opencover.xml`.
