internal struct EvaluationIncrementale
{
    private static readonly Evaluation.ContributionCase[] Contributions = ConstruireContributions();
    private int phase, score, roisMilieu, roisFinale;

    internal int MaterielPhase => phase;
    internal bool EstFinale => phase <= Evaluation.SeuilFinale;

    internal static EvaluationIncrementale DepuisPosition(PositionEchecs position)
    {
        var termes = new EvaluationIncrementale();
        for (int x = 0; x < 8; x++)
            for (int z = 0; z < 8; z++)
            {
                char piece = position.GetPiece(x, z);
                if (piece >= 128) continue;
                var contribution = Contributions[(piece << 6) + z * 8 + x];
                termes.phase += contribution.Phase;
                termes.score += contribution.Score;
                termes.roisMilieu += contribution.RoiMilieu;
                termes.roisFinale += contribution.RoiFinale;
            }
        return termes;
    }

    internal void Appliquer(PositionEchecs position, PositionEchecs.EtatAvantCoup etat)
    {
        Remplacer(etat.Depart, '.');
        Remplacer(etat.Arrivee, position.GetPiece(etat.Arrivee.X, etat.Arrivee.Z));
        // Une case supplémentaire inutilisée est une copie du départ.
        if (etat.Extra1.X != etat.Depart.X || etat.Extra1.Z != etat.Depart.Z)
            Remplacer(etat.Extra1, position.GetPiece(etat.Extra1.X, etat.Extra1.Z));
        if (etat.Extra2.X != etat.Depart.X || etat.Extra2.Z != etat.Depart.Z)
            Remplacer(etat.Extra2, position.GetPiece(etat.Extra2.X, etat.Extra2.Z));
    }

    private void Remplacer(PositionEchecs.CaseSauvee avant, char apres)
    {
        int index = avant.Z * 8 + avant.X;
        ref readonly Evaluation.ContributionCase sortie = ref Contributions[(avant.Piece << 6) + index];
        ref readonly Evaluation.ContributionCase entree = ref Contributions[(apres << 6) + index];
        phase += entree.Phase - sortie.Phase;
        score += entree.Score - sortie.Score;
        roisMilieu += entree.RoiMilieu - sortie.RoiMilieu;
        roisFinale += entree.RoiFinale - sortie.RoiFinale;
    }

    internal int Evaluer(PositionEchecs position, bool pourLesBlancs)
    {
        int bilan = score + (EstFinale ? roisFinale : roisMilieu) + Evaluation.EvaluerPionsEtFous(position);
        return pourLesBlancs ? bilan : -bilan;
    }

    private static Evaluation.ContributionCase[] ConstruireContributions()
    {
        var table = new Evaluation.ContributionCase[128 * 64];
        foreach (char piece in "pcftdrPCFTDR")
        {
            int valeur = Evaluation.ValeurPiece(piece);
            char type = char.ToUpperInvariant(piece);
            bool blanc = char.IsLower(piece);
            for (int x = 0; x < 8; x++)
                for (int z = 0; z < 8; z++)
                    table[(piece << 6) + z * 8 + x] = Evaluation.CalculerContribution(type, valeur, blanc, x, z);
        }
        return table;
    }
}
