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

## Critères d’adoption

- licence compatible ;
- version Unity 6 compatible ;
- maintenance active ;
- interfaces isolables ;
- stratégie de sortie ;
- gain de temps mesurable ;
- absence de propriété sur les systèmes centraux.
