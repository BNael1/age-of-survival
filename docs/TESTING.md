# Tests

## Stratégie

- EditMode pour le cœur C# pur ;
- PlayMode uniquement pour les adaptateurs, scènes et interactions nécessitant Unity ;
- tests déterministes et sans dépendance à l’ordre d’exécution ;
- chaque bug corrigé doit recevoir un test lorsque raisonnable.

## Référence actuelle

Le lot de fondation contient 11 méthodes de test. Une méthode utilise quatre cas paramétrés, soit **14 cas exécutés** au total.

Résultat validé le 31 juillet 2026 :

- éditeur : 14/14 réussis ;
- batchmode : 14/14 réussis, code de sortie 0.

## Exécution graphique

Dans Unity : `Window > General > Test Runner`, puis onglet EditMode.

## Exécution batchmode

Fermer l’éditeur Unity qui utilise le projet. Unity Hub peut rester ouvert.

```sh
./tools/run_editmode_tests.sh
```

Le chemin de l’éditeur peut être remplacé :

```sh
UNITY_EDITOR=/chemin/vers/Unity ./tools/run_editmode_tests.sh
```

Les résultats sont écrits dans :

```text
TestResults/editmode-results.xml
TestResults/editmode.log
```

Le dossier `TestResults/` est ignoré par Git.

## Lot 2 — validation réalisée

Le lot 2 ajoute trois cas EditMode :

- déterminisme du motif de débogage ;
- création d’une Tilemap isométrique contenant une tuile par cellule du Core ;
- reconstruction sans duplication de hiérarchie.

Validation du 31 juillet 2026 : **17/17 cas réussis** dans l’éditeur et **17/17 cas réussis** en batchmode. La grille isométrique 10 × 10 a également été vérifiée manuellement dans la Game View.

## Lot 3 — validation réalisée

Le lot 3 ajoute onze cas EditMode :

- sept cas sur l’état continu et la règle de déplacement du Core ;
- quatre cas paramétrés sur la conversion des directions écran vers le plan isométrique.

Validation du 31 juillet 2026 : **28/28 cas réussis** dans l'éditeur et **28/28 cas réussis** en batchmode, avec un code de sortie 0. Le déplacement ZQSD dans huit directions, la normalisation diagonale et la caméra fixe ont également été vérifiés manuellement dans la Game View.

## Lot 4 — validation réalisée

Le lot 4 ajoute dix-sept cas EditMode :

- onze cas Core sur l'identité stable, le ciblage déterministe, le rayon
  inclusif, les ressources récoltées et la récolte unique ;
- six cas Runtime sur la construction des marqueurs, l'indicateur de cible, la
  disparition d'une ressource récoltée, le rebuild, le nettoyage et la mise en
  file d'une interaction sans clavier physique.

Validation du 31 juillet 2026 : **45/45 cas réussis** dans l’éditeur et en
batchmode, zéro échec, zéro cas ignoré et code de sortie 0. La compilation avec
Unity `6000.3.19f1` est réussie.

La validation Play Mode a confirmé le ciblage unique, la disparition et le
changement de cible sur trois récoltes successives, l'absence de cible et de
mutation hors portée, ainsi qu'une Console à zéro erreur. Le Test Runner
graphique a également terminé à **45/45**, sans échec ni cas ignoré.

## Lot 5A — validation réalisée

Le lot 5A ajoute dix-neuf cas EditMode Core :

- valeurs par défaut, égalité, comparaison et hash des identifiants ;
- représentation entière et formatage de l'encombrement ;
- capacité exacte, dépassement, ajout total et ajout partiel ;
- refus lorsque la capacité restante est inférieure à une unité ;
- fusion des piles et ordre stable ;
- distinction et retrait des objets uniques ;
- protection de la collection publique ;
- retrait total et retrait impossible atomique ;
- transferts complets, partiels, vers une destination pleine et vers le même
  conteneur ;
- conservation de la somme source + destination ;
- stabilité du `ContainerId` d'un objet conteneur ;
- absence de référence Unity dans l'assembly Core.

Validation du 31 juillet 2026 : **64/64 cas réussis** dans le Test Runner
graphique et en batchmode, zéro échec, zéro cas ignoré et code de sortie 0. Le
journal ne contient aucune erreur de compilation ni avertissement C# ; les
messages de reconnexion de licence précèdent l'exécution et relèvent de
l'environnement Unity local.

## Lot 5B — validation réalisée

Le lot 5B ajoute vingt cas EditMode :

- onze cas Core sur les compatibilités d'emplacements, l'occupation, les
  échecs explicites, le déséquipement, les références stables, la capacité et
  la réduction de charge active ou inactive ;
- neuf cas Runtime sur les view-models, les copies en lecture seule, les
  commandes, la prévention de l'auto-transfert d'un sac, l'état des boutons et
  l'indépendance de la session vis-à-vis de `MonoBehaviour`.

Validation du 31 juillet 2026 : **84/84 cas réussis** dans le Test Runner
graphique et en batchmode, zéro échec, zéro cas ignoré et code de sortie 0.

Une validation Play Mode automatisée dans `SampleScene` a confirmé **16/16
assertions** : présence et contenu des deux listes, charges initiale et réduite,
capacité brute inchangée, activation des boutons, équipement/déséquipement du
sac et transfert de six branches. Les trois états ont été inspectés dans la Game
View avec le thème runtime Unity chargé et des textes lisibles. Le helper de
validation n'est pas conservé dans le produit.

## Lot 5C — validation réalisée

Le lot 5C ajoute dix-neuf cas EditMode :

- treize cas Core sur le rendement au sol, la capacité validée avant récolte,
  les identifiants distincts, le calcul
  de durée, la progression, la fin unique, les interruptions, la destination
  pleine et les modifications de source ou capacité pendant l'action ;
- six cas Runtime sur la session partagée, l'action unique, le transfert
  partiel, le view-model et la visibilité du marqueur jusqu'au vidage exact.

Validation du 31 juillet 2026 : **103/103 cas réussis** dans le Test Runner
graphique et en batchmode, zéro échec, zéro cas ignoré et code de sortie 0.

La validation Play Mode dans `SampleScene` a confirmé **12/12 assertions** :
commande `E`, rendement au sol, progression visible, transfert de trois branches
après la durée, reste exact de trois branches, interruption par déplacement sans
mutation, reprise ultérieure et disparition du marqueur seulement lorsque la
source est vide. La Console est propre après retrait du helper temporaire.

## Lot 5V — validation réalisée

Le lot 5V ajoute trois cas EditMode Runtime :

- disponibilité de toutes les textures temporaires via `Resources` ;
- utilisation de trois sprites distincts pour les catégories du sol ;
- visibilité du rayon d'interaction, du reste au sol et de la progression active.

Validation du 31 juillet 2026 : **116/116 cas EditMode** dans le Test Runner
graphique et en batchmode. La validation Play Mode confirme que le joueur, les
ressources, les piles au sol, la cible, le rayon et la progression restent
lisibles pendant le déplacement et les transferts, sans changement de portée,
de durée, de quantités ou de contrôles. La Console est propre.

## Lot 5D — validation réalisée

Le lot 5D ajoute seize cas EditMode :

- quatorze cas Core sur la vitesse normale sous capacité, les quatre points de
  contrôle, la borne supérieure, les quatre interpolations, l'utilisation de la
  charge perçue et le refus d'une capacité nulle ;
- un cas Core vérifiant que `PlayerMovement` applique le multiplicateur à la
  distance ;
- un cas Runtime vérifiant que la session suit immédiatement l'équipement du
  sac.

Les tests Runtime existants vérifient aussi les textes `112.5 % / ×0.91` puis
`95 % / ×1.00`, ainsi que leur présence dans l'UI Toolkit. Validation du 31 juillet 2026 : **132/132 cas EditMode** dans le Test Runner graphique et en batchmode, avec Play Mode et Console propres.

La validation Play Mode a confirmé la légère pénalité initiale, le retour à la
vitesse normale lorsque le sac est équipé, la mise à jour immédiate des deux
libellés et une transition sans saut perceptible lors d'une augmentation de
charge. Sprint, endurance et dégâts n'apparaissent pas dans ce lot.

## Correctif de revue globale — validation réalisée

Le correctif ajoute cinq cas EditMode :

- quatre cas Core vérifiant le rejet atomique des définitions partageant un
  identifiant stable mais contredisant le type d'état ou l'encombrement d'une
  entrée existante, y compris lors d'un transfert et de la construction de
  `PlayerInventoryState` ;
- un cas Runtime vérifiant que le multiplicateur de déplacement est recalculé
  séparément pour deux ticks entre lesquels l'équipement change.

Validation du 1 août 2026 : **137/137 cas EditMode** dans le Test Runner
graphique et en batchmode, zéro échec et zéro cas ignoré. Le Play Mode confirme
qu'un transfert ou un changement d'équipement affecte le tick suivant même
lorsque plusieurs ticks fixes sont exécutés dans la même frame. La Console est
propre.

## Correctifs finaux du registre canonique et d'atomicité — validation réalisée

Les deux correctifs ajoutent cinq cas EditMode Core :

- un conteneur enregistré mais vide refuse une définition contradictoire lors
  d'un ajout direct ;
- la liaison canonique reste active après retrait de la dernière entrée ;
- un identifiant absent du registre canonique est rejeté avant mutation ;
- un échec de construction de `PlayerInventoryState` causé par un registre
  contradictoire ne laisse aucune liaison canonique partielle dans les
  conteneurs fournis ;
- un identifiant de conteneur principal absent est rejeté avant toute liaison et
  laisse les conteneurs réutilisables sans registre imposé.

Validation du 1 août 2026 : **142/142 cas EditMode** dans le Test Runner
graphique et en batchmode, zéro échec et zéro cas ignoré. Le Play Mode confirme
les transferts temporisés, l'équipement et la surcharge ; la Console est propre.

## Lots 6B, 6C et 7A — validations réalisées

Le terrain raccordable porte la suite à **145/145 cas EditMode** après le
correctif 6C. Le lot 7A ajoute neuf cas caméra et porte la suite intégrée sur
`main` à **154/154**, avec code Unity `0`. La validation Play Mode confirme le
suivi du point au sol, le zoom provisoire `4.0625`, Z constant et l'absence de
retard observable sur deux déplacements successifs.

## Lot 7B — validation réalisée

Le lot ajoute **93 cas EditMode Core** :

- parsing canonique de seed et version explicite ;
- division plancher et aller-retour des coordonnées positives et négatives ;
- fixtures binaires de `FoundationV1` ;
- indépendance de la disposition des chunks et de l'ordre de génération ;
- continuité des bords, y compris autour de zéro ;
- génération, cache, déchargement et régénération à la demande ;
- immutabilité de `GeneratedChunk` ;
- séparation et ordre canonique de `ChunkModificationLayer<T>`.

Validation finale du 3 août 2026 : **247/247 cas EditMode**, zéro échec, zéro
ignoré, zéro inconclusif et code Unity `0`. Le XML
`/tmp/lot7b-editmode-results.xml` est bien formé et vérifié. Une première
exécution a révélé un débordement intermédiaire à la limite négative de `Int64` ;
le calcul de composition des coordonnées a été corrigé et le cas de régression
fait partie de la suite finale. Aucun Play Mode n'est requis car le lot ne
modifie ni Runtime, ni scène, ni rendu.

<!-- LOT7C_TESTING -->
## Lot 7C — validation

<!-- LOT7C_EDITMODE_STATUS: PASS -->

Le lot ajoute **69 cas EditMode**, pour un total de **316** :

- fixtures binaires de `PopulationV1` et préservation de `FoundationV1` ;
- champs Q16, coordonnées négatives et limites `Int64` ;
- terrains, zones, profils et révisions ;
- déterminisme, ordre de génération et indépendance du découpage ;
- distance minimale des ressources, y compris aux frontières ;
- identifiants stables incluant la seed ;
- spawn déterministe et dégagé ;
- immutabilité et cache à la demande ;
- raccord Runtime vers Tilemap, ressources et joueur ;
- activation de la population dans `SampleScene` sans recalibrer la caméra.

Validation EditMode du 3 août 2026 : **316/316**, zéro échec, zéro ignoré
ou inconclusif et code Unity `0`. XML SHA-256
`fd4a4404e4377ac9d3f8e286c1f6b5c9e07740db848b87699eb6e8c3676e4a8b`;
journal SHA-256
`1960eb2e86a4471b883021112de32ae9ab2aa876bf9d9a330c416de7d131ecde`.
La validation Play Mode est acquise sur la capture de remplacement SHA-256
`a182836b84bd27fbfa5ad04c47b12b404fef37a90ff8265bdc211c1ff8ce22db` :
l'eau est distincte, le spawn et la caméra restent corrects, et le rapport
visuel ne relève aucun motif d'erreur projet.

## Lot 7D-A — caméra et calibrage visuel

Le lot corrigé ajoute **32 cas EditMode**, pour un total de **348** :

- cible de zoom dans les deux sens, facteur multiplicatif et sensibilité ;
- normalisation `120 px/pas`, demi-pas, accumulation fractionnaire et
  sensibilité appliquée après normalisation ;
- rejet des calibrations pixel/pas nulles, négatives, `NaN` ou infinies ;
- delta matériel réaliste sans clamp immédiat et clamps `2.5` / `8.0` ;
- convergence amortie, monotonie et absence de dépassement ;
- conservation X/Y de l'ancre et Z caméra pendant le zoom ;
- pivots joueur/ressource/rendement, sprites fallback distincts sur texture
  partagée et échelle joueur `1.20` ;
- passe de tri unique par frame, joueur devant/derrière, égalités ordinales,
  ressource masquée et indépendance aux ordres de synchronisation/création ;
- absence de mutation de la position Core par le calibrage visuel.

Validation finale du 4 août 2026 : **348/348 EditMode** et **1/1 PlayMode**, zéro
échec, zéro ignoré ou inconclusif et code Unity `0` pour les deux exécutions. Le
cas PlayMode rejouable charge `SampleScene`, produit les cinq captures de revue
et valide tailles, ancres, passe unique de tri, ordres, échelle, Z caméra et
Console. Les SHA-256 sont consignés dans les artefacts de revue du lot.

## Lot 7D-B — frontend et menu pause

Le lot ajoute **10 cas EditMode**, pour un total de **358** :

- verrou global des entrées et libération explicite ;
- lancement de la scène de gameplay et refus d'une seconde transition active ;
- retour au menu principal et passage par l'adaptateur de fermeture ;
- refus de `Charger` sans sauvegarde ;
- présence des routes principales et désactivation des fonctions futures ;
- navigation entre Accueil, En ligne et Options ;
- contenu et visibilité initiale du menu pause ;
- restauration de l'état précédent du verrou lorsqu'une transition ne démarre
  pas.

Le lot ajoute **6 cas PlayMode**, pour un total de **7** :

- construction du menu principal au-dessus du monde assombri et commandes de
  gameplay bloquées ;
- transition `Nouvelle partie` vers `SampleScene` et vérification effective de
  la seed `0` dans le chunk peuplé ;
- pause, immobilité du tick, reprise puis attente du prochain tick fixe réel ;
- annulation d'une interaction mise en attente avant la pause, sans déclenchement
  différé à la reprise ;
- retour depuis la partie vers `MainMenu` ;
- pause demandée avant la construction différée du document, ensuite visible et
  correctement libérée au déchargement.

Validation finale du 4 août 2026 : **358/358 EditMode** et **7/7 PlayMode**.
La première compilation avait révélé une collision de namespace sur
`Resources.Load`; elle est corrigée par qualification `UnityEngine.Resources`.
La revue a ensuite ajouté les régressions de pause précoce et de restauration du
verrou. Le test de reprise n'attend plus deux frames arbitraires : il attend que
`PrototypeSession.CurrentTick` dépasse le tick observé pendant la pause. Une
régression supplémentaire vérifie qu'une commande `E` déjà mise en attente est
annulée pendant le blocage. La Console et la validation visuelle de Naël sont
propres.

<!-- LOT7EB_TESTING -->
## Lot 7E-B — validation finale locale

Le lot ajoute **30 cas EditMode** au total précédent de 380 :

- 13 cas Core sur l'extraction, l'ordre canonique, l'éviction, la restauration,
  les coordonnées négatives, les limites `Int64`, les identifiants inconnus,
  les quantités invalides et la propriété active/store exclusive ;
- 12 cas protocole et simulation autoritaire sur les deux clients, les commandes
  invalides ou rejouées, la reconnexion, les versions, les tailles et chaînes
  bornées, les octets réservés, les digests et les révisions divergentes ;
- 5 cas Runtime supplémentaires sur le cache maximal de 49, les traversées
  positives et négatives, la restauration exacte d'une récolte et d'un reste au
  sol, le report d'éviction pendant un transfert et l'atomicité d'une
  prévalidation échouée.

Validation finale du 5 août 2026 : **410/410 EditMode** et **8/8 PlayMode**,
zéro échec, zéro cas ignoré ou inconclusif. Les exécutions batchmode quittent
avec le code `0`.

Les trois cibles ont été reconstruites depuis le code final :

- macOS : Mach-O 64 bits ARM64 ;
- Windows : PE32+ x86-64, build seulement ;
- Linux Dedicated Server : ELF 64 bits x86-64.

Le smoke local lance un serveur et deux clients macOS séparés. Le récolteur
applique la mutation puis vérifie le rejet d'une commande invalide ;
l'observateur confirme la convergence après reconnexion. Les trois processus
quittent avec le code `0` et produisent :

```text
[AOS-NET] client_smoke_pass id=local-harvester reason=invalid_rejected digest=131296B9BAF759FF evictions=1 restorations=1
[AOS-NET] client_smoke_pass id=local-observer reason=reconnect_converged digest=131296B9BAF759FF evictions=1 restorations=1
[AOS-NET] server_smoke_pass clients=2 digest=131296B9BAF759FF evictions=1 restorations=1
```

`git diff --check` est propre. Les journaux de build peuvent contenir des
messages de finalisation de threads lors de la fermeture rapide de Unity ; les
builds retenus se terminent par le marqueur `AOS_BUILD_OK` et une sortie
batchmode réussie.

### Validation distante acquise

Le 5 août 2026, le binaire Linux x86-64 final a été transféré sur le VPS et
lancé avec une ouverture temporaire de UDP `17779`. Deux clients macOS ont
exécuté le même scénario autoritaire que le smoke local. Le serveur, l'observateur
et le récolteur ont tous quitté avec le code `0` :

```text
[AOS-NET] client_smoke_pass id=vps-observer reason=reconnect_converged digest=131296B9BAF759FF evictions=1 restorations=1
[AOS-NET] client_smoke_pass id=vps-harvester reason=invalid_rejected digest=131296B9BAF759FF evictions=1 restorations=1
[AOS-NET] server_smoke_pass clients=2 digest=131296B9BAF759FF evictions=1 restorations=1
```

L'archive Linux vérifiée sur les deux machines porte le SHA-256
`b52ace1a1e82b11241d3d084d15e5e9f05c89842fa5b6e081fdaa0cef5cd164d`.
La règle de pare-feu temporaire a été supprimée dans le nettoyage du test et
`firewall_rule_after_cleanup=absent` a été vérifié. Les preuves sont conservées
sous `TestResults/7eb-final-validation/vps-smoke-20260805-092509/`.

Le client Windows reste validé comme build x86-64 uniquement ; aucune exécution
Windows n'est revendiquée.

<!-- LOT7FA1_TESTING -->
## Lot 7F-A1 — validation du snapshot d'inventaire

Le lot ajoute **17 cas EditMode Core**, pour un total de **427** :

- unicité globale des instances entre conteneurs ;
- existence et propriétaire unique des conteneurs imbriqués ;
- refus du conteneur principal comme contenu ;
- refus des auto-références et cycles de possession ;
- restauration et validation des trois emplacements d'équipement ;
- rejet des équipements absents, dupliqués ou incompatibles ;
- rejet avant capture d'une duplication ou d'un équipement orphelin introduit
  après construction ;
- ordre canonique indépendant de l'ordre d'insertion ;
- immutabilité de la capture après mutation de l'état vivant ;
- empreinte canonique incluant les règles d'équipement.

Validation du 5 août 2026 : **427/427 EditMode**, zéro échec et zéro cas ignoré,
avec résultat `Passed`. Le lot ne modifie aucun adaptateur Runtime, scène,
contrôle ou rendu ; aucun nouveau PlayMode n'est requis.

<!-- LOT7FA2A_TESTING -->
## Lot 7F-A2a — validation du snapshot complet de partie

Le lot ajoute **17 cas EditMode Core**, pour un total de **444** :

- capture complète de l'identité monde, du tick, du joueur et de l'inventaire ;
- copie et ordre canonique des mutations évincées ;
- capture non destructive des mutations du store ;
- capture d'un chunk actif modifié sans éviction ;
- fusion canonique des mutations actives et stockées ;
- omission d'un chunk actif non modifié ;
- rejet d'une coordonnée simultanément active et stockée ;
- immutabilité de la collection capturée après remplacement dans le store ;
- normalisation de `-0.0` ;
- rejet des profils invalides, ticks négatifs, mutations vides, dispositions
  incompatibles et coordonnées dupliquées.

Validation du 5 août 2026 : **444/444 EditMode**, zéro échec et zéro cas ignoré,
avec résultat `Passed`. Le lot ne modifie aucun adaptateur Runtime, scène,
contrôle ou rendu ; aucun nouveau PlayMode n'est requis.

<!-- LOT7F_COMBINED_TESTING -->
## Lot 7F combiné — codec, stockage et restauration

Le lot ajoute **24 cas EditMode** : 21 dans le Core et 3 sur le raccord Runtime.
La validation locale exécutée sous Unity `6000.3.19f1` passe à
**468/468 EditMode** et conserve **8/8 PlayMode**, avec zéro échec et zéro cas
ignoré.

La matrice couvre le round-trip complet, la stabilité octet pour octet,
l'enveloppe V1, les versions et flags inconnus, les limites, longueurs et hashes
invalides, l'écriture principale, le backup, la récupération après corruption,
les slots sûrs, l'absence de temporaire après promotion, la résolution
éditoriale, la reconstruction de l'inventaire et du lifecycle de chunks, le
refus des définitions incompatibles, la provenance backup du coordinateur et le
chemin `persistentDataPath`.

La revue finale ajoute un durcissement de complexité sans changement de
comportement : les définitions sont indexées par identifiant pendant la
validation et la restauration. Les suites complètes ont été relancées après ce
correctif sans régression : **468/468 EditMode** et **8/8 PlayMode**.

Les tests disque utilisent des répertoires temporaires isolés. Le remplacement
principal/backup est validé sur APFS par cette exécution. Une validation
supplémentaire de coupure et de remplacement reste requise sur NTFS et le
système de fichiers Linux cible avant de revendiquer une durabilité matérielle.
<!-- LOT7GA_TESTING -->
## Validation du lot 7G-A

La suite EditMode ajoute **23 cas** autour des trois slots, du planificateur
d'autosave, des métadonnées best-effort, des résolveurs éditoriaux, du
round-trip canonique du monde et de l'isolation multi-slot. Elle couvre aussi un
sidecar parseable dont la durée dépasse `TimeSpan`, ainsi que l'annulation réelle
d'un transfert actif sans déplacement d'objet. Total final validé :
**491/491 EditMode**.

La suite PlayMode passe à **10/10**. Elle vérifie les transitions de frontend, la
pause et les entrées, le raccord de l'adaptateur de ressources à la session
installée par le bootstrap, puis un round-trip Runtime complet : position, tick,
inventaire, équipement, ressource récoltée, reliquat au sol, mutations de chunks,
rendu du tas et seconde sauvegarde stable.

Les tests disque utilisent des répertoires temporaires et les scénarios PlayMode
neutralisent la sélection statique lorsqu'ils ne doivent pas écrire de slot
utilisateur. Tout défaut de persistance découvert manuellement doit être ajouté
comme régression automatique lorsqu'il est reproductible.

Validation manuelle résiduelle : lisibilité, libellés, disposition et absence
d'erreur Console. L'intégrité des données et l'isolation des slots sont prouvées
principalement par les suites automatiques.

<!-- LOT7GB_TESTING -->
## Lot 7G-B — visibilité initiale et hygiène PlayMode

Une régression EditMode construit l'interface d'inventaire, vérifie que son
panneau est initialement masqué, l'ouvre par la commande de bascule puis le
referme. L'état logique et la valeur `DisplayStyle` doivent rester cohérents.

Le script `tools/run_playmode_tests.sh` mémorise également l'existence initiale
de `ProjectSettings/SceneTemplateSettings.json`. Lorsque Unity crée ce fichier
pendant le test alors qu'il était absent au départ, le script retire uniquement
ce fichier exact à sa sortie. Un fichier préexistant n'est pas supprimé.

Validation locale du 6 août 2026 :

- `bash -n tools/run_playmode_tests.sh` : réussi ;
- **492/492 EditMode**, zéro échec et zéro cas ignoré ;
- **10/10 PlayMode**, zéro échec et zéro cas ignoré ;
- aucun `ProjectSettings/SceneTemplateSettings.json` laissé dans le dépôt ;
- aucun changement du Core, du format de sauvegarde ou des règles de gameplay.
