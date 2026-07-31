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
- `dbcd442` — `feat: connect timed resource transfers` ;
- `93870f1` — `fix: harden inventory transfer invariants` ;
- `25fd671` — `feat: improve prototype visual readability`.

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

La revue indépendante des lots 5A à 5C a ensuite renforcé les contrats de
transfert et les sélections Runtime dans le commit `93870f1`. La suite validée
est désormais de **113/113 cas EditMode** dans l'éditeur et en batchmode, avec
Play Mode fonctionnel et Console propre.

Le lot 5V est validé et commité sous `25fd671`. Il ajoute uniquement une couche
de lisibilité visuelle temporaire : sol isométrique distinct, joueur, ressource,
reste au sol, cible, rayon d'interaction et progression en pixel art. Les PNG
sont produits dans le projet, remplaçables et sans dépendance tierce. Le Core,
les règles de gameplay et les contrôles ne changent pas. Les 116/116 cas
EditMode passent et le rendu Play Mode a été validé.

Le lot 5D est préparé mais pas encore validé localement. Il ajoute dans le Core
une courbe progressive de surcharge basée sur la charge perçue rapportée à la
capacité principale : `100 % → ×1,00`, `125 % → ×0,81`, `150 % → ×0,63`,
`175 % → ×0,44`, `200 % et plus → ×0,25`, avec interpolation linéaire.
`DebugPlayerController` applique le multiplicateur au déplacement à tick fixe et
l'interface affiche le pourcentage de charge et la vitesse résultante. Sprint,
endurance et dégâts restent hors périmètre. Le total attendu est 132 cas
EditMode après import.

## Limites techniques connues

- `InventoryOperations` garantit l'identité unique à l'intérieur d'un conteneur,
  mais pas encore l'unicité globale d'une `ItemInstanceId` entre tous les
  conteneurs d'un `PlayerInventoryState` ;
- retirer directement un objet unique équipé via une opération de conteneur peut
  laisser une référence d'équipement orpheline. Avant d'autoriser le dépôt au
  sol, la destruction ou la persistance d'objets uniques équipés, les mutations
  devront passer par une frontière agrégée qui maintient ces deux invariants.

## Prochaine action

1. Appliquer le patch du lot 5D sur `25fd671` et laisser Unity importer les deux
   nouveaux scripts et leurs métadonnées.
2. Obtenir 132/132 cas EditMode en batchmode et dans le Test Runner graphique.
3. Vérifier en Play Mode la vitesse initiale à environ `×0,91`, le retour à
   `×1,00` lorsque le sac est équipé, puis au moins un état de surcharge plus
   sévère sans rupture visible entre les points de contrôle.
4. Examiner le diff et commiter séparément sous
   `feat: add progressive encumbrance movement penalty`.

## Dépôts archivés

Le prototype Godot et le benchmark moteur restent des références historiques séparées. Ils ne doivent pas être modifiés par ce dépôt.
