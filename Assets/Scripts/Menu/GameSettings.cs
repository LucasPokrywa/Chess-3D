using UnityEngine;
using UnityEngine.SceneManagement;

// Mode choisi dans le menu principal : chaque mode a sa propre scène.
public enum ModeDeJeu
{
    Normal,
    Halloween
}

// Réglages de partie partagés entre le menu et les scènes de jeu.
// Ils sont sauvegardés (PlayerPrefs) pour être retrouvés au prochain lancement.
public static class GameSettings
{
    public const string SceneMenu = "MainMenu";
    public const string SceneClassique = "ChessGame_Classique";
    public const string SceneHalloween = "ChessGame_Halloween";

    private const string CleMode = "Chess3D.Mode";
    private const string CleDifficulte = "Chess3D.DifficulteIA";
    private static (string scene, DifficulteIA difficulte)? lancement;

    public static ModeDeJeu Mode
    {
        get => LireEnum(CleMode, ModeDeJeu.Normal);
        set => Ecrire(CleMode, (int)value);
    }

    public static DifficulteIA Difficulte
    {
        get => LireEnum(CleDifficulte, DifficulteIA.Moyen);
        set => Ecrire(CleDifficulte, (int)value);
    }

    public static string SceneDuMode(ModeDeJeu mode)
    {
        return mode == ModeDeJeu.Halloween ? SceneHalloween : SceneClassique;
    }

    internal static void LancerPartie(string scene, DifficulteIA difficulte)
    {
        lancement = (scene, difficulte);
        SceneManager.LoadScene(scene);
    }

    internal static DifficulteIA? PrendreDifficulteDeLancement(string scene)
    {
        if (!lancement.HasValue || lancement.Value.scene != scene) return null;

        DifficulteIA difficulte = lancement.Value.difficulte;
        lancement = null;
        return difficulte;
    }

    public static bool SceneDisponible(string scene) => Application.CanStreamedLevelBeLoaded(scene);

    public static void RetourMenu()
    {
        lancement = null;
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
