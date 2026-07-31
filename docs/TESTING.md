# Tests

## Stratégie

- EditMode pour le cœur C# pur ;
- PlayMode uniquement pour les adaptateurs, scènes et interactions nécessitant Unity ;
- tests déterministes et sans dépendance à l’ordre d’exécution ;
- chaque bug corrigé doit recevoir un test lorsque raisonnable.

## Référence actuelle

Le lot de fondation contient 11 méthodes de test. Une méthode utilise quatre cas paramétrés, soit **14 cas exécutés** au total.

Résultat validé le 31 juillet 2026 :

- éditeur : 14/14 réussis ;
- batchmode : 14/14 réussis, code de sortie 0.

## Exécution graphique

Dans Unity : `Window > General > Test Runner`, puis onglet EditMode.

## Exécution batchmode

Fermer l’éditeur Unity qui utilise le projet. Unity Hub peut rester ouvert.

```sh
./tools/run_editmode_tests.sh
```

Le chemin de l’éditeur peut être remplacé :

```sh
UNITY_EDITOR=/chemin/vers/Unity ./tools/run_editmode_tests.sh
```

Les résultats sont écrits dans :

```text
TestResults/editmode-results.xml
TestResults/editmode.log
```

Le dossier `TestResults/` est ignoré par Git.

## Lot 2 — validation réalisée

Le lot 2 ajoute trois cas EditMode :

- déterminisme du motif de débogage ;
- création d’une Tilemap isométrique contenant une tuile par cellule du Core ;
- reconstruction sans duplication de hiérarchie.

Validation du 31 juillet 2026 : **17/17 cas réussis** dans l’éditeur et **17/17 cas réussis** en batchmode. La grille isométrique 10 × 10 a également été vérifiée manuellement dans la Game View.
