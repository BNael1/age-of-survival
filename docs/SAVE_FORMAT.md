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

<!-- LOT7EB_SAVE_FORMAT -->
## Substrat sparse préparé par le lot 7E-B

Le lot 7E-B n'écrit toujours aucun fichier de sauvegarde et ne définit pas de
schéma disque. Il fournit toutefois le premier état mutable chunké exportable :

- coordonnées du chunk propriétaire ;
- ressources récoltées avec identifiant stable et position attendue ;
- conteneurs de sol non vides avec identifiant, position, définition de
  conteneur et capacité ;
- entrées empilées ou uniques avec identifiants, quantité, encombrement et
  référence éventuelle de conteneur imbriqué ;
- ordre canonique indépendant de l'ordre d'insertion Runtime.

Une future sauvegarde devra associer ces mutations à l'identité complète du
monde généré et du profil de population, puis les versionner et les migrer
explicitement. La restauration doit continuer à régénérer la base avant
d'appliquer la mutation ; sérialiser les 49 chunks du cache ou les Tilemaps est
interdit comme format principal.

Les révisions et digests du protocole multijoueur sont des contrats de
réplication de la tranche 7E-B. Ils ne constituent pas automatiquement la
version du futur format de sauvegarde. Les compteurs d'éviction et de
restauration sont de l'instrumentation et n'ont pas à être persistés sauf besoin
de diagnostic décidé ultérieurement.
