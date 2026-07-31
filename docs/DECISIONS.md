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
