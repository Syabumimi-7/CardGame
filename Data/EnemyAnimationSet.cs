using UnityEngine;

/// <summary>敵の待機と死亡のクリップを設定します。</summary>
[CreateAssetMenu(fileName = "EnemyAnimations", menuName = "Game/Enemy Animation Set")]
public class EnemyAnimationSet : ScriptableObject
{
    [Tooltip("待機中にループ再生する動作")]
    public AnimationClip idle;
    [Tooltip("HPが0になったときに再生し、最終姿勢を保持する動作")]
    public AnimationClip death;
    [Min(0f)] public float blendDuration = 0.12f;
}
