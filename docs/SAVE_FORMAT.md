# Format de sauvegarde

## Statut

Le format autoritaire courant est `AOSSAVE` V3. Le codec, le stockage atomique et la lecture rétrocompatible V1/V2/V3 sont implémentés dans le Core.

## Invariants décidés

- version explicite du format ;
- identifiants stables ;
- migrations explicites ;
- séparation entre monde généré et modifications persistantes ;
- chargement partiel futur par chunk ;
- écriture atomique avec récupération ;
- tests aller-retour et tests de migration ;
- aucune dépendance envers la sérialisation automatique d’une scène Unity.

Les évolutions futures doivent conserver une migration explicite et testée depuis chaque version encore supportée.

## Contrat préparé par le lot 7B

Une future sauvegarde de monde doit conserver au minimum :

- la seed 64 bits ;
- la version du générateur ;
- la disposition des chunks utilisée par les modifications ;
- les modifications sparse, séparées de la base générée ;
- la version du format de sauvegarde.

Les cellules générées reconstructibles ne doivent pas être dupliquées par
défaut dans la sauvegarde. Une migration explicite sera nécessaire si la
version du générateur ou la disposition des modifications change. Le lot 7B
n'écrit encore aucun fichier de sauvegarde.

<!-- LOT7C_SAVE_FORMAT -->
## Contrat de population préparé par le lot 7C

Une sauvegarde reconstruisant `PopulationV1` doit conserver, en plus de la seed
et de la version du générateur :

- l'identifiant du profil de population ;
- sa révision ;
- les suppressions ou transformations de ressources par identifiant stable ;
- les modifications de terrain séparées de la base générée ;
- le spawn choisi si le jeu autorise ensuite un déplacement ou un choix manuel.

Le contenu reconstructible d'un `PopulatedChunk` ne doit pas être sérialisé par
défaut. Modifier les paramètres d'un profil déjà persisté sans changer sa
révision est interdit.

<!-- LOT7EB_SAVE_FORMAT -->
## Substrat sparse préparé par le lot 7E-B

Le lot 7E-B n'écrit toujours aucun fichier de sauvegarde et ne définit pas de
schéma disque. Il fournit toutefois le premier état mutable chunké exportable :

- coordonnées du chunk propriétaire ;
- ressources récoltées avec identifiant stable et position attendue ;
- conteneurs de sol non vides avec identifiant, position, définition de
  conteneur et capacité ;
- entrées empilées ou uniques avec identifiants, quantité, encombrement et
  référence éventuelle de conteneur imbriqué ;
- ordre canonique indépendant de l'ordre d'insertion Runtime.

Une future sauvegarde devra associer ces mutations à l'identité complète du
monde généré et du profil de population, puis les versionner et les migrer
explicitement. La restauration doit continuer à régénérer la base avant
d'appliquer la mutation ; sérialiser les 49 chunks du cache ou les Tilemaps est
interdit comme format principal.

Les révisions et digests du protocole multijoueur sont des contrats de
réplication de la tranche 7E-B. Ils ne constituent pas automatiquement la
version du futur format de sauvegarde. Les compteurs d'éviction et de
restauration sont de l'instrumentation et n'ont pas à être persistés sauf besoin
de diagnostic décidé ultérieurement.

<!-- LOT7FA1_SAVE_FORMAT -->
## Snapshot canonique d'inventaire préparé par le lot 7F-A1

Le lot 7F-A1 ne définit toujours aucun octet de format et n'écrit aucun fichier.
Il fournit l'entrée Core validée du futur codec :

- identifiant du conteneur principal ;
- définitions triées avec type d'état, encombrement et règles d'équipement ;
- conteneurs triés avec identifiant, clé stable et capacité ;
- entrées triées avec définition, quantité, instance et conteneur imbriqué ;
- trois références d'équipement dans un ordre d'emplacements fixé.

Les textes d'affichage sont exclus des empreintes de compatibilité afin de ne
pas lier les sauvegardes à la localisation. La capture est une copie immuable :
une mutation ultérieure du jeu ne modifie pas le snapshot déjà produit.

Avant capture, le Core rejette les instances dupliquées, les conteneurs contenus
absents, possédés plusieurs fois ou cycliques, le conteneur principal utilisé
comme contenu et les équipements absents, dupliqués ou incompatibles.

La version, la magie, les longueurs bornées, l'intégrité SHA-256, le décodage et
les migrations appartiennent au lot 7F-A2. L'écriture atomique sur disque reste
hors périmètre jusqu'au lot 7F-B.

<!-- LOT7FA2A_SAVE_FORMAT -->
## Snapshot complet préparé par le lot 7F-A2a

Le futur codec reçoit désormais un `GameSaveSnapshot` canonique contenant :

- la seed, la version du générateur et la disposition des chunks ;
- l'identifiant et la révision du profil de population ;
- le tick fixe non négatif ;
- la position finie du joueur, avec zéro flottant normalisé ;
- le snapshot canonique d'inventaire 7F-A1 ;
- toutes les mutations sparse non vides, triées par coordonnées.

Les mutations sont capturées indépendamment de leur résidence au moment de la
sauvegarde : store évincé et chunks actifs sont réunis sans modifier l'état
vivant. Une disposition incompatible, une coordonnée dupliquée ou une mutation
vide rend la capture invalide.

Le lot ne définit toujours aucun octet et n'écrit aucun fichier. Le format
binaire V1, ses limites, son intégrité et son décodage appartiennent à 7F-A2b.

<!-- LOT7F_V1_SAVE_FORMAT -->
## Format binaire V1

Tous les entiers utilisent l'ordre little-endian. L'enveloppe est fixe :

| Offset | Taille | Champ |
|---:|---:|---|
| 0 | 8 | magie ASCII `AOSSAVE\0` |
| 8 | 2 | version, `1` |
| 10 | 2 | flags, `0` |
| 12 | 4 | longueur du payload |
| 16 | 32 | SHA-256 du payload |
| 48 | N | payload canonique |

Le payload encode dans l'ordre l'identité monde, le tick et la position joueur,
l'inventaire puis les mutations sparse. Les chaînes sont précédées d'une
longueur `u32`, utilisent UTF-8 strict sans BOM et sont limitées à 4096 octets.
Les identifiants optionnels utilisent une longueur nulle ; les champs
obligatoires refusent cette représentation.

Les limites V1 sont : payload 256 MiB, 4096 définitions, 4096 conteneurs joueur,
65536 entrées par conteneur, 1000000 chunks mutés, 65536 ressources récoltées ou
conteneurs au sol par chunk et 65536 objets par conteneur au sol. Le décodeur
vérifie la limite avant toute allocation dépendant du fichier.

Un slot disque utilise `<slot>.aos`, `<slot>.bak` et `<slot>.tmp`. La principale
est essayée en premier, puis le backup. Les fichiers invalides sont conservés
pour diagnostic ; aucune migration, promotion ou quarantaine implicite n'existe
en V1. Les écritures sont synchrones et exigent un seul écrivain à la fois pour
un même slot.
<!-- LOT7GA_SLOT_METADATA -->
## Métadonnées de chronologie V1

Chaque slot `slot-1` à `slot-3` peut posséder un fichier d'affichage
`slot-N.aosmeta`. Il contient une version, l'index du slot, l'horodatage UTC, la
durée jouée, la seed et un indicateur de récupération depuis le backup. Son
écriture utilise un temporaire et un remplacement avec backup.

Ce fichier n'appartient pas au format autoritaire de partie. Son absence ou sa
corruption ne doit pas rendre `.aos` ou `.bak` illisible; l'interface affiche
alors que les informations sont indisponibles et tente le chargement normal.
Aucune migration du codec V1 n'est requise pour modifier ultérieurement ces
métadonnées d'interface.

<!-- LOT7HA2_SAVE_FORMAT -->
## Format binaire V2 — santé du joueur

Le codec écrit désormais exclusivement la version `2` de l'enveloppe
`AOSSAVE\0`. La magie, les flags, la longueur, le SHA-256 et les limites
restent identiques à la V1. Le payload V2 encode dans l'ordre :

1. l'identité du monde ;
2. le tick fixe ;
3. la position du joueur ;
4. le snapshot de santé ;
5. l'inventaire ;
6. les mutations sparse de chunks.

Le snapshot de santé encode `MaximumHealth` et `CurrentHealth` en `i32`,
`CurrentTick` en `i64`, puis un booléen indiquant la présence de
`NextRegenerationTick` et, lorsqu'il est présent, ce tick en `i64`. Les
invariants de `PlayerHealthState` sont réappliqués au décodage. Le tick de
santé doit être exactement égal au tick fixe de la partie.

Le lecteur accepte les versions `1` et `2`, avec les flags `0` uniquement. Une
V1 ne contenant aucun champ de santé est migrée en mémoire vers `100/100` PV
sur son tick sauvegardé, sans régénération planifiée. Le fichier lu n'est ni
réécrit ni promu implicitement ; la prochaine sauvegarde normale produit une
V2. Une V2 dont la santé est invalide ou désynchronisée du tick de partie est
refusée avant installation de la session.

<!-- LOT7I_SAVE_FORMAT -->
## Format binaire V3 — nourriture et lots périssables

Le codec écrit désormais exclusivement `AOSSAVE` version `3`. L'enveloppe,
les flags, le SHA-256 et les limites générales restent compatibles avec les
versions précédentes.

Le payload V3 encode, dans l'ordre :

1. identité du monde ;
2. tick fixe ;
3. position du joueur ;
4. santé V2 ;
5. état de satiété ;
6. lots périssables ;
7. inventaire canonique ;
8. mutations sparse des chunks.

L'état de satiété conserve le maximum, la valeur courante, le tick alimentaire
et le prochain tick de perte. Son tick courant doit être identique au tick fixe
de la partie.

Chaque lot périssable conserve un `FoodBatchId`, le `ContainerId`, la définition
d'item, la quantité, l'âge accumulé en milli-ticks et le dernier tick évalué.
Aucun lot ne peut être daté après le tick fixe sauvegardé. Les quantités des lots
doivent correspondre aux piles périssables de l'inventaire restauré.

Une V1 est migrée en mémoire avec santé pleine et satiété pleine au tick fixe ;
une V2 conserve sa santé mais reçoit également la satiété pleine. V1 et V2
reçoivent zéro lot périssable. Le fichier source n'est pas réécrit lors de la
lecture. La prochaine sauvegarde normale produit une V3.

La restauration peut ajouter au registre les définitions éditoriales courantes
absentes d'une ancienne sauvegarde, sans ajouter d'entrée ni de quantité. Cela
permet à une partie V1/V2 de recevoir ensuite un nouvel aliment tel que `apple`.

La capture autoritative V3 exige explicitement `PlayerFoodState` et
`PerishableInventoryState` en plus de la santé et de l'inventaire. Une API de
capture qui ne possède pas ces états ne doit pas être utilisée comme chemin de
sauvegarde courant.
