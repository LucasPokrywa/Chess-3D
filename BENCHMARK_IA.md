# Benchmark IA

Hors mode Play, lancer **Halloween Chess > Benchmark IA** dans Unity.
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
La synthèse finale contient la médiane de durée pour chaque position, avec les
statistiques du même échantillon et le FEN. Conserver ce log avec la révision Git
avant une modification, puis comparer au prochain lancement.

`appel` mesure l'appel complet à **ChoisirCoupDepuisFen** avec une horloge haute
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
