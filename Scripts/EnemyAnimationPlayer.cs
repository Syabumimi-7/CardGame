using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>待機と単発動作をブレンドします。死亡は最優先で、最終姿勢を保持します。</summary>
[DisallowMultipleComponent]
public class EnemyAnimationPlayer : MonoBehaviour
{
    public Animator animator;
    public EnemyAnimationSet animations;

    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;
    private AnimationClipPlayable idlePlayable;
    private AnimationClipPlayable shotPlayable;
    private float idleTime;
    private float shotTime;
    private float shotDuration;
    private float shotSpeed;
    private bool dying;

    public bool IsBusy => isActiveAndEnabled && graph.IsValid() && shotPlayable.IsValid()
        && shotTime < shotDuration + (dying ? 0f : BlendDuration);
    private float BlendDuration => animations != null ? Mathf.Max(0f, animations.blendDuration) : 0f;

    public void ResetForBattle()
    {
        ReleaseGraph();
        dying = false;
        idleTime = 0f;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null || animations == null || animations.idle == null) return;

        // 立ち位置はEnemyManagerが管理し、クリップによる移動を持ち越しません。
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        graph = PlayableGraph.Create(name + " Enemy Animations");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        mixer = AnimationMixerPlayable.Create(graph, 2);
        idlePlayable = AnimationClipPlayable.Create(graph, animations.idle);
        idlePlayable.SetSpeed(0);
        graph.Connect(idlePlayable, 0, mixer, 0);
        mixer.SetInputWeight(0, 1f);
        AnimationPlayableOutput.Create(graph, "Enemy", animator).SetSourcePlayable(mixer);
        graph.Play();
        graph.Evaluate(0f);
    }

    public void PlayDeath()
    {
        if (dying) return;
        dying = true;
        if (animations != null) PlayShot(animations.death, 1f);
    }

    private bool PlayShot(AnimationClip clip, float speed)
    {
        if (!graph.IsValid() || clip == null || clip.length <= 0f) return false;
        if (shotPlayable.IsValid())
        {
            mixer.DisconnectInput(1);
            graph.DestroyPlayable(shotPlayable);
        }
        shotPlayable = AnimationClipPlayable.Create(graph, clip);
        shotPlayable.SetSpeed(0);
        graph.Connect(shotPlayable, 0, mixer, 1);
        shotSpeed = Mathf.Max(0.01f, speed);
        shotTime = 0f;
        shotDuration = clip.length / shotSpeed;
        return true;
    }

    private void LateUpdate()
    {
        AdvanceAnimation(Time.deltaTime);
    }

    private void AdvanceAnimation(float deltaTime)
    {
        if (!graph.IsValid()) return;
        idleTime = Mathf.Repeat(idleTime + deltaTime, Mathf.Max(0.001f, animations.idle.length));
        idlePlayable.SetTime(idleTime);
        float weight = 0f;
        if (shotPlayable.IsValid())
        {
            shotTime += deltaTime;
            // ループ設定のある素材でも、単発動作は最後のフレームで止めます。
            double lastTime = System.Math.Max(0, shotDuration * shotSpeed - 0.0001f);
            shotPlayable.SetTime(System.Math.Min(shotTime * shotSpeed, lastTime));
            weight = BlendDuration <= 0f ? 1f : Mathf.Clamp01(shotTime / BlendDuration);
            if (!dying && shotTime >= shotDuration)
                weight = BlendDuration <= 0f ? 0f : 1f - Mathf.Clamp01((shotTime - shotDuration) / BlendDuration);
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
        idlePlayable = default;
        shotPlayable = default;
    }
}
