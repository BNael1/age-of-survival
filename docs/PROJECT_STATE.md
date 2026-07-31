# État du projet

Dernière mise à jour : 31 juillet 2026

## Moteur

- Unity 6.3 LTS
- Éditeur 6000.3.19f1 ARM64
- Universal 2D / URP
- C#

## État actuel

Le dépôt Unity est sur la branche locale `codex/lot5-inventory-sequence`.

Lots validés et commités :

- `20633e2` — `chore: bootstrap Unity project foundation` ;
- `977ac30` — `feat: add isometric debug world` ;
- `26a1a27` — `feat: add player movement` ;
- `1a1f280` — `feat: add resource interaction`.

État validé au commit `26a1a27` :

- assembly `AgeOfSurvival.Core` en C# pur, sans référence à `UnityEngine` ;
- assembly `AgeOfSurvival.Runtime` dépendant du Core ;
- primitives de grille, position continue et horloge fixe ;
- Tilemap isométrique 10 × 10 pilotée par `DenseGrid<byte>` ;
- déplacement du joueur à huit directions normalisé, piloté par ZQSD ;
- 28/28 cas EditMode réussis dans l’éditeur et en batchmode ;
- arbre de travail propre.

Le lot 4 ajoute :

- identifiants de ressources stables et reproductibles dans le Core ;
- états `Available` et `Harvested` ;
- ciblage automatique déterministe de la ressource disponible la plus proche
  dans un rayon inclusif ;
- commande d’interaction explicite consommée sur le tick fixe ;
- touche `E` lue avec le package Input System ;
- trois marqueurs temporaires et un indicateur de cible générés au runtime ;
- disparition du marqueur récolté, sans récompense ni inventaire.

Validation locale du lot 4 :

- 45/45 cas EditMode réussis dans l’éditeur et en batchmode, code de sortie 0 ;
- compilation Unity 6000.3.19f1 réussie ;
- validation Play Mode réussie : ciblage, portée, trois récoltes successives,
  absence de mutation hors portée et Console sans erreur ;
- `git diff --check` sans erreur ;
- Test Runner graphique : 45/45 réussis, zéro échec et zéro cas ignoré ;
- revue complète du patch réalisée avant commit.

Le lot 5A ajoute :

- identifiants stables pour définitions, instances et conteneurs ;
- encombrement déterministe entier avec `1000` unités internes par unité
  affichée ;
- définitions séparées de l'état mutable ;
- piles de matériaux et instances uniques ;
- objets conteneurs uniques reliés à un `ContainerId` stable ;
- capacités brutes, ajouts complets ou partiels et retraits atomiques ;
- transferts complets ou partiels sans perte ni duplication ;
- collections publiques en lecture seule et ordre d'entrée stable ;
- 19 nouveaux cas EditMode, soit 64/64 réussis dans l'éditeur et en batchmode.

## Prochaine action

1. Rechercher les solutions UI Toolkit et assets temporaires pertinents.
2. Concevoir l'équipement logique minimal du lot 5B.
3. Ajouter l'interface runtime prototype sans déplacer l'état métier hors du
   Core.

## Dépôts archivés

Le prototype Godot et le benchmark moteur restent des références historiques séparées. Ils ne doivent pas être modifiés par ce dépôt.
