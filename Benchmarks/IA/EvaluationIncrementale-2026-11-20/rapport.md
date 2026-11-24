# Référence de performance après intégration de l’évaluation incrémentale

Mesures du 20 novembre 2026 sous Unity 6000.3.11f1 sur Windows, AMD Ryzen 5 7535HS. L’évaluation de comparaison et sa recherche sont issues de `d9eed0328e3f9ab2c54d9745af5f5e29c8ac698a`. Les deux variantes utilisent la même version des règles et du plateau, issue de la copie de travail testée.

Le corpus est constitué des dix lignes `Milieu` du fichier [positions.tsv](../PionsPasses-30Positions-2026-11-19/positions.tsv). Les deux variantes utilisent PVS, une table de transposition de 500 000 entrées et une limite de quiétude de quatre demi-coups, avec l’évasion bornée en cas d’échec à la limite. Profiler arrêté.

- [Recherche fixe à profondeur 5](fixe-p5.tsv). Coup, score, nœuds principaux, nœuds de quiétude et relances PVS concordent sur les dix FEN.
- [Cinquante paires à 1 000 ms](paires-1000ms.tsv). Une paire d’échauffement par FEN, suivie de cinq paires mesurées avec alternance de la première variante. Le plafond est fixé à 5. Le temps relevé est `TempsDerniereIterationMs`, cumulé jusqu’à la dernière profondeur validée. Les compteurs d’une recherche interrompue incluent les visites de son itération incomplète.

L’évaluation intégrée est plus rapide dans 50/50 paires à profondeur égale. Le gain médian par paire est de **9,4 %** (30,7 ms). Les deux variantes terminent la même profondeur dans les 50 paires.

| Position | Profondeur avant → intégrée | Temps médian avant → intégré (ms) | Gain médian par paire |
|---|---|---:|---:|
| Espagnole | P5 (5/5) → P5 (5/5) | 278,8 → 247,9 | 11,0 % |
| Italienne | P5 (5/5) → P5 (5/5) | 439,0 → 400,8 | 8,7 % |
| Sicilienne Najdorf | P4 (5/5) → P4 (5/5) | 249,7 → 232,9 | 6,7 % |
| Sicilienne Dragon | P5 (5/5) → P5 (5/5) | 641,0 → 580,1 | 9,7 % |
| Française | P5 (5/5) → P5 (5/5) | 236,7 → 218,6 | 7,6 % |
| Caro-Kann | P5 (5/5) → P5 (5/5) | 314,4 → 282,2 | 9,6 % |
| Gambit dame refusé | P5 (5/5) → P5 (5/5) | 276,8 → 258,0 | 6,7 % |
| Slave | P5 (5/5) → P5 (5/5) | 585,9 → 526,9 | 9,7 % |
| Est-indienne | P5 (5/5) → P5 (5/5) | 477,5 → 422,5 | 11,8 % |
| Anglaise symétrique | P5 (5/5) → P5 (5/5) | 258,0 → 231,0 | 10,5 % |

Les suites Unity de règles, d’évaluation, de compteurs, de tri, de table de transposition et de quiétude passent. Le contrôle incrémental compare 6 268 coups et leurs annulations au recalcul depuis le plateau, dont une séquence imbriquée de neuf coups. Sur la Najdorf et la Dragon, une interruption à 50 ms après le début de P4 restaure le FEN de travail et les quatre sommes, puis renvoie le résultat de P3.

## Empreintes SHA-256 de la copie mesurée


- Assets/Scripts/IA/Evaluation.cs — 1747eec7f74dba1211e44c685f3489c37badbc002a8232f0a75d5c671d49fd7d
- Assets/Scripts/IA/EvaluationIncrementale.cs — 2956333647111c7c4292c7549b9a7a51e85631e3a269a4942c5c7e30865764a7
- Assets/Scripts/IA/Minimax.cs — ded4a06f151bd611b198db59697b0f617cdb82801aeaba39fc018838da511f02
- Assets/Scripts/DeplacementPiece.cs — 721b0c63ade58e8f2b433af674463060497419a7476b35756d16604cd04898cd
- Assets/Scripts/Echiquier.cs — 1ee956208b1b5d0612f5edcda71bf08ac6e636fcf383427084401ad00d82f1c5
- Assets/Scripts/PositionEchecs.Fen.cs — e137e1e3265449036f9626cb68100804b952c05709f4ef116346aa567c25d1e5
- Benchmarks/IA/PionsPasses-30Positions-2026-11-19/positions.tsv — ed40e1d4bd956b14e9c8185c36007dbfdf0755ad1e530f683224447614032a94
