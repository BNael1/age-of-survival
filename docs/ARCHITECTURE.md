# Architecture

## Principe

La simulation est la source de vérité. Les objets Unity représentent seulement la partie visible ou interactive de cet état.

## Couches

### Core

C# pur, sans `UnityEngine`. Contient les états, règles, commandes, événements métier, algorithmes déterministes et formats logiques. Doit être testable sans scène.

### Runtime

Adaptateurs Unity : boucle de jeu, rendu, Tilemap, entrée, UI, audio, caméra et synchronisation entre l’état logique et les objets visibles.

### Content

Définitions éditoriales et assets. Les `ScriptableObject` pourront décrire des items, techniques ou constructions, mais ne posséderont pas l’état mutable principal d’une partie.

### Tests

Les tests EditMode couvrent le Core. Les tests PlayMode seront ajoutés seulement pour les intégrations qui nécessitent réellement le moteur.

## Invariants initiaux

- aucune référence `UnityEngine` dans `AgeOfSurvival.Core` ;
- coordonnées de grille entières ; positions d’entités continues et finies ;
- conversion position/index vérifiée et stable ;
- tick fixe indépendant du framerate ;
- identifiants persistants stables lorsqu’ils seront introduits ;
- sérialisation et migrations versionnées ;
- aucune dépendance tierce ne possède la simulation ou les sauvegardes.

## Découpage envisagé

```text
Assets/AgeOfSurvival/
  Core/
  Runtime/
  Content/
  Tests/
```

Le monde sera conçu pour un découpage en chunks, mais la taille de production des chunks n’est pas encore décidée.

## Premier adaptateur de rendu

Le lot 2 introduit `AgeOfSurvival.Runtime`, qui dépend de `AgeOfSurvival.Core`. La dépendance inverse est interdite.

`DebugIsometricWorld` est un adaptateur temporaire :

- il construit une `DenseGrid<byte>` déterministe ;
- il traduit les coordonnées logiques en cellules d’une Tilemap Unity isométrique ;
- il génère ses visuels de débogage au runtime ;
- il ne définit ni terrain de production, ni gameplay, ni sauvegarde.

Cette preuve d’intégration pourra être remplacée sans modifier les primitives du Core.

## Premier état de joueur et adaptateur d’entrée

Le lot 3 introduit un état de joueur continu dans `AgeOfSurvival.Core` :

- `WorldPosition` stocke deux coordonnées `double` finies sur le plan de simulation ;
- `PlayerState` possède cette position mutable ;
- `PlayerMovement` applique une direction, une vitesse et une durée sans dépendre de Unity ;
- les directions dont la magnitude dépasse un sont normalisées, ce qui évite un gain de vitesse en diagonale.

`DebugPlayerController` reste un adaptateur Unity temporaire :

- il lit ZQSD avec le package Input System ;
- il transforme les directions écran vers les axes du plan isométrique ;
- il fait avancer le Core à tick fixe ;
- il synchronise un marqueur généré au runtime avec la position logique ;
- il ne possède ni collision, ni animation, ni règle de terrain.

Le clavier et le marqueur peuvent être remplacés sans modifier la règle de déplacement du Core.

## Suivi de caméra Runtime

`GroundAnchorCameraFollow` est un adaptateur Unity local au Runtime. Il reçoit
uniquement la caméra et le `Transform` qui représente le point d'ancrage au sol
du visuel joueur. Le contrôleur synchronise ce visuel depuis `PlayerState` en
`Update`, puis la caméra le suit en `LateUpdate`. Le suivi conserve Z. Depuis le
lot 7D-A, la taille orthographique part de `4.0625`, vise une cible
multiplicative bornée à `[2.5, 8.0]` et amortit uniquement le zoom ; le suivi de
l'ancre joueur reste instantané.

Cette frontière interdit au Core toute référence à `UnityEngine.Camera` et
interdit à la simulation de dépendre du zoom. L'adaptateur ne consulte ni la
`DenseGrid`, ni la Tilemap, ni leurs bounds : changer la taille ou l'origine du
monde ne recadre pas la caméra et ne modifie pas le joueur.

## Fondation de génération déterministe

Le lot 7B place la génération dans `AgeOfSurvival.Core.World.Generation`, sans
référence à Unity. L'identité d'un monde est explicite : seed non signée sur
64 bits, version de générateur et disposition de chunks. Les coordonnées monde
et chunk utilisent des entiers signés sur 64 bits ; les positions locales
réutilisent `GridPosition` et une division plancher testée pour les coordonnées
négatives.

`DeterministicWorldSampler` est sans état et échantillonne une cellule depuis sa
coordonnée monde absolue et un `GenerationStream` stable. Le contenu ne dépend
ni de l'ordre de génération, ni de la présence de `GameObject`, ni de la taille
de la partition en chunks. `DeterministicChunkGenerator` produit une base
`GeneratedChunk` immuable ; `OnDemandGeneratedWorld` ne fait que mettre en cache
les chunks explicitement demandés.

Les modifications persistantes restent dans `ChunkModificationLayer<T>`, couche
sparse séparée et exportable en ordre ligne par ligne. Cette séparation interdit
de transformer la base générée en état mutable principal et prépare la future
sauvegarde seed + version + modifications, sans imposer encore un format disque.

## Première interaction avec une ressource

Le lot 4 ajoute au Core un petit état de ressource indépendant de Unity :

- `ResourceId` fournit une identité stable et un ordre ordinal déterministe ;
- `ResourceState` associe cet identifiant à une `WorldPosition` et à l'état
  `Available` ou `Harvested` ;
- `ResourceTargeting` choisit la ressource disponible la plus proche dans un
  rayon inclusif, avec départage par identifiant à distance égale ;
- `ResourceInteraction` applique une commande explicite et récolte au plus une
  ressource.

Le flux d'intégration est :

```text
Input System -> adaptateur Unity -> commande -> Core -> état ressource -> rendu Unity
```

`DebugResourceInteraction` met l'appui sur `E` en attente. Le
`DebugPlayerController` le fait consommer dans son callback de tick fixe après
le déplacement du joueur. L'adaptateur recalcule ensuite la cible via le Core et
met à jour des marqueurs et un contour générés au runtime. Il ne contient ni
règle de distance, ni mutation métier cachée, ni collider, ni récompense.

Le rayon `1.5` et les trois positions de ressources de `SampleScene` sont des
paramètres de débogage temporaires, pas des décisions d'équilibrage ou d'art.

`ExecuteAlways` sert uniquement à garantir le nettoyage des assets générés lors
des tests EditMode. La construction automatique, l'abonnement aux entrées et la
boucle `Update` sont bloqués hors Play Mode. `Rebuild()` reste une opération
explicite de débogage et de test, pas un outil de création de contenu persistant.

## Inventaire et conteneurs Core

Le lot 5A ajoute un domaine d'inventaire entièrement contenu dans
`AgeOfSurvival.Core` :

- `ItemDefinitionId`, `ItemInstanceId` et `ContainerId` sont des identifiants
  ordinaux stables dont la valeur par défaut est invalide et sûre ;
- `EncumbranceValue` stocke des unités entières (`1000` unités internes =
  `1,000` affiché) afin d'éviter les dérives de capacité en virgule flottante ;
- `ItemDefinition` et `ContainerDefinition` portent les données éditoriales
  immuables ;
- `StackedItemState`, `UniqueItemState` et `ContainerState` portent l'état
  mutable, sans l'exposer par une collection modifiable ;
- un objet unique peut référencer un `ContainerId` stable, ce qui distingue
  l'identité du sac de l'identité de son contenu ;
- `InventoryOperations` est l'unique frontière de mutation pour les ajouts,
  retraits et transferts synchrones.

Les entrées conservent leur ordre d'insertion. Un ajout fusionne d'abord une
pile compatible ; les objets uniques ne fusionnent jamais. Un transfert ajoute
d'abord ce qui rentre dans la destination, puis retire exactement cette quantité
de la source. La somme des quantités reste donc conservée, y compris lors d'un
transfert partiel ou vers une destination pleine.

Une entrée portant un `ItemDefinitionId` donné doit rester compatible avec la
définition canonique : même type d'état et même encombrement unitaire. Les
opérations vérifient cet invariant avant toute mutation, et
`PlayerInventoryState` refuse un registre dont les définitions contredisent les
entrées déjà présentes. Lors de sa construction, l'agrégat lie aussi chaque
conteneur enregistré au registre canonique, même lorsqu'il est vide. L'identifiant
du conteneur principal est validé avant cette liaison afin qu'un échec de
construction ne modifie aucun conteneur fourni. Le conteneur conserve ensuite
l'empreinte d'une définition après retrait de la
dernière entrée. Une mutation ultérieure ne peut donc introduire ni identifiant
inconnu, ni données physiques différentes pour contourner la capacité.

<!-- LOT7FA1_ARCHITECTURE -->
Depuis le lot 7F-A1, `PlayerInventoryState` valide la frontière de persistance à
sa construction, lors d'une restauration d'équipement et avant chaque
`CaptureSnapshot()`. L'agrégat refuse les identités d'instances dupliquées, les
conteneurs contenus absents ou possédés plusieurs fois, l'utilisation du
conteneur principal comme contenu, les cycles de possession, ainsi que les
références d'équipement absentes, dupliquées ou incompatibles.

`InventoryOperations` reste une frontière locale à un conteneur et peut encore
produire temporairement un état inter-conteneurs incohérent si un appelant la
contourne au niveau agrégé. Cet état ne peut plus être capturé. Les futures
opérations de dépôt, destruction et déplacement d'objets uniques devront
préserver ces invariants en continu au lieu de compter seulement sur le rejet
avant sauvegarde.

`PlayerInventorySnapshot` est une copie immuable et canonique. Définitions,
conteneurs et entrées sont triés par identifiants ordinaux ; les empreintes de
définition incluent le type d'état, l'encombrement et les règles d'équipement.
La capture ne dépend ni de `UnityEngine`, ni d'un `GameObject`, ni de l'ordre
d'insertion Runtime. Elle ne définit encore aucun codec, fichier ou migration.

Ce lot ne contient ni `MonoBehaviour`, ni rendu, ni UI, ni état possédé par un
`GameObject`. Les adaptateurs Unity et l'équipement sont introduits dans les lots
suivants sans déplacer la source de vérité hors du Core.

## Équipement et prototype d'inventaire

Le lot 5B étend le domaine Core sans le coupler à Unity :

- `EquipmentState` porte trois emplacements fixes (`LeftHand`, `RightHand`,
  `Back`) et référence uniquement des `ItemInstanceId` ;
- `EquipmentOperations` est la frontière de mutation explicite pour équiper et
  déséquiper une instance unique compatible ;
- `PlayerInventoryState` agrège conteneurs, définitions et équipement tout en
  exposant des vues en lecture seule ;
- `CarriedLoadOperations` distingue la charge brute de la charge perçue. Le
  contenu d'un conteneur équipé peut recevoir une réduction entière, sans
  modifier sa capacité brute ni sa propre masse.

`InventoryPrototypeSession` est un objet C# ordinaire possédé par un fournisseur
de session au niveau du processus. Aucun `GameObject` ne possède la source de
vérité. La couche Runtime construit des view-models immuables, puis l'interface
UI Toolkit émet des commandes via `InventoryPrototypeCommands`. Les `ListView`
ne modifient jamais directement les collections du Core.

Le déplacement chargé passe par `InventoryMovementStep`. Ce point de composition
Runtime recalcule la charge perçue et le multiplicateur à l'intérieur de chaque
callback de tick fixe. Une frame qui rattrape plusieurs ticks ne capture donc pas
une valeur unique susceptible de devenir périmée après un transfert ou un
changement d'équipement.

La scène ne contient qu'un `InventoryPrototypeUiBehaviour`, chargé de créer le
`UIDocument`, d'appliquer le thème runtime officiel Unity et de relier la vue à
la session. Les formes, couleurs et textes restent un prototype généré dans le
projet et sont remplaçables sans modifier les règles métier.

## Rendements au sol et transferts temporisés

Le lot 5C relie les ressources au domaine d'inventaire :

- `GroundContainerState` associe un identifiant stable, une `WorldPosition` Core
  et un `ContainerState` servant de source métier ;
- `ResourceYieldOperations` récolte la cible selon la règle existante, crée un
  conteneur de sol dérivé de son `ResourceId` et y dépose le rendement ;
- `TransferActionState` mémorise source, destination, définition, quantités,
  tick de départ, durée, progression, statut et raison finale ;
- `TransferActionOperations` ne réserve ni ne retire rien au démarrage. Au tick
  final, source et capacité sont relues avant un transfert conservatif ;
- un déplacement significatif ou un éloignement interrompt l'action sans
  modifier la source.

`InventoryPrototypeSession` possède désormais les ressources, conteneurs de sol
et l'unique action active du prototype. `DebugResourceInteraction` ne fait que
lire `E`, avancer la session depuis le tick fixe et refléter ressources et restes
au sol. Le marqueur Unity reste présent tant que le conteneur Core n'est pas vide.

La troisième `ListView` et la barre de progression consomment le view-model de
session. La progression affichée est calculée depuis les ticks du Core ; aucune
durée métier ne dépend de `Time.deltaTime`.

## Lisibilité visuelle temporaire du prototype

Le lot 5V ne modifie aucune frontière métier. Les adaptateurs Runtime chargent
des textures PNG temporaires depuis
`Assets/AgeOfSurvival/Runtime/Resources/PrototypeVisuals/` au moyen de
`PrototypeVisualAssets`, puis créent des wrappers `Sprite` détruits avec les
objets de débogage. Les textures importées restent des assets Unity partagés et
ne sont jamais détruites par les adaptateurs.

Le sol distingue visuellement cellules de base, accents et bordure. Le joueur,
les ressources disponibles et les rendements au sol utilisent des silhouettes
séparées. Le rayon d'interaction et la progression de transfert sont des vues
dérivées de la position et de l'action Core existantes ; ils ne portent aucune
règle de portée ou de durée.

Chaque adaptateur conserve son rendu géométrique généré au runtime comme
solution de repli lorsque les textures temporaires sont absentes. Cette couche
est donc réversible et remplaçable sans changement du Core, des identifiants,
de l'entrée ou de la simulation.

Les tuiles de terrain isométriques qui portent des pixels opaques sur leur
frontière suivent un contrat de rendu durable : `TilemapRenderer` en mode
`Individual`, ordre `TopRight`, transparence du Renderer 2D triée sur l'axe Y
et recouvrement rendu d'un pixel entre voisins diagonaux. Ce contrat appartient
uniquement à l'adaptateur visuel ; les coordonnées et dimensions logiques du
Core ne changent pas.

## Surcharge progressive et déplacement

Le lot 5D conserve la règle dans le Core. `EncumbranceMovementOperations` reçoit
la charge perçue et la capacité principale, puis produit un ratio de charge et
un multiplicateur de vitesse. La courbe est bornée et linéaire par morceaux :

```text
100 % -> ×1,00
125 % -> ×0,81
150 % -> ×0,63
175 % -> ×0,44
200 % et plus -> ×0,25
```

La capacité de référence est celle du conteneur principal du joueur. La charge
utilisée est la charge perçue calculée par `CarriedLoadOperations`, donc les
réductions des conteneurs équipés s'appliquent avant la pénalité de déplacement.
Les unités d'encombrement restent entières ; seul le ratio dérivé et son
multiplicateur sont des `double`.

`PlayerMovement` accepte un multiplicateur explicite sans connaître
l'inventaire. `DebugPlayerController` lit l'état de mouvement de la session du
prototype à chaque frame, puis le transmet à chaque tick fixe. L'interface ne
recalcule aucune règle : elle affiche les valeurs produites par le Core.

Sprint, endurance, dégâts de surcharge et effets d'animation sont reportés. Ils
pourront consommer le même ratio sans modifier la courbe ni déplacer la source
de vérité vers Unity.

<!-- LOT7C_ARCHITECTURE -->
## Population initiale déterministe

`WorldPopulationSettings` associe l'identité de génération à un profil éditorial
versionné. `DeterministicWorldPopulationGenerator` reste dans le Core pur : il
échantillonne les cellules absolues, génère `PopulatedChunk`, place les
ressources et recherche le spawn sans dépendre d'Unity ni d'un ordre de chargement.

`PopulatedChunk` est une base immuable. `OnDemandPopulatedWorld` est un cache
appelant. Le Runtime transforme les placements générés en `ResourceState`
mutables pour le prototype, sans écrire dans la base générée. `DebugIsometricWorld`
reste un adaptateur provisoire limité à un chunk et à la Tilemap existante.

## Caméra progressive et profondeur visuelle

Le lot 7D-A reste entièrement dans `AgeOfSurvival.Runtime`. L'état testable
`OrthographicZoomState` possède uniquement la taille courante, la cible et la
vélocité d'amortissement ; `GroundAnchorCameraFollow` échantillonne Input System
comme delta matériel en pixels, le normalise en pas logiques avec un réglage
Runtime positif et fini, puis applique ces valeurs à `Camera`. La sensibilité
est appliquée après cette normalisation. La simulation Core ne reçoit aucun
état de zoom et la position joueur n'est jamais réécrite par la caméra.

Le joueur et les ressources placent leur `Transform` visuel sur une ancre au
sol explicite. `GroundAnchorSortCoordinator` attend leur synchronisation en
`Update`, puis applique en `LateUpdate` une seule passe de `GroundAnchorSorting`
sur le joueur et toutes les ressources actives. Y puis l'identifiant ordinal
stable déterminent l'ordre de rendu. L'échelle `1.20` du joueur, les pivots et
les ordres de rendu sont donc des données de présentation Runtime, sans effet
sur les dimensions, interactions ou règles du Core. Le détail du contrat est
centralisé dans `CAMERA_AND_SORTING.md`.

## Frontend, navigation et pause

Le lot 7D-B reste dans `AgeOfSurvival.Runtime`. `FrontendRuntimeBootstrap`
observe les scènes chargées et installe l'adaptateur approprié :
`MainMenuBehaviour` dans `MainMenu`, `PauseMenuBehaviour` dans `SampleScene`.
Les documents UI Toolkit sont construits par code et ne possèdent ni état de
simulation, ni persistance, ni connexion réseau.

`FrontendController` dépend uniquement de petites interfaces testables :
`IFrontendSceneLoader`, `IApplicationQuitter`, `ISaveAvailability` et
`IOnlineFrontendAvailability`. Les adaptateurs Unity utilisent
`SceneManager.LoadSceneAsync` en mode `Single` et `Application.Quit`. Une
transition verrouille les commandes avant le chargement ; si elle est refusée
ou lève une exception, l'état précédent du verrou est restauré.

`GameplayInputGate` est un verrou local au processus et au Runtime. Les
adaptateurs joueur, ressources et caméra le consultent avant de lire les
commandes physiques ou de faire avancer le tick fixe. Lorsque le verrou est
actif, le contrôleur joueur appelle néanmoins la branche bloquée de l'adaptateur
de ressources afin d'annuler une interaction déjà mise en attente, sans avancer
le tick. La pause arrête donc le prototype et ses interactions sans utiliser
`Time.timeScale` et sans exposer le menu au Core. L'état de pause est réappliqué
après la construction différée du
`UIDocument`, ce qui évite un jeu bloqué avec une interface encore masquée.

La scène `MainMenu` réutilise provisoirement `DebugIsometricWorld` avec la seed
`0` comme arrière-plan non interactif. Le frontend d'inventaire y est désactivé
et un voile sombre sépare le décor de la navigation verticale. `Nouvelle
partie` charge la scène de gameplay existante ; la seed reste vérifiée dans le
monde généré, pas stockée comme état mutable du menu.

La sauvegarde et le réseau sont représentés par des services de disponibilité.
Leurs implémentations actuelles retournent `false`, ce qui conserve `Charger`,
`Rejoindre`, `Héberger` et `Serveurs favoris` dans la structure de navigation
sans simuler un backend inexistant. Le futur client et le serveur autoritaire
VPS remplaceront ces adaptateurs sans déplacer la simulation ou la sauvegarde
centrale hors du contrôle du projet.

<!-- LOT7EB_ARCHITECTURE -->
## Mutations sparse et cycle de vie des chunks

`ChunkMutationStore` appartient au Core et stocke uniquement les différences
non reconstructibles d'un chunk : ressources récoltées et conteneurs de sol non
vides, avec identifiants, positions, définitions et quantités nécessaires à une
restauration exacte. Les collections sont validées puis ordonnées
canoniquement. La base `PopulatedChunk` reste immuable et n'est jamais remplacée
par une copie sérialisée de l'état actif.

`ChunkStateLifecycle` impose une propriété exclusive : un chunk est soit actif,
soit représenté dans le store sparse, jamais les deux. L'activation génère la
base puis applique éventuellement la mutation ; l'éviction extrait la mutation
avant de retirer l'état actif. Un échec de restauration remet la mutation dans
le store afin de ne pas consommer partiellement l'état persistant.

Le Runtime conserve son propre raccord à la session prototype. Avant une
éviction, `DebugIsometricWorld` construit la fenêtre prospective et appelle un
unique propriétaire transactionnel. `InventoryPrototypeSession` prépare les
ressources et conteneurs futurs, vérifie les identités stables et les transferts
actifs, puis committe toutes ses collections en une seule étape. Le cache du
monde n'est déchargé qu'après cette prévalidation.

## Cache Runtime borné

La visibilité (`3 x 3`) et la préparation (`5 x 5`) restent séparées de la
rétention (`7 x 7`, rayon `3`). Les neuf Tilemaps visibles continuent d'être
mises en pool. Les chunks générés situés hors rétention sont supprimés du cache,
tandis que leurs seules mutations non reconstructibles restent dans le store.
La limite de 49 concerne le cache actif, pas l'étendue logique du monde ni le
nombre futur de mutations persistées.

## Tranche réseau autoritaire

`AgeOfSurvival.Protocol` contient le codec binaire et ses contrats sans posséder
le transport Unity. Le protocole version `1` borne chaque message à `1024`
octets, refuse les types inconnus, les octets réservés non nuls, les chaînes de
contrôle, les données terminales et les versions incompatibles. La version de
build de la tranche est `7E-B.1`.

`MultiplayerProcessSession` sélectionne au démarrage un rôle serveur ou client de
smoke par arguments de ligne de commande. Le serveur utilise Unity Transport
`2.7.4` avec un pipeline fiable et séquencé. Chaque connexion possède son état
d'authentification et de préparation. Les paquets ou transitions invalides
rejettent uniquement le pair concerné. Les envois défaillants sont confinés au
pair au lieu de remonter au gestionnaire fatal du processus.

`AuthoritativeMultiplayerSimulation` reste dans le Core. Le serveur seul applique
les commandes et produit `AuthoritativeWorldSnapshot`. Le digest couvre la
révision, l'identifiant et la disponibilité de la ressource ainsi que les
compteurs d'éviction et de restauration. Un client refuse une réécriture
divergente à révision identique et ne déclare sa complétion qu'après convergence.

Cette tranche n'est pas l'architecture multijoueur complète. Elle n'introduit
ni authentification de compte, chiffrement applicatif, navigateur de serveurs,
NAT traversal, prédiction, rollback, réplication générale des entités,
persistance serveur, administration, anti-triche ou serveur autoritaire de
production. Elle valide uniquement les frontières Core/protocole/transport et
les builds multiplateformes.

## Builds multiplateformes

`AgeOfSurvival.Editor.MultiplayerBuild` centralise les builds batchmode des
clients macOS ARM64 et Windows x86-64 ainsi que du serveur Linux x86-64. Les
scènes actives viennent des réglages de build ; l'architecture précédente de
l'éditeur est restaurée après chaque construction. Les artefacts de build ne
sont pas des sources de vérité et ne doivent pas être commités.

<!-- LOT7FA2A_ARCHITECTURE -->
## Snapshot complet de partie

Le lot 7F-A2a introduit `AgeOfSurvival.Core.Persistence` comme frontière
d'agrégation précédant tout codec. `WorldIdentitySnapshot` conserve l'identité
reconstructible du monde : seed, version de générateur, disposition des chunks,
identifiant du profil de population et révision.

`GameSaveSnapshot` regroupe cette identité, le tick fixe, la position continue
du joueur, le `PlayerInventorySnapshot` canonique et les mutations sparse de
chunks. Il copie et trie la collection de mutations, rejette les coordonnées
dupliquées, les mutations vides et les dispositions incompatibles, puis
normalise `-0.0` en `+0.0` pour stabiliser le futur encodage binaire.

La capture des mutations appartient à `ChunkStateLifecycle` afin de réunir sans
effet de bord les chunks déjà évincés dans `ChunkMutationStore` et les chunks
encore actifs. Les chunks actifs non modifiés sont omis. Une coordonnée ne peut
pas être simultanément active et stockée. La fenêtre de streaming au moment de
la sauvegarde ne change donc pas le contenu logique capturé.

Le snapshot reste un objet Core en mémoire. Il ne définit ni octet de format,
ni chemin disque, ni migration, ni commande Runtime ou UX.

<!-- LOT7F_COMBINED_ARCHITECTURE -->
## Pipeline de persistance V1

Le lot combiné 7F termine la première verticale technique de persistance sans
ajouter de politique d'interface. `GameSaveBinaryCodec` encode exclusivement un
`GameSaveSnapshot` canonique. L'enveloppe V1 porte la magie `AOSSAVE\0`, la
version, les flags, la longueur du payload et son SHA-256. Les entiers sont
little-endian, les chaînes utilisent UTF-8 strict sans BOM et toutes les
allocations pilotées par le fichier sont bornées avant création.

Le décodeur rejette les versions et flags inconnus, les longueurs incohérentes,
les hashes invalides, les données terminales, les booléens et enums inconnus,
les UTF-8 invalides, les doublons et les ordres non canoniques. Les factories de
restauration de l'inventaire reconstruisent directement les snapshots immuables
et réappliquent les invariants globaux de 7F-A1 sans reflection ni agrégat
mutable intermédiaire. Les définitions sont indexées par identifiant stable
pendant la validation et la restauration afin que le coût reste linéaire dans
le nombre d'entrées, plutôt que proportionnel au produit définitions × entrées.

`AtomicGameSaveStorage` utilise trois fichiers par slot : `.aos`, `.bak` et
`.tmp`. L'encodage est effectué en mémoire, le temporaire est écrit puis flushé,
et la promotion remplace la principale en conservant la précédente comme
backup. Le chargement essaie la principale puis le backup, retourne la
provenance et ne réécrit jamais implicitement une sauvegarde lue. Les appels
sont synchrones et le contrat V1 impose un seul écrivain à la fois par slot.

`GameSaveSnapshotRestorer` résout les données éditoriales du build courant,
vérifie leurs empreintes, reconstruit un nouvel inventaire et initialise un
`ChunkStateLifecycle` avec le store sparse restauré. Le Runtime fournit
uniquement le chemin plateforme et un `GameSaveCoordinator` sans UI. Le caller
n'installe la nouvelle session qu'après succès complet du décodage et de la
restauration.
<!-- LOT7GA_SAVE_LOAD_UX -->
## Lot 7G-A — orchestration visible de sauvegarde

Le frontend dépend d'une frontière Runtime dédiée et non du format binaire. Le
menu principal choisit une chronologie, le coordinateur Runtime installe une
session restaurée avant le démarrage des adaptateurs de joueur, et le menu pause
ne fait que demander une sauvegarde. La capture reste en C# pur et n'est
exécutée qu'entre deux mutations cohérentes du tick fixe.

`PrototypeSaveRuntimeBehaviour` possède la politique de cadence et les messages
visibles. Il ne possède pas la simulation. `InventoryPrototypeSession` fournit
un adaptateur provisoire entre l'état du prototype et `GameSaveSnapshot`; cette
frontière devra être remplacée par le futur agrégat Runtime de partie sans
modifier le codec V1.

Les métadonnées `.aosmeta` servent uniquement à l'affichage des slots. Le fichier
`.aos` et son `.bak` restent les seules sources autoritaires de restauration.

<!-- LOT7GA_HARDENING_ARCHITECTURE -->
### Durcissement de la frontière UX de sauvegarde

Le résolveur du prototype ne reconstruit que les deux conteneurs éditoriaux
connus (`player-main` et `prototype-bag`) avec leurs identifiants et capacités
canoniques. Le sidecar `.aosmeta` est lu et écrit en mode best-effort : il ne
participe jamais à la validité de l'état autoritaire. Une erreur de sauvegarde
réactive le menu pause et expose une sortie explicite sans nouvelle écriture.


Le bootstrap de sauvegarde est créé après les `Awake` des objets déjà présents
lors du chargement de scène. Après l'installation d'une nouvelle session ou
d'une session restaurée, il doit donc raccorder explicitement les adaptateurs de
scène à `InventoryPrototypeSessionProvider.Current`. Un `Rebuild` de ressources
ne peut jamais remplacer une session restaurée ni continuer à faire évoluer une
ancienne session.

La fenêtre initiale de chunks générés est synchronisée pendant ce raccord, avant
la première capture. L'état récolté, les conteneurs au sol, l'inventaire, le tick
et la position doivent ainsi appartenir au même agrégat dès la première frame
jouable.
