# État du projet

Dernière mise à jour : 31 juillet 2026

## Moteur

- Unity 6.3 LTS
- Éditeur 6000.3.19f1 ARM64
- Universal 2D / URP
- C#

## État actuel

Le dépôt Unity est initialisé sur `main`. Le lot 3 est appliqué et validé localement, mais pas encore commité.

Lots validés et commités :

- `20633e2` — `chore: bootstrap Unity project foundation` ;
- `977ac30` — `feat: add isometric debug world`.

État validé au commit `977ac30` :

- assembly `AgeOfSurvival.Core` en C# pur, sans référence à `UnityEngine` ;
- assembly `AgeOfSurvival.Runtime` dépendant du Core ;
- primitives de grille et horloge fixe ;
- Tilemap isométrique 10 × 10 pilotée par `DenseGrid<byte>` ;
- 17/17 cas EditMode réussis dans l’éditeur et en batchmode ;
- grille vérifiée dans la Game View ;
- arbre de travail propre.

Le lot 3 ajoute :

- état du joueur dans le Core C# pur ;
- position continue ;
- déplacement à huit directions avec normalisation diagonale ;
- ZQSD uniquement, lu par un adaptateur utilisant le package Input System ;
- marqueur temporaire généré au runtime ;
- caméra fixe ;
- aucune collision, interaction, animation, sauvegarde ou gestion d’inventaire.

Validation locale du lot 3 :

- 28/28 cas EditMode réussis dans l’éditeur ;
- 28/28 cas EditMode réussis en batchmode, code de sortie 0 ;
- déplacement ZQSD vérifié dans huit directions ;
- normalisation diagonale et caméra fixe vérifiées ;
- `git diff --check` sans erreur ;
- fichiers modifiés conformes au périmètre du lot.

## Prochaine action

1. Mettre à jour la documentation avec les résultats réels du lot 3.
2. Placer tous les fichiers du lot dans l’index Git.
3. Examiner le diff indexé et créer un commit dédié.
4. Vérifier que l’arbre de travail est propre.
5. Définir ensuite le lot 4, limité à une première interaction avec une ressource.

## Dépôts archivés

Le prototype Godot et le benchmark moteur restent des références historiques séparées. Ils ne doivent pas être modifiés par ce dépôt.
