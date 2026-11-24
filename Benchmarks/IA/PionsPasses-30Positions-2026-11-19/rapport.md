# Tournoi de 30 nouvelles positions — bonus de pion passé

Comparaison native Unity 6000.3.11f1 à 500 ms par coup. **60 parties, 30 paires**, et 3728 demi-coups vérifiés indépendamment avec python-chess 1.11.2.

## Résultat global

Version avec bonus : **14 victoires, 31 nulles, 15 défaites**, 0 parties indéterminées.

Score : **29,5/60 points**, soit **49,2 %**, contre 30,5/60 pour la version sans bonus.

Paires : **8 gagnées, 17 à égalité, 5 perdues**, 0 indéterminées.

Une paire est gagnée au-delà de 1 point sur 2, à égalité à 1 point sur 2 et perdue en dessous. Une partie indéterminée laisse sa paire indéterminée ; elle n'est jamais assimilée à une nulle.

| Catégorie | Parties | Points avec bonus | Paires gagnées | Égalités | Paires perdues |
|---|---:|---:|---:|---:|---:|
| Ouverture | 20 | 9,0/20 | 3 | 4 | 3 |
| Milieu | 20 | 10,0/20 | 4 | 4 | 2 |
| Finale | 20 | 10,5/20 | 1 | 9 | 0 |

## Interprétation du résultat

Le score global est pratiquement équilibré, légèrement en faveur de l'ancienne version : **29,5 contre 30,5 points**. Ce tournoi ne confirme donc pas un gain global de force du bonus constant de 30 centipions.

Le nombre de paires gagnées est supérieur au nombre de paires perdues, mais les marges diffèrent : **8 paires gagnées à 1,5–0,5**, **4 paires perdues à 0–2**, une perdue à 0,5–1,5 et 17 à égalité. Cela explique le score global inférieur à 50 % malgré 8 paires gagnées contre 5 perdues.

Les finales sont légèrement favorables au bonus, avec **10,5/20 points**, mais seule la paire « majorités opposées » départage les versions : une paire gagnée et neuf à égalité. Les milieux de jeu donnent 10/20 et les ouvertures 9/20. Le meilleur repérage des pions passés ne se traduit pas ici par une amélioration globale des résultats.

Le poids de 30 centipions reste donc un candidat à calibrer et à valider avant de cumuler d'autres critères d'évaluation. Aucune valeur ni règle du moteur n'a été changée pendant ou après les parties.

## Protocole fixé avant les parties

- Ancienne version : évaluation du commit `bb62b8c`, sans bonus de pion passé.
- Nouvelle version : évaluation locale avec le seul bonus constant de **30 centipions** pour chaque pion passé. Les empreintes des sources sont dans `manifest.json`.
- Même Minimax, mêmes règles, mêmes positions et mêmes optimisations précédentes. Classes de référence renommées uniquement pour compiler les deux variantes ensemble.
- Même entrée que le jeu : `ChercherAvecBudget`, **500 ms/coup**, profondeur maximale **10**, table de **500 000 entrées neuve à chaque appel**. Une recherche atteignant la profondeur maximale peut finir plus tôt.
- Recherche séquentielle, une seule instance Unity de test, mode batch sans rendu. Deux parties par FEN, couleurs inversées ; la version prenant les Blancs en premier alterne entre les paires.
- Échauffement hors score : une recherche par version sur le premier départ d'ouverture et le premier départ de finale.
- Mat et pat ; réclamation automatique par l'arbitre de la triple répétition ou des 50 coups ; cas simples de matériel insuffisant. Clé de répétition : plateau, trait, roques et prise en passant uniquement si légale.
- Aucun abandon, aucune adjudication à partir du score d'évaluation. Plafond technique de **400 demi-coups depuis chaque FEN**, fixé avant lancement ; résultat `*` si atteint.

## Liste et contrôle d'équilibre

Les 30 plateaux sont légaux, distincts et nouveaux par rapport au tournoi précédent. La liste est exportée dans `positions.csv` et `positions.tsv` avant le lancement. Le lanceur vérifie son SHA-256 avant toute partie.

Gel UTC : `2026-11-19T17:06:00.7785635Z`. SHA-256 du TSV : `ed40e1d4bd956b14e9c8185c36007dbfdf0755ad1e530f683224447614032a94`.

Dix familles d'ouvertures : Espagnole, Italienne, Najdorf, Dragon, Française, Caro-Kann, gambit dame refusé, Slave, Est-indienne et Anglaise symétrique. Dix milieux de jeu proviennent de prolongements de ces familles. Les dix finales comprennent six finales de pions, deux finales de tours, fou contre cavalier et fous de même couleur.

Avant toute partie, les positions ont été contrôlées à profondeur 4 avec l'ancienne version. Le seuil retenu pour les milieux de jeu était ±100 centipions : les dix départs finaux sont entre **−48 et +40 centipions pour les Blancs**. Un premier départ Slave, trop déséquilibré (+173 pour le joueur au trait), a été raccourci avant gel. Ce filtre est une estimation du moteur, pas une preuve d'équilibre. Les finales ont un matériel égal et mettent en jeu la création, la course ou le blocage de pions passés.

Aucune position n'a été ajoutée, retirée ou modifiée après le premier coup. Les premiers douze matchs de l'essai précédent ne sont pas mélangés avec ces résultats.

## Résultats des 30 paires

V/N/D sont les résultats de la version avec bonus, avec chaque couleur.

| N° | Départ | Bonus avec les Blancs | Bonus avec les Noirs | Points | Paire |
|---:|---|---|---|---:|---|
| 1 | Ouverture - Espagnole | D | D | 0/2 | Perdue |
| 2 | Ouverture - Italienne | D | N | 0,5/2 | Perdue |
| 3 | Ouverture - Sicilienne Najdorf | N | V | 1,5/2 | Gagnée |
| 4 | Ouverture - Sicilienne Dragon | V | D | 1/2 | Égalité |
| 5 | Ouverture - Francaise | N | N | 1,0/2 | Égalité |
| 6 | Ouverture - Caro-Kann | N | V | 1,5/2 | Gagnée |
| 7 | Ouverture - Gambit dame refuse | V | D | 1/2 | Égalité |
| 8 | Ouverture - Slave | V | N | 1,5/2 | Gagnée |
| 9 | Ouverture - Est-indienne | N | N | 1,0/2 | Égalité |
| 10 | Ouverture - Anglaise symetrique | D | D | 0/2 | Perdue |
| 11 | Milieu - Espagnole | D | V | 1/2 | Égalité |
| 12 | Milieu - Italienne | N | N | 1,0/2 | Égalité |
| 13 | Milieu - Sicilienne Najdorf | D | D | 0/2 | Perdue |
| 14 | Milieu - Sicilienne Dragon | N | N | 1,0/2 | Égalité |
| 15 | Milieu - Francaise | N | V | 1,5/2 | Gagnée |
| 16 | Milieu - Caro-Kann | N | N | 1,0/2 | Égalité |
| 17 | Milieu - Gambit dame refuse | V | N | 1,5/2 | Gagnée |
| 18 | Milieu - Slave | N | V | 1,5/2 | Gagnée |
| 19 | Milieu - Est-indienne | D | D | 0/2 | Perdue |
| 20 | Milieu - Anglaise symetrique | V | N | 1,5/2 | Gagnée |
| 21 | Pions - deux ailes verrouillees | N | N | 1,0/2 | Égalité |
| 22 | Pions - prises sur deux ailes | V | D | 1/2 | Égalité |
| 23 | Pions - majorite centrale bloquee | N | N | 1,0/2 | Égalité |
| 24 | Pions - passes eloignes concurrents | V | D | 1/2 | Égalité |
| 25 | Pions - quatre contre quatre | N | N | 1,0/2 | Égalité |
| 26 | Pions - majorites opposees | N | V | 1,5/2 | Gagnée |
| 27 | Tours - trois pions contre trois | N | N | 1,0/2 | Égalité |
| 28 | Tours - creation de passe | N | N | 1,0/2 | Égalité |
| 29 | Fou contre cavalier - blocage | N | N | 1,0/2 | Égalité |
| 30 | Fous de meme couleur - blocage | V | D | 1/2 | Égalité |

## Temps et profondeur

| Version | Coups | Profondeur terminée moyenne | Temps réel médian | Temps maximal |
|---|---:|---:|---:|---:|
| nouveau | 1864 | 5,539 | 500,069 ms | 503,285 ms |
| ancien | 1864 | 5,489 | 500,069 ms | 503,276 ms |

Les moyennes de profondeur portent sur des positions rencontrées en partie, qui diffèrent entre variantes. Elles ne mesurent pas directement un coût par position identique.

## Vérification et limites

Le vérificateur indépendant rejoue les PGN et les CSV : légalité et SAN de chaque coup, FEN complets avant/après, couleurs inversées, identité des FEN avec la liste gelée, somme des compteurs et motifs de fin. Bilan : **OK**. Les empreintes de la liste sont contrôlées à nouveau. Les résultats comprennent **29 mats, 30 nulles par triple répétition et une nulle par matériel insuffisant**. Aucune partie n'atteint le plafond de 400 demi-coups.

Le bilan décrit ce lot et ce budget. Les ouvertures et milieux sont issus de dix familles communes ; les 30 départs ne sont donc pas 30 échantillons totalement indépendants. Le temps limité rend la profondeur et les décisions sensibles aux variations de charge. Aucun Elo n'est déduit de ce tournoi.

## Fichiers conservés

- `parties.pgn`, `parties.csv`, `coups.csv` : toutes les parties et mesures.
- `paires.csv`, `verification.json` : score global, regroupement par paire et vérification.
- `positions.csv`, `positions.tsv`, `positions.sha256`, `manifest.json`, `protocole.txt` : liste figée et configuration.
- `equilibre_avant.csv` : contrôle initial à profondeur 4.
- Sources des lanceurs temporaires et scripts de préparation/vérification : reproductibilité.

Le moteur du projet n'a pas été modifié pour ce tournoi.
