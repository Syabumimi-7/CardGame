using System.Collections.Generic;
using UnityEngine;

/// <summary>Moves the existing camera continuously and fits both actors outside death shots.</summary>
[DefaultExecutionOrder(100), RequireComponent(typeof(Camera))]
public class BattleCameraMotion : MonoBehaviour
{
    [Header("通常時の緩やかな円弧移動")]
    [Range(0f, 10f)] public float orbitDegrees = 4f;
    [UnityEngine.Serialization.FormerlySerializedAs("orbitPeriod")]
    [Min(10f)] public float swayPeriod = 38f;
    [Header("死亡時のアップ")]
    public float deathFieldOfView = 29f;
    [Min(.05f)] public float deathBlendSeconds = .45f;
    [Tooltip("HUDと頭上のダメージ表示を避けるためのビューポート内側の領域")]
    public Rect combatFrame = new Rect(.25f, .27f, .50f, .56f);


    private Camera view;
    private BattleManager battle;
    private bool running, initialized;
    private Vector3 originalLocalPosition, baseWorldPosition, baseOffset, focus;
    private Quaternion originalLocalRotation, rotation;
    private float originalFov, distance, fov, phase;
    private Vector3 combatFocus;
    private Quaternion combatRotation;
    private float combatDistance, combatAspect;
    private Transform player, enemy, deathTarget;
    private Renderer[] playerRenderers, enemyRenderers;
    private Mesh bakedPose;
    private JointDeathCameraBounds enemyDeathBounds;
    private readonly List<Vector3> framePoints = new List<Vector3>(32);

    public static void Begin(BattleManager owner)
    {
        var camera = Camera.main;
        if (camera == null) return;
        var motion = camera.GetComponent<BattleCameraMotion>();
        if (motion == null) motion = camera.gameObject.AddComponent<BattleCameraMotion>();
        motion.BeginShot(owner);
    }

    public static void End(BattleManager owner)
    {
        var camera = Camera.main;
        var motion = camera != null ? camera.GetComponent<BattleCameraMotion>() : null;
        if (motion != null && motion.battle == owner) motion.Restore();
    }

    private void Awake()
    {
        view = GetComponent<Camera>();
        bakedPose = new Mesh { name = "Battle Camera Pose Bounds" };
    }

    private void BeginShot(BattleManager owner)
    {
        if (running) Restore();
        battle = owner;
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
        baseWorldPosition = transform.position;
        originalFov = view.fieldOfView;
        player = owner.playerVisualManager != null && owner.playerVisualManager.CurrentPlayer != null
            ? owner.playerVisualManager.CurrentPlayer.transform : null;
        enemy = owner.enemyManager.CurrentEnemy != null ? owner.enemyManager.CurrentEnemy.transform : null;
        if (player == null || enemy == null) return;
        playerRenderers = player.GetComponentsInChildren<Renderer>();
        enemyRenderers = enemy.GetComponentsInChildren<Renderer>();
        enemyDeathBounds = enemy.GetComponent<JointDeathCameraBounds>();
        deathTarget = null; phase = 0;
        running = true; initialized = false;
    }

    private void LateUpdate()
    {
        if (!running || battle == null) return;
        if (player == null || enemy == null) { Restore(); return; }
        if (battle.IsPaused || Time.deltaTime <= 0) return;

        if (!initialized)
        {
            Bounds playerBounds = ActorBounds(player, playerRenderers);
            Bounds enemyBounds = ActorBounds(enemy, enemyRenderers);
            Bounds pair = playerBounds; pair.Encapsulate(enemyBounds);
            focus = pair.center;
            baseOffset = baseWorldPosition - focus;
            if (baseOffset.sqrMagnitude < 1) baseOffset = new Vector3(3, 1, -10);
            rotation = transform.rotation; distance = baseOffset.magnitude; fov = originalFov;
            combatFocus = pair.center + Vector3.up * .18f;
            combatRotation = Quaternion.LookRotation(-baseOffset.normalized, Vector3.up);
            framePoints.Clear();
            // Reserve animation and damage-popup room once across the complete orbit arc.
            // Refitting each animated pose would make the camera move forward/backward again.
            Bounds playerEnvelope = playerBounds; playerEnvelope.Expand(new Vector3(.8f, .6f, .8f));
            Bounds enemyEnvelope = enemyBounds; enemyEnvelope.Expand(new Vector3(.8f, .6f, .8f));
            AddCombatBounds(playerEnvelope); AddCombatBounds(enemyEnvelope);
            FitCombatDistance();
            initialized = true;
        }
        // HP changes immediately, while FinishBattle waits for health/death animations.
        if (deathTarget == null)
        {
            if (battle.playerStatus.IsDead) deathTarget = player;
            else if (battle.enemyManager.CurrentEnemy != null && battle.enemyManager.CurrentEnemy.IsDead) deathTarget = enemy;
        }
        bool dying = deathTarget != null;
        if (!dying)
        {
            // Window resizing can require a new fit; animation and turns never change the distance.
            if (!Mathf.Approximately(combatAspect, view.aspect)) FitCombatDistance();
            phase += Time.deltaTime * Mathf.PI * 2 / Mathf.Max(10, swayPeriod);
            focus = combatFocus;
            rotation = Quaternion.AngleAxis(Mathf.Sin(phase) * orbitDegrees, Vector3.up) * combatRotation;
            distance = combatDistance; fov = originalFov;
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
            view.fieldOfView = fov;
            return;
        }
        // Normal orbit uses the initial fit; only a death shot needs the current animated bounds.
        Bounds subject = DeathSubjectBounds();
        Vector3 desiredFocus = subject.center;
        // Keep the current side of the arena and ease toward the fallen actor.
        Vector3 offset = -(rotation * Vector3.forward);
        offset.y = Mathf.Max(.18f, offset.y);
        float desiredDistance = Mathf.Max(2.2f, subject.size.y * 1.8f);
        float desiredFov = deathFieldOfView;
        framePoints.Clear();
        AddCorners(subject);
        Quaternion desiredRotation = Quaternion.LookRotation(-offset.normalized, Vector3.up);
        float blend = 1 - Mathf.Exp(-Time.deltaTime / Mathf.Max(.05f, deathBlendSeconds));
        focus = Vector3.Lerp(focus, desiredFocus, blend);
        rotation = Quaternion.Slerp(rotation, desiredRotation, blend);
        fov = Mathf.Lerp(fov, desiredFov, blend);
        distance = Mathf.Lerp(distance, desiredDistance, blend);

        // Refit the interpolated pose as well: a safe endpoint alone does not protect a moving camera.
        Rect safe = new Rect(.13f, .25f, .74f, .60f);
        float fitted = RequiredDistance(framePoints, focus, rotation, fov, view.aspect, safe, view.nearClipPlane);
        distance = Mathf.Max(distance, fitted);
        transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
        view.fieldOfView = fov;
    }

    private void FitCombatDistance()
    {
        combatAspect = view.aspect;
        combatDistance = baseOffset.magnitude * .72f;
        // Fit the entire circular sway in advance so its radius stays constant during play.
        for (int sample = 0; sample <= 128; sample++)
        {
            float angle = Mathf.Lerp(-orbitDegrees, orbitDegrees, sample / 128f);
            var orientation = Quaternion.AngleAxis(angle, Vector3.up) * combatRotation;
            combatDistance = Mathf.Max(combatDistance, RequiredDistance(framePoints, combatFocus,
                orientation, originalFov, combatAspect, combatFrame, view.nearClipPlane));
        }
        combatDistance += .03f;
    }

    private Bounds DeathSubjectBounds()
    {
        if (deathTarget == enemy && enemyDeathBounds != null && enemyDeathBounds.isActiveAndEnabled
            && enemyDeathBounds.TryGetBounds(out Bounds bounds)) return bounds;
        return ActorBounds(deathTarget, deathTarget == player ? playerRenderers : enemyRenderers);
    }

    private Bounds ActorBounds(Transform actor, Renderer[] renderers)
    {
        bool found = false;
        Bounds result = new Bounds(actor.position + Vector3.up * .9f, new Vector3(.7f, 1.8f, .7f));
        foreach (var renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer is ParticleSystemRenderer) continue;
            Bounds bounds = renderer.bounds;
            // Imported skinned bounds may include unrelated animations; use the current pose instead.
            var skin = renderer as SkinnedMeshRenderer;
            if (skin != null && skin.sharedMesh != null)
            {
                skin.BakeMesh(bakedPose); bakedPose.RecalculateBounds();
                Bounds local = bakedPose.bounds;
                bounds = new Bounds(skin.transform.TransformPoint(local.center), Vector3.zero);
                for (int i = 0; i < 8; i++) bounds.Encapsulate(skin.transform.TransformPoint(Corner(local, i)));
            }
            if (!found) { result = bounds; found = true; } else result.Encapsulate(bounds);
        }
        return result;
    }

    private void AddCombatBounds(Bounds bounds)
    {
        bounds.Expand(new Vector3(.24f, .16f, .24f));
        // Damage numbers start above the head and rise; reserve room for their full trajectory.
        float popupWorldMargin = Mathf.Max(.55f, bounds.size.y * .30f);
        bounds.Encapsulate(new Vector3(bounds.center.x, bounds.max.y + popupWorldMargin, bounds.center.z));
        AddCorners(bounds);
    }
    private void AddCorners(Bounds bounds)
    {
        for (int i = 0; i < 8; i++) framePoints.Add(Corner(bounds, i));
    }
    private static Vector3 Corner(Bounds bounds, int i)
    {
        return bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1,
            (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
    }

    /// <summary>Exact perspective constraints for every point, including asymmetric HUD margins.</summary>
    public static float RequiredDistance(IList<Vector3> points, Vector3 focus, Quaternion rotation,
        float fieldOfView, float aspect, Rect safe, float nearClip)
    {
        float tanV = Mathf.Tan(fieldOfView * Mathf.Deg2Rad * .5f);
        float tanH = tanV * Mathf.Max(.01f, aspect);
        float left = Mathf.Max(.01f, (1 - 2 * safe.xMin) * tanH);
        float right = Mathf.Max(.01f, (2 * safe.xMax - 1) * tanH);
        float bottom = Mathf.Max(.01f, (1 - 2 * safe.yMin) * tanV);
        float top = Mathf.Max(.01f, (2 * safe.yMax - 1) * tanV);
        Quaternion inverse = Quaternion.Inverse(rotation);
        float required = 0;
        foreach (Vector3 point in points)
        {
            Vector3 local = inverse * (point - focus);
            required = Mathf.Max(required, nearClip + .15f - local.z);
            required = Mathf.Max(required, (local.x < 0 ? -local.x / left : local.x / right) - local.z);
            required = Mathf.Max(required, (local.y < 0 ? -local.y / bottom : local.y / top) - local.z);
        }
        return required + .08f;
    }

    private void Restore()
    {
        if (!running) return;
        transform.localPosition = originalLocalPosition; transform.localRotation = originalLocalRotation;
        view.fieldOfView = originalFov;
        running = initialized = false; deathTarget = null;
    }
    private void OnDisable() { Restore(); }
    private void OnDestroy() { if (bakedPose != null) Destroy(bakedPose); }
}
