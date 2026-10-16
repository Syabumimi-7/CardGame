using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>実際のStartBattle/TryPlayCard/EndPlayerTurnで10連戦を自動操作するEditor専用検証。</summary>
[InitializeOnLoad]
public class CardBalancePlayModeVerification
{
    [Serializable] public class Run { public string strategy; public string[] deck; public int seed; public string boss; public string[] enemyOrder; }
    [Serializable] public class Plan { public Run[] runs; public string reportPath; }
    [Serializable] public class Result { public string strategy, boss; public int seed, reached, turns, hp; public bool win; public string[] enemies; }
    [Serializable] public class Report { public List<Result> results = new List<Result>(); public string error; }
    const string Request = "Temp/BalancePlayMode.request";
    const string PlanPath = "StageReview/Balance/playmode-plan.json";
    const string ReportPath = "StageReview/Balance/playmode-results.json";
    static CardBalancePlayModeVerification controller;
    static CardBalancePlayModeVerification() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!EditorApplication.isPlayingOrWillChangePlaymode && File.ReadAllText(Request).Trim() == "start")
        {
            File.WriteAllText(Request, "playing"); EditorApplication.isPlaying = true; return;
        }
        if (!EditorApplication.isPlaying || File.ReadAllText(Request).Trim() != "playing") return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (controller == null)
        {
            controller = new CardBalancePlayModeVerification(); controller.Initialize();
        }
        else controller.Tick();
    }

    Plan plan;
    Report report = new Report();
    BattleManager battle;
    DeckManager deck;
    PlayerStatus player;
    EnemyManager enemies;
    GameObject simulationRoot, enemyPrefab;
    readonly List<EnemyData> enemyCopies = new List<EnemyData>();
    int runIndex, turns, totalTurns;
    bool waitingForTurn;
    float previousTimeScale;
    bool previousLogging;
    bool previousRunInBackground;
    double started;
    string reportPath = ReportPath;
    readonly List<string> encountered = new List<string>();
    readonly List<GameObject> pausedSceneRoots = new List<GameObject>();

    void Initialize()
    {
        previousTimeScale = Time.timeScale; previousLogging = Debug.unityLogger.logEnabled;
        previousRunInBackground = Application.runInBackground; Application.runInBackground = true;
        Time.timeScale = 100; Debug.unityLogger.logEnabled = false;
        started = EditorApplication.timeSinceStartup;
        try
        {
            string planFile = File.Exists("Temp/BalancePlayMode.plan") ? File.ReadAllText("Temp/BalancePlayMode.plan").Trim() : PlanPath;
            plan = JsonUtility.FromJson<Plan>(File.ReadAllText(planFile));
            if (!string.IsNullOrEmpty(plan.reportPath)) reportPath = plan.reportPath;
            foreach (var sceneRoot in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!sceneRoot.activeSelf) continue;
                pausedSceneRoots.Add(sceneRoot); sceneRoot.SetActive(false);
            }
            simulationRoot = new GameObject("IsolatedBalanceBattle"); simulationRoot.SetActive(false);
            deck = simulationRoot.AddComponent<DeckManager>();
            player = simulationRoot.AddComponent<PlayerStatus>();
            enemies = simulationRoot.AddComponent<EnemyManager>();
            battle = simulationRoot.AddComponent<BattleManager>();
            battle.deckManager = deck; battle.playerStatus = player; battle.enemyManager = enemies;
            battle.balanceData = AssetDatabase.LoadAssetAtPath<GameBalanceData>("Assets/ScriptableObject/GameBalanceData/DefaultBalance.asset");
            battle.turnBannerDuration = 0; battle.enemyActionDelay = 0;
            enemyPrefab = new GameObject("VerificationEnemyPrefab"); enemyPrefab.SetActive(false);
            enemyPrefab.AddComponent<EnemyInstance>();
            enemies.enemySpawnPoint = simulationRoot.transform;
            foreach (string id in new[] { "Enemy1", "Enemy2", "Enemy3", "Enemy4", "Enemy5", "Enemy6", "Boss1", "Boss2" })
            {
                var source = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/ScriptableObject/EnemyData/" + id + ".asset");
                var copy = UnityEngine.Object.Instantiate(source); copy.enemyPrefab = enemyPrefab; enemyCopies.Add(copy);
                if (copy.enemyType == EnemyType.Boss) enemies.bossEnemies.Add(copy); else enemies.normalEnemies.Add(copy);
            }
            // Spawn point must only contain enemy instances; keep managers outside it.
            var spawn = new GameObject("VerificationSpawn"); spawn.transform.SetParent(simulationRoot.transform);
            enemies.enemySpawnPoint = spawn.transform;
            simulationRoot.SetActive(true); BeginRun();
        }
        catch (Exception e) { Stop(e.ToString()); }
    }
    void BeginRun()
    {
        var run = plan.runs[runIndex]; UnityEngine.Random.InitState(run.seed);
        encountered.Clear();
        deck.currentDeck.Clear();
        foreach (string id in run.deck)
            deck.currentDeck.Add(new CardInstance(AssetDatabase.LoadAssetAtPath<CardData>("Assets/ScriptableObject/CardData/" + id + ".asset")));
        player.currentHP = 50; battle.currentBattleCount = 1; totalTurns = 0; BeginBattle();
    }
    void BeginBattle()
    {
        var run = plan.runs[runIndex];
        enemies.normalEnemies = enemyCopies.Where(e => e.enemyType == EnemyType.Normal).ToList();
        enemies.bossEnemies = enemyCopies.Where(e => e.enemyType == EnemyType.Boss).ToList();
        if (!string.IsNullOrEmpty(run.boss)) enemies.bossEnemies = enemyCopies.Where(e => e.name == run.boss + "(Clone)").ToList();
        if (battle.currentBattleCount < 10 && run.enemyOrder != null && run.enemyOrder.Length > 0)
        {
            string id = run.enemyOrder[(battle.currentBattleCount - 1) % run.enemyOrder.Length];
            enemies.normalEnemies = enemyCopies.Where(e => e.name == id + "(Clone)").ToList();
        }
        turns = 0; waitingForTurn = true; battle.StartBattle();
        if (enemies.CurrentEnemy != null) encountered.Add(enemies.CurrentEnemy.Data.name.Replace("(Clone)", ""));
    }
    void Tick()
    {
        if (plan == null) return;
        try
        {
            File.WriteAllText("Temp/BalancePlayMode.progress", $"Run {runIndex}/{plan.runs.Length}, battle {battle.currentBattleCount}, state {battle.State}, turns {turns}, elapsed {EditorApplication.timeSinceStartup-started:F1}s");
            if (EditorApplication.timeSinceStartup - started > Math.Max(180, plan.runs.Length * 180 + 120)) throw new Exception("Play-mode verification timeout");
            if ((battle.State == BattleTurnState.Won || battle.State == BattleTurnState.Lost) && enemies.CurrentEnemy == null)
            {
                totalTurns += turns;
                if (battle.State == BattleTurnState.Lost || battle.currentBattleCount == 10) { FinishRun(battle.State == BattleTurnState.Won); return; }
                if (plan.runs[runIndex].strategy != "adversity" || player.currentHP < 22)
                    player.currentHP = Mathf.Min(50, player.currentHP + battle.balanceData.hpRecoveryAmount);
                battle.currentBattleCount++; BeginBattle(); return;
            }
            if (!battle.CanPlayerAct) return;
            if (waitingForTurn) { turns++; waitingForTurn = false; }
            if (turns > 60) { totalTurns += turns; battle.CancelBattle(); FinishRun(false); return; }
            CardInstance best = null; float bestScore = .05f;
            foreach (var card in battle.Hand)
            {
                card.CostReduction = player.CardCostReduction;
                if (card.EffectiveCost > player.currentCost) continue;
                float score = Score(card);
                if (score > bestScore) { best = card; bestScore = score; }
            }
            if (best != null) battle.TryPlayCard(best);
            else { waitingForTurn = true; battle.EndPlayerTurn(); }
        }
        catch (Exception e) { Stop(e.ToString()); }
    }
    float Score(CardInstance card)
    {
        var enemy = enemies.CurrentEnemy;
        float score = 0, damage = 0, selfDamage = 0;
        int attack = player.modifiers.cardDamageBonus + player.modifiers.outgoingDamageModifier;
        var effects = card.CurrentPerformance.effects;
        int shortage = Mathf.Max(0, EnemyIntentForecast.Create(enemy, player).attackDamage - player.currentBlock);
        var upcomingConversion = enemy.GetNextAction(1);
        if (upcomingConversion != null && upcomingConversion.effects.Any(e => e.effectType == BattleEffectType.ConsumeBlockDamage))
        {
            var upcomingAction = enemy.GetNextAction();
            int addedBlock = upcomingAction.effects.Where(e => e.effectType == BattleEffectType.GainBlock && e.target == EffectTarget.Self).Sum(e => e.value);
            int conversion = upcomingConversion.effects.First(e => e.effectType == BattleEffectType.ConsumeBlockDamage).value;
            int accumulated = player.cardStatus.endTurnBlocks.Sum(e => e.value);
            shortage = Mathf.Max(shortage, enemy.currentBlock + addedBlock + conversion + enemy.AttackBonus - player.currentBlock - accumulated * 2);
        }
        foreach (var e in effects)
        {
            float v = e.value, d = e.duration;
            switch (e.effectType)
            {
                case BattleEffectType.Damage:
                case BattleEffectType.GrowingDamage:
                case BattleEffectType.BlockDamage:
                case BattleEffectType.RandomDamage:
                case BattleEffectType.RemainingHPDamage:
                    if (e.effectType == BattleEffectType.GrowingDamage) v = card.GetBaseValue(e);
                    if (e.effectType == BattleEffectType.BlockDamage) v += player.currentBlock;
                    if (e.effectType == BattleEffectType.RandomDamage) v *= (e.minimumMultiplier + e.maximumMultiplier) * .5f;
                    if (e.effectType == BattleEffectType.RemainingHPDamage) v = (e.target == EffectTarget.Enemy ? enemy.currentHP : player.currentHP) / Mathf.Max(1, e.value);
                    v = Mathf.Max(0, v + attack);
                    if (e.target == EffectTarget.Enemy) damage += Mathf.Max(0, v + enemy.modifiers.incomingDamageModifier + enemy.modifiers.incomingCardDamageBonus);
                    else selfDamage += Mathf.Max(0, v + player.modifiers.incomingDamageModifier);
                    break;
                case BattleEffectType.GainBlock:
                    v = Mathf.Max(0, v + player.cardStatus.BlockBonus);
                    score += Mathf.Min(v, shortage) * .9f + Mathf.Max(0, v - shortage) * (plan.runs[runIndex].strategy == "block" ? .28f : .12f);
                    break;
                case BattleEffectType.ApplyPoison: score += (v + player.modifiers.cardPoisonDamageBonus) * Mathf.Min(d + player.modifiers.cardPoisonDurationBonus, 4) * .85f; break;
                case BattleEffectType.CardDamageBonus: score += v * Mathf.Min(d, 3) * .95f; break;
                case BattleEffectType.CardBlockBonus: score += v * Mathf.Min(d, 3) * .75f; break;
                case BattleEffectType.Thorns: score += v * Mathf.Min(d, 3) * .8f; break;
                case BattleEffectType.PoisonShieldPayload: score += v * d * 1.6f; break;
                case BattleEffectType.BuffToBlock: score += player.modifiers.PositiveAttackBonus * .75f; break;
                case BattleEffectType.ConditionalBlockPayload:
                    if (player.currentHP <= 25) score += Mathf.Min(v, shortage) + Mathf.Max(0, v - shortage) * .1f;
                    break;
                case BattleEffectType.EndTurnBlock: score += v * Mathf.Min(d, 3) * .8f; break;
                case BattleEffectType.NextTurnCost: score += v * 1.8f; break;
                case BattleEffectType.ExtraDraw: score += v * Mathf.Min(d, 3) * 1.6f; break;
                case BattleEffectType.AmplifyPoison: score += v * enemy.poison.Stacks.Sum(s => Mathf.Min(s.remainingTurns, 4)) * .85f; break;
                case BattleEffectType.PreserveCost: score += Mathf.Max(0, player.currentCost - card.EffectiveCost) * .7f + d * .6f; break;
                case BattleEffectType.Adversity: score += player.currentHP <= v ? 5 : 1; break;
                case BattleEffectType.Heal: score += Mathf.Min(v, 50 - player.currentHP) * (player.currentHP < 20 ? 1.25f : .65f); break;
                case BattleEffectType.IncomingDamageModifier:
                case BattleEffectType.IncomingCardDamageBonus: score += v * 1.6f; break;
                case BattleEffectType.OutgoingDamageModifier: score -= v * 1.5f; break;
                case BattleEffectType.ReduceBlock: score += Mathf.Min(v, enemy.currentBlock); break;
            }
        }
        int absorbedSelf = Mathf.Min(player.currentBlock, Mathf.RoundToInt(selfDamage));
        if (selfDamage - absorbedSelf >= player.currentHP) return -10000;
        damage = Mathf.Max(0, damage - enemy.currentBlock);
        if (damage >= enemy.currentHP) return 10000 - card.EffectiveCost;
        score += damage - (selfDamage - absorbedSelf) * (player.currentHP < 20 ? 1.25f : .65f);
        return score / (1 + .6f * card.EffectiveCost);
    }
    void FinishRun(bool won)
    {
        var run = plan.runs[runIndex];
        report.results.Add(new Result { strategy = run.strategy, boss = run.boss, seed = run.seed, win = won, hp = player.currentHP,
            reached = battle.currentBattleCount, turns = totalTurns, enemies = encountered.ToArray() });
        File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
        runIndex++;
        if (runIndex >= plan.runs.Length) Stop(null); else BeginRun();
    }
    void Stop(string error)
    {
        report.error = error; File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
        plan = null;
        if (battle != null) battle.CancelBattle();
        Time.timeScale = previousTimeScale; Debug.unityLogger.logEnabled = previousLogging;
        Application.runInBackground = previousRunInBackground;
        foreach (var copy in enemyCopies) UnityEngine.Object.Destroy(copy);
        if (simulationRoot != null) UnityEngine.Object.Destroy(simulationRoot);
        if (enemyPrefab != null) UnityEngine.Object.Destroy(enemyPrefab);
        foreach (var sceneRoot in pausedSceneRoots) if (sceneRoot != null) sceneRoot.SetActive(true);
        if (File.Exists(Request)) File.Delete(Request);
        if (File.Exists("Temp/BalancePlayMode.plan")) File.Delete("Temp/BalancePlayMode.plan");
        EditorApplication.isPlaying = false;
    }
}
