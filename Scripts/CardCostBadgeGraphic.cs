using UnityEngine;
using UnityEngine.UI;

/// <summary>Draw the complete cost medallion above the illustration viewport.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class CardCostBadgeGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        CardDesignGraphic.DrawCostBadge(vh);
    }
}
