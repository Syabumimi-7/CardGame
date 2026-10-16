using TMPro;
using UnityEngine;

/// <summary>Shared font references for authored and runtime-created buttons.</summary>
public class ButtonDesignTheme : ScriptableObject
{
    public TMP_FontAsset font;
    public Material textMaterial;
    static ButtonDesignTheme cached;
    public static ButtonDesignTheme Current => cached!=null?cached:(cached=Resources.Load<ButtonDesignTheme>("ButtonDesignTheme"));
}
