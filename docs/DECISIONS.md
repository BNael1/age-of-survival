# Décisions

## ADR-0001 — Unity 6.3 LTS

**Statut : active**
**Date : 30 juillet 2026**

Utiliser Unity 6000.3.19f1 ARM64 avec Universal 2D et C#.

Motifs principaux : écosystème, outils, marge CPU, disponibilité de tutoriels et trajectoire vers serveur dédié. Risques acceptés : dépendance commerciale et dette de packages.

## ADR-0002 — Simulation indépendante de Unity

**Statut : active**
**Date : 31 juillet 2026**

Le cœur de simulation est une assembly C# pure avec `noEngineReferences: true`. Les `MonoBehaviour` sont des adaptateurs, pas la source de vérité.

## ADR-0003 — Pas de DOTS au départ

**Statut : active**
**Date : 31 juillet 2026**

Ne pas introduire Entities/DOTS. Jobs et Burst ne seront étudiés qu’après profiling d’un besoin réel.

## ADR-0004 — Petits lots vérifiés

**Statut : active**
**Date : 31 juillet 2026**

Chaque lot doit avoir un comportement attendu, des tests, un diff lisible et un petit commit.

## ADR-0005 — Tilemap isométrique comme premier adaptateur de rendu

**Statut : active**
**Date : 31 juillet 2026**

Utiliser la Tilemap isométrique officielle de Unity pour la première représentation du monde. La Tilemap reste un adaptateur de présentation alimenté par des données du Core ; elle ne devient pas la source de vérité de la simulation.

Le lot initial utilise des tuiles et couleurs générées au runtime comme visuels de débogage. Elles ne constituent pas une décision de direction artistique.

## ADR-0006 — Déplacement continu et entrée ZQSD minimale

**Statut : active**
**Date : 31 juillet 2026**

Le premier joueur utilise une position continue dans le Core et un déplacement à huit directions normalisé. Le lot initial lit uniquement ZQSD avec le package officiel Unity Input System, en recherchant les caractères produits par la disposition active du clavier.

Cette lecture directe des touches est un adaptateur minimal et réversible. Une architecture d’actions reconfigurables sera décidée lorsqu’un second périphérique, les menus de remappage ou plusieurs contextes d’entrée deviendront nécessaires. Les flèches ne sont pas ajoutées comme raccourci redondant dans ce lot.

## ADR-0007 — Interaction minimale avec ciblage automatique

**Statut : active**
**Date : 31 juillet 2026**

Le premier système de ressource cible automatiquement la ressource
`Available` la plus proche dans un rayon limité. À distance égale, l'identifiant
stable départage les candidates de façon déterministe.

La touche `E` est l'unique commande d'interaction de ce lot. L'adaptateur Unity
met cette commande en attente et le Core la consomme sur un tick fixe. Les
marqueurs de ressources et l'indicateur de cible sont des visuels temporaires
générés au runtime.

Une interaction réussie passe une seule ressource à `Harvested`. Ce lot ne crée
aucune récompense et ne se connecte pas à un inventaire.

## ADR-0008 — Inventaire en liste et encombrement entier

**Statut : active**
**Date : 31 juillet 2026**

L'inventaire propriétaire utilise une liste ordonnée d'entrées, sans grille ni
nombre de cases. Les matériaux homogènes sont empilables ; les outils,
vêtements, récipients et objets possédant leur propre état restent des instances
uniques.

L'encombrement et les capacités sont représentés par `EncumbranceValue` avec
`1000` unités internes pour `1,000` unité affichée. Les comparaisons de capacité
n'utilisent pas de `float`. Un ajout peut accepter partiellement une quantité,
mais un retrait standard impossible reste atomique. Un transfert retire de la
source uniquement ce que la destination a réellement accepté.

Les collections mutables restent privées au Core. Les définitions éditoriales
et l'état mutable sont séparés afin de préserver une future sérialisation
versionnée et la possibilité de remplacer les adaptateurs Unity.

## ADR-0009 — Équipement fixe et UI Toolkit comme adaptateur

**Statut : active**
**Date : 31 juillet 2026**

Le premier équipement comporte trois emplacements fixes : main gauche, main
droite et dos. Une définition déclare explicitement ses emplacements compatibles
et une réduction entière éventuelle du poids du contenu lorsqu'elle représente
un conteneur équipé. Le prototype de sac applique `70 %` uniquement au contenu
du sac porté ; la capacité et les poids bruts restent inchangés.

La session d'inventaire demeure un objet C# hors des `GameObject`. UI Toolkit,
API officielle déjà disponible dans Unity 6000.3.19f1, est retenu pour le
prototype runtime en listes. La vue reçoit des view-models en lecture seule et
appelle des commandes validées pour transférer, équiper ou déséquiper. Ce choix
n'impose ni framework tiers, ni drag-and-drop, ni grille de cases.

## ADR-0010 — Transfert temporisé sans réservation destructive

**Statut : active**
**Date : 31 juillet 2026**

Une récolte crée d'abord tout son rendement dans un conteneur de sol stable.
Le transfert vers l'inventaire est une action Core distincte, exprimée en ticks
entiers. Son démarrage calcule une quantité planifiée mais ne retire et ne
réserve aucun objet. La source et la destination sont revalidées à la fin ; seule
la quantité encore disponible et admissible est déplacée.

Le prototype autorise une seule action de transfert active par joueur. Tout
déplacement significatif ou dépassement de la portée l'interrompt. Cette règle
est volontairement locale au prototype ; files d'actions, réservations
multi-acteurs et concurrence réseau sont reportées.

À 60 ticks par seconde, les valeurs temporaires sont : `15` ticks de base,
`30` ticks par unité d'encombrement affichée, minimum `15` ticks, portée `1,5`
et rendement de `6` branches par ressource. Elles sont centralisées et ne
constituent pas un équilibrage final.

## ADR-0011 — Pénalité de surcharge progressive

**Statut : active**
**Date : 31 juillet 2026**

La vitesse de déplacement dépend de la charge perçue divisée par la capacité du
conteneur principal. Jusqu'à `100 %`, le multiplicateur reste `×1,00`. Les
points de contrôle sont `125 % → ×0,81`, `150 % → ×0,63`,
`175 % → ×0,44` et `200 % → ×0,25`. Les valeurs intermédiaires utilisent une
interpolation linéaire ; au-delà de `200 %`, le minimum reste `×0,25`.

Cette adaptation reprend la sévérité générale de Project Zomboid sans ses
ruptures de paliers. La courbe appartient au Core et reste indépendante de
l'entrée et du rendu. Le lot initial n'ajoute ni sprint, ni endurance, ni dégâts
de surcharge.

## ADR-0012 — Terrain isométrique raccordé par chevauchement

**Statut : active**
**Date : 1 août 2026**

Naël valide pour le lot 6B la copie directe des deux tuiles internes à Age of
Survival depuis le fichier source Godot
`/Users/bensaadi/jeu/assets/placeholders/ground_tiles.png`, au commit
`deba36ea24f7c489994c2ba6104cdd7c7d02cc14`, vers les trois chemins de terrain
Unity existants. Aucun redessin n'est effectué : les régions `64 × 32` de
l'atlas source sont copiées pixel pour pixel. La tuile gauche alimente
`ground_grass.png`, la tuile droite alimente `ground_dirt.png` et
`ground_water.png` duplique provisoirement la tuile gauche.
Ces fichiers sont des assets internes à Age of Survival.

Le dépôt Godot reste une archive de référence inchangée. Unity conserve ses
cellules isométriques de `1 × 0,5` unité, `64 PPU`, le filtrage Point, l'absence
de mipmaps et de compression. Le raccord repose sur le chevauchement alpha déjà
présent dans les tuiles Godot plutôt que sur une réduction ou un redessin des
losanges.

**Amendement lot 6C — décision validée par Naël le 2 août 2026.** La capture
Play Mode a invalidé la preuve visuelle du lot 6B : sa métrique alpha ne
mesurait pas les coutures opaques. Sans modifier les PNG, le Core ni les
coordonnées logiques, Unity rend désormais chaque tuile avec un pas visuel
diagonal de `15 px`, un `TilemapRenderer` en mode `Individual`, l'ordre
`TopRight` et le tri de transparence sur l'axe Y. Naël valide la suppression des
coutures internes. Les tranches du périmètre extérieur sont acceptées : elles
correspondent à des cellules sans voisin pour les recouvrir. Deux Tilemaps
adjacentes de `5 × 5` cellules ont produit exactement le même rendu qu'une
Tilemap unique de `10 × 5`, joueur compris, avec `0` pixel différent.

## ADR-0013 — Caméra Runtime indépendante du monde

**Statut : active**
**Date : 3 août 2026**

La caméra principale reste orthographique et son adaptateur Unity Runtime suit
instantanément, en `LateUpdate`, le point d'ancrage au sol du visuel joueur mis
à jour en `Update`. Il conserve la profondeur Z courante et ne lit ni dimensions
de grille, ni bounds de Tilemap, ni cellules, ni ressources. Le Core ne connaît
ni `Camera`, ni zoom, et la simulation n'est jamais modifiée par le suivi.

Le zoom `orthographicSize = 4.0625` est une valeur technique fixe, explicite et
provisoire. Elle ne constitue pas un cadrage artistique définitif : ce calibrage
reste réservé au lot 7D. Le lot 7A n'introduit aucune génération de monde ; 7B
reste la génération déterministe et 7C la population initiale.

## ADR-0014 — Génération chunkée déterministe et versionnée

**Statut : active**
**Date : 3 août 2026**

Le monde généré est identifié par une seed non signée sur 64 bits, une version
de générateur strictement positive et une disposition explicite de chunks. La
version initiale est `FoundationV1` (`1`) et la disposition technique prototype
est `32 × 32`. L'échantillonnage se fait par coordonnée monde absolue et reste
indépendant de cette partition, afin qu'un changement futur de taille de chunk
ne change pas automatiquement le terrain logique.

Le Core n'utilise pas `System.Random`, dont la séquence n'est pas garantie entre
versions majeures de .NET. Il emploie un mélange entier sans état, verrouillé par
des fixtures de sortie. Toute modification volontaire de l'algorithme exige une
nouvelle version ; une version inconnue est refusée au lieu d'être régénérée
silencieusement.

`GeneratedChunk` est une base immuable. Les changements persistants résident
dans une couche sparse distincte et triable canoniquement. Le lot ne définit ni
biome, ni terrain final, ni ressources, ni spawn, ni rendu chunké : ces éléments
restent au lot 7C ou aux adaptateurs ultérieurs.
