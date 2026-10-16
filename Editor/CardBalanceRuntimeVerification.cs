using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>自動対戦ログを実際のBattleManagerの効果・状態処理で再現します。シーンは編集しません。</summary>
[InitializeOnLoad]
public static class CardBalanceRuntimeVerification
{
    [Serializable] public class Request { public string[] files; public string report; }
    [Serializable] public class CaseFile { public Case[] cases; }
    [Serializable] public class Case
    {
        public string[] deck;
        public string enemyId;
        public int initialHp, enemyHp;
        public float scaling;
        public ActionData[] actions;
        public Turn[] turns;
    }
    [Serializable] public class ActionData { public BattleEffect[] effects; }
    [Serializable] public class Play { public int index, tier; public int[] random; }
    [Serializable] public class Turn
    {
        public Play[] plays;
        public string phase;
        public int hp, block, enemyHp, enemyBlock, nextCost, preserve, poison, enemyPoison;
        public int permanent;
    }
    const string RequestPath = "Temp/BalanceRuntime.verify";
    const string ReportPath = "StageReview/Balance/runtime-verification.txt";
    static string currentReport = ReportPath;
    static CardBalanceRuntimeVerification() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string request = File.ReadAllText(RequestPath); File.Delete(RequestPath);
        try
        {
            var parsed = JsonUtility.FromJson<Request>(request);
            currentReport = string.IsNullOrEmpty(parsed.report) ? ReportPath : parsed.report;
            Verify(parsed.files, currentReport);
        }
        catch (Exception e) { File.WriteAllText(currentReport, "FAIL\n" + e); Debug.LogException(e); }
    }
    [MenuItem("Tools/Cards/Verify Balance Replays")]
    public static void VerifyAvailable()
    {
        Verify(Directory.GetFiles("StageReview/Balance", "replay-*.trace.json"));
    }
    public static void Verify(string[] files, string reportPath = ReportPath)
    {
        MethodInfo apply = typeof(BattleManager).GetMethod("ApplyCardEffects", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo enemyApply = typeof(BattleManager).GetMethod("ApplyEffect", BindingFlags.Instance | BindingFlags.NonPublic);
        PropertyInfo performance = typeof(CardInstance).GetProperty("CurrentPerformance");
        PropertyInfo determined = typeof(CardInstance).GetProperty("IsPerformanceDetermined");
        var root = new GameObject("BalanceReplayVerification"); root.SetActive(false);
        var data = ScriptableObject.CreateInstance<EnemyData>();
        bool logging = Debug.unityLogger.logEnabled;
        int caseCount = 0, turnCount = 0, playCount = 0;
        try
        {
            Debug.unityLogger.logEnabled = false;
            var player = root.AddComponent<PlayerStatus>();
            var enemy = root.AddComponent<EnemyInstance>();
            var enemies = root.AddComponent<EnemyManager>();
            var battle = root.AddComponent<BattleManager>();
            battle.playerStatus = player; battle.enemyManager = enemies;
            typeof(EnemyManager).GetField("<CurrentEnemy>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemies, enemy);
            foreach (string file in files)
            {
                var cases = JsonUtility.FromJson<CaseFile>(File.ReadAllText(file)).cases;
                foreach (Case test in cases)
                {
                    caseCount++;
                    player.ResetForBattle(); player.currentHP = test.initialHp;
                    data.enemyName = "Replay"; data.baseMaxHP = test.enemyHp; data.scalingFactor = 1;
                    bool actualEnemy = !string.IsNullOrEmpty(test.enemyId);
                    if (actualEnemy)
                    {
                        var source = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/ScriptableObject/EnemyData/" + test.enemyId + ".asset");
                        Require(source != null, "Missing enemy " + test.enemyId);
                        data.actionPattern = source.actionPattern; data.categoryActions = source.categoryActions;
                        data.actionsPerTurn = source.actionsPerTurn; data.reactsToPlayerCards = source.reactsToPlayerCards;
                    }
                    else { data.actionsPerTurn = 1; data.reactsToPlayerCards = false; }
                    enemy.Setup(data, 1);
                    typeof(EnemyInstance).GetProperty("BattleScalingMultiplier").SetValue(enemy, test.scaling > 0 ? test.scaling : 1f);
                    var deck = new List<CardInstance>();
                    foreach (string id in test.deck)
                    {
                        var card = AssetDatabase.LoadAssetAtPath<CardData>("Assets/ScriptableObject/CardData/" + id + ".asset");
                        Require(card != null, "Missing card " + id);
                        deck.Add(new CardInstance(card));
                    }
                    int actionIndex = 0;
                    foreach (Turn turn in test.turns)
                    {
                        turnCount++;
                        enemy.BeginPlayerTurn();
                        player.currentCost = 3 + player.cardStatus.nextTurnCost;
                        player.cardStatus.nextTurnCost = 0;
                        player.cardStatus.BeginTurnDrawBonus();
                        var played = new HashSet<int>();
                        foreach (Play play in turn.plays)
                        {
                            Require(played.Add(play.index), "Same card twice in one turn");
                            var card = deck[play.index];
                            if (!card.IsPerformanceDetermined)
                            {
                                performance.SetValue(card, play.tier == 0 ? card.Data.lowPerformance : play.tier == 1 ? card.Data.mediumPerformance : card.Data.highPerformance);
                                determined.SetValue(card, true);
                            }
                            card.CostReduction = player.CardCostReduction;
                            Require(card.EffectiveCost <= player.currentCost, "Overspending in case " + caseCount);
                            Require(player.currentHP > 0 && enemy.currentHP > 0, "Played after battle finished");
                            player.currentCost -= card.EffectiveCost;
                            CardPerformance originalPerformance = card.CurrentPerformance;
                            if (play.random != null && play.random.Length > 0)
                            {
                                CardPerformance replayPerformance = originalPerformance;
                                replayPerformance.effects = new List<BattleEffect>(originalPerformance.effects);
                                int randomIndex = 0;
                                for (int effectIndex = 0; effectIndex < replayPerformance.effects.Count; effectIndex++)
                                {
                                    BattleEffect effect = replayPerformance.effects[effectIndex];
                                    if (effect.effectType != BattleEffectType.RandomDamage) continue;
                                    int roll = play.random[randomIndex++];
                                    Require(roll >= effect.minimumMultiplier && roll <= effect.maximumMultiplier, "Invalid random roll");
                                    effect.minimumMultiplier = effect.maximumMultiplier = roll;
                                    replayPerformance.effects[effectIndex] = effect;
                                }
                                Require(randomIndex == play.random.Length, "Unexpected random rolls");
                                performance.SetValue(card, replayPerformance);
                            }
                            apply.Invoke(battle, new object[] { card }); card.RecordPlayed(); playCount++;
                            performance.SetValue(card, originalPerformance);
                            enemy.RecordPlayerCard(card.Data.category);
                        }
                        string phase = "card";
                        if (!player.IsDead && !enemy.IsDead)
                        {
                            phase = "poison";
                            player.cardStatus.EndPlayerTurn(player); player.TickModifiers(); enemy.TickPoison();
                            if (!enemy.IsDead)
                            {
                                phase = "enemy";
                                for (int actionNumber = 0; actionNumber < (actualEnemy ? enemy.ActionsPerTurn : 1); actionNumber++)
                                {
                                    if (player.IsDead || enemy.IsDead) break;
                                    IEnumerable<BattleEffect> effects = actualEnemy ? enemy.ResolveEffects(enemy.GetNextAction())
                                        : (IEnumerable<BattleEffect>)test.actions[actionIndex].effects;
                                    foreach (BattleEffect effect in effects)
                                    {
                                        if (player.IsDead || enemy.IsDead) break;
                                        enemyApply.Invoke(battle, new object[] { effect, false, null });
                                    }
                                    enemy.AdvanceActionIndex();
                                }
                                actionIndex = (actionIndex + 1) % test.actions.Length;
                                if (!player.IsDead && (!actualEnemy || !enemy.IsDead))
                                {
                                    phase = "complete";
                                    player.TickPoison(); player.cardStatus.EndEnemyTurn(); enemy.TickModifiers();
                                }
                            }
                        }
                        string context = " case " + caseCount + " turn " + turnCount;
                        Require(phase == turn.phase, "Phase" + context);
                        Require(player.currentHP == turn.hp && player.currentBlock == turn.block, "Player HP/block" + context + $" actual={player.currentHP}/{player.currentBlock} expected={turn.hp}/{turn.block}");
                        Require(enemy.currentHP == turn.enemyHp && enemy.currentBlock == turn.enemyBlock, "Enemy HP/block" + context);
                        Require(player.cardStatus.nextTurnCost == turn.nextCost && player.cardStatus.preserveCostTurns == turn.preserve, "Reserved cost" + context);
                        Require(PoisonTotal(player.poison) == turn.poison && PoisonTotal(enemy.poison) == turn.enemyPoison, "Poison stacks" + context);
                        if (actualEnemy) Require(enemy.PermanentAttackBonus == turn.permanent, "Permanent rage" + context);
                    }
                }
            }
            Require(caseCount > 0, "No replay cases supplied");
            File.WriteAllText(reportPath, $"PASS\nFiles: {files.Length}\nBattles: {caseCount}\nTurns: {turnCount}\nCard plays: {playCount}\nActual BattleManager effects, PlayerStatus, EnemyInstance, poison, modifiers and reservations match simulation.\nThis verifies combat logic, not UI/input/animation play.\n");
        }
        finally
        {
            Debug.unityLogger.logEnabled = logging;
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(data);
        }
    }
    static int PoisonTotal(PoisonStatus poison)
    {
        int total = 0; foreach (var stack in poison.Stacks) total += stack.damagePerTurn * stack.remainingTurns; return total;
    }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
