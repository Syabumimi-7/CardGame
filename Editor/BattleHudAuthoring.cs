using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static partial class BattleHudAuthoring
{
    const string Request = "Temp/BattleHud.request";
    const string Report = "Temp/BattleHud-report.txt";
    public const string ScenePath = "Assets/Scenes/RuinsStageRefined.unity";
    static BattleHudAuthoring() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string command = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        try
        {
            if (command == "inspect") Inspect();
            else Dispatch(command);
        }
        catch (Exception ex) { File.WriteAllText(Report, ex.ToString()); Debug.LogException(ex); }
    }
    static partial void Dispatch(string command);
    static void Inspect()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Scene: " + SceneManager.GetActiveScene().path + " dirty=" + SceneManager.GetActiveScene().isDirty);
        var game = Object.FindObjectOfType<GameManager>(true);
        if (game != null)
        {
            sb.AppendLine("Game: " + game.name + " battle root=" + game.battleUIRoot.name);
            foreach (var t in game.battleUIRoot.GetComponentsInChildren<Transform>(true))
            {
                var rect = t as RectTransform;
                sb.AppendLine(t.name + " parent=" + t.parent.name + " active=" + t.gameObject.activeSelf + " " +
                    (rect != null ? rect.anchoredPosition + " " + rect.sizeDelta : "") + " " +
                    string.Join(",", t.GetComponents<Component>().Select(c => c == null ? "MISSING" : c.GetType().Name)));
            }
            sb.AppendLine("Font: " + AssetDatabase.GetAssetPath(game.battleManager.turnText.font));
        }
        File.WriteAllText(Report, sb.ToString());
    }
}
