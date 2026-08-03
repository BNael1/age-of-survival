# Génération du monde

## Statut

Le lot 7B introduit uniquement la fondation déterministe et chunkée du monde.
Il ne choisit encore ni biome, ni type de terrain final, ni densité de ressources,
ni point d'apparition. Ces décisions appartiennent au lot 7C.

## Identité d'un monde généré

L'identité minimale est composée de :

- `WorldSeed` : entier non signé sur 64 bits, affiché sous la forme canonique
  `0x0000000000000000` ;
- `WorldGeneratorVersion` : entier strictement positif ;
- `ChunkLayout` : dimensions explicites de la partition chargée et sauvegardée.

La version active est `FoundationV1` (`1`). La taille technique prototype est
`32 × 32` cellules. Cette taille n'est pas incorporée dans l'échantillonnage
d'une cellule : une même coordonnée monde conserve donc le même échantillon si
la partition en chunks change. Une migration de disposition restera néanmoins
nécessaire pour les modifications persistantes déjà enregistrées.

Une version inconnue est refusée. Le jeu ne doit jamais régénérer silencieusement
un ancien monde avec un nouvel algorithme.

## Coordonnées

- `WorldCellCoordinate` utilise deux entiers signés sur 64 bits ;
- `ChunkCoordinate` utilise deux entiers signés sur 64 bits ;
- la position locale dans un chunk réutilise `GridPosition` ;
- les coordonnées négatives utilisent une division plancher explicite.

Exemple avec des chunks de largeur 32 :

- monde `x = -1` → chunk `x = -1`, local `x = 31` ;
- monde `x = -32` → chunk `x = -1`, local `x = 0` ;
- monde `x = -33` → chunk `x = -2`, local `x = 31`.

Ces conversions doivent rester indépendantes de l'ordre de chargement.

## Échantillonnage déterministe

`DeterministicWorldSampler` est sans état. Il reçoit :

- la seed ;
- la version de générateur ;
- la coordonnée absolue de cellule ;
- un `GenerationStream` stable.

Il retourne un entier de 64 bits, ou un réel dans `[0, 1)` dérivé des 53 bits
de poids fort. Il n'utilise pas `System.Random`, dont la séquence n'est pas
garantie entre versions majeures de .NET.

Le mélange de la version 1 est écrit directement dans le Core avec des
opérations entières `unchecked`. Il reprend les constantes de diffusion du
mélange SplitMix64 publié par Steele, Lea et Flood, sans intégrer de bibliothèque
ou de code tiers. Il n'est pas cryptographique.

Les sorties de référence de la version 1 sont verrouillées par des tests. Toute
modification volontaire de l'algorithme doit créer une nouvelle version au lieu
de modifier les fixtures existantes.

## Chunks générés à la demande

`DeterministicChunkGenerator` calcule chaque cellule à partir de sa coordonnée
monde absolue. Par conséquent :

- même seed + même version + même coordonnée = même résultat ;
- l'ordre de génération des chunks n'influence pas leur contenu ;
- décharger puis régénérer un chunk reproduit son contenu ;
- les bords positifs et négatifs restent contigus ;
- deux dispositions de chunks peuvent échantillonner le même monde logique.

`OnDemandGeneratedWorld` est seulement un cache possédé par l'appelant. Il ne
contient pas de simulation hors écran, de politique d'éviction, de chargement
asynchrone ni d'adaptateur Unity.

## Séparation généré / modifié

`GeneratedChunk` est une base immuable. Il n'expose aucune opération de mutation
et retourne des copies de ses tableaux.

`ChunkModificationLayer<T>` est une couche sparse distincte, indexée par
position locale. Sa copie publique est triée en ordre ligne par ligne afin de
fournir une base canonique pour un futur DTO de sauvegarde.

La sauvegarde future devra conserver au minimum :

- la seed ;
- la version de générateur ;
- la disposition des chunks utilisée par les modifications ;
- les modifications persistantes sparse ;
- la version du format de sauvegarde.

Elle ne devra pas sauvegarder par défaut chaque cellule générée si celle-ci peut
être reconstruite à l'identique.

## Flux de génération

Le lot 7B réserve uniquement `GenerationStreams.Foundation`. Le lot 7C devra
ajouter des identifiants de flux nommés et stables pour séparer les décisions,
par exemple terrain de base, humidité, végétation, ressources et spawn. Ajouter
un nouveau flux ne doit pas décaler les résultats des flux existants.

## Hors périmètre

- bruit continu et biomes ;
- variantes visuelles de terrain ;
- placement de ressources ;
- règles de densité et distances minimales ;
- spawn du joueur ;
- rendu ou Tilemap chunkée ;
- streaming autour de la caméra ;
- sauvegarde sur disque et migrations ;
- simulation hors écran ;
- Jobs, Burst ou DOTS.
