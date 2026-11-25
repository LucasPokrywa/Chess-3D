using System;

public sealed partial class PositionEchecs
{
    // La grille reste la source de vérité. Seule une position de travail du moteur D
    // demande ce cache, qui suit ensuite le jeu et l'annulation des coups.
    private MasquesAttaque masquesAttaque;

    internal MasquesAttaque MasquesActifs => masquesAttaque;

    internal void ConfigurerMasquesAttaque(bool actifs)
    {
        if (!actifs)
        {
            masquesAttaque = null;
            return;
        }

        if (masquesAttaque != null) return;
        masquesAttaque = new MasquesAttaque();
        for (int z = 0; z < 8; z++)
            for (int x = 0; x < 8; x++)
                masquesAttaque.Changer(x, z, '.', plateau[x, z]);
    }
}

internal sealed class MasquesAttaque
{
    // Six types par couleur. L'indice zéro est réservé aux codes sans masque.
    private const string Alphabet = "pcftdrPCFTDR";
    private const int Pion = 1, Cavalier = 2, Fou = 3, Tour = 4, Dame = 5, Roi = 6;
    private readonly ulong[] pieces = new ulong[Alphabet.Length + 1];
    private ulong occupation;

    private static readonly byte[] Codes = FabriquerCodes();
    private static readonly ulong[][] Rayons = FabriquerRayons();
    private static readonly ulong[] Cavaliers = FabriquerSauts(new[]
        { (1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1), (-1, 2) });
    private static readonly ulong[] Rois = FabriquerSauts(new[]
        { (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1) });
    // Ces masques remontent de la case attaquée vers les cases de départ des pions.
    private static readonly ulong[] PionsBlancs = FabriquerSauts(new[] { (1, -1), (-1, -1) });
    private static readonly ulong[] PionsNoirs = FabriquerSauts(new[] { (1, 1), (-1, 1) });

    internal void Changer(int x, int z, char avant, char apres)
    {
        if (avant == apres) return;
        ulong bit = 1UL << (z * 8 + x);
        int ancienCode = Code(avant), nouveauCode = Code(apres);
        if (ancienCode != 0) pieces[ancienCode] &= ~bit;
        if (nouveauCode != 0) pieces[nouveauCode] |= bit;
        if (apres != '.') occupation |= bit;
        else occupation &= ~bit;
    }

    // La simulation courte ne déplace ni roi, ni tour de roque, ni victime en passant.
    // Le même XOR restaure les masques de la pièce et de sa victime à l'annulation.
    internal void DeplacerCourt(int x, int z, int tx, int tz, char piece, char capture, bool annuler)
    {
        ulong depart = 1UL << (z * 8 + x), arrivee = 1UL << (tz * 8 + tx);
        int codePiece = Code(piece), codeCapture = Code(capture);
        if (codePiece != 0) pieces[codePiece] ^= depart | arrivee;
        if (codeCapture != 0) pieces[codeCapture] ^= arrivee;

        if (annuler)
        {
            occupation |= depart;
            if (capture == '.') occupation &= ~arrivee;
        }
        else
        {
            occupation = (occupation & ~depart) | arrivee;
        }
    }

    internal MasquesAttaque Copier()
    {
        var copie = new MasquesAttaque { occupation = occupation };
        Array.Copy(pieces, copie.pieces, pieces.Length);
        return copie;
    }

    internal bool EstAttaque(int x, int z, bool blancs)
    {
        int index = z * 8 + x;
        int couleur = blancs ? 0 : 6;
        if (((blancs ? PionsBlancs[index] : PionsNoirs[index]) & pieces[couleur + Pion]) != 0 ||
            (Cavaliers[index] & pieces[couleur + Cavalier]) != 0 ||
            (Rois[index] & pieces[couleur + Roi]) != 0)
        {
            return true;
        }

        ulong orthogonaux = pieces[couleur + Tour] | pieces[couleur + Dame];
        ulong diagonaux = pieces[couleur + Fou] | pieces[couleur + Dame];
        ulong[] rayons = Rayons[index];
        return SurRayon(rayons[0], orthogonaux, croissant: true) ||
            SurRayon(rayons[1], orthogonaux, croissant: false) ||
            SurRayon(rayons[2], orthogonaux, croissant: true) ||
            SurRayon(rayons[3], orthogonaux, croissant: false) ||
            SurRayon(rayons[4], diagonaux, croissant: true) ||
            SurRayon(rayons[5], diagonaux, croissant: true) ||
            SurRayon(rayons[6], diagonaux, croissant: false) ||
            SurRayon(rayons[7], diagonaux, croissant: false);
    }

    private bool SurRayon(ulong rayon, ulong attaquants, bool croissant)
    {
        if ((rayon & attaquants) == 0) return false;
        ulong obstacles = rayon & occupation;

        // Le premier obstacle est le bit le plus bas ou le plus haut, selon la direction.
        if (croissant) return (obstacles & unchecked(0UL - obstacles) & attaquants) != 0;
        obstacles |= obstacles >> 1;
        obstacles |= obstacles >> 2;
        obstacles |= obstacles >> 4;
        obstacles |= obstacles >> 8;
        obstacles |= obstacles >> 16;
        obstacles |= obstacles >> 32;
        return ((obstacles - (obstacles >> 1)) & attaquants) != 0;
    }

    private static int Code(char c) => c < Codes.Length ? Codes[c] : 0;

    private static byte[] FabriquerCodes()
    {
        var codes = new byte[128];
        for (int i = 0; i < Alphabet.Length; i++)
            codes[Alphabet[i]] = (byte)(i + 1);
        return codes;
    }

    private static ulong[] FabriquerSauts((int dx, int dz)[] directions)
    {
        var masques = new ulong[64];
        for (int i = 0; i < 64; i++)
            foreach (var (dx, dz) in directions)
            {
                int x = (i & 7) + dx, z = (i >> 3) + dz;
                if ((uint)x < 8 && (uint)z < 8)
                    masques[i] |= 1UL << (z * 8 + x);
            }
        return masques;
    }

    private static ulong[][] FabriquerRayons()
    {
        var rayons = new ulong[64][];
        var directions = new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1) };
        for (int i = 0; i < 64; i++)
        {
            rayons[i] = new ulong[directions.Length];
            for (int d = 0; d < directions.Length; d++)
            {
                var (dx, dz) = directions[d];
                int x = (i & 7) + dx, z = (i >> 3) + dz;
                while ((uint)x < 8 && (uint)z < 8)
                {
                    rayons[i][d] |= 1UL << (z * 8 + x);
                    x += dx;
                    z += dz;
                }
            }
        }
        return rayons;
    }
}
