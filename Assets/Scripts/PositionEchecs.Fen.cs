using System;
using System.Globalization;
using System.Text;
using UnityEngine;

public sealed partial class PositionEchecs
{
    public const string FenInitial = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    // FEN utilise les lettres anglaises, majuscules pour les blancs.
    private const string CodesFen = "PNBRQKpnbrqk";
    private const string CodesInternes = "pcftdrPCFTDR";

    public string ExporterFen()
    {
        var fen = new StringBuilder();
        for (int z = 7; z >= 0; z--)
        {
            int vides = 0;
            for (int x = 0; x < 8; x++)
            {
                char piece = plateau[x, z];
                if (piece == '.') { vides++; continue; }
                if (vides > 0) { fen.Append(vides); vides = 0; }
                int index = CodesInternes.IndexOf(piece);
                if (index < 0) throw new InvalidOperationException("Pièce logique inconnue.");
                fen.Append(CodesFen[index]);
            }
            if (vides > 0) fen.Append(vides);
            if (z > 0) fen.Append('/');
        }
        fen.Append(TourBlanc ? " w " : " b ");
        string droits = "";
        if (DroitRoque(true, true)) droits += "K";
        if (DroitRoque(true, false)) droits += "Q";
        if (DroitRoque(false, true)) droits += "k";
        if (DroitRoque(false, false)) droits += "q";
        fen.Append(droits.Length == 0 ? "-" : droits).Append(' ');
        if (CaseEnPassant.x < 0) fen.Append('-');
        else fen.Append((char)('a' + CaseEnPassant.x)).Append((char)('1' + CaseEnPassant.y));
        fen.Append(' ').Append(DemiCoupsSansPriseNiPion.ToString(CultureInfo.InvariantCulture));
        fen.Append(' ').Append(NumeroCoup.ToString(CultureInfo.InvariantCulture));
        return fen.ToString();
    }

    private bool DroitRoque(bool blanc, bool petit)
    {
        int z = blanc ? 0 : 7;
        int x = petit ? 7 : 0;
        return plateau[4, z] == (blanc ? 'r' : 'R') && !dejaBouge[4, z] &&
            plateau[x, z] == (blanc ? 't' : 'T') && !dejaBouge[x, z];
    }

    // Validation du format et des états spéciaux ; ne prouve pas la légalité historique.
    public static PositionEchecs DepuisFen(string fen)
    {
        if (string.IsNullOrWhiteSpace(fen)) throw new FormatException("FEN vide.");
        string[] champs = fen.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (champs.Length != 6) throw new FormatException("Un FEN doit contenir six champs.");
        string[] rangees = champs[0].Split('/');
        if (rangees.Length != 8) throw new FormatException("Le FEN doit décrire huit rangées.");
        var p = new PositionEchecs(false);
        int roisBlancs = 0, roisNoirs = 0;
        for (int i = 0; i < 8; i++)
        {
            int x = 0;
            foreach (char code in rangees[i])
            {
                if (code >= '1' && code <= '8') x += code - '0';
                else
                {
                    int index = CodesFen.IndexOf(code);
                    if (index < 0 || x >= 8) throw new FormatException("Pièce ou rangée FEN invalide.");
                    if ((code == 'P' || code == 'p') && (i == 0 || i == 7))
                        throw new FormatException("Un pion ne peut pas rester sur la première ou dernière rangée.");
                    p.SetPiece(x++, 7 - i, CodesInternes[index]);
                    if (code == 'K') roisBlancs++;
                    if (code == 'k') roisNoirs++;
                }
                if (x > 8) throw new FormatException("Une rangée FEN dépasse huit cases.");
            }
            if (x != 8) throw new FormatException("Chaque rangée doit décrire huit cases.");
        }
        if (roisBlancs != 1 || roisNoirs != 1) throw new FormatException("Un roi par couleur est requis.");
        if (champs[1] != "w" && champs[1] != "b") throw new FormatException("Trait FEN attendu : w ou b.");
        p.TourBlanc = champs[1] == "w";
        if (champs[2] != "-")
        {
            string vus = "";
            foreach (char droit in champs[2])
            {
                if ("KQkq".IndexOf(droit) < 0 || vus.IndexOf(droit) >= 0)
                    throw new FormatException("Droits de roque invalides.");
                vus += droit;
                bool blanc = char.IsUpper(droit);
                int z = blanc ? 0 : 7;
                int x = char.ToUpperInvariant(droit) == 'K' ? 7 : 0;
                if (p.plateau[4, z] != (blanc ? 'r' : 'R') || p.plateau[x, z] != (blanc ? 't' : 'T'))
                    throw new FormatException("Droit de roque sans roi ou tour sur sa case initiale.");
                p.dejaBouge[4, z] = false;
                p.dejaBouge[x, z] = false;
            }
        }
        if (champs[3] != "-")
        {
            string ep = champs[3];
            if (ep.Length != 2 || ep[0] < 'a' || ep[0] > 'h' || ep[1] != (p.TourBlanc ? '6' : '3'))
                throw new FormatException("Case de prise en passant invalide.");
            int x = ep[0] - 'a', z = ep[1] - '1';
            int direction = p.TourBlanc ? -1 : 1;
            if (!p.EstCaseVide(x, z) || p.GetPiece(x, z + direction) != (p.TourBlanc ? 'P' : 'p') ||
                !p.EstCaseVide(x, z - direction))
                throw new FormatException("Prise en passant incohérente avec le dernier double pas.");
            p.CaseEnPassant = new Vector2Int(x, z);
        }
        if (!int.TryParse(champs[4], NumberStyles.None, CultureInfo.InvariantCulture, out int demiCoups) ||
            !int.TryParse(champs[5], NumberStyles.None, CultureInfo.InvariantCulture, out int numero) || numero < 1)
            throw new FormatException("Compteurs FEN invalides.");
        if (p.CaseEnPassant.x >= 0 && demiCoups != 0)
            throw new FormatException("Le compteur doit être nul après un double pas de pion.");
        p.DemiCoupsSansPriseNiPion = demiCoups;
        p.NumeroCoup = numero;
        return p;
    }
}
