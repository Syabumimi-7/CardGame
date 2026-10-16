using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared help copy. Named placeholders retain live game values.</summary>
[CreateAssetMenu(menuName = "Game/Help Text Content")]
public class HelpTextContent : ScriptableObject
{
    [Serializable]
    public class TextEntry
    {
        [Tooltip("識別名です。変更せず、文章だけを編集してください。")]
        public string key;
        [TextArea(3, 12)]
        [Tooltip("{draw}などの部分はゲーム設定の値に置き換わります。")]
        public string text;
    }

    [Tooltip("タイトル・ポーズ共通のヘルプ文章。変更はヘルプを開き直すと反映されます。")]
    public List<TextEntry> texts = new List<TextEntry>();

    public string Get(string key, string fallback)
    {
        foreach (var entry in texts)
            if (entry != null && entry.key == key) return entry.text ?? fallback;
        return fallback;
    }
}
