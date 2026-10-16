using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class HitStatusVerification
{
    static HitStatusVerification() { EditorApplication.update += Poll; }
    static int checks;
    static void Check(bool result, string name) { checks++; if (!result) throw new Exception(name); }
    static void Poll()
    {
        if (!File.Exists("Temp/HitStatus.verify") || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete("Temp/HitStatus.verify");
        try { Verify(); } catch (Exception e) { File.WriteAllText("Temp/HitStatus.result", "FAIL\n" + e); Debug.LogException(e); }
    }
    [MenuItem("Tools/Statuses/Verify Bleeding Electrocution Poison")]
    public static void Verify()
    {
        checks = 0;
        var root = new GameObject("HitStatusVerification"); root.SetActive(false);
        var data = ScriptableObject.CreateInstance<EnemyData>(); data.enemyName = "Test"; data.baseMaxHP = 100;
        var cardData = ScriptableObject.CreateInstance<CardData>();
        bool logging = Debug.unityLogger.logEnabled;
        try
        {
            Debug.unityLogger.logEnabled = false;
            var player = root.AddComponent<PlayerStatus>();
            var enemy = root.AddComponent<EnemyInstance>();
            var manager = root.AddComponent<EnemyManager>();
            typeof(EnemyManager).GetField("<CurrentEnemy>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, enemy);
            var battle = root.AddComponent<BattleManager>(); battle.playerStatus = player; battle.enemyManager = manager;
            var apply = typeof(BattleManager).GetMethod("ApplyEffect", BindingFlags.Instance | BindingFlags.NonPublic);
            Action reset = () => { enemy.Setup(data, 1); player.ResetForBattle(); player.currentHP = 50; };
            Action<int> hit = damage => apply.Invoke(battle, new object[] { new BattleEffect { effectType = BattleEffectType.Damage, target = EffectTarget.Enemy, value = damage }, false, null });
            reset(); enemy.AddBleeding(5, 1); player.currentBlock = 100;
            hit(3); Check(enemy.currentHP == 95 && enemy.bleeding.Stacks[0].value == 4 && player.currentHP == 50, "Blocked hit triggers bleeding");
            hit(3); Check(enemy.currentHP == 91 && enemy.bleeding.Stacks[0].value == 3, "Second hit triggers and decays separately");
            enemy.TickModifiers(); enemy.TickPoison(); Check(enemy.bleeding.Stacks[0].value == 3, "No passive decay");
            reset(); enemy.AddBleeding(5, 1); enemy.AddBleeding(2, 2); enemy.currentBlock = 10;
            enemy.modifiers.Add(BattleEffectType.IncomingDamageModifier, 100, 3);
            hit(0); Check(enemy.currentHP == 100 && enemy.currentBlock == 3 && enemy.bleeding.Stacks.Count == 1 && enemy.bleeding.Stacks[0].value == 4, "Stack sum bypasses modifiers but consumes block");
            reset(); enemy.AddElectrocution(6, 2); enemy.AddElectrocution(3, 3); enemy.currentBlock = 50;
            var performance = new CardPerformance { effects = new List<BattleEffect>(), cost = 0 };
            cardData.lowPerformance = cardData.mediumPerformance = cardData.highPerformance = performance;
            var card = new CardInstance(cardData); card.RollPerformance();
            var attack = new BattleEffect { effectType = BattleEffectType.Damage, target = EffectTarget.Enemy, value = 1 };
            cardData.category = CardCategory.Defense;
            apply.Invoke(battle, new object[] { attack, true, card }); Check(enemy.electrocution.Stacks[0].value == 6, "Defense card damage does not trigger shock");
            cardData.category = CardCategory.Attack;
            apply.Invoke(battle, new object[] { attack, true, card }); Check(enemy.currentBlock == 39 && enemy.electrocution.Stacks.Count == 1 && enemy.electrocution.Stacks[0].value == 4, "Blocked attack card triggers stacked shock");
            apply.Invoke(battle, new object[] { attack, true, card }); Check(enemy.currentBlock == 34 && enemy.electrocution.Stacks[0].value == 2, "Shock decays per hit");
            enemy.TakeDamage(1); enemy.TickPoison(); Check(enemy.electrocution.Stacks[0].value == 2, "Retaliation/status damage never triggers shock");
            reset(); player.AddElectrocution(6, 2); player.AddBleeding(5, 1); player.currentBlock = 20;
            hit(2); Check(player.currentBlock == 12 && player.electrocution.Stacks[0].value == 4 && player.bleeding.Stacks[0].value == 5, "Enemy hit triggers player shock, not player bleeding");
            cardData.category = CardCategory.Attack; apply.Invoke(battle, new object[] { attack, true, card });
            Check(player.currentBlock == 7 && player.bleeding.Stacks[0].value == 4, "Player hit triggers player bleeding");
            reset(); player.currentBlock = 50; enemy.currentBlock = 50;
            player.modifiers.Add(BattleEffectType.IncomingDamageModifier, 99, 2); enemy.modifiers.Add(BattleEffectType.IncomingDamageModifier, 99, 2);
            player.AddPoison(2, 7); enemy.AddPoison(2, 7); player.TickPoison(); enemy.TickPoison();
            Check(player.currentHP == 43 && enemy.currentHP == 93 && player.currentBlock == 50 && enemy.currentBlock == 50, "Poison bypasses block and ignores modifiers for both sides");
            reset(); enemy.currentHP = 3; enemy.AddBleeding(5, 1); data.actionsPerTurn = 2;
            var action = ScriptableObject.CreateInstance<EnemyActionData>();
            try
            {
                action.effects = new List<BattleEffect> { attack }; data.actionPattern = new List<EnemyActionData> { action };
                Check(EnemyIntentForecast.Create(enemy, player).attackDamage == 1 && enemy.bleeding.Stacks[0].value == 5, "Forecast stops second hit after bleeding death without mutating status");
                hit(1); Check(enemy.IsDead && player.currentHP == 49, "Bleeding death after first hit");
            }
            finally { UnityEngine.Object.DestroyImmediate(action); }
            reset(); Check(!enemy.bleeding.IsActive && !enemy.electrocution.IsActive && !player.bleeding.IsActive && !player.electrocution.IsActive, "Battle reset clears statuses");
            enemy.AddElectrocution(2, 2); enemy.TriggerElectrocution(); Check(!enemy.electrocution.IsActive, "Zero stack is removed");
            reset(); player.currentHP = 3; player.AddBleeding(5, 1);
            performance.effects.Add(attack); performance.effects.Add(attack); performance.effects.Add(new BattleEffect { effectType = BattleEffectType.Heal, value = 20, target = EffectTarget.Self });
            typeof(BattleManager).GetMethod("ApplyCardEffects", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(battle, new object[] { card });
            Check(player.IsDead && enemy.currentHP == 99, "Bleeding death stops later card hits and healing");
            reset();
            foreach (var type in new[] { BattleEffectType.ApplyBleeding, BattleEffectType.ApplyElectrocution })
                apply.Invoke(battle, new object[] { new BattleEffect { effectType = type, target = EffectTarget.Enemy, value = 5, decayPerTrigger = 1 }, true, card });
            Check(enemy.bleeding.IsActive && enemy.electrocution.IsActive, "Both effect definitions can apply statuses");
            File.WriteAllText("Temp/HitStatus.result", "PASS\nChecks: " + checks);
        }
        finally
        {
            Debug.unityLogger.logEnabled = logging;
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(data); UnityEngine.Object.DestroyImmediate(cardData);
        }
    }
}
