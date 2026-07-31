# Dépendances et composants tiers

## État

Aucun code communautaire ou asset tiers n’a été ajouté par le lot de fondation.

Le template Universal 2D contient des packages officiels Unity déclarés dans `Packages/manifest.json`, notamment URP, Input System, Tilemap et Unity Test Framework. Ils relèvent de l’écosystème Unity et doivent rester suivis lors des mises à niveau.

Le lot 5B n'ajoute aucune dépendance, aucun code communautaire et aucun asset
tiers. L'interface utilise UI Toolkit et son thème runtime officiels déjà fournis
par Unity ; les autres candidats étudiés sont documentés dans
`TECH_REUSE_ROADMAP.md` et n'ont pas été importés.

## Règle

Toute future dépendance externe doit documenter : nom, version, auteur, URL, licence, fichiers concernés, obligations, raison d’adoption, risques et stratégie de sortie.
