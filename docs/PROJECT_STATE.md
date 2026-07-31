# État du projet

Dernière mise à jour : 31 juillet 2026

## Moteur

- Unity 6.3 LTS
- Éditeur 6000.3.19f1 ARM64
- Universal 2D / URP
- C#

## État actuel

Le dépôt Unity est initialisé sur `main`.

Lot 1 validé et commité :

- commit `20633e2` — `chore: bootstrap Unity project foundation` ;
- assembly `AgeOfSurvival.Core` en C# pur, sans référence à `UnityEngine` ;
- primitives `GridPosition`, `GridBounds`, `DenseGrid<T>` et `FixedTickClock` ;
- 14/14 cas EditMode réussis dans l’éditeur et en batchmode ;
- arbre de travail propre après le commit.

Le lot 2 est appliqué et validé localement, mais pas encore commité. Il ajoute :

- une assembly `AgeOfSurvival.Runtime` séparée du Core ;
- un adaptateur Unity qui lit une `DenseGrid<byte>` du Core et la rend dans une Tilemap isométrique ;
- une texture en losange et une palette neutre générées au runtime, uniquement comme visuels de débogage ;
- trois nouveaux cas EditMode d’intégration ;
- une scène `SampleScene` configurée pour afficher une grille 10 × 10 en mode Play ;
- le tri transparent isométrique `(0, 1, 0)` dans les réglages graphiques.

Validation locale du lot 2 :

- 17/17 cas EditMode réussis dans l’éditeur ;
- 17/17 cas EditMode réussis en batchmode ;
- grille isométrique 10 × 10 vérifiée dans la Game View ;
- caméra centrée, bordure complète et aucune tuile manquante visible ;
- `git diff --check` sans erreur ;
- fichiers modifiés conformes au périmètre du lot.

Aucun déplacement, inventaire, sauvegarde, construction ou règle de terrain de production n’est ajouté dans ce lot.

## Prochaine action

1. Mettre à jour la documentation avec les résultats réels du lot 2.
2. Placer tous les fichiers du lot dans l’index Git.
3. Examiner le diff indexé et créer un commit dédié.
4. Vérifier que l’arbre de travail est propre.
5. Définir ensuite le lot 3 ; tout choix visible de joueur, déplacement ou contrôles doit être confirmé par Naël avant implémentation.

## Dépôts archivés

Le prototype Godot et le benchmark moteur restent des références historiques séparées. Ils ne doivent pas être modifiés par ce dépôt.
