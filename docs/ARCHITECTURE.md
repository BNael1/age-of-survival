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
- il ne possède ni collision, ni animation, ni caméra suiveuse, ni règle de terrain.

Le clavier et le marqueur peuvent être remplacés sans modifier la règle de déplacement du Core.

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
entrées déjà présentes. Un identifiant stable ne peut donc pas être utilisé avec
des données physiques différentes pour contourner la capacité.

La frontière actuelle de `InventoryOperations` est locale à un conteneur. Elle
ne suffit pas encore à garantir qu'une même `ItemInstanceId` n'existe jamais
dans deux conteneurs d'un même joueur, ni qu'un retrait d'objet unique efface ou
refuse une référence d'équipement active. Ces invariants devront être portés par
une opération agrégée avant d'autoriser le dépôt au sol, la destruction ou la
persistance d'objets uniques équipés.

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
