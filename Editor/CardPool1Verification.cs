using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CardPool1Verification
{
    static readonly string[] Names = { "Kandenkougeki", "Renzokugiri", "Rensakougeki", "Shugeki", "Kougokougeki", "Kadenryu", "Saikido", "Renkeibougyo", "Judenbougyo", "Junkanbougyo", "Henkanbougyo", "Rensakyouka", "Kandenzoufuku", "Keiryoka", "Junkan", "Kosutokangen", "Shuruirenkei", "Kanden", "Doden", "Kafuka" };
    static int checks;
    static CardPool1Verification() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists("Temp/CardPool1.verify") || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete("Temp/CardPool1.verify");
        try { Verify(); }
        catch (Exception e) { File.WriteAllText("StageReview/CardPool1/verification.txt", "FAIL\n" + e); Debug.LogException(e); }
    }
    static void Check(bool valid, string name) { checks++; if (!valid) throw new Exception(name); }
    static BattleEffect E(BattleEffectType type, int value = 0, int duration = 0, EffectTarget target = EffectTarget.Self, int secondary = 0, int decay = 2)
        => new BattleEffect { effectType = type, value = value, duration = duration, target = target, secondaryValue = secondary, decayPerTrigger = decay };
    static CardInstance Instance(CardData data, CardPerformance tier)
    {
        var card = new CardInstance(data);
        typeof(CardInstance).GetProperty("CurrentPerformance").SetValue(card, tier);
        typeof(CardInstance).GetProperty("IsPerformanceDetermined").SetValue(card, true);
        return card;
    }
    [MenuItem("Tools/Cards/Verify Pool 1")]
    public static void Verify()
    {
        checks = 0;
        var assets = Names.Select(n => AssetDatabase.LoadAssetAtPath<CardData>("Assets/ScriptableObject/CardData/" + n + ".asset")).ToArray();
        Check(assets.All(a => a != null), "All twenty cards loaded");
        Check(assets.Count(a => a.category == CardCategory.Attack) == 7 && assets.Count(a => a.category == CardCategory.Defense) == 4
            && assets.Count(a => a.category == CardCategory.Buff) == 6 && assets.Count(a => a.category == CardCategory.Debuff) == 3, "Category counts");
        foreach (var data in assets)
        {
            Check(Enum.IsDefined(typeof(CardRarity), data.rarity), "Valid rarity: " + data.cardName);
            var descriptions = new List<string>();
            foreach (var tier in new[] { data.lowPerformance, data.mediumPerformance, data.highPerformance })
            {
                Check(tier.cost >= 0 && tier.effects.Count > 0 && tier.effects.All(e => Enum.IsDefined(typeof(BattleEffectType), e.effectType)), "Valid effects: " + data.cardName);
                string description = Instance(data, tier).GetDescription();
                Check(!description.Contains("{") && !description.Contains("?"), "Resolved description: " + data.cardName);
                descriptions.Add(description);
            }
            Check(descriptions.Distinct().Count() == 3, "Distinct performances: " + data.cardName);
        }
        var ownGuids = Names.Select(n => AssetDatabase.AssetPathToGUID("Assets/ScriptableObject/CardData/" + n + ".asset")).ToArray();
        foreach (var scene in new[] { "Assets/TitleScene.unity", "Assets/Scenes/RuinsStagePreview.unity", "Assets/Scenes/RuinsStageRefined.unity" })
        {
            string text = File.ReadAllText(scene);
            Func<string, string[]> pool = name => Regex.Matches(Regex.Match(text, @"(?m)^  " + name + @":\r?\n(?:  -[^\r\n]*\r?\n)+").Value, @"guid: (\w+)").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
            var pools = new[] { pool("balancePool"), pool("attackHeavyPool"), pool("defenseHeavyPool") };
            Check(pools.All(p => p.Length == 30 && p.Distinct().Count() == 30), "Each pool has thirty unique cards: " + scene);
            Check(pools[0].Take(10).SequenceEqual(pools[1].Take(10)) && pools[0].Take(10).All(g => pools[2].Contains(g)), "Common ten preserved: " + scene);
            Check(ownGuids.All(g => pools[0].Contains(g) && !pools[1].Contains(g) && !pools[2].Contains(g)), "New cards only in pool 1: " + scene);
        }
        var root = new GameObject("CardPool1Verification"); root.SetActive(false);
        var enemyData = ScriptableObject.CreateInstance<EnemyData>(); enemyData.enemyName = "Test"; enemyData.baseMaxHP = 1000; enemyData.scalingFactor = 1;
        var owned = new List<CardData>();
        bool logging = Debug.unityLogger.logEnabled;
        var randomState = UnityEngine.Random.state;
        try
        {
            Debug.unityLogger.logEnabled = false;
            var player = root.AddComponent<PlayerStatus>();
            var enemy = root.AddComponent<EnemyInstance>();
            var manager = root.AddComponent<EnemyManager>();
            typeof(EnemyManager).GetField("<CurrentEnemy>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, enemy);
            var battle = root.AddComponent<BattleManager>(); battle.playerStatus = player; battle.enemyManager = manager;
            var hand = (List<CardInstance>)typeof(BattleManager).GetField("hand", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(battle);
            var draw = (List<CardInstance>)typeof(BattleManager).GetField("drawPile", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(battle);
            var discard = (List<CardInstance>)typeof(BattleManager).GetField("discardPile", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(battle);
            Action reset = () => { enemy.Setup(enemyData, 1); player.ResetForBattle(); player.currentHP = player.maxHP = 50; player.currentCost = 100; player.cardStatus.BeginPlayerTurn(); hand.Clear(); draw.Clear(); discard.Clear(); };
            Func<CardCategory, int, BattleEffect[], CardInstance> synthetic = (category, cost, effects) =>
            {
                var data = ScriptableObject.CreateInstance<CardData>(); owned.Add(data); data.category = category;
                var tier = new CardPerformance { cost = cost, effects = effects.ToList() }; data.lowPerformance = data.mediumPerformance = data.highPerformance = tier;
                return Instance(data, tier);
            };
            Action<CardInstance> play = card =>
            {
                typeof(BattleManager).GetProperty("State").SetValue(battle, BattleTurnState.Player); hand.Add(card);
                Check(battle.TryPlayCard(card), "Card successfully used");
            };
            Action<CardCategory, int, BattleEffect[]> use = (category, cost, effects) => play(synthetic(category, cost, effects));
            Action plain = () => use(CardCategory.Defense, 0, new BattleEffect[0]);

            reset(); use(CardCategory.Attack, 2, new[] { E(BattleEffectType.PlayedCardsDamage, 4, target: EffectTarget.Enemy) });
            Check(enemy.currentHP == 1000 && player.cardStatus.CardsPlayedThisTurn == 1 && player.cardStatus.CostSpentThisTurn == 2, "Chain excludes itself and tracks paid cost after effect");
            use(CardCategory.Attack, 3, new[] { E(BattleEffectType.SpentCostDamage, 4, target: EffectTarget.Enemy) });
            Check(enemy.currentHP == 992 && player.cardStatus.CostSpentThisTurn == 5, "Assault excludes own cost");
            use(CardCategory.Defense, 1, new[] { E(BattleEffectType.PlayedCardsBlock, 3) });
            Check(player.currentBlock == 6, "Linked defense uses previously played cards only");
            use(CardCategory.Defense, 2, new[] { E(BattleEffectType.GainBlock, 4), E(BattleEffectType.SpentCostBlock, 2) });
            Check(player.currentBlock == 22, "Conversion defense excludes own payment");
            player.cardStatus.BeginPlayerTurn();
            Check(player.cardStatus.CardsPlayedThisTurn == 0 && player.cardStatus.CostSpentThisTurn == 0 && !player.cardStatus.PreviousCategory.HasValue, "Turn counters reset");

            reset(); use(CardCategory.Attack, 1, new[] { E(BattleEffectType.AlternatingDamage, 4, target: EffectTarget.Enemy, secondary: 5) });
            Check(enemy.currentHP == 996, "First attack has no alternating bonus");
            plain(); use(CardCategory.Attack, 1, new[] { E(BattleEffectType.AlternatingDamage, 4, target: EffectTarget.Enemy, secondary: 5) });
            Check(enemy.currentHP == 987, "Alternating bonus after defense");
            use(CardCategory.Attack, 1, new[] { E(BattleEffectType.AlternatingDamage, 4, target: EffectTarget.Enemy, secondary: 5) });
            Check(enemy.currentHP == 983, "No alternating bonus after attack");
            plain(); player.cardStatus.BeginPlayerTurn(); use(CardCategory.Attack, 1, new[] { E(BattleEffectType.AlternatingDamage, 4, target: EffectTarget.Enemy, secondary: 5) });
            Check(enemy.currentHP == 979, "Previous turn category ignored");

            reset(); enemy.AddElectrocution(6, 2); enemy.AddElectrocution(3, 1); play(Instance(assets[1], assets[1].lowPerformance));
            Check(enemy.currentHP == 979 && enemy.electrocution.TotalValue == 3, "Double slash triggers and decays shock per hit");
            reset(); enemy.AddElectrocution(6, 2); enemy.AddElectrocution(3, 1);
            use(CardCategory.Attack, 1, new[] { E(BattleEffectType.ElectrocutionBonusDamage, 3, target: EffectTarget.Enemy, secondary: 2) });
            Check(enemy.currentHP == 970 && enemy.electrocution.TotalValue == 6, "Overcurrent sums current shock then triggers one hit");
            reset(); enemy.currentBlock = 30; enemy.AddElectrocution(6, 2); play(Instance(assets[0], assets[0].lowPerformance));
            Check(enemy.currentBlock == 30-assets[0].lowPerformance.effects[0].value-6 && enemy.electrocution.Stacks.Count == 2 && enemy.electrocution.TotalValue == 4+assets[0].lowPerformance.effects[1].value, "Shock attack consumes existing shock before adding new stack, fully blocked");
            reset(); play(Instance(assets[12], assets[12].lowPerformance)); play(Instance(assets[17], assets[17].lowPerformance));
            int appliedShock=assets[12].lowPerformance.effects[0].value+assets[17].lowPerformance.effects[0].value;
            Check(enemy.electrocution.TotalValue == appliedShock, "Shock amplification affects direct card application");
            use(CardCategory.Debuff, 0, new[] { E(BattleEffectType.ApplyElectrocution, 4) });
            Check(player.electrocution.TotalValue == 4 && enemy.electrocution.TotalValue == appliedShock, "Enemy-directed amplification does not enhance self-applied shock");
            play(Instance(assets[8], assets[8].lowPerformance));
            appliedShock+=assets[8].lowPerformance.effects[1].value;
            Check(enemy.electrocution.TotalValue == appliedShock && player.currentBlock == assets[8].lowPerformance.effects[0].value, "Charging defense amplifies existing stacks without application buff");
            play(Instance(assets[18], assets[18].lowPerformance)); Check(enemy.electrocution.TotalValue == appliedShock+assets[18].lowPerformance.effects[0].value, "Conduction increases every existing stack");
            reset(); enemy.AddElectrocution(6, 4); enemy.AddElectrocution(3, 1); play(Instance(assets[19], assets[19].highPerformance));
            Check(enemy.electrocution.Stacks.All(s => s.decayPerTrigger == 1) && enemy.electrocution.TotalValue == 9, "Overload decay floor one and values preserved");

            reset(); use(CardCategory.Buff, 0, new[] { E(BattleEffectType.LightweightCards, 3) });
            use(CardCategory.Buff, 0, new[] { E(BattleEffectType.LightweightCards, 3) });
            Check(player.cardStatus.LightweightReduction == 2 && player.cardStatus.lightweightRemaining.SequenceEqual(new[] { 2, 3 }), "Lightweight activation consumes old stack only");
            use(CardCategory.Defense, 3, new BattleEffect[0]);
            Check(player.currentCost == 99 && player.cardStatus.CostSpentThisTurn == 1 && player.cardStatus.LightweightReduction == 2, "Overlapping lightweights reduce actual payment by two");
            plain(); Check(player.cardStatus.LightweightReduction == 1 && player.cardStatus.lightweightRemaining.Single() == 1, "Zero-cost use consumes both, then remaining one at minus one");
            use(CardCategory.Defense, 3, new BattleEffect[0]); Check(player.currentCost == 97 && player.cardStatus.CostSpentThisTurn == 3 && player.cardStatus.LightweightReduction == 0, "Remaining stack reduces next card by one");
            reset(); use(CardCategory.Buff, 0, new[] { E(BattleEffectType.LightweightCards, 2) });
            var unaffordable = synthetic(CardCategory.Defense, 4, new BattleEffect[0]); player.currentCost = 0;
            typeof(BattleManager).GetProperty("State").SetValue(battle, BattleTurnState.Player); hand.Add(unaffordable);
            Check(!battle.TryPlayCard(unaffordable) && player.cardStatus.lightweightRemaining.Single() == 2 && player.cardStatus.CardsPlayedThisTurn == 1, "Rejected use does not consume lightweight or count");

            reset(); use(CardCategory.Buff, 1, new[] { E(BattleEffectType.EveryCardsCostRecovery, 3, 3) }); plain(); plain();
            use(CardCategory.Buff, 1, new[] { E(BattleEffectType.EveryCardsCostRecovery, 3, 3) });
            Check(player.currentCost == 99 && player.cardStatus.CostSpentThisTurn == 2 && player.cardStatus.usageBuffs[0].progress == 0 && player.cardStatus.usageBuffs[1].progress == 0, "Old refund counts new activation, new effect starts afterward and refund does not subtract spent cost");
            plain(); player.cardStatus.EndPlayerTurn(player); player.cardStatus.BeginPlayerTurn(); plain();
            Check(player.cardStatus.usageBuffs.All(b => b.progress == 2), "Per-effect progress carries across turns");
            plain(); Check(player.currentCost == 101 && player.cardStatus.usageBuffs.All(b => b.progress == 0), "Stacked refund effects trigger independently");
            reset(); use(CardCategory.Buff, 1, new[] { E(BattleEffectType.EveryCardsDraw, 2, 2) });
            var drawn = synthetic(CardCategory.Attack, 0, new BattleEffect[0]); draw.Add(drawn); plain(); plain();
            Check(hand.Count == 1 && hand[0] == drawn && draw.Count == 0 && player.cardStatus.usageBuffs[0].progress == 0, "Cycle draws real card into hand after threshold");
            reset(); use(CardCategory.Buff, 1, new[] { E(BattleEffectType.EveryCardsDraw, 1, 2) }); plain();
            Check(hand.Count == 1 && draw.Count == 1 && discard.Count == 0, "Cycle recycles real discard pile when draw pile empty");

            reset(); use(CardCategory.Buff, 0, new[] { E(BattleEffectType.CategoryElectrocution, 4, 3, decay: 2) });
            use(CardCategory.Buff, 0, new[] { E(BattleEffectType.CardElectrocutionBonus, 2, 3) }); plain();
            use(CardCategory.Attack, 0, new BattleEffect[0]);
            Check(enemy.electrocution.TotalValue == 6 && player.cardStatus.usageBuffs[0].categoryMask == 0, "Third distinct category applies amplified shock and clears record");
            use(CardCategory.Attack, 0, new BattleEffect[0]); plain(); use(CardCategory.Debuff, 0, new BattleEffect[0]);
            Check(enemy.electrocution.Stacks.Count == 2 && enemy.electrocution.TotalValue == 12, "Three new categories re-trigger in same turn");
            plain(); player.cardStatus.BeginPlayerTurn();
            Check(player.cardStatus.usageBuffs[0].categoryMask == 0, "Category record clears each turn");
            use(CardCategory.Attack, 0, new BattleEffect[0]); use(CardCategory.Debuff, 0, new BattleEffect[0]);
            Check(enemy.electrocution.Stacks.Count == 2, "Previous turn partial categories do not carry");
            use(CardCategory.Buff, 0, new[] { E(BattleEffectType.CategoryElectrocution, 3, 2) });
            Check(player.cardStatus.usageBuffs[1].categoryMask == 0 && enemy.electrocution.Stacks.Count == 3, "Old category effect counts activation, new one begins with empty record");

            reset(); plain(); use(CardCategory.Buff, 0, new[] { E(BattleEffectType.ChainAttackBonus, 2, 3) });
            Check(player.cardStatus.ChainAttackBonus == 2, "Chain strength excludes its own card");
            use(CardCategory.Attack, 0, new[] { E(BattleEffectType.Damage, 3, target: EffectTarget.Enemy) });
            Check(enemy.currentHP == 993 && player.cardStatus.ChainAttackBonus == 4, "Chain strength updates for current other card before damage");
            plain(); Check(player.cardStatus.ChainAttackBonus == 6, "Chain strength responds to all categories");
            use(CardCategory.Buff, 0, new[] { E(BattleEffectType.ChainAttackBonus, 1, 2) });
            Check(player.cardStatus.ChainAttackBonus == 12, "Each stacked chain excludes only its own activation");
            player.cardStatus.EndPlayerTurn(player); player.cardStatus.BeginPlayerTurn();
            Check(player.cardStatus.ChainAttackBonus == 0, "Chain strength resets next turn");
            use(CardCategory.Attack, 0, new[] { E(BattleEffectType.Damage, 3, target: EffectTarget.Enemy) });
            Check(enemy.currentHP == 987 && player.cardStatus.ChainAttackBonus == 3, "Next turn count starts at zero and includes new other cards");
            player.cardStatus.EndPlayerTurn(player); player.cardStatus.BeginPlayerTurn(); plain();
            Check(player.cardStatus.ChainAttackBonus == 2 && player.cardStatus.usageBuffs.Count == 1, "Chain buff durations expire independently");

            reset(); plain(); use(CardCategory.Attack, 1, new[] { E(BattleEffectType.PlayedCardsCostRecovery, 2, secondary: 3) });
            Check(player.currentCost == 99, "Restart excludes itself at below threshold");
            use(CardCategory.Attack, 1, new[] { E(BattleEffectType.PlayedCardsCostRecovery, 2, secondary: 3) });
            Check(player.currentCost == 101 && player.cardStatus.CostSpentThisTurn == 2, "Restart recovers at exact threshold, expenditure unchanged");
            reset(); plain(); use(CardCategory.Defense, 1, new[] { E(BattleEffectType.GainBlock, 5), E(BattleEffectType.PlayedCardsConditionalBlock, 2, secondary: 4) });
            Check(player.currentBlock == 5, "Cycle defense excludes self below threshold");
            use(CardCategory.Defense, 1, new[] { E(BattleEffectType.GainBlock, 5), E(BattleEffectType.PlayedCardsConditionalBlock, 2, secondary: 4) });
            Check(player.currentBlock == 14, "Cycle defense bonus at exact threshold");

            foreach (var asset in assets)
                foreach (var tier in new[] { asset.lowPerformance, asset.mediumPerformance, asset.highPerformance })
                {
                    reset(); plain(); plain(); plain(); plain(); enemy.AddElectrocution(8, 3);
                    play(Instance(asset, tier));
                    Check(player.currentCost >= 0 && player.currentBlock >= 0 && enemy.currentHP >= 0, "Each performance runs through TryPlayCard: " + asset.cardName);
                }
            reset(); Check(player.cardStatus.usageBuffs.Count == 0 && player.cardStatus.lightweightRemaining.Count == 0 && player.cardStatus.ChainAttackBonus == 0, "Battle reset clears new usage effects");
            CardRarityVerification.Verify();
            File.WriteAllText("StageReview/CardPool1/verification.txt", "PASS\nChecks: " + checks + "\n20 cards x 3 tiers through actual TryPlayCard; all pool registrations; self exclusion, spending, per-effect progress, dynamic chain, shock, lightweight and real mid-turn draws. Rarity lottery regression PASS.\n");
        }
        finally
        {
            Debug.unityLogger.logEnabled = logging; UnityEngine.Random.state = randomState;
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(enemyData);
            foreach (var card in owned) UnityEngine.Object.DestroyImmediate(card);
        }
    }
}
