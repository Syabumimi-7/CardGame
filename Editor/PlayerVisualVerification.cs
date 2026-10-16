using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>実シーンでパック連動、連戦、各敗北経路、タイトル復帰を確認します。</summary>
[InitializeOnLoad]
public static class PlayerVisualVerification
{
    const string Key = "PlayerVisualVerification";
    const string Report = "StageReview/player-visual-play-verification.txt";
    static float deathStarted;
    static bool lostEvent;
    static PlayerBattleVisual dyingPlayer;
    static Quaternion[] idlePose;
    static Transform[] bones;
    static readonly List<Object> temporaryAssets = new List<Object>();
    static PlayerVisualVerification() { EditorApplication.update += Tick; }
    public static void Begin()
    {
        PlayerVisualAuthoring.Require(SceneManager.GetActiveScene().path == PlayerVisualAuthoring.ScenePath, "Open RuinsStageRefined first");
        SessionState.SetInt(Key, 1);
        SessionState.SetInt(Key + "Pack", 0);
        SessionState.SetFloat(Key + "Due", 0);
        SessionState.SetFloat(Key + "Deadline", float.MaxValue);
        File.WriteAllText(Report, "Player visual Play mode verification\n");
        EditorApplication.EnterPlaymode();
    }
    static void Next(int step, float delay = 0)
    {
        SessionState.SetInt(Key, step);
        SessionState.SetFloat(Key + "Due", Time.realtimeSinceStartup + delay);
        SessionState.SetFloat(Key + "Deadline", Time.realtimeSinceStartup + delay + 45);
    }
    static void Check(bool value, string message) { PlayerVisualAuthoring.Require(value, message); }
    static void Pass(string message) { File.AppendAllText(Report, "PASS " + message + "\n"); }
    static bool GameOverVisible(GameManager game) => game.battleUIRoot.GetComponentsInChildren<TMPro.TMP_Text>()
        .Any(t => t.text == "GAME OVER");
    static void Tick()
    {
        int step = SessionState.GetInt(Key, 0);
        if (step == 0 || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (Time.realtimeSinceStartup < SessionState.GetFloat(Key + "Due", 0)) return;
        try
        {
            Application.runInBackground = true;
            Check(Time.realtimeSinceStartup < SessionState.GetFloat(Key + "Deadline", float.MaxValue), "Timeout at step " + step);
            var game = Object.FindObjectOfType<GameManager>();
            var battle = game.battleManager;
            var manager = battle.playerVisualManager;
            int pack = SessionState.GetInt(Key + "Pack", 0);
            if (step == 1)
            {
                Check(manager != null && manager.CurrentPlayer == null, "Player visible on title");
                game.StartGame(pack);
                Next(2, 8);
            }
            else if (step == 2)
            {
                Check(game.SelectedPoolType == pack && manager.CurrentPlayer == null, "Pack/preview state");
                game.StartBattleWithTransition();
                Next(3, 1);
            }
            else if (step == 3)
            {
                if (!battle.CanPlayerAct) return;
                Check(manager.CurrentPlayer != null && manager.CurrentPlayer.name == "Player" + (pack + 1), "Wrong player for pack");
                Check(Object.FindObjectsOfType<PlayerBattleVisual>().Length == 1, "Duplicate player");
                Check(!manager.CurrentPlayer.IsDying, "Player starts dead");
                Vector3 toEnemy = battle.enemyManager.CurrentEnemy.transform.position - manager.CurrentPlayer.transform.position;
                toEnemy.y = 0;
                Check(Vector3.Dot(manager.CurrentPlayer.transform.forward, toEnemy.normalized) > .99f, "Player not facing enemy");
                var bounds = PlayerVisualAuthoring.PosedBounds(manager.CurrentPlayer.gameObject);
                Vector3 viewport = Camera.main.WorldToViewportPoint(bounds.center);
                Check(viewport.z > 0 && viewport.x > .15f && viewport.x < .85f && viewport.y > .25f && viewport.y < .8f, "Player outside visible battlefield");
                Check(Mathf.Abs(bounds.min.y - manager.playerSpawnPoint.position.y) < .15f, "Player floating/buried");
                bones = manager.CurrentPlayer.animator.GetComponentsInChildren<Transform>();
                idlePose = bones.Select(b => b.localRotation).ToArray();
                Next(4, .7f);
            }
            else if (step == 4)
            {
                Check(bones.Where((b, i) => Quaternion.Angle(b.localRotation, idlePose[i]) > .1f).Any(), "Idle frozen");
                ScreenCapture.CaptureScreenshot("StageReview/player" + (pack + 1) + "-battle.png");
                Pass($"pack{pack + 1} -> Player{pack + 1}, one visible model, facing enemy, grounded, animated idle");
                if (pack == 0)
                {
                    battle.enemyManager.CurrentEnemy.TakeDamage(99999);
                    battle.EndPlayerTurn();
                    Next(5);
                }
                else Next(7, .3f);
            }
            else if (step == 5)
            {
                if (battle.State != BattleTurnState.Won || manager.CurrentPlayer != null) return;
                Check(!GameOverVisible(game), "Victory became game over");
                battle.currentBattleCount++;
                game.StartBattleWithTransition();
                Next(6, 1);
            }
            else if (step == 6)
            {
                if (!battle.CanPlayerAct) return;
                Check(manager.CurrentPlayer.name == "Player1" && !manager.CurrentPlayer.IsDying && Object.FindObjectsOfType<PlayerBattleVisual>().Length == 1, "Next battle did not reset player");
                Pass("victory cleanup and next battle retain selected Player1 with idle reset");
                Next(7);
            }
            else if (step == 7)
            {
                lostEvent = false;
                battle.onBattleLost.AddListener(OnLost);
                game.ProgressionUI.gameOverReturnDelay = 1f;
                if (pack < 2)
                {
                    var enemy = battle.enemyManager.CurrentEnemy;
                    var data = Object.Instantiate(enemy.Data);
                    var action = ScriptableObject.CreateInstance<EnemyActionData>();
                    temporaryAssets.Add(data); temporaryAssets.Add(action);
                    action.actionName = pack == 0 ? "Verification attack" : "Verification wait";
                    if (pack == 0) action.effects.Add(new BattleEffect { effectType = BattleEffectType.Damage, target = EffectTarget.Enemy, value = 9999 });
                    data.actionPattern = new List<EnemyActionData> { action };
                    enemy.Setup(data, battle.currentBattleCount);
                    if (pack == 1) battle.playerStatus.AddPoison(1, 9999);
                    battle.EndPlayerTurn();
                }
                else
                {
                    var data = ScriptableObject.CreateInstance<CardData>();
                    temporaryAssets.Add(data);
                    data.cardName = "Verification self damage";
                    data.lowPerformance = data.mediumPerformance = data.highPerformance = new CardPerformance
                    {
                        cost = 0, effects = new List<BattleEffect> { new BattleEffect { effectType = BattleEffectType.Damage, target = EffectTarget.Self, value = 9999 } }
                    };
                    var card = new CardInstance(data); card.RollPerformance();
                    var hand = (List<CardInstance>)typeof(BattleManager).GetField("hand", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(battle);
                    hand.Add(card);
                    Check(battle.TryPlayCard(card), "Self damage card rejected");
                }
                Next(8);
            }
            else if (step == 8)
            {
                if (!battle.playerStatus.IsDead) return;
                dyingPlayer = manager.CurrentPlayer;
                Check(dyingPlayer != null && dyingPlayer.IsAnimatingDeath, "Lethal damage did not start death");
                Check(!lostEvent && !GameOverVisible(game) && !battle.CanPlayerAct, "Game over/input before death");
                deathStarted = Time.time;
                Next(9, Mathf.Min(.6f, dyingPlayer.death.length * .3f));
            }
            else if (step == 9)
            {
                Check(manager.CurrentPlayer == dyingPlayer && dyingPlayer.IsAnimatingDeath && !lostEvent && !GameOverVisible(game), "Death interrupted by game over");
                Check(battle.enemyManager.CurrentEnemy != null, "Enemy removed before death finished");
                ScreenCapture.CaptureScreenshot("StageReview/player" + (pack + 1) + "-death.png");
                Next(10);
            }
            else if (step == 10)
            {
                if (!GameOverVisible(game)) return;
                Check(lostEvent && battle.State == BattleTurnState.Lost && manager.CurrentPlayer == null, "Game over cleanup/event missing");
                Pass($"Player{pack + 1}: {(pack == 0 ? "enemy attack" : pack == 1 ? "poison" : "self damage")} starts death, blocks input, waits for clip before loss event and GAME OVER; observed wait={Time.time - deathStarted:F2}s");
                battle.onBattleLost.RemoveListener(OnLost);
                Next(11, 2);
            }
            else if (step == 11)
            {
                if (!game.startScreenRoot.activeInHierarchy) return;
                Check(manager.CurrentPlayer == null && Object.FindObjectsOfType<PlayerBattleVisual>().Length == 0, "Player left on title");
                foreach (var asset in temporaryAssets) if (asset != null) Object.Destroy(asset);
                temporaryAssets.Clear();
                Pass("title cleanup after pack" + (pack + 1));
                if (pack < 2) { SessionState.SetInt(Key + "Pack", pack + 1); Next(1, .3f); }
                else
                {
                    Pass("ALL player battle checks");
                    SessionState.SetInt(Key, 0);
                    EditorApplication.ExitPlaymode();
                }
            }
        }
        catch (Exception e)
        {
            File.AppendAllText(Report, "FAIL " + e + "\n");
            SessionState.SetInt(Key, 0);
            Time.timeScale = 1;
            EditorApplication.ExitPlaymode();
            Debug.LogException(e);
        }
    }
    static void OnLost()
    {
        Check(dyingPlayer != null && !dyingPlayer.IsAnimatingDeath, "Loss event fired before death completed");
        lostEvent = true;
    }
}
