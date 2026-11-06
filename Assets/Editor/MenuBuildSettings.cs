using System.Collections.Generic;
using System.Linq;
using UnityEditor;

// Garantit que le menu et les deux scènes de jeu sont dans les Build Settings
// (le menu en premier), sinon SceneManager.LoadScene échoue.
// N'ajoute que les scènes manquantes : l'ordre et les autres scènes sont conservés.
[InitializeOnLoad]
internal static class MenuBuildSettings
{
    private static readonly string[] ScenesRequises =
    {
        "Assets/Scenes/MainMenu.unity",
        "Assets/Scenes/ChessGame_Classique.unity",
        "Assets/Scenes/ChessGame_Halloween.unity"
    };

    static MenuBuildSettings()
    {
        EditorApplication.delayCall += Verifier;
    }

    [MenuItem("Chess 3D/Vérifier les scènes du build")]
    private static void Verifier()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool modifie = false;

        for (int i = 0; i < ScenesRequises.Length; i++)
        {
            string chemin = ScenesRequises[i];
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(chemin) == null)
                continue;

            EditorBuildSettingsScene existante = scenes.FirstOrDefault(s => s.path == chemin);
            if (existante == null)
            {
                // Le menu doit être la première scène chargée au lancement.
                scenes.Insert(i == 0 ? 0 : scenes.Count, new EditorBuildSettingsScene(chemin, true));
                modifie = true;
            }
            else if (!existante.enabled)
            {
                existante.enabled = true;
                modifie = true;
            }
        }

        if (!modifie) return;
        EditorBuildSettings.scenes = scenes.ToArray();
        UnityEngine.Debug.Log("[Chess 3D] Build Settings : menu et scènes de jeu ajoutés.");
    }
}
