# Population initiale du monde

<!-- LOT7C_EDITMODE_STATUS: PASS -->
<!-- LOT7C_PLAYMODE_STATUS: PASS -->

## Statut

Le lot 7C relie la fondation déterministe du lot 7B à un premier monde logique
peuplé. Le profil `temperate-prototype@1` reste **provisoire artistiquement**,
mais il est retenu pour ce jalon. Naël avait accepté le terrain, la densité, le
spawn et le cadrage lors de la première revue ; la capture de remplacement
résout l'unique réserve sur la lisibilité de l'eau. Une modification persistée
de ses paramètres exigera une nouvelle révision de profil ou une nouvelle
version de générateur.

Validation EditMode : **316/316 réussis sous Unity 6000.3.19f1, code Unity `0`**.
Validation Play Mode : **acquise** sur la capture de remplacement SHA-256
`a182836b84bd27fbfa5ad04c47b12b404fef37a90ff8265bdc211c1ff8ce22db`.
L'eau de débogage est clairement distincte ; l'asset final reste reporté.

## Identité save-facing

Une population est reconstruite depuis :

- `WorldSeed` sur 64 bits ;
- `WorldGeneratorVersion` ;
- `WorldPopulationProfileId` ;
- la révision entière du profil ;
- la coordonnée monde absolue ;
- un flux de génération nommé.

La nouvelle version est `PopulationV1` (`2`). `FoundationV1` (`1`) reste
supportée et ses fixtures ne sont pas modifiées. Une version inconnue est
refusée.

## Flux déterministes

Les décisions utilisent des domaines séparés :

- `TerrainElevation` ;
- `TerrainSoil` ;
- `LandscapeZone` ;
- `ResourceCandidate` ;
- `ResourcePriority` ;
- `SpawnPriority`.

Ajouter un flux ultérieur ne décale pas les décisions existantes. Aucun flux
mutable ni `System.Random` n'est utilisé.

## Champs continus entiers

`DeterministicWorldFields.SampleSmoothed16` construit un champ Q16 à partir de
quatre échantillons de lattice, d'un lissage smoothstep et d'interpolations
entières. Le résultat dépend uniquement de la seed, de la version, du flux et de
la coordonnée monde. Il ne dépend ni de l'ordre de chargement, ni des
`GameObject`, ni de la taille de la partition en chunks.

Les coordonnées négatives utilisent une division plancher. Les limites signées
sur 64 bits sont couvertes par les tests.

## Profil `temperate-prototype@1`

Valeurs initiales, non définitives :

| Paramètre | Valeur |
|---|---:|
| échelle d'élévation | 24 cellules |
| échelle de sol | 11 cellules |
| échelle de zone | 16 cellules |
| seuil eau | 12 288 / 65 535 |
| seuil terre nue | 19 660 / 65 535 |
| seuil boisé | 34 406 / 65 535 |
| chance de ressource en zone ouverte | 2 621 / 65 536, environ 4 % |
| chance de ressource en zone boisée | 7 864 / 65 536, environ 12 % |
| rayon d'exclusion des ressources | 2 cellules |
| dégagement du spawn | 1 cellule |
| rayon maximal de recherche du spawn | 48 cellules |

Ces nombres sont éditoriaux. Ils ne doivent pas être confondus avec une
direction artistique finale ou un équilibrage de gameplay validé.

## Terrain et zones

Chaque cellule générée contient :

- un terrain : `Grass`, `Dirt` ou `Water` ;
- une zone : `Open` ou `Wooded` pour les cellules terrestres ;
- `None` pour l'eau.

Le terrain et la zone sont calculés en coordonnées monde absolues. Deux chunks
adjacents partagent donc les mêmes décisions de bord, quelle que soit leur ordre
de génération.

## Ressources

Le premier type logique est `Shrub`, raccordé au rendement de branches du
prototype existant. Une cellule terrestre devient d'abord candidate selon la
densité de sa zone. Un amincissement déterministe par priorité conserve ensuite
uniquement le candidat de priorité minimale dans le rayon d'exclusion. En cas
d'égalité, l'ordre stable des coordonnées tranche.

Cette règle garantit la distance minimale y compris entre deux chunks générés
séparément. Elle s'inspire du besoin de distributions à distance minimale, mais
l'implémentation est originale, sans bibliothèque ni code tiers importé.

L'identifiant d'une ressource inclut la seed, la version de générateur, le profil,
sa révision, le type et la coordonnée monde. Il reste indépendant du découpage en
chunks.

## Spawn

La recherche part d'une cellule préférée et visite les anneaux de distance de
Chebyshev croissante. Une cellule valide doit :

- être terrestre ;
- appartenir à une zone ouverte ;
- respecter le dégagement de ressources.

Parmi les cellules valides du premier anneau disponible, un flux de priorité
stable choisit le résultat. Le spawn est donc reproductible et ne dépend pas de
l'ordre d'énumération des chunks.

## Base générée et état mutable

`PopulatedChunk` est immuable et expose uniquement des copies de ses tableaux.
Les ressources générées deviennent des `ResourceState` mutables seulement à la
frontière Runtime du prototype. Récolter ou masquer une ressource ne doit jamais
modifier la base générée. La persistance future stockera les modifications ou
suppressions par identifiant stable, pas une copie complète du chunk.

`OnDemandPopulatedWorld` reste un cache détenu par l'appelant. Il ne constitue ni
un système de streaming, ni une simulation hors écran, ni une sauvegarde.

## Adaptateur Unity provisoire

`SampleScene` affiche un chunk `32 × 32`, seed `0`, coordonnée `[0, 0]`. Le
Runtime :

- remplit la Tilemap par bloc ;
- réutilise les sprites grass/dirt/water existants ;
- applique une teinte bleue de débogage à l'eau, car `ground_water.png` est encore
  le duplicata provisoire du grass ;
- crée les ressources depuis la base générée ;
- place le joueur sur le spawn généré ;
- conserve la caméra et le zoom technique `4.0625` du lot 7A.

Cette teinte n'est pas un asset final. L'échelle, les pivots, le tri Y, les trois
zooms et le cadrage artistique restent au lot 7D.

## Hors périmètre

- collision ou interdiction de marcher sur l'eau ;
- streaming de plusieurs chunks autour du joueur ;
- biomes multiples, rivières, routes et relief physique ;
- arbres, rochers, animaux ou tables de ressources multiples ;
- sauvegarde disque des suppressions de ressources ;
- simulation hors écran ;
- Jobs, Burst ou DOTS ;
- calibrage visuel final.
