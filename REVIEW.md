# Consignes de revue des PR

Les standards du dépôt sont dans `CLAUDE.md`. Ce fichier dit comment les appliquer en revue.

## Ce qui bloque un merge

- Un bug : résultat faux, plantage, régression d'un parcours existant.
- Une faille : endpoint ou action sans contrôle d'accès, rôle vérifié seulement à
  l'affichage, donnée personnelle (nom, courriel complet, jeton de carte, mot de passe) dans
  un log ou une réponse d'API.
- Une violation d'une règle de `CLAUDE.md` introduite par la PR : dépendance entre couches
  interdite, `catch (Exception)` hors frontière, page interactive sans `TryRunAsync`, liste
  de rôles recopiée au lieu d'une politique, `DateTime.Now`, version de paquet dans un
  `.csproj`, message de log interpolé, texte utilisateur en anglais.
- Un changement de comportement sans test, ou un test qui passerait aussi sans la correction.
- Une migration EF modifiée après coup, ou qui échouerait sur des données déjà en production
  (contrainte unique ajoutée sans traiter les doublons existants, colonne non nulle sans
  valeur par défaut).

## Ce qui ne bloque pas

- Le style que les outils ou les tests d'architecture ne vérifient pas : le signaler comme
  suggestion, une fois, sans répéter.
- Ce qui existait avant la PR : le mentionner seulement s'il est grave, en précisant qu'il
  est antérieur.

## Comment écrire les commentaires

- En français.
- Un commentaire par problème, sur la ligne concernée : ce qui ne va pas, le scénario
  concret qui le déclenche, et la correction attendue.
- Citer la règle de `CLAUDE.md` en cause quand il y en a une.
- Ne pas signaler ce que la CI vérifie déjà (compilation, tests, tests d'architecture).
