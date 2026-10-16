using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RuinsColumnPlacement
{
    [MenuItem("Tools/Stage/Stand And Place Broken Column")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        PlayerVisualAuthoring.Require(scene.path == PlayerVisualAuthoring.ScenePath && !EditorApplication.isPlayingOrWillChangePlaymode,
            "Open RuinsStageRefined outside Play mode.");
        var architecture = GameObject.Find("Ruins Stage").transform.Find("02 Ruined colonnade and northern arch");
        var columns = architecture.Cast<Transform>().Where(t => t.name == "Broken fluted column" || t.name == "Fluted column").ToArray();
        var tilted = columns.Where(t => t.name == "Broken fluted column" && Vector3.Angle(t.up, Vector3.up) > 30).ToArray();
        PlayerVisualAuthoring.Require(tilted.Length == 1, "Expected exactly one fallen broken column.");
        var column = tilted[0];
        var neighbors = columns.Where(t => t.name == "Fluted column" && t.localPosition.x > 0).OrderBy(t => t.localPosition.z).Take(2).ToArray();
        PlayerVisualAuthoring.Require(neighbors.Length == 2, "Right colonnade neighbors missing.");
        var others = columns.Where(t => t != column).ToDictionary(t => t, t => EditorJsonUtility.ToJson(t));
        Vector3 previousPosition = column.position;
        Vector3 previousRotation = column.eulerAngles;
        Undo.RecordObject(column, "Stand and place broken column");
        column.localRotation = Quaternion.identity;
        column.localPosition = (neighbors[0].localPosition + neighbors[1].localPosition) * .5f;
        var baseRenderer = column.Find("Lower plinth").GetComponent<Renderer>();
        float ground = neighbors.Average(t => t.Find("Lower plinth").GetComponent<Renderer>().bounds.min.y);
        column.position += Vector3.up * (ground - baseRenderer.bounds.min.y);
        PlayerVisualAuthoring.Require(others.All(pair => EditorJsonUtility.ToJson(pair.Key) == pair.Value), "Another column changed.");
        float gap0 = Vector2.Distance(new Vector2(column.position.x, column.position.z), new Vector2(neighbors[0].position.x, neighbors[0].position.z));
        float gap1 = Vector2.Distance(new Vector2(column.position.x, column.position.z), new Vector2(neighbors[1].position.x, neighbors[1].position.z));
        PlayerVisualAuthoring.Require(gap0 > 1.5f && Mathf.Abs(gap0 - gap1) < .01f, "Invalid spacing.");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("StageReview/column-placement.txt", $"Saved {scene.path}\nBefore: {previousPosition:F3}, rotation={previousRotation:F3}\nAfter: {column.position:F3}, rotation={column.eulerAngles:F3}, local={column.localPosition:F3}\nNeighbor spacing: {gap0:F3}m / {gap1:F3}m\nPASS: upright, base aligned to adjacent plinths, equal spacing; other columns preserved.\n");
    }
}
