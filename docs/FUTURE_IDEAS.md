# Idées futures

Ce fichier contient les idées volontairement reportées. Une entrée ici n’est pas une décision active.

## Reporté après le socle Unity

- météo et saisons ;
- température corporelle ;
- blessures et maladies ;
- animaux, PNJ et communautés ;
- multijoueur et serveur dédié ;
- API de modding ;
- Jobs, Burst ou DOTS ;
- pipeline artistique Aseprite/Pixelorama ;
- outils de réparation avancée des sauvegardes.

Les règles de construction, de refuges et de familiarité déjà validées ne sont pas des idées futures, mais leur portage Unity attend la validation du socle.

## Reporté après le socle d'inventaire

- conteneurs imbriqués arbitraires et prévention des cycles ;
- filtres par catégorie et règles d'acceptation propres aux conteneurs ;
- nutrition corporelle avancée, poids et besoins macro/micronutritionnels ;
- liquides, mélanges et récipients partiellement remplis ;
- durabilité et états complexes des objets ;
- tri, recherche et regroupements avancés ;
- drag-and-drop, menus contextuels et raccourcis d'équipement ;
- emplacements d'équipement éditoriaux ou extensibles au-delà des deux mains et
  du dos ;
- thème visuel définitif, icônes, portraits et navigation manette ;
- file d'actions, réservations multi-acteurs et résolution réseau concurrente ;
- regroupement spatial, fusion et persistance des conteneurs de sol ;
- durées propres aux catégories d'objets, compétences et animations ;
- format de sauvegarde versionné, migrations et réparation de données.

<!-- LOT7C_FUTURE_IDEAS -->
## Reporté après le lot 7C

- streaming de plusieurs chunks autour du joueur ;
- collision, navigation et interdiction de marcher sur l'eau ;
- biomes multiples, rivières, routes et relief physique ;
- tables de ressources variées, arbres, rochers et animaux ;
- sauvegarde des ressources récoltées et migrations correspondantes ;
- asset d'eau final et calibrage artistique, à traiter avec le lot 7D ou un lot
  d'assets dédié.

## Reporté après le lot 7D-B

- client réseau et protocole de connexion au futur VPS ;
- serveur autoritaire dédié, déploiement et supervision ;
- navigateur de serveurs, favoris persistants et historique ;
- authentification, permissions, administration et sécurité réseau ;
- hébergement de partie depuis le client et invitations.
<!-- FUTURE_7GA_SAVE_TRIGGERS -->
## Déclencheurs de sauvegarde différés

- ajouter une sauvegarde après repos ou sommeil terminé lorsque ces systèmes
  existent réellement dans le Runtime ;
- raccorder la politique serveur autoritaire : le serveur sauvegarde, les
  clients connectés n'exposent aucune sauvegarde locale ;
- décider séparément d'un chargement pendant une partie, actuellement rejeté ;
- ajouter les validations de durabilité matérielle sur NTFS et sur le système de
  fichiers Linux cible avant toute revendication multiplateforme.

<!-- LOT7HA1_FUTURE_INJURIES -->
## Idée future — blessures localisées

Le système vital global du lot 7H ne doit pas devenir un cadre générique de
statistiques. Un système ultérieur pourra ajouter des états localisés par zone
corporelle, notamment plaies, fractures, brûlures, morsures, saignement,
douleur, infection et soins spécifiques.

Ces blessures devront alimenter les conséquences globales — perte de vie,
mobilité, vitesse d'action, capacité de travail ou risque d'infection — sans
remplacer brutalement le contrat de santé vital et sans casser les sauvegardes
existantes. Cette idée est volontairement reportée et n'est pas une décision
d'implémentation active du lot 7H-A1.

<!-- LOT7I_FUTURE_IDEAS -->
## Reporté après le lot 7I

- conséquences sanitaires de la consommation d'aliments pourris ;
- cuisson, cuisson excessive et recettes ;
- température, congélation, réfrigérateurs et multiplicateurs de stockage ;
- persistance spécialisée des aliments périssables déposés au sol ;
- nutrition corporelle, poids, calories réellement consommées et carences ;
- diversité alimentaire au-delà de la pomme prototype et équilibrage définitif ;
- localisation des libellés alimentaires et présentation visuelle finale.
