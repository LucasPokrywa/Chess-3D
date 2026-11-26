using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

internal enum ResultatPartie
{
    EnCours,
    Blancs,
    Noirs,
    Nulle
}

internal sealed class PartiePgn
{
    private readonly List<string> coups = new List<string>();
    private string fenDepart;
    private string date;
    private bool blancAuDepart;
    private int premierNumero;

    internal void Initialiser(PositionEchecs position)
    {
        coups.Clear();
        fenDepart = position.ExporterFen();
        date = DateTime.Today.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture);
        blancAuDepart = position.TourBlanc;
        premierNumero = position.NumeroCoup;
    }

    internal static string NotationAvantCoup(PositionEchecs position, Vector2Int depart, Vector2Int arrivee)
    {
        char code = position.GetPiece(depart.x, depart.y);
        char piece = char.ToUpperInvariant(code);
        if (piece == 'R' && Mathf.Abs(arrivee.x - depart.x) == 2)
            return arrivee.x > depart.x ? "O-O" : "O-O-O";

        var notation = new StringBuilder();
        bool pion = piece == 'P';
        // Pour un pion, un déplacement de colonne est une capture, y compris en passant.
        bool capture = pion ? depart.x != arrivee.x : position.GetPiece(arrivee.x, arrivee.y) != '.';
        if (pion)
        {
            if (capture) notation.Append((char)('a' + depart.x));
        }
        else
        {
            notation.Append(piece switch
            {
                'R' => 'K', 'D' => 'Q', 'T' => 'R', 'F' => 'B', 'C' => 'N',
                _ => throw new ArgumentException("Pièce inconnue pour la notation PGN.", nameof(depart))
            });
            if (piece != 'R') AjouterDesambiguïsation(notation, position, code, depart, arrivee);
        }

        if (capture) notation.Append('x');
        notation.Append((char)('a' + arrivee.x)).Append((char)('1' + arrivee.y));
        if (pion && (arrivee.y == 0 || arrivee.y == 7)) notation.Append("=Q");
        return notation.ToString();
    }

    private static void AjouterDesambiguïsation(StringBuilder notation, PositionEchecs position, char code,
        Vector2Int depart, Vector2Int arrivee)
    {
        bool ambigu = false;
        bool memeColonne = false;
        bool memeRangee = false;
        var regles = new ReglesEchecs(position);
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                var autre = new Vector2Int(x, y);
                if (autre == depart || position.GetPiece(x, y) != code || !regles.EstCoupLegalAuTrait(autre, arrivee))
                    continue;

                ambigu = true;
                memeColonne |= x == depart.x;
                memeRangee |= y == depart.y;
            }

        if (!ambigu) return;
        if (!memeColonne || memeRangee) notation.Append((char)('a' + depart.x));
        if (memeColonne) notation.Append((char)('1' + depart.y));
    }

    internal void AjouterCoup(string notation, bool echec, bool aUnCoupLegal)
    {
        coups.Add(echec ? notation + (aUnCoupLegal ? "+" : "#") : notation);
    }

    internal string Exporter(ResultatPartie resultat, bool joueurBlanc)
    {
        string jetonResultat = resultat switch
        {
            ResultatPartie.Blancs => "1-0",
            ResultatPartie.Noirs => "0-1",
            ResultatPartie.Nulle => "1/2-1/2",
            _ => "*"
        };
        var pgn = new StringBuilder();
        pgn.AppendLine("[Event \"HalloweenChess\"]");
        pgn.AppendLine("[Site \"?\"]");
        pgn.Append("[Date \"").Append(date).AppendLine("\"]");
        pgn.AppendLine("[Round \"?\"]");
        pgn.Append("[White \"").Append(joueurBlanc ? "Joueur" : "IA").AppendLine("\"]");
        pgn.Append("[Black \"").Append(joueurBlanc ? "IA" : "Joueur").AppendLine("\"]");
        pgn.Append("[Result \"").Append(jetonResultat).AppendLine("\"]");
        if (fenDepart != PositionEchecs.FenInitial)
        {
            pgn.AppendLine("[SetUp \"1\"]");
            pgn.Append("[FEN \"").Append(fenDepart).AppendLine("\"]");
        }
        pgn.AppendLine();

        bool blanc = blancAuDepart;
        long numero = premierNumero;
        int longueurLigne = 0;
        for (int i = 0; i < coups.Count; i++)
        {
            if (blanc || i == 0)
                AjouterJeton(pgn, numero.ToString(CultureInfo.InvariantCulture) + (blanc ? "." : "..."), ref longueurLigne);
            AjouterJeton(pgn, coups[i], ref longueurLigne);
            if (!blanc) numero++;
            blanc = !blanc;
        }
        AjouterJeton(pgn, jetonResultat, ref longueurLigne);
        pgn.AppendLine().AppendLine();
        return pgn.ToString();
    }

    private static void AjouterJeton(StringBuilder pgn, string jeton, ref int longueurLigne)
    {
        if (longueurLigne + 1 + jeton.Length >= 80)
        {
            pgn.AppendLine();
            longueurLigne = 0;
        }
        if (longueurLigne > 0)
        {
            pgn.Append(' ');
            longueurLigne++;
        }
        pgn.Append(jeton);
        longueurLigne += jeton.Length;
    }
}
