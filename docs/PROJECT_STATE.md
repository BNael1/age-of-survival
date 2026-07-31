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
- `1a1f280` — `feat: add resource interaction` ;
- `f44bae1` — `feat: add core inventory containers` ;
- `6d5b4dd` — `feat: add inventory equipment UI` ;
- `dbcd442` — `feat: connect timed resource transfers`.

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

Le lot 5B ajoute :

- trois emplacements d'équipement fixes et des compatibilités explicites ;
- une charge brute et une charge perçue calculées en unités entières ;
- un sac à dos prototype dont le contenu reçoit `70 %` de réduction uniquement
  lorsqu'il est équipé au dos ;
- une session d'inventaire C# indépendante des `GameObject` ;
- des commandes de transfert, équipement et déséquipement avec résultats
  explicites ;
- une interface runtime UI Toolkit en deux listes avec capacités, charges,
  emplacements et actions dont l'état activé reflète la validité des commandes ;
- 20 nouveaux cas EditMode, soit 84/84 réussis dans l'éditeur et en batchmode ;
- 16 assertions Play Mode réussies au travers des boutons UI réels.

Le lot 5C ajoute :

- conteneurs de sol Core identifiés et positionnés de façon stable ;
- rendement prototype de six branches créé intégralement au sol ;
- actions de transfert temporisées en ticks, progressives et à application
  unique ;
- revalidation finale de la source et de la capacité sans réservation
  destructive ;
- interruption par mouvement ou éloignement, sans perte ;
- troisième liste UI, barre de progression, quantité transférée/restante et
  marqueur de sol portant la quantité ;
- maintien du reste exact au sol puis reprise ultérieure ;
- 19 nouveaux cas EditMode, soit 103/103 réussis graphiquement et en batchmode ;
- 12 assertions Play Mode sur le cycle complet du lot.

## Limites techniques connues

- `InventoryOperations` garantit l'identité unique à l'intérieur d'un conteneur,
  mais pas encore l'unicité globale d'une `ItemInstanceId` entre tous les
  conteneurs d'un `PlayerInventoryState` ;
- retirer directement un objet unique équipé via une opération de conteneur peut
  laisser une référence d'équipement orpheline. Avant d'autoriser le dépôt au
  sol, la destruction ou la persistance d'objets uniques équipés, les mutations
  devront passer par une frontière agrégée qui maintient ces deux invariants.

## Prochaine action

1. Définir le profil de surcharge progressif du lot 5D.
2. Appliquer son multiplicateur au déplacement Core existant.
3. Exposer niveau, vitesse et possibilité de course dans l'interface prototype.

## Dépôts archivés

Le prototype Godot et le benchmark moteur restent des références historiques séparées. Ils ne doivent pas être modifiés par ce dépôt.
