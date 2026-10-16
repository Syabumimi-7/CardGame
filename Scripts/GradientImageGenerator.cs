using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GradientImageGenerator : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("グラデーションを適用したいImageのリスト（複数指定可能）")]
    public List<Image> targetImages = new List<Image>();

    [Header("画像サイズ")]
    [Tooltip("生成する画像の幅（解像度）")]
    public int textureWidth = 256;
    [Tooltip("生成する画像の高さ（解像度）")]
    public int textureHeight = 64;

    void Start()
    {
        GenerateAndApplyGradient();
    }

    void GenerateAndApplyGradient()
    {
        if (targetImages == null || targetImages.Count == 0)
        {
            Debug.LogWarning("Target Images リストが空です！インスペクターからImageを追加してください。");
            return;
        }

        // 1. 空の画像データ（Texture2D）を作成します
        Texture2D texture = new Texture2D(textureWidth, textureHeight);

        // 【★ここが修正ポイント★】
        // テクスチャの端の境界処理を Clamp（クランプ）に設定します。
        // これにより、右端の描画時に反対側（左端）の不透明な色が混ざり込むのを防ぎます。
        texture.wrapMode = TextureWrapMode.Clamp;

        // 2. 基本となる色を設定します
        Color baseColor = new Color(1f, 1f, 0.5f);

        // 3. 全行で共通の色と透明度を計算し、1行ずつまとめて設定します
        Color[] row = new Color[textureWidth];
        for (int x = 0; x < textureWidth; x++)
        {
            // 左端(x=0)で透明度1(不透明)、右端(x=textureWidth-1)で透明度0(完全透明)になります
            float alpha = 1f - ((float)x / (textureWidth - 1));

            row[x] = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        }
        for (int y = 0; y < textureHeight; y++)
        {
            texture.SetPixels(0, y, textureWidth, 1, row);
        }

        // 4. 塗った色を画像に反映（保存）させます
        texture.Apply();

        // 5. 作成した画像データをUI用の「Sprite（スプライト）」に変換します
        Rect rect = new Rect(0, 0, textureWidth, textureHeight);
        Vector2 pivot = new Vector2(0.5f, 0.5f);
        Sprite gradientSprite = Sprite.Create(texture, rect, pivot);

        // 6. リストに登録されたすべてのImageに対して適用します
        foreach (Image img in targetImages)
        {
            if (img != null)
            {
                img.sprite = gradientSprite;
                img.color = Color.white; // 色が引っ張られないように白に設定
            }
        }
    }
}
