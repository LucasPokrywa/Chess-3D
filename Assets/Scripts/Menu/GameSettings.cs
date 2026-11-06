using UnityEngine;
using UnityEngine.SceneManagement;

// Mode choisi dans le menu principal : chaque mode a sa propre scène.
public enum ModeDeJeu
{
    Normal,
    Halloween
}

// Niveau de l'IA. Pas encore branché : l'IA ne joue pas encore ses coups.
public enum DifficulteIA
{
    Facile,
    Normal,
    Difficile
}

// Réglages de partie partagés entre le menu et les scènes de jeu.
// Ils sont sauvegardés (PlayerPrefs) pour être retrouvés au prochain lancement.
public static class GameSettings
{
    public const string SceneMenu = "MainMenu";
    public const string SceneClassique = "ChessGame_Classique";
    public const string SceneHalloween = "ChessGame_Halloween";

    // Passer à true quand l'IA jouera : le menu déverrouille alors le choix de difficulté.
    public static readonly bool DifficulteDisponible = false;

    private const string CleMode = "Chess3D.Mode";
    private const string CleDifficulte = "Chess3D.DifficulteIA";

    public static ModeDeJeu Mode
    {
        get => LireEnum(CleMode, ModeDeJeu.Normal);
        set => Ecrire(CleMode, (int)value);
    }

    public static DifficulteIA Difficulte
    {
        get => LireEnum(CleDifficulte, DifficulteIA.Normal);
        set => Ecrire(CleDifficulte, (int)value);
    }

    public static string SceneDuMode(ModeDeJeu mode)
    {
        return mode == ModeDeJeu.Halloween ? SceneHalloween : SceneClassique;
    }

    // Profondeur de recherche prévue pour chaque niveau.
    // À appliquer sur ChessIA.Profondeur quand l'IA jouera ses coups.
    public static int ProfondeurIA(DifficulteIA difficulte)
    {
        switch (difficulte)
        {
            case DifficulteIA.Facile: return 2;
            case DifficulteIA.Difficile: return Mathf.Min(6, Minimax.ProfondeurMax);
            default: return 4;
        }
    }

    public static bool SceneDisponible(string scene) => Application.CanStreamedLevelBeLoaded(scene);

    public static void RetourMenu()
    {
        if (SceneDisponible(SceneMenu))
            SceneManager.LoadScene(SceneMenu);
        else
            Debug.LogWarning($"La scène '{SceneMenu}' n'est pas dans les Build Settings.");
    }

    private static T LireEnum<T>(string cle, T defaut) where T : System.Enum
    {
        int valeur = PlayerPrefs.GetInt(cle, System.Convert.ToInt32(defaut));
        return System.Enum.IsDefined(typeof(T), valeur) ? (T)System.Enum.ToObject(typeof(T), valeur) : defaut;
    }

    private static void Ecrire(string cle, int valeur)
    {
        PlayerPrefs.SetInt(cle, valeur);
        PlayerPrefs.Save();
    }
}
