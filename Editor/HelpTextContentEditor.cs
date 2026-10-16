using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HelpTextContent))]
public class HelpTextContentEditor : Editor
{
    private string search = "";

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("ヘルプの文章を編集します。タイトル画面とポーズ画面で共通です。\n{draw}などの部分は削除せず残すと、ゲーム設定の数値が自動反映されます。\n敵のHP・行動内容は敵データから自動生成されます。変更後はヘルプを開き直してください。", MessageType.Info);
        search = EditorGUILayout.TextField("文章を検索", search);
        var entries = serializedObject.FindProperty("texts");
        for (int i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            var key = entry.FindPropertyRelative("key");
            var text = entry.FindPropertyRelative("text");
            if (!string.IsNullOrEmpty(search) &&
                key.stringValue.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                text.stringValue.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded, key.stringValue, true);
            if (entry.isExpanded) EditorGUILayout.PropertyField(text, new GUIContent("文章"));
            EditorGUILayout.EndVertical();
        }
        serializedObject.ApplyModifiedProperties();
    }
}
