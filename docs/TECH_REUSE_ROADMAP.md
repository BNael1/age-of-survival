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

## Critères d’adoption

- licence compatible ;
- version Unity 6 compatible ;
- maintenance active ;
- interfaces isolables ;
- stratégie de sortie ;
- gain de temps mesurable ;
- absence de propriété sur les systèmes centraux.
