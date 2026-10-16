using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyBehaviorVerification
{
    const string Request = "Temp/EnemyBehavior.verify";
    const string Report = "StageReview/Enemies/behavior-verification.txt";
    static int checks;
    static EnemyBehaviorVerification() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Verify(); }
        catch (Exception e) { File.WriteAllText(Report, "FAIL\n" + e); Debug.LogException(e); }
    }
    static void Require(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static BattleEffect E(BattleEffectType t, int v, int d = 0, EffectTarget target = EffectTarget.Enemy, int extra = 0)
        => new BattleEffect { effectType = t, value = v, duration = d, target = target, valuePerExtraCard = extra };

    [MenuItem("Tools/Enemies/Verify Behavior")]
    public static void Verify()
    {
        checks = 0;
        string[] ids = { "Enemy1", "Enemy2", "Enemy3", "Enemy4", "Enemy5", "Enemy6", "Boss1", "Boss2" };
        var loaded = ids.Select(id => AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/ScriptableObject/EnemyData/" + id + ".asset")).ToArray();
        foreach (var asset in loaded)
        {
            Require(asset != null && asset.enemyPrefab != null && asset.baseMaxHP > 0 && asset.actionPattern.Count > 0, "Enemy asset/visual/pattern");
            foreach (var rosterAction in asset.actionPattern.Concat(asset.categoryActions))
            {
                Require(rosterAction != null && rosterAction.effects.Count > 0, "Missing action");
                foreach (var effect in rosterAction.effects) Require(Enum.IsDefined(typeof(BattleEffectType), effect.effectType), "Effect enum");
            }
        }
        Require(loaded.Select(e => e.enemyName).Distinct().Count() == 8, "Distinct names");
        Require(loaded[2].actionsPerTurn == 2 && loaded[7].reactsToPlayerCards && loaded[7].categoryActions.Count == 4, "Duo/demon definitions");
        var root = new GameObject("EnemyBehaviorVerification"); root.SetActive(false);
        var owned = new List<UnityEngine.Object>();
        var data = ScriptableObject.CreateInstance<EnemyData>(); owned.Add(data);
        var action = ScriptableObject.CreateInstance<EnemyActionData>(); owned.Add(action);
        bool logging = Debug.unityLogger.logEnabled;
        try
        {
            Debug.unityLogger.logEnabled = false;
            var player = root.AddComponent<PlayerStatus>();
            var enemy = root.AddComponent<EnemyInstance>();
            var manager = root.AddComponent<EnemyManager>();
            var savedRandom = UnityEngine.Random.state;
            try
            {
                manager.normalEnemies = loaded.Take(6).ToList(); manager.bossEnemies = loaded.Skip(6).ToList();
                for (int sample = 0; sample < 100; sample++)
                {
                    var draws = Enumerable.Range(1, 9).Select(n => manager.SelectEnemyData(n)).ToArray();
                    Require(draws.Take(6).Distinct().Count() == 6, "First cycle contains all six normal enemies");
                    Require(draws.Skip(6).Distinct().Count() == 3, "Second cycle draws without replacement");
                    Require(draws[5] != draws[6], "No consecutive enemy at cycle boundary");
                    Require(manager.bossEnemies.Contains(manager.SelectEnemyData(10)), "Final fight selects a boss");
                }
            }
            finally { UnityEngine.Random.state = savedRandom; }
            var battle = root.AddComponent<BattleManager>(); battle.playerStatus = player; battle.enemyManager = manager;
            typeof(EnemyManager).GetField("<CurrentEnemy>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, enemy);
            var apply = typeof(BattleManager).GetMethod("ApplyEffect", BindingFlags.Instance | BindingFlags.NonPublic);
            var applyAction = typeof(BattleManager).GetMethod("ApplyEnemyAction", BindingFlags.Instance | BindingFlags.NonPublic);
            Action<BattleEffect> effect = e => apply.Invoke(battle, new object[] { e, false, null });
            Action reset = () => { data.enemyName = "Test"; data.baseMaxHP = 100; data.scalingFactor = 1; data.reactsToPlayerCards = false;
                data.actionsPerTurn = 1; data.actionPattern = new List<EnemyActionData> { action }; enemy.Setup(data, 1); player.ResetForBattle(); player.currentHP = 50; };
            reset();
            effect(E(BattleEffectType.PermanentAttackBonus, 3, target: EffectTarget.Self));
            enemy.TickModifiers(); effect(E(BattleEffectType.Damage, 5));
            Require(player.currentHP == 42 && enemy.PermanentAttackBonus == 3, "Permanent rage affects attacks and survives turns");
            reset(); Require(enemy.PermanentAttackBonus == 0, "Rage resets between battles");

            enemy.currentHP = 60; player.currentBlock = 10;
            effect(E(BattleEffectType.LifestealDamage, 10, 50));
            Require(player.currentHP == 50 && enemy.currentHP == 60, "Fully blocked bite never heals");
            player.currentBlock = 4; effect(E(BattleEffectType.LifestealDamage, 10, 50));
            Require(player.currentHP == 44 && enemy.currentHP == 63, "Partial block heals only actual HP damage");
            player.currentHP = 3; effect(E(BattleEffectType.LifestealDamage, 10, 50));
            Require(player.currentHP == 0 && enemy.currentHP == 64, "Overkill is excluded and fractional heal rounds down");
            reset(); enemy.currentHP = 99; effect(E(BattleEffectType.LifestealDamage, 10, 100));
            Require(enemy.currentHP == 100, "Healing cannot exceed max HP");
            reset(); enemy.currentHP = 60; player.AddPoison(2, 5); player.TickPoison();
            Require(enemy.currentHP == 60, "Poison does not heal vampire");

            reset(); enemy.currentBlock = 12; player.currentBlock = 20;
            player.cardStatus.reactions.Add(new PlayerCardStatus.Reaction { damage = 4, turns = 1 });
            effect(E(BattleEffectType.ConsumeBlockDamage, 5));
            Require(enemy.currentBlock == 0 && player.currentBlock == 3 && enemy.currentHP == 96, "Conversion consumes defense before retaliation");
            reset(); enemy.currentBlock = 10; enemy.AddPoison(1, 3);
            action.effects = new List<BattleEffect> { E(BattleEffectType.ConsumeBlockDamage, 5) };
            Require(EnemyIntentForecast.Create(enemy, player).attackDamage == 15, "Poison bypasses block before conversion");
            Require(enemy.currentBlock == 10 && enemy.poison.Stacks.Count == 1, "Forecast has no side effects");
            enemy.TickPoison(); effect(action.effects[0]); Require(player.currentHP == 35 && enemy.currentBlock == 0, "Conversion matches forecast");

            reset(); data.actionsPerTurn = 2;
            action.effects = new List<BattleEffect> { E(BattleEffectType.Damage, 4), E(BattleEffectType.ApplyPoison, 2, 3) };
            player.currentBlock = 20; player.cardStatus.reactions.Add(new PlayerCardStatus.Reaction { damage = 5, turns = 1 });
            applyAction.Invoke(battle, new object[] { action }); applyAction.Invoke(battle, new object[] { action });
            Require(enemy.currentHP == 90 && player.currentBlock == 12 && player.poison.Stacks.Count == 2, "Each independent hit triggers retaliation, even fully blocked");
            player.TickPoison(); player.cardStatus.EndEnemyTurn(); enemy.TickModifiers();
            Require(player.currentBlock == 12 && player.currentHP == 46 && player.cardStatus.reactions.Count == 0 && player.poison.Stacks.All(p => p.remainingTurns == 2), "Poison bypasses block and durations tick once per two-action turn");
            reset(); enemy.currentHP = 3; player.currentBlock = 20;
            player.cardStatus.reactions.Add(new PlayerCardStatus.Reaction { damage = 5, turns = 1 });
            applyAction.Invoke(battle, new object[] { action });
            Require(enemy.IsDead && player.poison.Stacks.Count == 0, "Death by thorns prevents subsequent effects");
            reset(); player.currentHP = 2;
            applyAction.Invoke(battle, new object[] { action });
            Require(player.IsDead && player.poison.Stacks.Count == 0, "Player death prevents subsequent effects");

            reset(); data.reactsToPlayerCards = true; data.categoryActions = loaded[7].categoryActions;
            Require(enemy.GetNextAction() == action, "No cards uses normal action");
            enemy.RecordPlayerCard(CardCategory.Attack); enemy.RecordPlayerCard(CardCategory.Defense);
            Require(enemy.DominantCategory == CardCategory.Defense, "Tie uses last played category");
            enemy.RecordPlayerCard(CardCategory.Attack); enemy.RecordPlayerCard(CardCategory.Defense); enemy.RecordPlayerCard(CardCategory.Buff);
            Require(enemy.DominantCategory == CardCategory.Defense, "Last use among tied dominant categories wins; minority last card does not override");
            int original = enemy.GetNextAction().effects[0].value;
            Require(enemy.ResolveEffects(enemy.GetNextAction())[0].value == original + enemy.GetNextAction().effects[0].valuePerExtraCard * 4, "Reactive strength uses total played cards");
            Require(enemy.GetNextAction().effects[0].value == original, "Shared action asset is immutable");
            var intent = EnemyIntentForecast.Create(enemy, player);
            Require(intent.text.Contains("防御") && intent.text.Contains("使用5枚"), "Demon forecast names category and card count");
            enemy.BeginPlayerTurn(); Require(enemy.CardsPlayedThisTurn == 0 && enemy.CategoryCount(CardCategory.Defense) == 0 && enemy.GetNextAction() == action, "Counts reset every player turn");

            reset(); action.effects = new List<BattleEffect> { E(BattleEffectType.PermanentAttackBonus, 2, target: EffectTarget.Self), E(BattleEffectType.Damage, 5) };
            player.modifiers.Add(BattleEffectType.IncomingDamageModifier, 3, 1);
            Require(EnemyIntentForecast.Create(enemy, player).attackDamage == 7, "Forecast resolves buff before attack and expires player modifier at turn end");
            player.TickModifiers(); applyAction.Invoke(battle, new object[] { action }); Require(player.currentHP == 43, "Buff/attack matches forecast");
            reset(); action.effects = new List<BattleEffect> { E(BattleEffectType.Damage, 5) }; data.actionsPerTurn = 2;
            enemy.currentHP = 3; player.cardStatus.reactions.Add(new PlayerCardStatus.Reaction { damage = 4, turns = 1 });
            Require(EnemyIntentForecast.Create(enemy, player).attackDamage == 5, "Forecast omits second action after lethal retaliation");
            reset(); data.actionsPerTurn = 2; action.effects = new List<BattleEffect> { E(BattleEffectType.Damage, 5) };
            var remainingIntent = EnemyIntentForecast.Create(enemy, player, false, 1, 2);
            Require(remainingIntent.attackDamage == 5 && remainingIntent.text.StartsWith("2. "), "Remaining-action forecast shows only the second hit");
            enemy.currentHP = 3; player.cardStatus.reactions.Add(new PlayerCardStatus.Reaction { damage = 5, turns = 1 });
            action.effects.Insert(0, E(BattleEffectType.IncomingDamageModifier, -3, 2, EffectTarget.Self));
            Require(EnemyIntentForecast.Create(enemy, player).attackDamage == 10, "Forecast resolves newly gained armor before retaliation");

            reset(); effect(E(BattleEffectType.CardBlockBonus, -3, 3));
            effect(E(BattleEffectType.CardCostModifier, 1, 2));
            Require(player.CardCostReduction == -1, "Cost curse raises cost for the next player turn");
            player.TickModifiers();
            Require(player.CardCostReduction == -1, "Cost curse survives its first player turn");
            player.TickModifiers();
            Require(player.CardCostReduction == 0, "Cost curse expires after its second player turn");
            reset(); effect(E(BattleEffectType.CardBlockBonus, -3, 3));
            Require(player.cardStatus.BlockBonus == -3, "Shaman erosion affects every card block source");
            effect(E(BattleEffectType.OutgoingPoisonDurationModifier, -1, 3));
            Require(player.modifiers.outgoingPoisonDurationModifier == -1, "Shaman poison-duration curse is active");
            effect(E(BattleEffectType.ApplyPoison, 2, 3));
            Require(player.poison.Stacks[0].remainingTurns == 3, "Player curse never changes enemy poison duration");

            var textObject = new GameObject("IntentLayoutVerification", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(root.transform, false);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/NotoSansJP-Medium SDF.asset");
            Require(text.font != null, "Japanese HUD font");
            text.fontSize = 14; text.enableWordWrapping = true;
            text.rectTransform.sizeDelta = new Vector2(442, 104);
            foreach (var definition in loaded)
            {
                enemy.Setup(definition, 1); player.ResetForBattle(); player.currentHP = 50;
                int variants = definition.reactsToPlayerCards ? 4 : definition.actionPattern.Count;
                for (int variant = 0; variant < variants; variant++)
                {
                    if (definition.reactsToPlayerCards)
                    {
                        enemy.BeginPlayerTurn();
                        for (int i = 0; i < 20; i++) enemy.RecordPlayerCard((CardCategory)variant);
                    }
                    enemy.currentBlock = 120;
                    string sample = EnemyIntentForecast.Create(enemy, player).text;
                    Require(text.GetPreferredValues(sample, 442, Mathf.Infinity).y <= 104, "Intent overflow: " + definition.enemyName + ": " + sample);
                    if (!definition.reactsToPlayerCards) enemy.AdvanceActionIndex();
                }
            }

            Directory.CreateDirectory("StageReview/Enemies");
            File.WriteAllText(Report, $"PASS\nChecks: {checks}\nAll eight assets, rage, conversion, lifesteal, retaliation/death ordering, two-hit timing, curses, dynamic demon ties/counts and nonmutating forecasts.\nAll action variants and demon responses at 20 played cards fit the 442x104 Japanese intent panel.\n");
        }
        finally
        {
            Debug.unityLogger.logEnabled = logging;
            UnityEngine.Object.DestroyImmediate(root);
            foreach (var obj in owned) UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
