# Feuille de route de réutilisation technique

## Politique

Avant chaque système important : rechercher les APIs officielles, tutoriels actuels, dépôts avec licence, packages officiels et solutions commerciales. Comparer intégration, adaptation et développement spécifique.

## Décisions initiales

| Domaine | Décision actuelle | Motif |
|---|---|---|
| Grille et simulation | Développer le cœur | Système central et spécifique |
| Rendu isométrique | Adapter les outils Tilemap/URP officiels | Éviter les conversions et batching maison inutiles |
| Tests | Unity Test Framework officiel | Déjà présent dans le template |
| Entrée | Évaluer le nouveau Input System au premier lot jouable | Package officiel déjà présent |
| Sauvegarde | Développer un format métier versionné | Ne pas dépendre d’un plugin central |
| Pathfinding | Construire une référence simple, réévaluer A* Pathfinding Project après besoin réel | Monde mutable et chunké |
| Outils éditeur | Outils internes ciblés ; évaluer Odin seulement lorsque le volume de données le justifie | Éviter une dépendance prématurée |
| DOTS / Burst | Reporter après profiling | Complexité non justifiée au départ |

## Recherche du lot 5B — inventaire runtime

Recherche réalisée le 31 juillet 2026 pour Unity `6000.3.19f1`.

| Candidat | Auteur, version et URL | Licence | Compatibilité et dépendances | Gain estimé | Risques et stratégie de sortie | Décision |
|---|---|---|---|---|---|---|
| UI Toolkit runtime (`UIDocument`, `ListView`, `PanelSettings`) | Unity Technologies, documentation Unity 6, [exemples runtime](https://docs.unity3d.com/ja/6000.0/Manual/UIE-runtime-examples.html) | Composant officiel couvert par les conditions Unity du projet | API intégrée et compilée sous 6000.3.19f1 ; aucune dépendance additionnelle | Plusieurs jours sur le rendu, la sélection et la virtualisation des listes | Couplage isolé dans la couche Runtime ; sortie par remplacement de `InventoryPrototypeUiDocument` | **Adopté** |
| UI Toolkit Manual Code Examples | Unity Technologies, branche `master` consultée à 108 commits, [dépôt](https://github.com/Unity-Technologies/ui-toolkit-manual-code-examples) | Unity Companion License pour le code ; documentation autrement CC BY-NC-ND | Exemples de référence, pas de package importé | Quelques heures de vérification des pratiques `ListView` | Licence et exemples multi-versions : ne copier aucun fichier ; conserver seulement les principes d'API | **Référence seulement** |
| UI Toolkit Unity Royale Runtime Demo | Unity Technologies, dépôt archivé/testé avec Unity 2020.1, [dépôt](https://github.com/Unity-Technologies/UIToolkitUnityRoyaleRuntimeDemo) | MIT | Ancienne version Unity et dépendance Addressables non nécessaire au prototype | Potentiellement un à deux jours de mise en page | Dette de migration et architecture de démonstration surdimensionnée ; sortie simple si aucun import | **Rejeté** |
| Kenney UI Pack 2.0 | Kenney, version 2.0, [asset](https://kenney.nl/assets/ui-pack) | CC0 | Images raster compatibles, mais sans bénéfice fonctionnel pour le prototype | Quelques heures d'habillage | Direction artistique prématurée et poids d'assets ; formes UI Toolkit déjà suffisantes | **Rejeté, non importé** |

Le prototype adopte donc uniquement l'API UI Toolkit officielle déjà incluse.
Les couleurs, formes et textes sont créés localement ; aucun code ou asset
externe issu des références n'est copié.

## Recherche du lot 7B — génération déterministe

Recherche réalisée le 3 août 2026.

| Candidat | Provenance | Licence / statut | Analyse | Décision |
|---|---|---|---|---|
| `System.Random` | Microsoft .NET | API de plateforme | Microsoft précise que l'implémentation et donc la séquence ne sont pas garanties entre versions majeures de .NET. Inadapté comme contrat de sauvegarde longue durée. | **Rejeté pour la génération persistante** |
| SplitMix / mélange SplitMix64 | Steele, Lea et Flood, OOPSLA 2014, DOI `10.1145/2660193.2660195` | Publication scientifique ; aucune bibliothèque importée | Mélange entier rapide, reproductible et simple à réimplémenter. Les constantes de diffusion sont utilisées dans une implémentation locale sans état, avec fixtures. | **Principe adapté, aucun code tiers copié** |
| PCG | Melissa O'Neill, rapport HMC-CS-2014-0905 | Papier et implémentations de référence disponibles séparément | Excellent PRNG séquentiel, mais un état séquentiel introduirait une dépendance à l'ordre des appels. | **Référence seulement** |
| Frameworks de génération Unity / Asset Store | Divers | Variables | Ils posséderaient une partie centrale du monde, compliqueraient versions, sauvegardes, mods et stratégie de sortie. | **Rejetés pour le Core** |

Le lot n'ajoute aucune dépendance. Le mélange local n'est pas cryptographique ;
il sert uniquement aux décisions reproductibles de génération.

## Critères d’adoption

- licence compatible ;
- version Unity 6 compatible ;
- maintenance active ;
- interfaces isolables ;
- stratégie de sortie ;
- gain de temps mesurable ;
- absence de propriété sur les systèmes centraux.

<!-- LOT7C_REUSE -->
## Recherche du lot 7C — champs et espacement

Recherche réalisée le 3 août 2026.

| Candidat | Statut | Décision |
|---|---|---|
| Tilemap `SetTilesBlock` officiel | API Unity incluse | **Utilisé** pour remplir le chunk en bloc ; aucun package ajouté. |
| ScriptableObject | API Unity incluse | **Réservé aux profils éditoriaux futurs** ; l'état mutable et la sauvegarde n'y résident pas. |
| Poisson disk / Bridson | publication scientifique | **Référence conceptuelle seulement** pour la distance minimale ; aucun code repris. |
| Bibliothèques de bruit externes | licences et versions variables | **Non nécessaires au prototype** ; les champs Q16 sont implémentés localement. |
| Générateurs Asset Store | dépendance centrale | **Rejetés pour le Core propriétaire**. |

Aucune dépendance Runtime externe n'est ajoutée.

## Recherche du lot 7D-A — caméra et tri 2D

Recherche réalisée le 3 août 2026 pour Unity `6000.3.19f1` et Input System
`1.19.0`.

| Candidat | Provenance et licence | Analyse | Décision |
|---|---|---|---|
| `Mouse.scroll` / `DeltaControl` | API et manuel Input System officiels ; package Unity Companion License déjà installé | Le delta vertical est un delta matériel en pixels, cumulé pendant la frame, et peut provenir d'une molette ou d'un trackpad. Aucun sample n'a besoin d'être importé. | **Normalisé localement** en pas logiques à raison provisoire de `120 px/pas`, avant sensibilité ; jamais transmis directement au zoom logique. |
| `Camera.orthographicSize` | API Unity officielle incluse | Définit la demi-hauteur verticale de la vue orthographique et ne dépend pas des bounds rendus. | **Utilisé** |
| `Mathf.SmoothDamp` | API Unity officielle incluse | Interpolation amortie conçue pour approcher une cible sans dépassement ; le pas de temps est fourni explicitement et le résultat reste clampé. | **Utilisé** |
| `Renderer.sortingOrder` et `SpriteSortPoint.Pivot` | Manuel de tri 2D Unity 6 et API officielles | L'ordre entier permet un départage stable explicite ; le pivot matérialise le point au sol. | **Utilisé avec calcul local** |
| Samples Input System 1.19.0 présents dans `Library/PackageCache` | Unity Technologies, révision package `ca8d898…`, Unity Companion License | Exemples maintenus avec le package, examinés comme référence d'API ; aucun fichier importé ou copié. | **Référence seulement** |
| Cinemachine | Package officiel non présent dans le manifeste | Surdimensionné pour une cible orthographique unique et ajouterait une dépendance sans gain sur le contrat demandé. | **Rejeté pour ce lot** |
| Frameworks externes de caméra/tri | Licences et maintenance variables | Le comportement tient dans des composants Runtime testables, dont un coordinateur unique de tri en `LateUpdate`, et ne justifie ni dépendance ni propriété externe. | **Rejetés** |

Sources officielles consultées :

- https://docs.unity3d.com/Packages/com.unity.inputsystem@1.19/manual/Mouse.html ;
- https://docs.unity3d.com/ScriptReference/Camera-orthographicSize.html ;
- https://docs.unity3d.com/ScriptReference/Mathf.SmoothDamp.html ;
- https://docs.unity3d.com/6000.0/Documentation/Manual/2d-renderer-sorting.html.

Aucun code d'exemple n'est repris et aucune dépendance n'est ajoutée.

<!-- LOT7HA3_REUSE -->
## Lot 7H-A3 — réutilisation technique

Recherche retenue : réutiliser UI Toolkit déjà fourni par Unity et déjà présent
dans le projet. `UIDocument`, `PanelSettings`, `ProgressBar` et le thème
prototype existant couvrent le HUD sans package, asset ou licence externe.

Décision :

- pas de framework de statistiques ou de survie ;
- pas de package de barre de vie ;
- pas de dépendance Asset Store ;
- pas de modification de `THIRD_PARTY.md`, car aucune dépendance tierce n'est
  ajoutée ;
- conserver le HUD programmatique cohérent avec le prototype d'inventaire ;
- conserver la logique de dégâts et de respawn sous contrôle direct du projet.

Stratégie de sortie : le HUD provisoire peut être remplacé par une présentation
artistique sans modifier `PlayerHealthState`, la sauvegarde V2 ou
`PlayerHealthRuntimeStep`.
