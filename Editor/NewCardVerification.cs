using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class NewCardVerification
{
    const string Request = "Temp/NewCards.verify";
    static NewCardVerification() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Verify(); File.WriteAllText("Temp/NewCards.result", "PASS: card assets, pools, combat effects, timing, stacking, cost display and reset"); }
        catch (Exception e) { File.WriteAllText("Temp/NewCards.result", e.ToString()); Debug.LogException(e); }
    }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static BattleEffect E(BattleEffectType type, int value = 0, int duration = 0, EffectTarget target = EffectTarget.Self)
        => new BattleEffect { effectType = type, value = value, duration = duration, target = target };

    [MenuItem("Tools/Cards/Verify New Cards")]
    public static void Verify()
    {
        string[] names = { "Kougekiryokuagenagarabougyo", "Tsuikaburokku", "Togeburokku", "Dokunotate", "Bafutenkanbougyo", "Pinchinaratsuikabougyo", "Chikusekibougyo", "Bougyokyouka", "Kosutokyouka", "Dorookyouka", "Dokuzoufuku", "Kosutoonzon", "Gyakkyou", "Kaifuku", "Jakutai", "Zeijaku", "Kuzushi" };
        foreach (string name in names)
        {
            string path = "Assets/ScriptableObject/CardData/" + name + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<CardData>(path);
            Require(data != null && Enum.IsDefined(typeof(CardRarity), data.rarity), "Card missing or invalid rarity: " + name);
            foreach (var performance in new[] { data.lowPerformance, data.mediumPerformance, data.highPerformance })
            {
                Require(performance.effects.Count > 0 && performance.cost >= 0, "Performance: " + name);
                var instance = new CardInstance(data);
                typeof(CardInstance).GetProperty("CurrentPerformance").SetValue(instance, performance);
                typeof(CardInstance).GetProperty("IsPerformanceDetermined").SetValue(instance, true);
                Require(!instance.GetDescription().Contains("?") && !instance.GetDescription().Contains("{value"), "Description: " + name);
            }
            string guid = AssetDatabase.AssetPathToGUID(path);
            foreach (string scene in new[] { "Assets/TitleScene.unity", "Assets/Scenes/RuinsStagePreview.unity", "Assets/Scenes/RuinsStageRefined.unity" })
                Require(File.ReadAllText(scene).Split(new[] { guid }, StringSplitOptions.None).Length - 1 ==
                    (new[] { "Kougekiryokuagenagarabougyo", "Kosutokyouka", "Dorookyouka", "Jakutai", "Zeijaku" }.Contains(name) ? 3 : 1), "Pool registration: " + name);
        }

        var root = new GameObject("NewCardVerification");
        root.SetActive(false);
        var dataEnemy = ScriptableObject.CreateInstance<EnemyData>();
        var dataCard = ScriptableObject.CreateInstance<CardData>();
        try
        {
            var player = root.AddComponent<PlayerStatus>();
            var enemy = root.AddComponent<EnemyInstance>();
            var manager = root.AddComponent<EnemyManager>();
            var battle = root.AddComponent<BattleManager>();
            dataEnemy.enemyName = "Verification"; dataEnemy.baseMaxHP = 100; dataEnemy.scalingFactor = 1;
            enemy.Setup(dataEnemy, 1);
            typeof(EnemyManager).GetField("<CurrentEnemy>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, enemy);
            battle.playerStatus = player; battle.enemyManager = manager;
            var apply = typeof(BattleManager).GetMethod("ApplyEffect", BindingFlags.Instance | BindingFlags.NonPublic);
            var cardApply = typeof(BattleManager).GetMethod("ApplyCardEffects", BindingFlags.Instance | BindingFlags.NonPublic);
            Action<BattleEffect, bool> effect = (e, fromPlayer) => apply.Invoke(battle, new object[] { e, fromPlayer, fromPlayer ? new CardInstance(dataCard) : null });
            Action<BattleEffect[]> card = effects =>
            {
                dataCard.lowPerformance = dataCard.mediumPerformance = dataCard.highPerformance = new CardPerformance { cost = 2, effects = effects.ToList() };
                var instance = new CardInstance(dataCard); instance.RollPerformance();
                cardApply.Invoke(battle, new object[] { instance });
            };

            card(new[] { E(BattleEffectType.CardBlockBonus, 2, 2) });
            effect(E(BattleEffectType.GainBlock, 3), true);
            Require(player.currentBlock == 5, "Card block bonus includes non-defense cards");
            card(new[] { E(BattleEffectType.EndTurnBlock, 3, 1), E(BattleEffectType.Thorns, 4, 1), E(BattleEffectType.PoisonShield, 0, 1), E(BattleEffectType.PoisonShieldPayload, 2, 3) });
            player.cardStatus.EndPlayerTurn(player); player.TickModifiers();
            Require(player.currentBlock == 8 && player.cardStatus.endTurnBlocks.Count == 0, "Accumulation excludes block bonus and ticks once");
            player.modifiers.Add(BattleEffectType.CardDamageBonus, 20, 5);
            effect(E(BattleEffectType.Damage, 2, 0, EffectTarget.Enemy), false);
            Require(player.currentHP == 50 && enemy.currentHP == 96 && enemy.poison.Stacks.Single().damagePerTurn == 2, "Fully blocked attack triggers fixed thorns and poison");
            player.AddPoison(1, 1); player.TickPoison();
            Require(enemy.currentHP == 96 && enemy.poison.Stacks.Count == 1, "Poison does not trigger reactions");
            player.cardStatus.EndEnemyTurn();
            Require(player.cardStatus.reactions.Count == 0, "One-turn reactions expire after enemy turn");
            enemy.AddPoison(2, 3); effect(E(BattleEffectType.AmplifyPoison, 1), true);
            Require(enemy.poison.Stacks.Sum(s => s.damagePerTurn) == 7, "Amplifies each poison stack");

            player.ResetForBattle(); player.currentBlock = 0;
            player.modifiers.Add(BattleEffectType.CardDamageBonus, 3, 2);
            player.modifiers.Add(BattleEffectType.CardDamageBonus, 5, 4);
            card(new[] { E(BattleEffectType.GainBlock, 2), E(BattleEffectType.BuffToBlock) });
            Require(player.currentBlock == 10, "Attack buffs sum without duration weighting");
            player.currentHP = 25;
            card(new[] { E(BattleEffectType.LowHPBlock, 2), E(BattleEffectType.ConditionalBlockPayload, 6) });
            Require(player.currentBlock == 16, "Half HP boundary included");
            player.currentHP = 26;
            card(new[] { E(BattleEffectType.LowHPBlock, 2), E(BattleEffectType.ConditionalBlockPayload, 6) });
            Require(player.currentBlock == 16, "Above boundary excludes conditional block");

            card(new[] { E(BattleEffectType.NextTurnCost, 3), E(BattleEffectType.ExtraDraw, 2, 2), E(BattleEffectType.PreserveCost), E(BattleEffectType.PreserveCost) });
            player.currentCost = 2; player.cardStatus.EndPlayerTurn(player);
            Require(player.cardStatus.nextTurnCost == 5 && !player.cardStatus.preserveCost, "Cost reservation stacks; preservation occurs once");
            Require(player.cardStatus.BeginTurnDrawBonus() == 2 && player.cardStatus.BeginTurnDrawBonus() == 2 && player.cardStatus.BeginTurnDrawBonus() == 0, "Draw buff applies for exactly two future turns");
            card(new[] { E(BattleEffectType.Adversity, 30), E(BattleEffectType.Adversity, 20) });
            player.currentHP = 20;
            Require(player.cardStatus.CostReduction(player.currentHP) == 2, "Adversity stacks");
            effect(E(BattleEffectType.Heal, 15), true);
            Require(player.cardStatus.CostReduction(player.currentHP) == 0, "Healing dynamically disables adversity");
            player.currentHP = 19; Require(player.cardStatus.CostReduction(player.currentHP) == 2, "Adversity reactivates below thresholds");
            var costCard = new CardInstance(dataCard); costCard.RollPerformance(); costCard.CostReduction = 10;
            Require(costCard.EffectiveCost == 0, "Card cost floor");

            enemy.modifiers.Add(BattleEffectType.IncomingCardDamageBonus, 4, 2); enemy.currentBlock = 0;
            int hp = enemy.currentHP; enemy.TakeDamage(2, isCard: true); Require(enemy.currentHP == hp - 6, "Vulnerability adds to card damage");
            hp = enemy.currentHP; enemy.TakeDamage(2); Require(enemy.currentHP == hp - 2, "Vulnerability excludes thorns");
            hp = enemy.currentHP; enemy.TakeDamage(2, isPoison: true); Require(enemy.currentHP == hp - 2, "Vulnerability excludes poison");
            enemy.currentBlock = 20; effect(E(BattleEffectType.ReduceBlock, 8, 0, EffectTarget.Enemy), true);
            Require(enemy.currentBlock == 12, "Remove specified block amount");
            hp = enemy.currentHP;
            effect(E(BattleEffectType.ReduceBlock, 15, 0, EffectTarget.Enemy), true);
            Require(enemy.currentBlock == 0 && enemy.currentHP == hp, "Block reduction floor; no HP damage");
            enemy.currentBlock = 50; effect(E(BattleEffectType.RemoveBlock, 0, 0, EffectTarget.Enemy), true);
            Require(enemy.currentBlock == 0, "Remove all block");
            player.cardStatus.nextTurnCost = 0;
            card(new[] { E(BattleEffectType.PreserveCost, 0, 3), E(BattleEffectType.PreserveCost, 0, 1) });
            player.currentCost = 2;
            player.cardStatus.EndPlayerTurn(player);
            Require(player.cardStatus.nextTurnCost == 2 && player.cardStatus.preserveCostTurns == 2, "Longer reservation survives shorter reapplication");
            player.cardStatus.nextTurnCost = 0; player.currentCost = 4;
            player.cardStatus.EndPlayerTurn(player);
            Require(player.cardStatus.nextTurnCost == 4 && player.cardStatus.preserveCostTurns == 1, "Second turn reserves that turn's unused cost once");
            player.cardStatus.nextTurnCost = 0; player.currentCost = 1;
            player.cardStatus.EndPlayerTurn(player);
            Require(player.cardStatus.nextTurnCost == 1 && !player.cardStatus.preserveCost, "Three-turn reservation expires after third turn");
            enemy.modifiers.Add(BattleEffectType.OutgoingDamageModifier, -3, 2);
            player.currentBlock = 0; hp = player.currentHP;
            effect(E(BattleEffectType.Damage, 5, 0, EffectTarget.Enemy), false);
            Require(player.currentHP == hp - 2, "Weakness reduces enemy attack");
            player.ResetForBattle();
            Require(player.cardStatus.adversityThresholds.Count == 0 && player.cardStatus.nextTurnCost == 0 && player.cardStatus.extraDraws.Count == 0, "New battle clears all card effects");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(dataEnemy); UnityEngine.Object.DestroyImmediate(dataCard); }
    }
}
