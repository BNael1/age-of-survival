# État du projet

Dernière mise à jour : 3 août 2026

## Moteur

- Unity 6.3 LTS
- Éditeur 6000.3.19f1 ARM64
- Universal 2D / URP
- C#

## État actuel

Le dépôt Unity est sur la branche locale `codex/lot7b-deterministic-world-generation`.

Lots validés et commités :

- `20633e2` — `chore: bootstrap Unity project foundation` ;
- `977ac30` — `feat: add isometric debug world` ;
- `26a1a27` — `feat: add player movement` ;
- `1a1f280` — `feat: add resource interaction` ;
- `f44bae1` — `feat: add core inventory containers` ;
- `6d5b4dd` — `feat: add inventory equipment UI` ;
- `dbcd442` — `feat: connect timed resource transfers` ;
- `93870f1` — `fix: harden inventory transfer invariants` ;
- `25fd671` — `feat: improve prototype visual readability` ;
- `2d198a4` — `feat: add progressive encumbrance movement penalty` ;
- `4d2be34` — `fix: preserve inventory and fixed-tick invariants` ;
- `83cb82c` — `fix: bind inventory containers to canonical definitions` ;
- `52c7517` — `fix: preserve inventory construction atomicity` ;
- `e83b590` — `docs: close lot 5 inventory review` ;
- `f7d4923` — `feat: add seamless isometric terrain` ;
- `93c22db` — `docs: close lot 6b terrain review` ;
- `4df2a2e` — `fix: remove isometric terrain seams` ;
- `a46e994` — `feat: add ground-anchor camera follow`.

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

Le lot 5D est validé et commité sous `2d198a4`. Il ajoute dans le Core une
courbe progressive de surcharge basée sur la charge perçue rapportée à la
capacité principale : `100 % → ×1,00`, `125 % → ×0,81`, `150 % → ×0,63`,
`175 % → ×0,44`, `200 % et plus → ×0,25`, avec interpolation linéaire.
`DebugPlayerController` applique le multiplicateur au déplacement à tick fixe et
l'interface affiche le pourcentage de charge et la vitesse résultante. Les
132/132 cas EditMode passent en batchmode et dans le Test Runner ; le Play Mode
et la Console sont validés. Sprint, endurance et dégâts restent hors périmètre.

La revue globale avant fusion a trouvé deux invariants à corriger : une
définition contradictoire partageant un identifiant stable pouvait fausser les
calculs de capacité, et plusieurs ticks fixes d'une même frame pouvaient
réutiliser un multiplicateur de surcharge périmé. Le commit `4d2be34` impose la
compatibilité identifiant/type/encombrement avant mutation et recalcule la
charge dans chaque tick fixe. Les 137/137 cas EditMode passent dans l'éditeur et
en batchmode ; le Play Mode et la Console sont validés.

La revue finale des artefacts a ensuite couvert le cas d'un conteneur enregistré
mais vide. Tous les conteneurs d'un `PlayerInventoryState` sont désormais liés
au registre canonique dès la construction, même sans entrée, et conservent les
empreintes des définitions après retrait de la dernière entrée. Une mutation
ultérieure ne peut donc introduire ni identifiant inconnu, ni définition
contradictoire. Un contrôle supplémentaire valide le conteneur principal avant
toute liaison, afin qu'une construction invalide ne modifie pas les conteneurs
fournis. Cinq cas supplémentaires portent la suite à 142/142 dans le Test Runner
graphique et en batchmode ; les transferts, l'équipement, la surcharge et la
Console sont validés en Play Mode.

Le lot 6B remplace uniquement les trois textures de sol temporaires par les
deux tuiles 64 × 32 de l'atlas Godot interne validé par Naël. Les chemins et
GUID Unity restent inchangés ; la grille isométrique 1 × 0,5, les 64 PPU et le
code Runtime ne changent pas. Le pavage alpha déterministe 10 × 10 passe de
10,3515625 % à 0,421875 % de pixels internes non couverts. Deux contrôles
EditMode vérifient désormais les réglages d'import pixel art et le raccord : la
suite complète atteint **144/144 cas EditMode** en batchmode, avec code de
sortie 0. La capture Play Mode réelle a toutefois invalidé la conclusion
visuelle du lot 6B : le test alpha ne couvrait pas les coutures opaques répétées
et ne constituait donc pas une preuve suffisante de raccord.

Le lot 6C est validé visuellement et techniquement par Naël. Le diagnostic
confirme que les tuiles 64 × 32 portent une bordure opaque qui doit être
recouverte dans l'ordre isométrique. Unity rendait les cellules en bloc, sans le
recouvrement d'un pixel nécessaire. Le correctif configure un tri individuel
`TopRight`, l'axe vertical du Renderer 2D et un pas diagonal rendu de 15 pixels.
Les PNG, les 64 PPU et les positions Core restent inchangés. La suite atteint
**145/145 cas EditMode**. Deux Tilemaps adjacentes de 5 × 5 cellules produisent
exactement le même rendu qu'une Tilemap unique de 10 × 5, joueur compris, avec
**0 pixel différent**. Le lot 6C a été intégré dans `main` par fast-forward au
commit `4df2a2ef3ac2e86528f6172520388bcf5484084e`, arbre
`33dbb7f6da032827e83b4d2c3e9edba515b712a3`. La suite complète sur `main` a
confirmé **145/145 cas EditMode** réussis. Le lot 6C est fermé.

Le lot 7A retire le cadrage automatique fondé sur les dimensions et les bounds
de la Tilemap. `GroundAnchorCameraFollow` suit en `LateUpdate` le point d'ancrage
au sol du visuel joueur, conserve Z et applique le zoom technique provisoire
`4.0625`, sans dépendance aux cellules ni au Core. Le lot est intégré dans
`main` au commit `a46e99434ad68ea5f87c037328d4a95bc3d15435`, arbre
`4405d7e1c3a3327625f6e2d28e3ca5d16c8134d9`. La suite complète sur `main`
passe à **154/154 cas EditMode**, avec validation Play Mode favorable. Le lot
7A est fermé.

Le lot 7B est installé et validé sur la branche dédiée
`codex/lot7b-deterministic-world-generation`, sans modification Runtime ni scène.
Il introduit dans le Core une seed 64 bits canonique, une version de générateur,
des coordonnées monde/chunk signées sur 64 bits, une disposition technique
`32 × 32`, un échantillonneur entier sans état, un générateur de chunks
immuables à la demande et une couche sparse séparée pour les modifications.
Les sorties de la version 1, les coordonnées négatives, les limites `Int64`, les
bords, l'ordre de génération, le déchargement/régénération et la séparation
généré/modifié sont couverts par **93 nouveaux cas EditMode**. La suite complète
est validée à **247/247** sous Unity 6000.3.19f1, avec zéro échec, zéro cas ignoré
ou inconclusif, code Unity `0` et XML `/tmp/lot7b-editmode-results.xml` vérifié.
Aucun Play Mode n'est requis. Biomes, terrain final, ressources, spawn, streaming
Unity et sauvegarde disque restent hors périmètre. Le lot n'est pas encore commité.

## Limites techniques connues

- `ground_water.png` contient provisoirement l'apparence de grass et ne
  constitue pas une vraie tuile d'eau ;
- `InventoryOperations` garantit l'identité unique à l'intérieur d'un conteneur,
  mais pas encore l'unicité globale d'une `ItemInstanceId` entre tous les
  conteneurs d'un `PlayerInventoryState` ;
- retirer directement un objet unique équipé via une opération de conteneur peut
  laisser une référence d'équipement orpheline. Avant d'autoriser le dépôt au
  sol, la destruction ou la persistance d'objets uniques équipés, les mutations
  devront passer par une frontière agrégée qui maintient ces deux invariants.

## Prochaine action

1. Terminer la revue indépendante du patch et du rapport du lot 7B.
2. Obtenir la validation de Naël avant tout commit.
3. Créer un commit unique sur la branche dédiée, puis contrôler son parent, son
   arbre, ses 27 chemins et l'état Git final.
4. Intégrer ensuite par avance rapide pure dans `main` et relancer les **247/247**
   cas EditMode sur l'arbre intégré.
5. Ne commencer ni le lot 7C ni un adaptateur de rendu chunké avant fermeture
   complète de 7B.

## Dépôts archivés

Le prototype Godot et le benchmark moteur restent des références historiques séparées. Ils ne doivent pas être modifiés par ce dépôt.
