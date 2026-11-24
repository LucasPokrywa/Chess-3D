# Benchmark IA

Hors mode Play, lancer **Halloween Chess > Benchmark IA** dans Unity.
Le mode **Halloween Chess > Benchmark IA - 500 ms** réutilise exactement les mêmes
quatre FEN avec `Minimax.ChercherAvecBudget`, l'entrée utilisée par
`ChessIA.ChoisirCoupAsync` en partie. Son budget est de **500 ms**, son plafond de
**10 demi-coups**, sa table de **500 000 entrées**. Le plafond est constant pour
les comparaisons, indépendant de la profondeur Inspector de la scène de jeu.
**Halloween Chess > Benchmark IA - 1000 ms** utilise le même protocole avec le
budget du jeu. En partie, les 1 000 ms sont partagées entre toutes les
itérations et la quiétude d'un même coup. Le jeu garde son plafond Inspector.
Les modes du benchmark utilisent la PVS par défaut. Pour un comparatif,
`Minimax.Chercher` et `Minimax.ChercherAvecBudget` acceptent
`utiliserPvs: false` pour choisir alpha-bêta.
**Halloween Chess > Verifier les compteurs IA** vérifie la répartition des visites
et la référence fixe actuelle du milieu de jeu avec PVS : **2 827 nœuds
principaux + 25 754 en quiétude = 28 581**, **9 relances**, **e2a6**, score **-16**.
Avec la même évaluation, la recherche alpha-bêta précédente visitait 27 630 nœuds.
**Halloween Chess > Verifier les pions passes** vérifie le critère géométrique,
les scores et la symétrie des couleurs, ainsi que huit finales à coups attendus.
**Halloween Chess > Tester IA - Suites de quietude** vérifie notamment les deux
visites en quiétude (et aucune principale) lors de l'évasion à la limite.
Le benchmark utilise un composant `ChessIA` temporaire dans une scène de prévisualisation,
sans modifier la scène de jeu ni jouer de coup. La commande est désactivée pendant
une compilation ou une exécution déjà en cours.

## Protocole IA-v1

- Quatre FEN inscrits dans `Assets/Editor/IABenchmark.cs` : ouverture, milieu de jeu,
  tactique (mat en un), finale de pions.
- Profondeur fixe : **4 demi-coups**.
- Table de transposition activée, capacité explicitement fixée à **500 000 entrées**.
- Un échauffement par position, puis **3 recherches mesurées** ; chaque appel utilise
  une table et un historique neufs, comme le moteur actuel.
- Un appel par update de l'éditeur. Annulation possible entre les appels via la
  barre de progression ; une recherche synchrone en cours va jusqu'à son terme.

Le composant de la scène, sa profondeur Inspector et les valeurs par défaut de
`Minimax.Chercher` ne configurent pas ce benchmark. Conserver la suite, ses FEN,
sa profondeur et ses options identiques pour comparer deux versions.

## Console et comparaison

Chaque mesure affiche le temps, la profondeur atteinte, les nœuds visités et les
nœuds par seconde, ainsi que le coup, le score et les statistiques de la table.
Les visites sont réparties entre **principal** et **quiétude** : leur somme doit
égaler le total, ce que la commande vérifie à chaque appel. La racine de chaque
itération et les retours de table comptent en recherche principale. Une feuille
transférée à la quiétude compte uniquement en quiétude, ainsi que chaque position
évaluée après une évasion supplémentaire à sa limite. Les vérifications internes
de légalité ne sont pas des visites de recherche.
Le compteur de **relances PVS** indique combien de recherches en fenêtre large
ont débuté après un test en fenêtre étroite. Les visites de ces recherches
sont déjà incluses dans les compteurs de nœuds.
Les **coupures avant génération** comptent les nœuds où la recherche du coup
légal de table suffit pour couper avant de générer la liste complète. Les
retours directs de la table sont comptés séparément.
En mode budgété, le **temps de dernière itération validée** est le temps cumulé
depuis le démarrage du chronomètre interne jusqu'à la validation de l'itération.
Il vaut zéro si aucune itération n'a été validée.

En mode 500 ms, la profondeur, le coup et le score proviennent de la dernière
itération entièrement terminée. À profondeur 0, le moteur renvoie son coup et son
score de secours. **budget interrompu=True** indique une expiration du budget
avant validation d'une itération (False si le plafond est atteint ou la racine
terminale). Les compteurs incluent toutes les itérations, y compris le travail
interrompu. Les résultats peuvent donc varier entre répétitions : aucun avertissement
de déterminisme n'est émis dans ce mode. Le budget ne couvre pas le parsing FEN
ni toute la préparation : le temps réel de l'appel peut dépasser 500 ms.
La synthèse finale contient la médiane de durée pour chaque position, avec les
statistiques du même échantillon et le FEN. Conserver ce log avec la révision Git
avant une modification, puis comparer au prochain lancement.

`appel` mesure l'appel complet à **ChoisirCoupDepuisFen** (mode fixe) ou au parsing
FEN suivi de **ChercherAvecBudget** (mode 500 ms) avec une horloge haute
résolution : parsing FEN, préparation et recherche. `N/s` = `NoeudsVisites` divisé
par cette durée en secondes. `Minimax` est le temps interne entier en millisecondes
déjà fourni par le moteur ; son périmètre est plus étroit. Les deux temps peuvent
donc différer. Les nœuds résolus par la table restent compris dans les visites.

Comparer sur la même machine, la même version de Unity et le même état du Profiler.
Fermer le mode Play et les autres recherches IA. Pour une référence de temps,
désactiver l'enregistrement Profiler et **Deep Profile**. Le premier échauffement
réduit les effets d'initialisation ; il ne supprime pas la variabilité liée au GC,
au système ou aux autres tâches. Le benchmark ne force pas de collecte GC et ne
change aucun réglage global du Profiler.

## Allocations dans le Profiler

1. Ouvrir **Window > Analysis > Profiler**, sélectionner la cible **Editor**
   (`Profile Editor` selon la présentation de la fenêtre), activer **CPU Usage**
   et l'enregistrement. Laisser **Deep Profile désactivé**.
2. Lancer **Halloween Chess > Benchmark IA** hors mode Play.
   Ou **Halloween Chess > Benchmark IA - 500 ms** pour le mode budgété ; les
   marqueurs sont identiques, le mode est indiqué dans la console.
3. Sélectionner une frame contenant une recherche et ouvrir la vue **Hierarchy**.
   Filtrer sur **HalloweenChess.IA.Benchmark.Recherche** et consulter la colonne
   **GC Alloc** : elle indique les allocations effectuées sous ce marqueur,
   y compris celles de ses enfants.
4. Les marqueurs enfants **Ouverture**, **MilieuDeJeu**, **Tactique** et **Finale**
   identifient le FEN. **HalloweenChess.IA.Benchmark.Echauffement** est séparé des
   mesures : ne pas l'inclure dans la comparaison.

Le marqueur couvre le même appel FEN + IA que le temps `appel`, sans les logs,
la barre de progression ni la création/destruction du composant. Ne pas additionner
son `GC Alloc` et celui de ses marqueurs enfants : ce serait compter deux fois.
`GC Alloc` représente des octets alloués pendant l'appel, pas la mémoire encore
occupée par la table après la recherche. Conserver les captures avant/après pour
comparer les allocations sur le même FEN. L'instrumentation du Profiler peut
modifier les temps : ne pas comparer un temps profilé à un temps non profilé.
