using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ChessIA)), CanEditMultipleObjects]
public sealed class ChessIAEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        if (serializedObject.isEditingMultipleObjects)
            EditorGUILayout.HelpBox("Sélectionnez un seul composant pour choisir sa difficulté active.", MessageType.Info);
        else
            AfficherDifficulteActive();

        DrawPropertiesExcluding(serializedObject, "m_Script", "difficulteActiveId");
        serializedObject.ApplyModifiedProperties();
    }

    private void AfficherDifficulteActive()
    {
        SerializedProperty configurations = serializedObject.FindProperty("configurations");
        SerializedProperty active = serializedObject.FindProperty("difficulteActiveId");
        var noms = new GUIContent[configurations.arraySize + 1];
        var identifiants = new string[noms.Length];
        noms[0] = new GUIContent("Personnalisé");
        identifiants[0] = "";
        int indexActif = 0;

        for (int i = 0; i < configurations.arraySize; i++)
        {
            SerializedProperty element = configurations.GetArrayElementAtIndex(i);
            string nom = element.FindPropertyRelative("Nom").stringValue;
            string id = element.FindPropertyRelative("id").stringValue;
            noms[i + 1] = new GUIContent($"{i + 1}. {(string.IsNullOrWhiteSpace(nom) ? "Sans nom" : nom)}");
            identifiants[i + 1] = id;
            if (!string.IsNullOrEmpty(id) && id == active.stringValue) indexActif = i + 1;
        }

        Rect zone = EditorGUILayout.GetControlRect();
        GUIContent libelle = EditorGUI.BeginProperty(zone, new GUIContent("Difficulté active"), active);
        int index = EditorGUI.Popup(zone, libelle, indexActif, noms);
        if (index != indexActif) active.stringValue = identifiants[index];
        EditorGUI.EndProperty();
    }
}
