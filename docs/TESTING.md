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

## Lot 5V — validation requise

Le lot 5V ajoute trois cas EditMode Runtime :

- disponibilité de toutes les textures temporaires via `Resources` ;
- utilisation de trois sprites distincts pour les catégories du sol ;
- visibilité du rayon d'interaction, du reste au sol et de la progression active.

Total attendu après import : **116/116 cas EditMode** dans le Test Runner
graphique et en batchmode. La validation Play Mode doit vérifier que le joueur,
les ressources, les piles au sol, la cible, le rayon et la progression restent
lisibles pendant le déplacement et les transferts, sans changement de portée,
de durée, de quantités ou de contrôles. La Console doit rester sans erreur ni
avertissement provenant du projet.
