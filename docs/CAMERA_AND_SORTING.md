# Caméra, ancres au sol et tri visuel

## Contrat du lot 7D-A

`GroundAnchorCameraFollow` est l'unique adaptateur de caméra du prototype. Il
lit le delta matériel en pixels avec Input System, le normalise en pas logiques,
accumule ces pas dans une cible orthographique multiplicative, avance
l'amortissement en `LateUpdate`, puis copie X/Y depuis l'ancre visuelle du
joueur en conservant le Z de la caméra. Il ne consulte ni Tilemap, ni bounds,
ni dimensions de chunk.

Calibration initiale :

- taille initiale `4.0625` ;
- zoom proche `2.5`, zoom éloigné `8.0` ;
- normalisation provisoire `120 pixels = 1 pas logique` ;
- facteur par pas logique `1.10` ;
- amortissement `0.12 s` avec `Mathf.SmoothDamp` ;
- sensibilité `1.0`, exposée par `SetZoomSensitivity` pour le futur menu Options.

L'adaptateur calcule `logicalSteps = rawPixelDelta / scrollPixelsPerStep`, puis
la sensibilité utilisateur s'applique à ce résultat. Un pas logique positif
rapproche la vue (`target / 1.10`) et un pas négatif l'éloigne
(`target × 1.10`). Ainsi, `60` pixels donnent un demi-pas avec le réglage par
défaut. Les fractions successives s'accumulent et le trackpad n'est jamais
réduit à un simple signe. `scrollPixelsPerStep` refuse zéro, les valeurs
négatives, `NaN` et les infinis. Cette calibration demeure provisoire jusqu'à
la validation physique de Naël sur son Mac.

## Calibration du joueur

Le sprite joueur conserve ses dimensions logiques et son point Core. Son
`Transform.localScale` Runtime vaut `1.20` sur X et Y. Son pivot normalisé
`(0.50, 0.12)` désigne le centre des pieds et le `Transform` du visuel est
l'ancre suivie par la caméra. Aucun rayon, collision, texture ou déplacement
Core n'est modifié.

## Ressources et rendements au sol

Chaque racine de marqueur représente une ancre au sol explicite issue de la
`WorldPosition` Core de la ressource. Le buisson emploie le pivot
`(0.50, 0.12)` et le rendement posé le pivot `(0.50, 0.20)`. Le ciblage reste
effectué dans le Core avec cette même position logique ; les sprites ne
deviennent jamais source de vérité.

Le chemin fallback crée deux objets `Sprite` distincts sur la même texture :
le buisson garde le pivot Y `0.12` et le rendement posé le pivot Y `0.20`.

## Tri Y déterministe

Les composants mettent à jour leurs positions visuelles pendant `Update`, sans
appliquer eux-mêmes le classement. `GroundAnchorSortCoordinator` possède
l'unique passe, exécutée en `LateUpdate` au maximum une fois par frame ; elle
inclut toujours le joueur et toutes les ressources actives. Les entrées
visibles sont classées d'arrière en avant par Y exact, puis par comparaison
`StringComparison.Ordinal` de l'identifiant (`ResourceId` ou `player:local`) en
cas d'égalité. Un rang unique est attribué à chaque entrée, indépendamment de
l'ordre de création des `GameObject` et de l'ordre des `Update`. Huit ordres
sont réservés par rang à partir du biais dynamique `1000`, ce qui maintient
joueur et ressources au-dessus de la Tilemap du sol. Les enfants d'une
ressource utilisent des offsets fixes dans ce bloc : cible `0`, corps `1`,
quantité `5`, progression `6/7`. Aucun Script Execution Order global ne fait
partie de ce contrat.

La plage est bornée au contrat Unity `[-32768, 32767]`. La capacité actuelle
est donc de `3971` rangs simultanés avant saturation. Le lot 7E devra profiler
la population maximale de la fenêtre chargée ; aucune coordonnée ou position
Core ne devra être rabattue pour cette adaptation visuelle.
