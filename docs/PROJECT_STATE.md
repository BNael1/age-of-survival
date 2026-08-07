# État du projet

Dernière mise à jour : 6 août 2026

## Moteur

- Unity 6.3 LTS
- Éditeur 6000.3.19f1 ARM64
- Universal 2D / URP
- C#

## État actuel

La base publique intègre les lots 7G-A et 7G-B. Le commit fonctionnel
`5fbf3b16b71346f251039d9320582db6001fa259`, descendant direct de
`b9229c8fe9859ffb47718758d41fb24a38ba985e`, ferme l'inventaire à l'entrée
dans `SampleScene`, ajoute sa régression EditMode et sécurise le nettoyage
PlayMode. Le présent commit documentaire clôt l'intégration.

La validation acquise sous Unity `6000.3.19f1` est de **492/492 EditMode** et
**10/10 PlayMode**, sans échec ni cas ignoré. La validation visuelle confirme
l'inventaire fermé au démarrage, son ouverture, sa fermeture, sa réouverture et
une Console sans erreur.

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
- `a46e994` — `feat: add ground-anchor camera follow` ;
- `15fae58` — `feat: add deterministic world generation foundation` ;
- `07023ead` — `feat: add deterministic world population` ;
- `686eaf3` — `feat: add progressive camera and ground-anchor sorting` ;
- `c75000d` — `docs: close lot 7da project state` ;
- `224a5fb` — `feat: add main menu and pause frontend` ;
- `8d8d87e` — `feat: add chunk streaming window planner` ;
- `9ee8676` — `feat: stream terrain and resources across chunks` ;
- `4abf29c` — `feat: add bounded chunk mutations and authoritative multiplayer slice` ;
- `9d9d7b0` — `docs: close lot 7eb project state` ;
- `1a4612d` — `fix: enforce inventory snapshot invariants` ;
- `40a2db9` — `feat: add canonical game save snapshot` ;
- `6790a30` — `feat: add versioned game save pipeline` ;
- `b9229c8` — `feat: add save and load UX` ;
- `5fbf3b1` — `fix: close inventory by default and finalize lot 7g`.

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

Le lot 7B est intégré dans `main` au commit
`15fae587d8ea084349ca60889075c543c7aa57e0`, arbre
`77f316b4ae0254047d06b99aa62dd4ef5a474ea2`, par avance rapide pure depuis
`a46e99434ad68ea5f87c037328d4a95bc3d15435`. La suite complète intégrée passe
à **247/247 cas EditMode**, zéro échec, zéro ignoré ou inconclusif, code Unity
`0`. Unity et le dépôt Godot sont propres. Le lot 7B est fermé.

<!-- LOT7C_PROJECT_STATE -->
Le lot 7C est intégré sur `main` dans la base approuvée du lot 7D-A,
`07023eadf81ea1468a90ec6ed422326aebd4907b`.
Il ajoute `PopulationV1` (`2`), des champs Q16 lissés en C# pur, trois terrains,
des zones ouvertes et boisées, un placement de ressources à distance minimale,
des identifiants incluant la seed, un spawn déterministe et un cache de chunks
peuplés immuables. `SampleScene` rend provisoirement un chunk `32 × 32` de seed
`0`, réutilise les assets existants et conserve le zoom `4.0625`.
Validation EditMode 7C : **316/316**, zéro échec, zéro ignoré ou inconclusif,
code Unity `0`, XML SHA-256
`fd4a4404e4377ac9d3f8e286c1f6b5c9e07740db848b87699eb6e8c3676e4a8b`.
La capture Play Mode de remplacement `lot7c-playmode.png`, SHA-256
`a182836b84bd27fbfa5ad04c47b12b404fef37a90ff8265bdc211c1ff8ce22db`,
rend les 143 cellules d'eau nettement distinctes en bleu sombre. Le rapport
visuel, SHA-256
`12f424522eb21c180ab2d6b528859e1f18d7b2fd676558b4aa509956caf01977`,
ne relève aucun motif d'erreur projet. Naël avait accepté tous les autres choix
visibles lors de la première revue ; l'unique réserve est résolue. La validation
visuelle 7C est acquise.

## Lot 7D-A fermé

Le lot 7D-A est validé par Naël et fermé. Il est intégré sur `main` au commit
`686eaf329a664cd8b797400f869ac3edbb9c8643`, arbre
`52bb1ec1b6a9a4eed6971260f252abc33b1adb16`, depuis le parent
`07023eadf81ea1468a90ec6ed422326aebd4907b`. L'intégration est une avance rapide
pure. Le commit porte **26 chemins, 1870 insertions et 37 suppressions**. Il ajoute le
zoom orthographique multiplicatif amorti, conserve le suivi de l'ancre joueur,
agrandit uniquement le visuel joueur à `1.20` et trie joueur/ressources selon
leurs ancres au sol avec un départage ordinal stable. La correction de revue
normalise la molette à `120 pixels par pas logique`, centralise le tri dans une
unique passe `LateUpdate` et crée deux sprites fallback distincts pour les
pivots ressource/rendement. Le Core, la scène, les textures, les règles de
déplacement, d'inventaire et de ressources restent inchangés.

Validation finale de la correction : **348/348 EditMode** et **1/1 PlayMode**,
zéro échec ou cas ignoré/inconclusif, code Unity `0` pour les deux exécutions.
Les cinq captures ont été régénérées et confirment les trois tailles, les deux
chevauchements et la passe unique de tri ; l'instrumentation relève zéro erreur
projet. La validation manuelle de Naël est acquise.

Après l'intégration, `main` et `origin/main` pointaient sur le même commit et le
worktree était propre. Les essais Unity AI ont été retirés sans commit : aucun
paquet ni réglage Unity AI additionnel ne subsiste dans le projet.

## Lot 7D-B intégré

Le lot 7D-B ajoute une scène `MainMenu` séparée et un frontend UI Toolkit
programmatique. Le menu principal affiche le monde procédural de seed `0` sous
un voile sombre et propose `Nouvelle partie`, `Charger`, `En ligne`, `Options`
et `Quitter`. `Charger` reste désactivé tant que la persistance n'expose aucune
sauvegarde. Le panneau `En ligne` conserve `Rejoindre un serveur`, `Héberger une
partie` et `Serveurs favoris`, tous visibles mais inactifs jusqu'à
l'implémentation du client et du serveur autoritaire sur VPS.

`Nouvelle partie` charge `SampleScene` de manière asynchrone. Le menu pause
propose `Reprendre`, `Options`, `Retour au menu principal` et `Quitter`.
`GameplayInputGate` bloque le déplacement, l'interaction, le zoom et
l'avancement du tick fixe pendant les menus, sans déplacer cette règle dans le
Core. Une transition refusée ou en exception restaure l'état antérieur du
verrou. Une pause demandée avant la construction différée du document UI est
réappliquée dès que celui-ci existe. Une interaction mise en attente juste avant
la pause est annulée pendant le blocage et ne se déclenche pas à la reprise.

La première importation a révélé une collision entre `UnityEngine.Resources` et
le namespace `AgeOfSurvival.Runtime.Resources`; les deux chargements de thème
sont désormais qualifiés explicitement. La revue du patch a ensuite corrigé la
course de construction du menu pause et la restauration du verrou lors d'un
échec de transition. Le test PlayMode de reprise attend maintenant le prochain
tick fixe réel au lieu d'un nombre arbitraire de frames.

Validation finale du 4 août 2026 : **358/358 EditMode** et **7/7 PlayMode**.
La Console et la validation visuelle de Naël sont propres. Les tests couvrent
notamment la seed `0`, les routes visibles mais différées, le chargement
asynchrone, la pause précoce, l'annulation d'une interaction en attente, le
blocage puis la reprise du tick et le retour au menu principal. Aucun package
ou asset tiers n'est ajouté.

## Limites techniques connues

- `ground_water.png` contient provisoirement l'apparence de grass et ne
  constitue pas une vraie tuile d'eau ;
- `InventoryOperations` garantit l'identité unique à l'intérieur d'un conteneur,
  mais pas encore l'unicité globale d'une `ItemInstanceId` entre tous les
  conteneurs d'un `PlayerInventoryState` ;
- retirer directement un objet unique équipé via une opération de conteneur peut
  laisser une référence d'équipement orpheline. Avant d'autoriser le dépôt au
  sol, la destruction ou la persistance d'objets uniques équipés, les mutations
  devront passer par une frontière agrégée qui maintient ces deux invariants ;
- `NoSaveAvailability` maintient provisoirement `Charger` désactivé : aucun
  format de sauvegarde Runtime n'est branché sur le frontend ;
- les routes `En ligne` sont uniquement des contrats d'interface désactivés.
  Aucun transport, authentification, navigateur de serveurs ou protocole VPS
  n'est implémenté dans ce lot.

## Transition 7D-B vers 7E

Le lot 7D-B a été intégré au commit
`224a5fb15655efefda3862e3fd29a3a7697f1b5c`. Les commits
`8d8d87e542d93224f00b6bf8db7cbdbfd26c92aa` et
`9ee8676a6905d633be9d888641e2a7425c14472d` ont ensuite établi la
planification puis le streaming effectif des chunks avant le lot 7E-B.

## Dépôts archivés

Le prototype Godot et le benchmark moteur restent des références historiques séparées. Ils ne doivent pas être modifiés par ce dépôt.

<!-- LOT7EB_PROJECT_STATE -->
## Lot 7E-B — mutations sparse, cache borné et tranche multijoueur autoritaire

Le lot 7E-B est intégré et poussé sur `main` au commit
`4abf29cd899593cac494033b89ed8cd526454dba`, parent
`9ee8676a6905d633be9d888641e2a7425c14472d`. Le commit porte 40 chemins,
4 154 insertions et 4 suppressions, sans modification du gameplay ou de la
direction artistique. `HEAD`, `origin/main` et `origin/HEAD` sont synchronisés.

Le sous-lot B1 ajoute dans le Core une représentation sparse et canonique des
mutations de chunks. Une ressource récoltée et un conteneur de sol non vide
peuvent être extraits d'un chunk actif, stockés séparément de la base générée,
puis restaurés contre cette base sans perte, duplication ou changement
d'identifiant. Un chunk ne peut pas être simultanément actif et stocké.

Le sous-lot B2 borne le cache Runtime avec un rayon de rétention technique de
`3`, soit au plus `7 x 7 = 49` chunks générés autour du centre. La fenêtre
visible reste `3 x 3` et la fenêtre préparée `5 x 5`. L'éviction est
transactionnelle : le monde calcule d'abord le futur ensemble retenu, l'unique
propriétaire d'état prévalide et capture les mutations, puis le cache est
modifié. Une source de transfert active hors rétention diffère l'éviction au
lieu de laisser monde, inventaire et rendu diverger.

Le sous-lot B3 fournit une tranche verticale multijoueur autoritaire limitée au
scénario de validation. Le serveur possède la simulation, accepte deux clients,
applique une récolte, rejette une commande invalide, diffuse des snapshots,
force une éviction/restauration et vérifie la convergence après reconnexion. Le
protocole binaire versionné et borné est isolé dans son propre assembly ; le
transport Unity utilise un pipeline fiable et séquencé. Les erreurs propres à un
pair entraînent son rejet, pas l'arrêt du serveur.

Validation locale finale du 5 août 2026 :

- **410/410 EditMode**, zéro échec, zéro ignoré ou inconclusif ;
- **8/8 PlayMode**, zéro échec, zéro ignoré ou inconclusif ;
- client macOS ARM64 construit ;
- client Windows x86-64 construit, sans prétendre à une exécution Windows ;
- serveur Linux x86-64 construit ;
- smoke local à trois processus réussi : `server=0`, `observer=0`,
  `harvester=0` ;
- digest convergent `131296B9BAF759FF`, une éviction et une restauration ;
- `git diff --check` propre.

Le smoke VPS du binaire Linux final est acquis le 5 août 2026. Le serveur
`7E-B.1` a démarré sur UDP `17779`, puis deux clients macOS distincts ont validé
le rejet d'une commande invalide et la convergence après reconnexion. Les trois
processus ont quitté avec le code `0` et convergent sur le digest
`131296B9BAF759FF`, avec une éviction et une restauration.

L'archive Linux transférée porte le SHA-256
`b52ace1a1e82b11241d3d084d15e5e9f05c89842fa5b6e081fdaa0cef5cd164d`.
La règle UDP temporaire a été retirée après le test et son absence a été
vérifiée. Aucun domaine, hôte Nginx, service web ou répertoire applicatif
existant n'a été modifié. Les preuves locales sont conservées sous
`TestResults/7eb-final-validation/vps-smoke-20260805-092509/`.

<!-- LOT7FA1_PROJECT_STATE -->
## Lot 7F-A1 — invariants d'agrégat et snapshot canonique

Le lot est intégré et poussé sur `main` au commit
`1a4612dd561959905eb0d8731b6720e44cc2fa76`. Il ajoute une capture immuable et
déterministe de l'inventaire. La construction, la restauration d'équipement et
la capture refusent les instances dupliquées, les références de conteneurs
absentes ou ambiguës, les cycles de possession et les équipements absents,
dupliqués ou incompatibles.

Les définitions, conteneurs et entrées sont exportés dans un ordre ordinal
stable. Les empreintes incluent les règles d'équipement mais excluent les textes
d'affichage. Le Core reste indépendant de Unity et aucun package n'est ajouté.

Validation acquise le 5 août 2026 : **427/427 EditMode**, résultat `Passed`,
zéro échec et zéro cas ignoré. Le périmètre fonctionnel reste limité au Core et
aux tests ; aucun PlayMode supplémentaire n'est requis.

<!-- LOT7FA2A_PROJECT_STATE -->
## Lot 7F-A2a — snapshot complet de partie

Le lot est intégré et poussé sur `main` au commit
`40a2db94784f16689d6978b9e38b84f83e8f71ac`, parent
`1a4612dd561959905eb0d8731b6720e44cc2fa76`. Il ajoute la frontière
`GameSaveSnapshot`, la capture complète des mutations actives et évincées et la
documentation de cette frontière.

Validation acquise le 5 août 2026 : **444/444 EditMode**, résultat `Passed`,
zéro échec et zéro cas ignoré. `main`, la branche 7F-A2a et leurs références
distantes ont été vérifiées sur le même commit.

<!-- LOT7F_COMBINED_PROJECT_STATE -->
## Lot 7F combiné — A2b, B et intégration technique C

Le lot a été développé et validé sur `feature/lot7f-combined-persistence-v1`
depuis `main` à `40a2db94784f16689d6978b9e38b84f83e8f71ac`. Aucun bouton,
raccourci, autosave, nombre de slots visible ou message joueur n'est décidé.

Le lot ajoute les factories internes nécessaires à la reconstruction stricte du
snapshot d'inventaire, un codec binaire V1 SHA-256 borné et canonique, un
stockage principal/backup/temporaire, un restaurateur créant un nouvel état Core
et un coordinateur Runtime qui ne remplace pas implicitement la session active.
La revue finale indexe les définitions par identifiant stable pendant la
validation et la restauration, afin d'éviter une recherche
définitions × entrées sur les sauvegardes de forte cardinalité.

Validation locale sur Unity `6000.3.19f1` macOS ARM64 : **468/468 EditMode** et
**8/8 PlayMode**, zéro échec et zéro cas ignoré. Le périmètre reste limité à
30 chemins Core, Runtime, tests et documentation. Le fichier
`ProjectSettings/SceneTemplateSettings.json` généré par PlayMode a été retiré.

Limites actives : écritures synchrones avec un seul écrivain par slot ; aucune
politique UX ; aucune promotion ou réparation implicite après récupération du
backup ; validation de durabilité matérielle encore requise sur NTFS et le
système de fichiers Linux cible.

### Prochaine action

Cadrer avec Naël la politique UX visible de sauvegarde et chargement — slots,
sauvegarde manuelle, autosave, écrasement, messages et chargement en partie —
avant tout raccord aux menus existants. Les validations NTFS et Linux restent
requises avant de revendiquer la durabilité matérielle multiplateforme.
<!-- LOT7GA_PROJECT_STATE -->
## Lot 7G-A — sauvegarde et chargement visibles, intégré

Le lot 7G-A est intégré et poussé sur `main` au commit
`b9229c8fe9859ffb47718758d41fb24a38ba985e`, parent
`6790a30689268bec2dd3bd6ea45ec2d4412e520f`.

Il fournit trois chronologies persistantes, `Continuer`, la confirmation
d'écrasement, le chargement depuis le menu principal, les métadonnées
informatives, l'autosave à dix minutes, la sauvegarde manuelle et la sauvegarde
avant retour ou fermeture normale.

La validation finale est de **491/491 EditMode** et **10/10 PlayMode**. La
validation visuelle couvre les trois slots, leurs positions distinctes, les
récoltes restaurées et les messages joueur. Le lot 7G-A est fermé.

<!-- LOT7GB_PROJECT_STATE -->
## Lot 7G-B — clôture, inventaire initial et hygiène PlayMode

Le mini lot corrige l'état documentaire devenu obsolète après l'intégration de
7G-A. Il ferme également le panneau d'inventaire lors de l'entrée dans
`SampleScene`. Le bouton visible existant permet toujours de l'ouvrir et de le
refermer ; aucun raccourci clavier supplémentaire n'est ajouté.

L'état ouvert ou fermé est désormais explicite dans la vue et ne dépend plus de
`resolvedStyle`. Une régression EditMode vérifie l'état initial fermé,
l'ouverture puis la fermeture.

Le script PlayMode mémorise si
`ProjectSettings/SceneTemplateSettings.json` existait avant le test. Il ne
retire ce fichier que s'il était absent au départ et a été généré pendant
l'exécution. Un fichier préexistant reste intact et le chemin n'est pas masqué
globalement dans `.gitignore`.

Validation acquise : **492/492 EditMode**, **10/10 PlayMode**, syntaxe Bash
valide, aucun résidu `SceneTemplateSettings.json` et validation visuelle sans
erreur Console. Le lot 7G-B est intégré dans `main` par avance rapide pure,
sans merge commit.

### Priorité gameplay confirmée

Naël fixe l'ordre de travail visible suivant :

1. points de vie ;
2. nourriture ;
3. ressources ;
4. craft ;
5. construction.

La construction n'est donc pas le prochain système de gameplay. Chaque
périmètre devra être cadré séparément avant son implémentation.

### Prochaine action

Cadrer le premier petit lot consacré aux points de vie, avant la nourriture,
les ressources, le craft et la construction.

<!-- LOT7HA1_PROJECT_STATE -->
## Lot 7H-A1 — noyau santé et session

État de la branche `feature/lot7ha-health-loop` au 6 août 2026 :

- modèle de santé déterministe en C# pur ;
- `100` PV maximum ;
- dégâts et soins bornés ;
- régénération après huit secondes à deux PV par seconde ;
- mort sans régénération ;
- soin ordinaire sans résurrection ;
- respawn de l'état vital à pleine santé ;
- propriété canonique de la santé dans `InventoryPrototypeSession` ;
- avancement de la santé par le tick fixe canonique de session ;
- restauration transitoire des sauvegardes V1 à pleine santé ;
- aucun HUD, source de dégâts Unity, téléportation de respawn ou format de
  sauvegarde V2 dans ce sous-lot.

Le sous-lot ajoute vingt tests Core et cinq tests Runtime. La suite complète
passe à **517/517 EditMode**, zéro échec, zéro ignoré ou inconclusif, avec code
Unity `0`.

Le lot 7H-A2 est décrit dans la section suivante.

<!-- LOT7HA2_PROJECT_STATE -->
## Lot 7H-A2 — sauvegarde santé V2

Le lot 7H-A2 est validé techniquement sur
`feature/lot7ha-health-loop`, à partir du commit intégré
`385267be49674dacc2159ad8e073c4a9422908ee`.

Le lot ajoute :

- un `PlayerHealthSnapshot` canonique dans le Core ;
- l'écriture systématique du format `AOSSAVE` V2 ;
- la lecture rétrocompatible des V1 et V2 ;
- la migration en mémoire des V1 vers `100/100` PV au tick sauvegardé ;
- la conservation du maximum, de la santé courante et du calendrier de
  régénération en V2 ;
- la restauration de la santé dans la session prototype ;
- la généralisation des messages de limites partagés par V1 et V2 ;
- la documentation du format, de l'architecture, des décisions et des tests.

La suite complète passe à **522/522 EditMode**, zéro échec, zéro ignoré ou
inconclusif, avec code Unity `0`. Le correctif de compilation a consisté
uniquement à ajouter l'import Core manquant dans le restaurateur.

La clôture Git de 7H-A2 doit conserver ce périmètre validé et ses preuves.
Le sous-lot de gameplay suivant reste 7H-A3 : respawn Runtime, HUD et source de
dégâts temporaire intégrée.

<!-- LOT7HA3_PROJECT_STATE -->
## Lot 7H-A3 — respawn Runtime, HUD et dégâts temporaires

Le lot 7H-A3 est techniquement validé sur la base intégrée
`9981c6552909be82b1813111f7e671e857b3b022` et est prêt pour sa clôture Git.

Périmètre actuel :

- repositionnement explicite du joueur dans le Core ;
- respawn atomique de la santé et de la position sauvegardable ;
- composition Runtime déterministe de la zone temporaire et du respawn ;
- anneau rouge de dégâts près du spawn, sans raccourci ;
- HUD UI Toolkit avec barre et valeur numérique ;
- quinze tests EditMode et six tests PlayMode propres au lot ;
- documentation de l'architecture, des décisions, des tests et de la
  réutilisation technique.

La première exécution a révélé une divergence entre la session locale de
`DebugResourceInteraction` et la session globale. `DebugPlayerController.Start`
utilise désormais `ResolvePrototypeSession()`. La validation obtenue avant
l'extension était **535/535 EditMode** et **13/13 PlayMode**.

L'extension de régression demandée après la validation visuelle porte les cibles
à **537/537 EditMode** et **16/16 PlayMode**. Elle automatise les limites de la
zone, la séquence complète de dégâts, le HUD, le respawn, la caméra, l'inventaire
équipé et la pause.

Validation finale pré-clôture :

- **537/537 EditMode**, zéro échec, zéro ignoré ou inconclusif ;
- **16/16 PlayMode**, zéro échec, zéro ignoré ou inconclusif ;
- validation visuelle de Naël : PASS pour le HUD, la zone rouge, les dégâts,
  le respawn et la caméra, la conservation de l'inventaire, la pause et la
  Console ;
- autotest du mécanisme de relance PlayMode : PASS ;
- `git diff --check` : PASS ;
- périmètre de revue : **21 chemins** ;
- index Git vide au moment de la validation pré-clôture.
<!-- LOT7HA3_PLAYMODE_RETRY_STATE -->
### Incident PlayMode natif et durcissement du runner

Lors de la validation 7H-A3, Unity 6000.3.19f1 a subi une fois un `SIGABRT`
pendant `BootstrapCompilation`, avant le démarrage des tests. Une relance propre
a ensuite validé **16/16 PlayMode**.

Le runner autorise une seule relance pour la combinaison exacte code `134` et
signature connue. Toute autre erreur ou répétition reste un échec explicite.
