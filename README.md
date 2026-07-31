# Age of Survival

Prototype de production sous **Unity 6.3 LTS (6000.3.19f1, Apple Silicon)** avec le template **Universal 2D**.

Le projet vise un jeu de survie isométrique systémique et persistant. Le cœur de simulation reste en C# pur et ne dépend pas des `GameObject`. Unity sert d’adaptateur pour le rendu, l’entrée, l’UI, l’audio et les outils d’édition.

## État

Le dépôt Unity est en phase de fondation. Le premier lot ajoute :

- une structure de projet stable ;
- une assembly `AgeOfSurvival.Core` sans référence à UnityEngine ;
- des primitives de grille et d’horloge fixe ;
- des tests EditMode ;
- la documentation technique initiale ;
- un script batchmode reproductible.

## Tests

```sh
./tools/run_editmode_tests.sh
```

Voir `docs/TESTING.md`.
