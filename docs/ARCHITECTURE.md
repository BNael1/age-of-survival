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

Ce lot ne contient ni `MonoBehaviour`, ni rendu, ni UI, ni état possédé par un
`GameObject`. Les adaptateurs Unity et l'équipement sont introduits dans les lots
suivants sans déplacer la source de vérité hors du Core.
