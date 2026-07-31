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
