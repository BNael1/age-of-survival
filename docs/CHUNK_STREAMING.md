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
