# Revue automatique des PR par Claude

Chaque PR vers `main` est relue par Claude (`.github/workflows/claude-code-review.yml`).
Il vérifie que le code respecte les standards du dépôt et poste ses remarques en
commentaires sur les lignes concernées, en français. Il n'approuve ni ne bloque la PR :
la décision de merger reste humaine.

## Ce qu'il vérifie

- **`CLAUDE.md`** : les standards du dépôt (couches, erreurs, rôles, journalisation, tests…).
  Claude le lit aussi dans toute session de travail sur le dépôt.
- **`REVIEW.md`** : ce qui bloque un merge, ce qui n'est qu'une suggestion, et comment
  rédiger les commentaires.
- Les bugs et failles évidents dans le diff, même hors des règles écrites.

Pour ajouter un standard : l'écrire dans `CLAUDE.md` (une règle courte, vérifiable, avec la
raison si elle n'est pas évidente). Pour changer la façon de relire : `REVIEW.md`.

## Mise en place (une fois)

1. L'app GitHub Claude est installée sur le dépôt (normalement déjà fait : c'est elle qui pousse les branches de Claude). Sinon : [github.com/apps/claude](https://github.com/apps/claude).
2. Sur ton ordinateur, avec Claude Code connecté à ton abonnement : `claude setup-token`.
   Copie le jeton affiché.
3. Sur GitHub : *Settings → Secrets and variables → Actions → New repository secret*,
   nom `CLAUDE_CODE_OAUTH_TOKEN`, valeur : le jeton.

Sans ce secret, le job « Revue Claude » se termine sans rien faire et l'indique en
avertissement.

## Bon à savoir

- Les revues consomment l'usage de l'abonnement Claude de la personne qui a créé le jeton.
- Les PR en brouillon ne sont pas relues ; elles le sont quand elles passent « prêtes ».
- Claude ne relit pas une PR qui a déjà un commentaire de sa part : pour une nouvelle revue
  après des corrections importantes, utiliser `/code-review` dans une session Claude Code.
- La revue ne remplace pas la CI : la compilation et les tests restent vérifiés par le job
  `test`, et les parcours navigateur par le job `e2e`.
- Les actions du workflow sont épinglées sur un commit précis (la version est en
  commentaire). Pour monter de version : `git ls-remote https://github.com/anthropics/claude-code-action refs/tags/v1^{}`
  donne le commit de la dernière v1.
