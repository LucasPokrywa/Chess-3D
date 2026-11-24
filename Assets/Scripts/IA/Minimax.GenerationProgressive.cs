using System.Collections.Generic;
using UnityEngine;

public partial class Minimax
{
    private sealed partial class Recherche
    {
        private enum PhaseCoups
        {
            Tactiques,
            Calmes,
            CapturesDefavorables
        }

        // Chaque enfant utilise ses propres tampons. Les captures faibles sont produites
        // dans la première passe puis gardées en réserve jusqu'à la dernière phase.
        private readonly List<CoupTrie>[] capturesDefavorablesParNiveau;

        private List<CoupTrie> GenererPhase(PositionEchecs position, int distance, PhaseCoups phase)
        {
            if (phase == PhaseCoups.CapturesDefavorables)
                return capturesDefavorablesParNiveau[distance];

            var coups = coupsParNiveau[distance] ??= new List<CoupTrie>(32);
            coups.Clear();
            bool tactiques = phase == PhaseCoups.Tactiques;
            var capturesDefavorables = capturesDefavorablesParNiveau[distance];
            if (tactiques)
            {
                capturesDefavorables ??= capturesDefavorablesParNiveau[distance] = new List<CoupTrie>(12);
                capturesDefavorables.Clear();
            }

            for (int x = 0; x < 8; x++)
                for (int z = 0; z < 8; z++)
                {
                    annulation.ThrowIfCancellationRequested();
                    char code = position.GetPiece(x, z);
                    if (code == '.' || char.IsLower(code) != position.TourBlanc) continue;

                    foreach (Vector2Int arrivee in regles.PseudoCoupsPourPhase(x, z, code, tactiques))
                    {
                        var coup = new Coup(new Vector2Int(x, z), arrivee);
                        int priorite = tactiques ? Priorite(position, coup) :
                            PrioriteRecherche(position, coup, distance, tactique: 0);
                        var candidat = new CoupTrie(coup, priorite, estCalme: !tactiques);
                        if (tactiques && priorite < 0)
                            capturesDefavorables.Add(candidat);
                        else
                            coups.Add(candidat);
                    }
                }

            coups.Sort(ComparaisonCoups);
            if (tactiques) capturesDefavorables.Sort(ComparaisonCoups);
            return coups;
        }
    }
}
