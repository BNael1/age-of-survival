# Streaming de chunks

## Statut

Le lot 7E-A introduit le streaming multi-chunks par petits sous-lots. Le présent
sous-lot 7E-A1 ne modifie ni le Runtime Unity, ni la scène, ni le rendu. Il
verrouille uniquement la fenêtre logique et ses transitions dans le Core pur.

## Paramètres initiaux validés

- rayon visible `1`, soit une fenêtre `3 x 3` autour du chunk du joueur ;
- rayon de préparation `2`, soit une fenêtre `5 x 5` ;
- aucune frontière de chunk visible en jeu normal ;
- déplacement continu, y compris dans les coordonnées négatives ;
- aucune limite artificielle de carte ;
- aucune sauvegarde, éviction définitive ou simulation hors écran dans 7E-A1.

Ces rayons sont des paramètres techniques initiaux, pas une distance d'affichage
artistique définitive. La garde maximale de `64` empêche une allocation
accidentelle démesurée ; elle ne limite pas le monde logique.

## Contrat Core

`ChunkStreamingWindowPlanner` reçoit un chunk central et des rayons validés. Il
produit un plan immuable dont l'ordre est déterministe :

1. centre ;
2. anneaux de Chebyshev croissants ;
3. coordonnées triées par Y puis X à l'intérieur d'un anneau.

Les coordonnées qui dépasseraient `Int64` sont ignorées au bord extrême du
domaine au lieu de reboucler. Les chunks visibles forment toujours un
sous-ensemble des chunks préparés et aucune coordonnée n'est dupliquée.

`ChunkStreamingWindowTransition` compare deux plans. Les ajouts et affichages
suivent l'ordre du nouveau plan ; les retraits et masquages suivent l'ordre de
l'ancien. Le Runtime pourra donc appliquer un budget par frame sans posséder la
politique de sélection.

## Hors périmètre 7E-A1

- création et pooling des Tilemaps de chunks ;
- origine visuelle flottante ;
- chargement progressif sur plusieurs frames ;
- raccord des ressources et de l'inventaire à plusieurs chunks ;
- éviction du cache généré ;
- persistance des modifications ;
- Jobs, Burst, DOTS ou package externe.

## Validation attendue

Le sous-lot ajoute quatorze cas EditMode Core. Le total attendu passe de
`358` à `372`. Aucun nouveau test PlayMode n'est requis tant que le Runtime et
les scènes restent inchangés.

## Extension 7E-A2/A3 — Runtime et ressources

Le Runtime conserve une fenêtre visible de `3 x 3` Tilemaps et prépare une
fenêtre logique de `5 x 5`. Les neuf vues visibles sont mises en pool et
réutilisées lors d'un changement de chunk. Les chunks visibles sont générés
immédiatement ; l'anneau extérieur est préparé progressivement avec un budget
par frame. Lors d'un déplacement d'un chunk, la nouvelle bordure visible doit
donc déjà appartenir à l'ancienne fenêtre préparée. Une génération synchrone
reste un repli instrumenté, pas le chemin normal.

Le recentrage change uniquement le repère de présentation. L'origine logique,
la position Core du joueur et les coordonnées des ressources ne sont jamais
réécrites. Le nouvel ancrage Unity est calculé depuis l'ancien mapping avant le
changement d'origine, ce qui conserve exactement la position visuelle d'une
même coordonnée monde et évite saut de caméra ou téléportation.

Chaque Tilemap est remplie par bloc avec `SetTilesBlock`, puis reçoit les
couleurs de cellules nécessaires au prototype. Le mode `Individual` et l'ordre
`TopRight` restent actifs afin de préserver le contrat de recouvrement
isométrique et le tri avec les autres renderers. Aucun package externe, Job,
Burst ou DOTS n'est introduit.

Les ressources générées visibles sont fusionnées dans la session prototype par
identifiant stable. Une ressource déjà connue conserve son instance mutable et
donc son état récolté ; une même identité qui réapparaîtrait à une position
différente est refusée. Les marqueurs Unity sont reconstruits uniquement pour
la fenêtre visible, tandis que la session conserve l'état des chunks déjà
visités. Il n'y a toujours ni éviction définitive, ni sauvegarde disque.

## Instrumentation 7E-A2/A3

`DebugIsometricWorld` expose le centre courant, les comptes visible, préparé,
en attente et mis en cache, le nombre de vues créées, les préparations de la
frame et les générations synchrones de repli. Trois `ProfilerMarker` encadrent
la préparation, le rendu d'un chunk et le changement de fenêtre.

Le coordinateur de tri expose aussi le nombre d'entrées au-delà des `3971`
rangs distincts garantis par la plage de `sortingOrder`. Ce compteur doit rester
à zéro dans le prototype visible ; une saturation future exigera une stratégie
de couches ou de groupes, pas un silence métrique.

## Validation attendue 7E-A2/A3

Le lot combiné ajoute huit cas EditMode Runtime et un cas PlayMode. Le total
attendu passe à `380/380` EditMode et `8/8` PlayMode. Les validations couvrent
la fenêtre `3 x 3`, la préparation `5 x 5`, le pooling, les coordonnées
négatives, la continuité du mapping visuel, l'absence de repli synchrone après
préparation, l'unicité des ressources, la conservation de leur état mutable et
l'absence de saturation du tri dans la scène prototype.

<!-- LOT7EB_CHUNK_STREAMING -->
## Extension 7E-B1/B2 — mutations et rétention

La fenêtre visible reste de rayon `1` (`3 x 3`) et la fenêtre préparée de rayon
`2` (`5 x 5`). Le cache généré reçoit une troisième politique indépendante : un
rayon de rétention `3`, soit une borne de `49` chunks autour du centre après
stabilisation.

Lorsqu'un chunk sort de cette rétention, sa base générée peut être supprimée.
Le Core extrait uniquement les ressources récoltées et les conteneurs de sol non
vides. Les entrées stockées conservent leurs identifiants stables, positions,
types, quantités et encombrements ; leur ordre exporté est canonique. Un chunk
sans mutation ne crée aucun enregistrement sparse.

La transition Runtime suit deux phases :

1. calcul du futur ensemble retenu, capture/restauration prospective et
   validation de toutes les identités ;
2. commit de la session puis retrait effectif des chunks du cache monde.

Il doit exister exactement un propriétaire transactionnel de l'état lié aux
chunks. Une prévalidation échouée laisse inchangés cache, ressources,
conteneurs, compteurs et store. Une action de transfert active dont la source
sortirait de la rétention diffère l'éviction jusqu'à sa terminaison.

Les compteurs `ChunkStateEvictionCount` et `ChunkStateRestorationCount` servent
à l'instrumentation et aux preuves de cycle de vie. Ils ne remplacent ni la
mutation sparse ni un format de sauvegarde.

## Extension 7E-B3 — preuve réseau

La simulation autoritaire de smoke exerce volontairement une récolte suivie
d'une éviction et d'une restauration. Le snapshot répliqué inclut les compteurs
et son digest les couvre, afin que la reconnexion prouve à la fois l'état de
jeu et le passage effectif par le cycle de vie chunké.
