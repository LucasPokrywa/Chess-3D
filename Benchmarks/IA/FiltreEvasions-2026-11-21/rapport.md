# Filtre des évasions en échec simple

Mesures du 21 novembre 2026 sous Unity 6000.3.11f1 en mode batch. Les deux variantes utilisent l’évaluation incrémentale, PVS, la table de transposition de 500 000 entrées et le budget en jeu de 1 000 ms. Le plafond du composant `ChessIA` de la scène Halloween est de 7. Le Profiler est arrêté. Le corpus est constitué des dix lignes `Milieu` du fichier [positions.tsv](../PionsPasses-30Positions-2026-11-19/positions.tsv).

Cinq paires alternées par FEN après une paire d’échauffement. Le témoin de chaque paire est le moteur actuel avec évaluation incrémentale. Les gains prennent le temps de la dernière validation commune, avec la formule `(témoin - filtre) / témoin`. Les médianes des deux temps sont affichées séparément du gain médian apparié.

Le gain médian est de 1,4 % sur 50 paires, dont 32 favorables. Les 100 recherches mesurées s’arrêtent par expiration du budget. Aucune paire ne change de profondeur.

| FEN | Profondeur commune | Temps médian témoin → filtre (ms) | Gain médian apparié | Paires favorables |
|---|---:|---:|---:|---:|
| Espagnole | 5 | 319,4 → 318,0 | +0,4 % | 3/5 |
| Italienne | 5 | 506,6 → 514,2 | −1,2 % | 2/5 |
| Sicilienne Najdorf | 4 | 291,7 → 273,0 | +6,1 % | 5/5 |
| Sicilienne Dragon | 5 | 737,6 → 751,5 | −0,5 % | 2/5 |
| Française | 5 | 290,7 → 259,7 | +12,0 % | 4/5 |
| Caro-Kann | 5 | 348,2 → 369,2 | −7,8 % | 1/5 |
| Gambit dame refusé | 5 | 332,6 → 295,8 | +15,9 % | 4/5 |
| Slave | 5 | 711,6 → 693,0 | +0,5 % | 3/5 |
| Est-indienne | 5 | 556,0 → 539,5 | +6,4 % | 3/5 |
| Anglaise symétrique | 5 | 309,9 → 291,3 | +8,4 % | 5/5 |

## Confirmation des trois médianes négatives

Cinq nouvelles paires alternées par FEN après échauffement, avec un témoin mesuré dans leurs propres séries.

| FEN | Gain médian de confirmation | Paires favorables |
|---|---:|---:|
| Caro-Kann | +1,6 % | 3/5 |
| Italienne | −1,8 % | 2/5 |
| Sicilienne Dragon | +0,6 % | 3/5 |

Le ralentissement de l’Italienne se retrouve dans les deux séries. Le filtre reste dans la copie de test.

À profondeur fixe 5, les dix recherches concordent sur les coups, scores et compteurs, dont les nœuds principaux, de quiétude et les relances PVS. Le FEN de la position de travail est restauré après chaque recherche fixe. Les évasions et leur ordre concordent sur 1 009 positions, dont 11 cas ciblés.

- [Résumé des 65 paires](paires-1000ms.tsv), séries principale et de confirmation.
- [Temps cumulé et compteurs à chaque validation](iterations-1000ms.tsv), par essai et par variante.
- [Contrôles à profondeur fixe 5](fixe-p5.tsv).

