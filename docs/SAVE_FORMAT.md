# Format de sauvegarde

## Statut

Le format Unity de production n’est pas encore implémenté.

## Invariants décidés

- version explicite du format ;
- identifiants stables ;
- migrations explicites ;
- séparation entre monde généré et modifications persistantes ;
- chargement partiel futur par chunk ;
- écriture atomique avec récupération ;
- tests aller-retour et tests de migration ;
- aucune dépendance envers la sérialisation automatique d’une scène Unity.

Le premier format sera spécifié avant l’écriture du système de sauvegarde.
