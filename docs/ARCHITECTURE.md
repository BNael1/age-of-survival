# Architecture

## Principe

La simulation est la source de vérité. Les objets Unity représentent seulement la partie visible ou interactive de cet état.

## Couches

### Core

C# pur, sans `UnityEngine`. Contient les états, règles, commandes, événements métier, algorithmes déterministes et formats logiques. Doit être testable sans scène.

### Runtime

Adaptateurs Unity : boucle de jeu, rendu, Tilemap, entrée, UI, audio, caméra et synchronisation entre l’état logique et les objets visibles.

### Content

Définitions éditoriales et assets. Les `ScriptableObject` pourront décrire des items, techniques ou constructions, mais ne posséderont pas l’état mutable principal d’une partie.

### Tests

Les tests EditMode couvrent le Core. Les tests PlayMode seront ajoutés seulement pour les intégrations qui nécessitent réellement le moteur.

## Invariants initiaux

- aucune référence `UnityEngine` dans `AgeOfSurvival.Core` ;
- coordonnées logiques entières ;
- conversion position/index vérifiée et stable ;
- tick fixe indépendant du framerate ;
- identifiants persistants stables lorsqu’ils seront introduits ;
- sérialisation et migrations versionnées ;
- aucune dépendance tierce ne possède la simulation ou les sauvegardes.

## Découpage envisagé

```text
Assets/AgeOfSurvival/
  Core/
  Runtime/
  Content/
  Tests/
```

Le monde sera conçu pour un découpage en chunks, mais la taille de production des chunks n’est pas encore décidée.

## Premier adaptateur de rendu

Le lot 2 introduit `AgeOfSurvival.Runtime`, qui dépend de `AgeOfSurvival.Core`. La dépendance inverse est interdite.

`DebugIsometricWorld` est un adaptateur temporaire :

- il construit une `DenseGrid<byte>` déterministe ;
- il traduit les coordonnées logiques en cellules d’une Tilemap Unity isométrique ;
- il génère ses visuels de débogage au runtime ;
- il ne définit ni terrain de production, ni gameplay, ni sauvegarde.

Cette preuve d’intégration pourra être remplacée sans modifier les primitives du Core.
