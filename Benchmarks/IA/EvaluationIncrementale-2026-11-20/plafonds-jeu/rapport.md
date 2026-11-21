# Évaluation incrémentale aux plafonds de jeu

Unity 6000.3.11f1 sur Windows, AMD Ryzen 5 7535HS. Le plafond de `ChessIA` est de 6 dans la scène Classique et de 7 dans Halloween. Budget de 1 000 ms, PVS activée, table de transposition de 500 000 entrées et Profiler arrêté. Les deux variantes sont celles de la [référence initiale](../rapport.md), sur les mêmes [dix FEN de milieu de jeu](../../PionsPasses-30Positions-2026-11-19/positions.tsv).

Pour chaque plafond, une paire d’échauffement par FEN, puis cinq paires mesurées avec alternance du premier passage. Les temps sont relevés au point où la recherche valide l’itération. L’arrêt est le motif renvoyé par le moteur. Le temps total en fin de recherche inclut l’éventuelle itération interrompue.

Dans les 100 paires en jeu, la version intégrée termine plus vite chaque fois à profondeur égale. Le gain médian par paire est de **9,5 %**. Les 200 essais mesurés s’arrêtent par expiration du budget.

| FEN | Profondeur avant et après, sur les deux plafonds | Médiane Classique avant → intégré (ms) | Médiane Halloween avant → intégré (ms) |
|---|---:|---:|---:|
| Espagnole | P5 (5/5) | 282,7 → 249,7 | 285,1 → 250,5 |
| Italienne | P5 (5/5) | 444,3 → 402,4 | 442,3 → 404,3 |
| Sicilienne Najdorf | P4 (5/5) | 253,4 → 238,8 | 254,8 → 238,9 |
| Sicilienne Dragon | P5 (5/5) | 652,0 → 577,1 | 654,0 → 590,0 |
| Française | P5 (5/5) | 239,4 → 221,0 | 239,0 → 219,2 |
| Caro-Kann | P5 (5/5) | 312,3 → 281,8 | 312,8 → 284,4 |
| Gambit dame refusé | P5 (5/5) | 278,7 → 258,7 | 280,3 → 258,3 |
| Slave | P5 (5/5) | 584,9 → 532,7 | 590,6 → 536,5 |
| Est-indienne | P5 (5/5) | 480,7 → 426,6 | 482,8 → 425,9 |
| Anglaise symétrique | P5 (5/5) | 259,7 → 232,2 | 260,0 → 233,7 |

## Temps jusqu’à la profondeur suivante

Deux passages de sélection à P6 avec l’évaluation intégrée sur chacune des neuf FEN hors Najdorf. Les deux médianes les plus basses sont celles de la Caro-Kann et de l’Anglaise symétrique. Une paire d’échauffement et cinq paires alternées ont ensuite été réalisées sur ces positions à P6 et sur la Najdorf à P5. Budget de test de 60 000 ms. Toutes ces recherches ont atteint le plafond fixé.

| FEN | Cible | Temps cumulé médian avant → intégré (ms) | Dernière itération intégrée (ms) | Écart médian à 1 000 ms | Nœuds principaux de l’itération | Nœuds de quiétude de l’itération |
|---|---:|---:|---:|---:|---:|---:|
| Sicilienne Najdorf | 5 | 1 485,1 → 1 368,0 | 1 131,1 | 368,0 ms | 14 065 | 324 112 |
| Caro-Kann | 6 | 1 712,1 → 1 604,9 | 1 321,1 | 604,9 ms | 48 882 | 268 508 |
| Anglaise symétrique | 6 | 2 073,6 → 1 938,9 | 1 697,7 | 938,9 ms | 59 971 | 332 282 |

| Sélection P6 avec évaluation intégrée | Médiane de la sélection (ms) |
|---|---:|
| Caro-Kann | 1 603,8 |
| Anglaise symétrique | 1 947,8 |
| Gambit dame refusé | 1 984,6 |
| Espagnole | 2 006,2 |
| Italienne | 2 545,3 |
| Est-indienne | 2 556,9 |
| Sicilienne Dragon | 2 725,3 |
| Slave | 3 061,9 |
| Française | 5 236,0 |

Le prochain poste à profiler est la validation des coups générés en quiétude, de `GenererCoups()` à `LeavesKingInCheck()`. La Najdorf est la position la plus proche de la prochaine profondeur et sa P5 visite 324 112 nœuds de quiétude sur 338 177. Le profil devra mesurer le coût CPU de la validation des listes tactiques et des évasions séparément des tests d’existence d’un coup légal.

- [Un relevé par recherche](essais.tsv), avec le motif d’arrêt et le temps de la dernière validation.
- [Un relevé par itération validée](iterations.tsv), avec le temps cumulé ainsi que le temps, les nœuds et les relances propres à cette itération.
- La phase `Jeu` contient les mesures aux plafonds des scènes. `SelectionP6` contient les deux passages de classement. `Suivante` contient les répétitions à la profondeur cible. L’essai `0` désigne l’échauffement.

