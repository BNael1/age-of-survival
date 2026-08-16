# Dépendances et composants tiers

## État

Aucun code communautaire ou asset tiers n’a été ajouté par le lot de fondation.

Le template Universal 2D contient des packages officiels Unity déclarés dans `Packages/manifest.json`, notamment URP, Input System, Tilemap et Unity Test Framework. Ils relèvent de l’écosystème Unity et doivent rester suivis lors des mises à niveau.

Le lot 5B n'ajoute aucune dépendance, aucun code communautaire et aucun asset
tiers. L'interface utilise UI Toolkit et son thème runtime officiels déjà fournis
par Unity ; les autres candidats étudiés sont documentés dans
`TECH_REUSE_ROADMAP.md` et n'ont pas été importés.

Le lot 5V n'ajoute lui non plus aucun asset tiers. Ses neuf PNG temporaires ont
été produits spécifiquement pour le prototype Age of Survival et sont conservés
sous `Assets/AgeOfSurvival/Runtime/Resources/PrototypeVisuals/`. Ils ne fixent
pas la direction artistique et pourront être remplacés sans migration du Core.
Les packs Kenney CC0 ont été étudiés, mais aucun de leurs fichiers n'est importé
dans ce lot.

## Assets internes transférés pour le lot 6B

Les trois textures de terrain Unity suivantes sont des assets internes à Age of
Survival, pas des dépendances tierces :

- source : `/Users/bensaadi/jeu/assets/placeholders/ground_tiles.png` ;
- commit source Godot : `deba36ea24f7c489994c2ba6104cdd7c7d02cc14` ;
- destinations Unity :
  `Assets/AgeOfSurvival/Runtime/Resources/PrototypeVisuals/ground_grass.png`,
  `ground_dirt.png` et `ground_water.png` ;
- provenance : atlas produit pour le prototype Age of Survival et introduit
  dans son historique par Naël ;
- transformation : découpe directe des deux régions `64 × 32`, sans redessin ;
- obligations : aucune obligation de licence tierce identifiée.

Le dépôt Godot demeure inchangé. Les fichiers `.meta` Unity existants sont
conservés afin de préserver leurs GUID et leurs réglages d'import.

## Lot 7B — génération déterministe

Aucune bibliothèque, aucun package et aucun asset externe n'est ajouté. Le Core
contient une implémentation locale d'un échantillonneur entier sans état. Les
constantes de diffusion associées à SplitMix64 sont documentées comme référence
algorithmique dans `WORLD_GENERATION.md` et `TECH_REUSE_ROADMAP.md`; aucun
fichier source tiers n'est copié.

## Règle

Toute future dépendance externe doit documenter : nom, version, auteur, URL, licence, fichiers concernés, obligations, raison d’adoption, risques et stratégie de sortie.

<!-- LOT7C_THIRD_PARTY -->
## Lot 7C — population initiale

Aucun package, code source ou asset tiers n'est ajouté. L'implémentation des
champs Q16 et de l'amincissement par priorité est locale. L'article de Bridson
sur l'échantillonnage à distance minimale sert uniquement de référence
algorithmique ; aucun extrait de son implémentation n'est intégré.

La Tilemap, les sprites déjà présents et les APIs Unity incluses sont réutilisés.

## Lot 7D-A — caméra et tri

Aucun package, code, asset ou framework tiers n'est ajouté. Le lot réutilise
Input System `1.19.0`, `Camera`, `Mathf` et le rendu 2D fournis par Unity. Les
samples du package Input System, couverts par la Unity Companion License, ont
uniquement servi de référence locale ; aucun fichier n'est copié dans `Assets`.

<!-- LOT7EB_THIRD_PARTY -->
## Lot 7E-B — transport et toolchains officiels Unity

Le lot ajoute uniquement des packages officiels provenant du registre Unity :

- `com.unity.transport` `2.7.4` pour le transport UDP fiable/séquencé de la
  tranche de smoke ;
- `com.unity.toolchain.macos-arm64-linux` `1.1.0` pour la compilation Linux
  depuis macOS ARM64 ;
- `com.unity.sdk.linux-x86_64` `1.1.0` pour le serveur Linux retenu ;
- `com.unity.sdk.linux-arm64` `1.1.0`, installé avec la toolchain mais non retenu
  comme cible serveur du lot.

Le verrou de packages introduit aussi les dépendances officielles nécessaires,
notamment le sysroot Unity et les dépendances de Unity Transport vers Burst,
Collections et Mathematics. Leur présence transitive ne constitue pas une
adoption de DOTS, Jobs ou Burst pour l'architecture de simulation. Le Core du
jeu reste en C# pur et ne dépend pas de ces packages.

Aucun code communautaire, asset, service réseau tiers, SDK de compte,
matchmaking ou bibliothèque de sérialisation n'est ajouté. Unity Transport ne
possède ni la simulation autoritaire, ni le store de mutations, ni la future
sauvegarde. Une stratégie de sortie reste possible derrière le codec et les
adaptateurs de transport séparés.

<!-- CONSTRUCTION_ASSETS_20260811 -->
## Assets Construction du 11 août 2026

Les huit PNG `construction_*.png` ont été générés spécifiquement pour Age of
Survival avec l'outil intégré de génération d'images OpenAI, puis découpés,
passés sur chroma key magenta et redimensionnés localement. Aucun pack, auteur,
URL d'asset ou fichier tiers préexistant n'a été copié. Les sorties sont conservées comme assets de projet générés pour Age of
Survival. Leur provenance OpenAI est consignée ici ; aucune mention n'est
intégrée directement aux PNG du prototype. Les obligations de divulgation et
conditions applicables seront revérifiées avant toute distribution publique ou
commerciale.

Le code Runtime n'a aucune dépendance envers l'outil de génération. Les PNG
peuvent être remplacés sans toucher au Core, au catalogue logique ni aux
sauvegardes. La planche source et les découpes de travail restent hors dépôt
dans `~/Downloads/age-of-survival-asset-work/`.

### Remise à niveau des assets historiques prioritaires

`player_survivor.png`, `resource_tree.png` et `resource_shrub.png` ont été
remplacés par des créations originales produites pour ce projet avec l'outil
intégré de génération d'images OpenAI. Les anciens sprites locaux ont servi
uniquement de cibles de continuité. Les sorties ont reçu une chroma key magenta,
supprimée localement, puis ont été réduites en nearest-neighbor aux dimensions
historiques `32×48`, `40×48` et `40×48`. Les `.meta`, PPU, point filtering et
pivots Runtime existants sont conservés.

Aucun asset, pack, auteur ou fichier tiers n'a été copié. Ces créations suivent la même politique de provenance que les sprites
Construction ci-dessus : leur origine OpenAI est documentée dans le dépôt, sans
mention directement intégrée aux PNG du prototype. Les conditions applicables
seront revérifiées avant distribution publique ou commerciale ; elles
n'ajoutent aucune dépendance Runtime.

<!-- LOT7LD_THIRD_PARTY -->
## Lot 7L-D — recherche régions dynamiques, sans réutilisation de code

Aucune dépendance, package, source ou asset tiers n'est ajouté. L'implémentation
est propriétaire, en C# pur, et réutilise exclusivement les primitives Core du
projet.

Les Javadocs publiques officielles de Project Zomboid ont été consultées comme
retour d'expérience architectural : `IsoRegions` expose notamment des flags de
mur/sol/toit, des requêtes chunk/world et une notification `squareChanged`,
tandis que `IsoChunkRegion` expose enclosure, voisins et comptages Roof. Aucun
code, nom de structure interne sérialisée ou asset n'a été copié. Références :

- https://projectzomboid.com/modding/zombie/iso/areas/isoregion/IsoRegions.html
- https://projectzomboid.com/modding/zombie/iso/areas/isoregion/regions/IsoChunkRegion.html

Les publications suivantes ont été lues pour comparer les familles de
connectivité dynamique et split/merge :

- Holm, de Lichtenberg et Thorup, « Poly-logarithmic deterministic
  fully-dynamic algorithms for connectivity… », STOC 1998,
  https://doi.org/10.1145/276698.276715 ;
- Kistler et Webb, « Connected Components with Split and Merge », IPPS 1991,
  https://publications.ri.cmu.edu/connected-components-with-split-and-merge.

Ces textes restent des références bibliographiques sous les droits de leurs
éditeurs/auteurs ; aucun extrait de code ou de texte n'est incorporé. Le lot ne
transpose pas leurs algorithmes avancés : il retient un regroupement local par
sommets, des scopes explicitement bornés et un flood-fill C4 existant, solution
plus petite et vérifiable pour le besoin actuel.
