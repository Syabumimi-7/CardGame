using System;
using System.IO;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CardSurfaceRedrawVerification
{
    const string Request = "Temp/CardSurfaceRedrawVerification.request";
    const string Folder = "StageReview/WebGLPerformance/Fix1/";
    static CardSurfaceRedrawVerification() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
        try
        {
            string phase = File.ReadAllText(Request).Trim(); File.Delete(Request);
            if (phase != "before" && phase != "after") throw new ArgumentException(phase);
            Run(phase);
        }
        catch (Exception e) { File.WriteAllText(Folder + "error.txt", e.ToString()); Debug.LogException(e); }
    }
    static TextMeshProUGUI Text(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        return go.AddComponent<TextMeshProUGUI>();
    }
    static void Run(string phase)
    {
        var original = SceneManager.GetActiveScene(); bool dirty = original.isDirty;
        var preview = EditorSceneManager.NewPreviewScene();
        CardData data = null;
        var output = new StringBuilder(); int redraws = 0, checks = 0;
        try
        {
            var root = new GameObject("Card surface verification", typeof(RectTransform));
            root.SetActive(false); SceneManager.MoveGameObjectToScene(root, preview);
            ((RectTransform)root.transform).sizeDelta = new Vector2(225,337.5f);
            var view = root.AddComponent<CardUIView>();
            view.cardNameText = Text(root.transform,"Name"); view.costText = Text(root.transform,"Cost");
            view.descriptionText = Text(root.transform,"Description");
            data = ScriptableObject.CreateInstance<CardData>();
            data.cardName = "Verification"; data.description = "Description";
            data.lowPerformance = new CardPerformance { cost = 3 };
            var card = new CardInstance(data); view.Setup(card);
            root.SetActive(true);
            var design = root.GetComponent<CardDesignLayout>();
            var surface = design.Surface;
            view.Refresh();
            UnityAction count = () => redraws++;
            surface.RegisterDirtyVerticesCallback(count);
            for (int i=0; i<100; i++) view.Refresh();
            surface.UnregisterDirtyVerticesCallback(count);

            var emphasis = typeof(CardDesignLayout).GetField("emphasis", BindingFlags.Instance|BindingFlags.NonPublic);
            var populate = typeof(CardDesignGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);
            foreach (CardRarity rarity in Enum.GetValues(typeof(CardRarity)))
            foreach (float highlight in new[] {0f,.5f,1f,0f})
            {
                bool needsRedraw = surface.rarity != rarity || !surface.highlight.Equals(highlight);
                int changedRedraws = 0;
                UnityAction changed = () => changedRedraws++;
                surface.RegisterDirtyVerticesCallback(changed);
                data.rarity = rarity; emphasis.SetValue(design, highlight);
                view.Refresh();
                surface.UnregisterDirtyVerticesCallback(changed);
                if (needsRedraw && changedRedraws == 0) throw new InvalidOperationException("Changed rarity/highlight did not request a redraw");
                output.AppendLine($"{rarity}/{highlight}; {view.cardNameText.text}; {view.costText.text}; {view.descriptionText.text}");
                foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
                    output.AppendLine($"{rect.name}; sibling={rect.GetSiblingIndex()}; pos={JsonUtility.ToJson(rect.anchoredPosition)}; size={JsonUtility.ToJson(rect.sizeDelta)}; scale={JsonUtility.ToJson(rect.localScale)}");
                using (var vh = new VertexHelper())
                {
                    populate.Invoke(surface,new object[]{vh});
                    var vertices = new System.Collections.Generic.List<UIVertex>(); vh.GetUIVertexStream(vertices);
                    foreach (var vertex in vertices) output.AppendLine(JsonUtility.ToJson(vertex));
                }
                checks++;
            }
            // Cost/content updates still run independently of the frame redraw decision.
            card = CardInstance.CreateCollectionPreview(data,0); view.Setup(card);
            for (int reduction=0; reduction<3; reduction++)
            {
                card.CostReduction=reduction; data.description="Changed " + reduction;
                view.Refresh(); output.AppendLine($"cost={view.costText.text}; description={view.descriptionText.text}"); checks++;
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            if(data!=null) Object.DestroyImmediate(data);
        }
        if(SceneManager.GetActiveScene()!=original || original.isDirty!=dirty) throw new InvalidOperationException("Working scene changed");
        string snapshot=output.ToString(); File.WriteAllText(Folder+phase+"-visuals.txt",snapshot);
        if(phase=="after" && snapshot!=File.ReadAllText(Folder+"before-visuals.txt")) throw new InvalidOperationException("Visual output changed");
        if(phase=="after" && redraws!=0) throw new InvalidOperationException("Redundant surface redraw requests: "+redraws);
        File.WriteAllText(Folder+phase+"-result.txt",$"PASS: {checks} visual/content cases; 100 unchanged Refresh calls requested {redraws} surface redraws.\nAll generated surface vertices, layout, rarity/highlight and text snapshots recorded.\nWorking scene unchanged, dirty={dirty}; no Play mode or game build.\n");
    }
}
