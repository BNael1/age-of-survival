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
