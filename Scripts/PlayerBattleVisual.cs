using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>待機をループし、死亡を一度だけ再生して最後の姿勢を保持します。</summary>
[DisallowMultipleComponent]
public class PlayerBattleVisual : MonoBehaviour
{
    public Animator animator;
    public AnimationClip idle;
    public AnimationClip death;
    [Min(0f)] public float blendDuration = 0.12f;

    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;
    private AnimationClipPlayable idlePlayable, deathPlayable;
    private float idleTime, deathTime;
    public bool IsDying { get; private set; }
    public bool IsAnimatingDeath => isActiveAndEnabled && graph.IsValid() && IsDying
        && death != null && deathTime < death.length;

    public void PlayIdle()
    {
        ReleaseGraph();
        IsDying = false;
        idleTime = deathTime = 0f;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null || idle == null || death == null)
        {
            Debug.LogError("PlayerBattleVisualのAnimator / Idle / Deathが未設定です。", this);
            return;
        }
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        graph = PlayableGraph.Create(name + " Player Animations");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        mixer = AnimationMixerPlayable.Create(graph, 2);
        idlePlayable = AnimationClipPlayable.Create(graph, idle);
        deathPlayable = AnimationClipPlayable.Create(graph, death);
        idlePlayable.SetSpeed(0);
        deathPlayable.SetSpeed(0);
        graph.Connect(idlePlayable, 0, mixer, 0);
        graph.Connect(deathPlayable, 0, mixer, 1);
        mixer.SetInputWeight(0, 1f);
        mixer.SetInputWeight(1, 0f);
        AnimationPlayableOutput.Create(graph, "Player", animator).SetSourcePlayable(mixer);
        graph.Play();
        graph.Evaluate(0f);
    }

    public void PlayDeath()
    {
        if (IsDying) return;
        IsDying = true;
        deathTime = 0f;
    }

    private void LateUpdate() { AdvanceAnimation(Time.deltaTime); }

    private void AdvanceAnimation(float deltaTime)
    {
        if (!graph.IsValid()) return;
        idleTime = Mathf.Repeat(idleTime + deltaTime, Mathf.Max(0.001f, idle.length));
        idlePlayable.SetTime(idleTime);
        float weight = 0f;
        if (IsDying)
        {
            deathTime += deltaTime;
            // ループ素材も最後のフレームで止め、待機には戻しません。
            deathPlayable.SetTime(System.Math.Min(deathTime, System.Math.Max(0, death.length - 0.0001f)));
            weight = blendDuration <= 0f ? 1f : Mathf.Clamp01(deathTime / Mathf.Min(blendDuration, Mathf.Max(.001f, death.length)));
        }
        mixer.SetInputWeight(0, 1f - weight);
        mixer.SetInputWeight(1, weight);
        graph.Evaluate(0f);
    }

    private void OnDisable() { ReleaseGraph(); }
    private void OnDestroy() { ReleaseGraph(); }
    private void ReleaseGraph()
    {
        if (graph.IsValid()) graph.Destroy();
        graph = default;
    }
}
