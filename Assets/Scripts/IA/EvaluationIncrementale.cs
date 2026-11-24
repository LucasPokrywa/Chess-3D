internal struct EvaluationIncrementale
{
    private static readonly Evaluation.ContributionCase[] Contributions = ConstruireContributions();
    private int phase, score, roisMilieu, roisFinale;

    internal int MaterielPhase => phase;
    internal bool EstFinale => phase <= Evaluation.SeuilFinale;

    /// <summary>
    /// Reconstruit les quatre sommes signées ou non signées à partir des contributions précalculées. Cette initialisation s’effectue avant la recherche.
    /// </summary>
    /// <param name="position">Position logique correspondant à ce calcul.</param>
    /// <returns>État valeur des sommes qui correspond au plateau fourni.</returns>
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

    /// <summary>
    /// Met les sommes en accord avec un coup déjà appliqué au plateau. Départ, arrivée et cases supplémentaires couvrent aussi le roque, la promotion et la prise en passant.
    /// </summary>
    /// <param name="position">Position logique correspondant à ce calcul.</param>
    /// <param name="etat">Cases et contenus antérieurs sauvegardés avant le coup.</param>
    internal void Appliquer(PositionEchecs position, PositionEchecs.EtatAvantCoup etat)
    {
        // La position contient déjà le nouvel état. L’instantané fournit les seules contributions à retirer.
        Remplacer(etat.Depart, '.');
        Remplacer(etat.Arrivee, position.GetPiece(etat.Arrivee.X, etat.Arrivee.Z));
        // Une case supplémentaire inutilisée est une copie du départ.
        if (etat.Extra1.X != etat.Depart.X || etat.Extra1.Z != etat.Depart.Z)
            Remplacer(etat.Extra1, position.GetPiece(etat.Extra1.X, etat.Extra1.Z));
        if (etat.Extra2.X != etat.Depart.X || etat.Extra2.Z != etat.Depart.Z)
            Remplacer(etat.Extra2, position.GetPiece(etat.Extra2.X, etat.Extra2.Z));
    }

    /// <summary>
    /// Retranche la contribution enregistrée pour la case et ajoute celle de son nouveau contenu. Les quatre sommes évoluent ensemble.
    /// </summary>
    /// <param name="avant">Instantané de la case avant le changement.</param>
    /// <param name="apres">Code de pièce présent sur la case après le changement.</param>
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

    /// <summary>
    /// Sélectionne le bonus de roi selon le matériel restant et recalcule les structures qui dépendent de plusieurs pièces.
    /// </summary>
    /// <param name="position">Position logique correspondant à ce calcul.</param>
    /// <param name="pourLesBlancs">Vrai pour orienter le score du côté blanc, faux pour le côté noir.</param>
    /// <returns>Score statique du camp demandé en centipions.</returns>
    internal int Evaluer(PositionEchecs position, bool pourLesBlancs)
    {
        int bilan = score + (EstFinale ? roisFinale : roisMilieu) + Evaluation.EvaluerPionsEtFous(position);
        return pourLesBlancs ? bilan : -bilan;
    }

    /// <summary>
    /// Construit un tableau compact pour les codes français du plateau. Les cases correspondant au vide conservent leur contribution nulle.
    /// </summary>
    /// <returns>Contributions indexées par code de pièce et indice de case.</returns>
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
