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

## Contrat préparé par le lot 7B

Une future sauvegarde de monde doit conserver au minimum :

- la seed 64 bits ;
- la version du générateur ;
- la disposition des chunks utilisée par les modifications ;
- les modifications sparse, séparées de la base générée ;
- la version du format de sauvegarde.

Les cellules générées reconstructibles ne doivent pas être dupliquées par
défaut dans la sauvegarde. Une migration explicite sera nécessaire si la
version du générateur ou la disposition des modifications change. Le lot 7B
n'écrit encore aucun fichier de sauvegarde.

<!-- LOT7C_SAVE_FORMAT -->
## Contrat de population préparé par le lot 7C

Une sauvegarde reconstruisant `PopulationV1` doit conserver, en plus de la seed
et de la version du générateur :

- l'identifiant du profil de population ;
- sa révision ;
- les suppressions ou transformations de ressources par identifiant stable ;
- les modifications de terrain séparées de la base générée ;
- le spawn choisi si le jeu autorise ensuite un déplacement ou un choix manuel.

Le contenu reconstructible d'un `PopulatedChunk` ne doit pas être sérialisé par
défaut. Modifier les paramètres d'un profil déjà persisté sans changer sa
révision est interdit.
