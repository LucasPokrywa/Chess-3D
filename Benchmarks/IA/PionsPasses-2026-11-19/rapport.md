# Comparaison en parties : bonus de pion passé

Tournoi exécuté le 19 novembre 2026 sous Unity 6000.3.11f1, en mode batch sans
rendu, sur un AMD Ryzen 5 7535HS. Douze parties ont été terminées et leurs
645 demi-coups vérifiés indépendamment avec python-chess 1.11.2.

## Résultat

Du point de vue de la nouvelle version, avec le bonus : **5 victoires, 3 nulles,
4 défaites**, soit **6,5/12 points (54,2 %)**. L'ancienne version marque 5,5/12.
Aucune partie n'a atteint le plafond de durée du tournoi.

Dans le tableau, V/N/D désignent le résultat de la nouvelle version.

| Position de départ | Nouvelle version avec les Blancs | Nouvelle version avec les Noirs | Points nouvelle version |
|---|---|---|---:|
| Ouverture initiale | Nulle | Victoire | 1,5/2 |
| Milieu de jeu du benchmark | Défaite | Victoire | 1/2 |
| Tactique, mat en un | Victoire | Défaite | 1/2 |
| Finale de pions du benchmark | Défaite | Victoire | 1/2 |
| Finale du choix structurel | Victoire | Défaite | 1/2 |
| Finale à trois pions contre trois | Nulle | Nulle | 1/2 |

Les neuf parties décisives se terminent par mat. Les trois nulles se terminent
par triple répétition. Aucun abandon ni résultat fondé sur une évaluation.

## Versions et protocole

- Ancienne évaluation : `Assets/Scripts/IA/Evaluation.cs` au commit `bb62b8c`.
- Nouvelle évaluation : version locale, avec un bonus constant de 30 centipions
  pour chaque pion sans pion adverse devant lui sur sa colonne ou les voisines.
- Les règles, la position avec coordonnées des rois mémorisées et le Minimax
  sont identiques. Seule l'évaluation utilisée par chaque moteur diffère.
  Les noms des classes de référence sont renommés dans le projet temporaire
  pour pouvoir compiler les deux versions ensemble.
- Même entrée que le jeu : `Minimax.ChercherAvecBudget`, budget de **500 ms par
  coup**, profondeur maximale **10**, table de **500 000 entrées**, neuve à
  chaque appel. Une recherche qui atteint le plafond de profondeur peut terminer
  avant 500 ms.
- Deux parties pour chaque FEN, en inversant les couleurs. La version qui prend
  les Blancs lors de la première partie alterne entre les paires.
- Échauffement non compté : une recherche de 500 ms par version sur la position
  initiale et sur la finale du choix structurel.
- Positions et règles de fin annoncées avant le lancement. Pas d'ajout ou de
  retrait de position après observation des résultats.

Un arbitre externe applique les fins de partie, sans modifier le moteur : mat,
pat, triple répétition avec réclamation automatique, 100 demi-coups sans prise
ni mouvement de pion avec réclamation automatique, et cas simples de matériel
insuffisant. La clé de répétition inclut plateau, trait, droits de roque et
prise en passant uniquement lorsqu'elle est légale.

Une partie encore en cours après **200 demi-coups joués depuis le FEN** aurait
été classée `*`, indéterminée, sans être comptée comme nulle. Ce plafond n'a
servi dans aucune des douze parties.

## Positions de départ

1. Ouverture initiale :
   `rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1`
2. Milieu de jeu :
   `r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1`
3. Tactique :
   `7k/5Q2/6K1/8/8/8/8/8 w - - 0 1`
4. Finale de pions :
   `8/5pk1/6p1/8/4P3/5K2/8/8 w - - 0 35`
5. Choix structurel :
   `4k3/8/5p2/3p4/2P1P3/6P1/8/4K3 w - - 0 1`
6. Finale à trois pions contre trois :
   `8/5pk1/6p1/3p4/3P4/5KP1/5P2/8 w - - 0 35`

## Temps et profondeurs réellement mesurés

| Version | Coups joués | Profondeur terminée moyenne | Temps réel médian | Temps réel maximal |
|---|---:|---:|---:|---:|
| Avec bonus | 323 | 6,440 | 500,067 ms | 503,239 ms |
| Sans bonus | 322 | 6,494 | 500,069 ms | 502,829 ms |

Ces moyennes concernent les positions rencontrées pendant les parties ; elles
ne constituent pas une comparaison de coût sur des positions identiques.
Le temps mesuré est celui de l'appel de recherche synchrone, préparation interne
incluse, hors arbitrage, notation et écriture des fichiers.

## Vérifications

Le lanceur contrôle la légalité de chaque coup, la restauration du FEN après
recherche et l'égalité `principal + quiétude = total`.

Le vérificateur indépendant a rejoué les **12 PGN et 645 demi-coups**. Il a
contrôlé la légalité, la notation SAN, les FEN complets avant/après chaque coup,
les affectations de couleurs, les compteurs et chaque motif de fin, notamment
les répétitions. Résultat : **OK**.

## Interprétation

Le bonus termine légèrement devant sur ce lot. **Tout l'écart vient d'une seule
paire**, depuis la position initiale. Les cinq autres paires sont à égalité.
La finale du choix structurel est gagnée par les Blancs avec les deux versions :
le meilleur choix structurel observé dans les tests n'y procure donc pas de point
supplémentaire dans ce tournoi.

Ce résultat est un signal favorable limité, pas une démonstration d'un gain de
force. Le petit nombre de positions, les départs parfois très déséquilibrés et
la variabilité d'une recherche chronométrée empêchent de conclure solidement.
Avant d'ajouter un critère d'évaluation, il serait pertinent d'élargir le lot à
des départs variés, plutôt équilibrés, et de répéter les paires à couleurs
inversées. Aucun Elo n'est estimé à partir de ces douze parties.

## Fichiers

- `manifest.json` : versions, empreintes SHA-256 et réglages.
- `protocole.txt` : protocole enregistré avant les parties.
- `parties.csv` : résultats individuels, couleurs et motifs de fin.
- `coups.csv` : tous les coups, FEN, scores, temps, profondeurs et compteurs.
- `parties.pgn` : parties rejouables dans un logiciel d'échecs.
- `verification.json` : bilan et contrôles indépendants.
- `verifier_parties.py` : vérificateur, nécessitant python-chess.
- `PassedPawnMatchTemp.cs.txt` : source du lanceur Unity temporaire utilisé.

Le moteur du projet n'a pas été modifié pour ce tournoi. Le projet Unity isolé
et les dépendances temporaires ont été supprimés après vérification ; les
résultats et le source du lanceur sont conservés ici.
