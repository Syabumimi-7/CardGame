using System;
using UnityEngine;

/// <summary>Opt-in death framing from animated joints, without baking the skinned mesh.</summary>
[DisallowMultipleComponent]
public sealed class JointDeathCameraBounds : MonoBehaviour
{
    [Serializable]
    public struct Anchor
    {
        public Transform joint;
        [Min(0f), Tooltip("このオブジェクトのスケールを1とした、関節周囲の余白")]
        public float radius;
    }

    [Tooltip("死亡カメラが追う関節。未設定・参照切れの場合は従来の計算を使います。")]
    public Anchor[] anchors;

    public bool TryGetBounds(out Bounds bounds)
    {
        bounds = default;
        if (anchors == null || anchors.Length == 0) return false;
        Vector3 scale = transform.lossyScale;
        float radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        for (int i = 0; i < anchors.Length; i++)
        {
            Anchor anchor = anchors[i];
            if (anchor.joint == null || anchor.radius < 0f) return false;
            Bounds jointBounds = new Bounds(anchor.joint.position, Vector3.one * (anchor.radius * radiusScale * 2f));
            if (i == 0) bounds = jointBounds;
            else bounds.Encapsulate(jointBounds);
        }
        return true;
    }
}
