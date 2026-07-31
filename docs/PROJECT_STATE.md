# État du projet

Dernière mise à jour : 31 juillet 2026

## Moteur

- Unity 6.3 LTS
- Éditeur 6000.3.19f1 ARM64
- Universal 2D / URP
- C#

## État actuel

Le projet Unity a été créé avec `Visible Meta Files` et `Force Text`.

Le lot de fondation est importé et compile sans erreur. Il contient :

- une assembly `AgeOfSurvival.Core` en C# pur, sans référence à `UnityEngine` ;
- les primitives `GridPosition`, `GridBounds`, `DenseGrid<T>` et `FixedTickClock` ;
- des tests EditMode ;
- la documentation technique initiale ;
- un script d’exécution des tests en batchmode.

Validation locale :

- Test Runner de l’éditeur : 14/14 cas réussis ;
- batchmode : 14/14 cas réussis, code de sortie 0 ;
- dépôt Git initialisé sur la branche `main` ;
- premier ensemble de fichiers placé dans l’index, sans commit pour le moment.

Aucun gameplay visible n’est encore porté.

## Prochaine action

1. Finaliser les règles Git pour les fichiers YAML générés par Unity.
2. Examiner le diff indexé et vérifier qu’il ne contient que le template Unity et le lot de fondation.
3. Créer le premier commit du dépôt.
4. Définir ensuite le deuxième lot : petite grille logique et représentation isométrique minimale, sans encore porter les systèmes Godot complets.

## Dépôts archivés

Le prototype Godot et le benchmark moteur restent des références historiques séparées. Ils ne doivent pas être modifiés par ce dépôt.
