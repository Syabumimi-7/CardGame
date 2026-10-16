using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CardPool2Verification
{
    static readonly string[] Names = { "Shukketsukougeki", "Jakutaikougeki", "Seisai", "Ketsujin", "Shokei", "Hasaikougeki", "Shukketsunotate", "Jakutainotate", "Yokuatsu", "Houfukubougyo", "Kinkyuubougyo", "Kutsuuzoufuku", "Jakushagari", "Houfukutaisei", "Shukketsu", "Kougekifuuji", "Bougyosogai", "Suijaku", "Fukade", "Jubaku" };
    static int checks;
    static CardPool2Verification() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists("Temp/CardPool2.verify") || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete("Temp/CardPool2.verify");
        try { Verify(); }
        catch (Exception e) { File.WriteAllText("StageReview/CardPool2/verification.txt", "FAIL\n" + e); Debug.LogException(e); }
    }
    static void Check(bool valid, string message) { checks++; if (!valid) throw new Exception(message); }
    static BattleEffect E(BattleEffectType type, int value = 0, int duration = 0, EffectTarget target = EffectTarget.Enemy,
        int secondary = 0, int tertiary = 0, int buffDuration = 0, int decay = 1)
        => new BattleEffect { effectType = type, value = value, duration = duration, target = target,
            secondaryValue = secondary, tertiaryValue = tertiary, secondaryDuration = buffDuration, decayPerTrigger = decay };
    static CardInstance Instance(CardData data, CardPerformance performance)
    {
        var card = new CardInstance(data);
        typeof(CardInstance).GetProperty("CurrentPerformance").SetValue(card, performance);
        typeof(CardInstance).GetProperty("IsPerformanceDetermined").SetValue(card, true);
        return card;
    }
    [MenuItem("Tools/Cards/Verify Pool 2")]
    public static void Verify()
    {
        checks = 0;
        var assets = Names.Select(n => AssetDatabase.LoadAssetAtPath<CardData>("Assets/ScriptableObject/CardData/" + n + ".asset")).ToArray();
        Check(assets.All(a => a != null), "All twenty assets loaded");
        Check(assets.Count(a => a.category == CardCategory.Attack) == 6 && assets.Count(a => a.category == CardCategory.Defense) == 5
            && assets.Count(a => a.category == CardCategory.Buff) == 3 && assets.Count(a => a.category == CardCategory.Debuff) == 6, "Category totals");
        foreach (var data in assets)
        {
            Check(Enum.IsDefined(typeof(CardRarity), data.rarity), "Valid rarity: " + data.cardName);
            var descriptions = new List<string>();
            foreach (var tier in new[] { data.lowPerformance, data.mediumPerformance, data.highPerformance })
            {
                Check(tier.cost >= 0 && tier.effects.Count > 0 && tier.effects.All(e => Enum.IsDefined(typeof(BattleEffectType), e.effectType)), "Valid effects: " + data.cardName);
                string description = Instance(data, tier).GetDescription();
                Check(!description.Contains("{") && !description.Contains("?") && !description.Contains("−-"), "Resolved description: " + data.cardName + " " + description);
                descriptions.Add(description);
            }
            Check(descriptions.Distinct().Count() == 3, "Three distinct performances: " + data.cardName);
        }
        var ownGuids = Names.Select(n => AssetDatabase.AssetPathToGUID("Assets/ScriptableObject/CardData/" + n + ".asset")).ToArray();
        foreach (var scene in new[] { "Assets/TitleScene.unity", "Assets/Scenes/RuinsStagePreview.unity", "Assets/Scenes/RuinsStageRefined.unity" })
        {
            var text = File.ReadAllText(scene);
            Func<string, string[]> pool = name => Regex.Matches(Regex.Match(text, @"(?m)^  " + name + @":\r?\n(?:  -[^\r\n]*\r?\n)+").Value, @"guid: (\w+)").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
            var p1 = pool("balancePool"); var p2 = pool("attackHeavyPool"); var p3 = pool("defenseHeavyPool");
            Check(p1.Length == 30 && p2.Length == 30 && p3.Length == 30, "Pool counts: " + scene);
            Check(p2.Distinct().Count() == 30 && p2.Take(10).SequenceEqual(p1.Take(10)) && ownGuids.All(g => p2.Contains(g)), "Common ten plus unique twenty: " + scene);
            Check(!ownGuids.Any(g => p1.Contains(g) || p3.Contains(g)), "No new cards in pools 1/3: " + scene);
        }

        var root = new GameObject("CardPool2Verification"); root.SetActive(false);
        var enemyData = ScriptableObject.CreateInstance<EnemyData>(); enemyData.enemyName = "Verification"; enemyData.baseMaxHP = 100; enemyData.scalingFactor = 1;
        var cardData = ScriptableObject.CreateInstance<CardData>(); cardData.category = CardCategory.Attack;
        var action = ScriptableObject.CreateInstance<EnemyActionData>(); action.actionName = "Verification attack";
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
            var applyCard = typeof(BattleManager).GetMethod("ApplyCardEffects", BindingFlags.Instance | BindingFlags.NonPublic);
            var applyAction = typeof(BattleManager).GetMethod("ApplyEnemyAction", BindingFlags.Instance | BindingFlags.NonPublic);
            Action reset = () => { enemy.Setup(enemyData, 1); player.ResetForBattle(); player.maxHP = player.currentHP = 50; };
            Action<BattleEffect> cast = effect => apply.Invoke(battle, new object[] { effect, true, Instance(cardData, new CardPerformance { effects = new List<BattleEffect> { effect } }) });
            Action<int> hit = damage => apply.Invoke(battle, new object[] { E(BattleEffectType.Damage, damage), false, null });
            Action<CardData, CardPerformance> play = (data, tier) => applyCard.Invoke(battle, new object[] { Instance(data, tier) });

            reset(); enemy.AddBleeding(5, 1); enemy.AddElectrocution(6, 2); enemy.AddPoison(3, 5);
            enemy.modifiers.Add(BattleEffectType.OutgoingDamageModifier, -2, 3);
            enemy.modifiers.Add(BattleEffectType.OutgoingDamageModifier, -1, 3);
            enemy.modifiers.Add(BattleEffectType.IncomingDamageModifier, 2, 3);
            enemy.modifiers.Add(BattleEffectType.IncomingDamageModifier, -3, 3);
            enemy.GainPermanentAttack(5);
            Check(enemy.modifiers.DebuffCount == 3, "Same-kind stacks counted individually, statuses and buffs excluded");
            cast(E(BattleEffectType.DebuffCountDamage, 4));
            Check(enemy.currentHP == 83, "Sanction applies normal damage modifiers then shock");
            player.cardStatus.AddTimed(player.cardStatus.blockBonuses, E(BattleEffectType.CardBlockBonus, 2, 3));
            cast(E(BattleEffectType.DebuffCountBlock, 4, target: EffectTarget.Self));
            Check(player.currentBlock == 14, "Suppression counts debuffs and one card block bonus");
            cast(E(BattleEffectType.DebuffCountCardBonus, 2, 3, EffectTarget.Self));
            enemy.modifiers.Add(BattleEffectType.OutgoingDamageModifier, -1, 3);
            Check(player.modifiers.cardDamageBonus == 6, "Weak hunter uses cast-time count");

            reset(); enemy.AddBleeding(5, 1); enemy.AddBleeding(3, 2); enemy.AddElectrocution(6, 2);
            cast(E(BattleEffectType.BleedingBonusDamage, 3, secondary: 2));
            Check(enemy.currentHP == 75 && enemy.electrocution.TotalValue == 4 && enemy.bleeding.TotalValue == 8, "Blood blade sums bleed and deals one hit");
            reset(); enemy.currentHP = 25; cast(E(BattleEffectType.ExecutionDamage, 5, secondary: 4, tertiary: 8));
            Check(enemy.currentHP == 12, "Execution includes exact quarter threshold");
            reset(); enemy.currentHP = 26; cast(E(BattleEffectType.ExecutionDamage, 5, secondary: 4, tertiary: 8));
            Check(enemy.currentHP == 21, "Execution excludes above threshold");
            int fracture = assets[5].lowPerformance.effects[0].value;
            int fractureDamage = assets[5].lowPerformance.effects[1].value;
            reset(); enemy.currentBlock = fracture; play(assets[5], assets[5].lowPerformance);
            Check(enemy.currentBlock == 0 && enemy.currentHP == 100-fractureDamage, "Fracture removes block before damage");
            reset(); enemy.currentBlock = fracture+fractureDamage+2; play(assets[5], assets[5].lowPerformance);
            Check(enemy.currentBlock == 2 && enemy.currentHP == 100, "Fracture damage is still blocked after block removal");
            reset(); play(assets[0], assets[0].lowPerformance);
            Check(enemy.currentHP == 100-assets[0].lowPerformance.effects[0].value && enemy.bleeding.TotalValue == assets[0].lowPerformance.effects[1].value, "Bleeding attack deals damage then status");
            reset(); play(assets[1], assets[1].lowPerformance);
            Check(enemy.currentHP == 100-assets[1].lowPerformance.effects[0].value && enemy.AttackBonus == assets[1].lowPerformance.effects[1].value && enemy.modifiers.DebuffCount == 1, "Weak attack deals damage then weakness");
            reset(); player.currentHP = 25; play(assets[10], assets[10].highPerformance);
            Check(player.currentBlock == assets[10].highPerformance.effects[0].value+assets[10].highPerformance.effects[2].value, "Emergency defense threshold includes half health");
            reset(); player.currentHP = 26; play(assets[10], assets[10].highPerformance);
            Check(player.currentBlock == assets[10].highPerformance.effects[0].value, "Emergency defense above threshold");
            reset(); enemy.AddBleeding(3, 1); enemy.AddBleeding(5, 2); cast(E(BattleEffectType.AmplifyBleeding, 2));
            Check(enemy.bleeding.TotalValue == 12 && enemy.bleeding.Stacks[1].decayPerTrigger == 2, "Pain amplification preserves per-stack decay");
            reset(); cast(E(BattleEffectType.AmplifyBleeding, 2)); Check(!enemy.bleeding.IsActive, "Amplification creates no new bleed");

            reset(); player.currentBlock = 100;
            cast(E(BattleEffectType.BleedingShield, 2, 1, EffectTarget.Self)); hit(5);
            Check(enemy.currentHP == 100 && enemy.bleeding.TotalValue == 2, "Bleed shield triggers through full block after existing bleed");
            hit(5); Check(enemy.currentHP == 98 && enemy.bleeding.TotalValue == 3, "Bleed shield new stack begins next hit");
            player.cardStatus.EndEnemyTurn(); Check(player.cardStatus.reactions.Count == 0 && enemy.bleeding.IsActive, "Shield expires independently of acquired bleed");
            reset(); player.currentBlock = 100;
            cast(E(BattleEffectType.WeakeningShield, -2, 1, EffectTarget.Self, buffDuration: 1)); hit(5); hit(5);
            Check(player.currentBlock == 92 && enemy.AttackBonus == -4 && enemy.modifiers.DebuffCount == 2, "Weak shield stacks per hit and affects second hit");
            player.cardStatus.EndEnemyTurn(); enemy.TickModifiers();
            Check(player.cardStatus.reactions.Count == 0 && enemy.AttackBonus == -4, "Earned one-turn weakness survives creation turn");
            enemy.TickModifiers(); Check(enemy.AttackBonus == 0, "Earned weakness expires after independent duration");

            reset(); player.modifiers.Add(BattleEffectType.CardDamageBonus, 20, 3);
            cast(E(BattleEffectType.HPDamageRetaliation, 6, 2, EffectTarget.Self)); player.currentBlock = 5; hit(5);
            Check(enemy.currentHP == 100, "Revenge does not trigger on fully blocked hit");
            player.currentBlock = 2; hit(5);
            Check(enemy.currentHP == 94 && player.currentHP == 47, "Partial block revenge is fixed damage without attack buff");
            reset(); cast(E(BattleEffectType.HPDamageRetaliation, 6, 2, EffectTarget.Self)); player.currentBlock = 5; player.AddElectrocution(6, 2); hit(5);
            Check(player.currentHP == 44 && enemy.currentHP == 100, "Shock HP loss does not trigger revenge when primary hit is blocked");
            player.AddPoison(1, 5); player.TickPoison(); Check(enemy.currentHP == 100, "Poison never triggers revenge");

            reset(); cast(E(BattleEffectType.DamageTriggeredCardBonus, 4, 1, EffectTarget.Self, buffDuration: 2)); hit(5); hit(3);
            Check(player.modifiers.cardDamageBonus == 3, "Revenge stance rounds each primary hit upward");
            player.cardStatus.EndEnemyTurn();
            Check(player.cardStatus.damageTriggeredBonuses.Count == 0 && player.modifiers.cardDamageBonus == 3, "Acquired attack buffs outlive stance");
            player.TickModifiers(); Check(player.modifiers.cardDamageBonus == 3, "Acquired two-turn buffs survive first player turn");
            player.TickModifiers(); Check(player.modifiers.cardDamageBonus == 0, "Acquired buffs expire independently");
            reset(); cast(E(BattleEffectType.DamageTriggeredCardBonus, 4, 2, EffectTarget.Self, buffDuration: 2));
            cast(E(BattleEffectType.DamageTriggeredCardBonus, 3, 2, EffectTarget.Self, buffDuration: 1)); hit(5);
            Check(player.modifiers.cardDamageBonus == 4, "Multiple revenge stances stack");
            reset(); cast(E(BattleEffectType.DamageTriggeredCardBonus, 4, 2, EffectTarget.Self, buffDuration: 2));
            player.currentBlock = 5; player.AddElectrocution(6, 2); hit(5); player.AddPoison(1, 2); player.TickPoison(); player.TakeDamage(1);
            Check(player.modifiers.cardDamageBonus == 0, "Blocked hit, shock, poison and self damage do not earn stance buffs");

            reset(); cast(E(BattleEffectType.NextAttackReduction, 3)); cast(E(BattleEffectType.NextAttackReduction, 2));
            for (int i = 0; i < 5; i++) enemy.TickModifiers();
            Check(enemy.modifiers.NextAttackReduction == 5, "Attack seal does not expire during nonattack turns");
            player.currentBlock = 10; hit(7);
            Check(player.currentBlock == 8 && enemy.modifiers.NextAttackReduction == 0, "All seals consumed by next one hit even when blocked");
            hit(7); Check(player.currentBlock == 1, "Second hit has no seal reduction");
            reset(); enemy.currentBlock = 10; cast(E(BattleEffectType.BlockGainModifier, -5, 2)); enemy.GainBlock(3);
            Check(enemy.currentBlock == 10, "Block inhibition clamps gains and preserves existing block");
            enemy.GainBlock(9); Check(enemy.currentBlock == 14, "Block inhibition reduces each gain");
            cast(E(BattleEffectType.BlockGainModifier, -2, 2)); enemy.GainBlock(10);
            Check(enemy.currentBlock == 17 && enemy.modifiers.DebuffCount == 2, "Block inhibition stacks and counts as individual debuffs");
            reset(); enemy.GainPermanentAttack(6);
            enemy.modifiers.Add(BattleEffectType.OutgoingDamageModifier, 8, 4);
            enemy.modifiers.Add(BattleEffectType.IncomingDamageModifier, -5, 3);
            enemy.modifiers.Add(BattleEffectType.BlockGainModifier, 2, 2);
            enemy.modifiers.Add(BattleEffectType.OutgoingDamageModifier, -4, 3);
            enemy.modifiers.Add(BattleEffectType.IncomingCardDamageBonus, 2, 2);
            cast(E(BattleEffectType.ReduceBuffs, 3));
            Check(enemy.PermanentAttackBonus == 3 && enemy.modifiers.outgoingDamageModifier == 1 && enemy.modifiers.incomingDamageModifier == -2
                && enemy.modifiers.blockGainModifier == 0 && enemy.modifiers.incomingCardDamageBonus == 2 && enemy.modifiers.DebuffCount == 2, "Weaken all temporary/permanent beneficial effects, keep debuffs");
            Check(enemy.modifiers.Active.First(m => m.effectType == BattleEffectType.OutgoingDamageModifier && m.value > 0).remainingTurns == 4, "Weaken preserves durations");

            reset(); player.currentBlock = 100; cast(E(BattleEffectType.DeepWound, 3, 1)); hit(5);
            Check(enemy.currentHP == 100 && enemy.bleeding.TotalValue == 3, "Deep wound gains bleed after attack, no immediate trigger");
            hit(5); Check(enemy.currentHP == 97 && enemy.bleeding.TotalValue == 5, "Deep wound triggers old bleed then adds new stack per hit");
            enemy.TickModifiers(); Check(enemy.modifiers.DebuffCount == 0 && enemy.bleeding.IsActive, "Deep wound duration separate from permanent bleed decay");
            reset(); player.currentBlock = 100; cast(E(BattleEffectType.CursedAttacks, 2, 1, buffDuration: 1)); hit(5); hit(5);
            Check(player.currentBlock == 92 && enemy.AttackBonus == -4 && enemy.modifiers.DebuffCount == 3, "Curse adds one distinct attack debuff after each hit");
            enemy.TickModifiers(); Check(enemy.AttackBonus == -4 && enemy.modifiers.DebuffCount == 2, "Curse expires, earned one-turn debuffs persist");
            enemy.TickModifiers(); Check(enemy.AttackBonus == 0, "Earned curse debuffs independently expire");
            reset(); cast(E(BattleEffectType.DeepWound, 3, 2)); cast(E(BattleEffectType.CursedAttacks, 2, 2, buffDuration: 2));
            enemy.AddPoison(1, 2); enemy.TickPoison();
            Check(!enemy.bleeding.IsActive && enemy.AttackBonus == 0, "Poison damage does not trigger attack traps");

            enemyData.actionsPerTurn = 2;
            action.effects = new List<BattleEffect> { E(BattleEffectType.GainBlock, 6, target: EffectTarget.Self), E(BattleEffectType.Damage, 7) };
            enemyData.actionPattern = new List<EnemyActionData> { action };
            for (int configuration = 0; configuration < 128; configuration++)
            {
                reset(); player.currentBlock = configuration % 4 * 3;
                enemy.currentBlock = configuration % 3 * 2;
                if ((configuration & 1) != 0) cast(E(BattleEffectType.NextAttackReduction, 5));
                if ((configuration & 2) != 0) cast(E(BattleEffectType.DeepWound, 3, 2));
                if ((configuration & 4) != 0) cast(E(BattleEffectType.CursedAttacks, 2, 2, buffDuration: 2));
                if ((configuration & 8) != 0) cast(E(BattleEffectType.WeakeningShield, -2, 2, EffectTarget.Self, buffDuration: 1));
                if ((configuration & 16) != 0) cast(E(BattleEffectType.BleedingShield, 4, 2, EffectTarget.Self));
                if ((configuration & 32) != 0) cast(E(BattleEffectType.HPDamageRetaliation, 3, 2, EffectTarget.Self));
                if ((configuration & 64) != 0) { cast(E(BattleEffectType.BlockGainModifier, -3, 2)); player.AddElectrocution(5, 2); }
                int originalBleed = enemy.bleeding.TotalValue, originalSeal = enemy.modifiers.NextAttackReduction;
                var forecast = EnemyIntentForecast.Create(enemy, player, false);
                Check(enemy.bleeding.TotalValue == originalBleed && enemy.modifiers.NextAttackReduction == originalSeal, "Forecast has no state mutations " + configuration);
                for (int i = 0; i < 2 && !player.IsDead && !enemy.IsDead; i++) applyAction.Invoke(battle, new object[] { action });
                Check(forecast.hpDamage == 50 - player.currentHP, "Forecast matches combined runtime effects " + configuration + " predicted=" + forecast.hpDamage + " actual=" + (50 - player.currentHP));
            }
            foreach (var data in assets)
                foreach (var tier in new[] { data.lowPerformance, data.mediumPerformance, data.highPerformance })
                {
                    reset(); enemy.currentHP = 40; player.currentHP = 10; enemy.currentBlock = 12;
                    enemy.modifiers.Add(BattleEffectType.OutgoingDamageModifier, -2, 2); enemy.modifiers.Add(BattleEffectType.IncomingCardDamageBonus, 1, 2);
                    enemy.GainPermanentAttack(3); enemy.AddBleeding(4, 1); play(data, tier); hit(5);
                    Check(player.currentHP >= 0 && enemy.currentHP >= 0 && player.currentBlock >= 0 && enemy.currentBlock >= 0, "All twenty card performances execute: " + data.cardName);
                }
            reset(); Check(player.cardStatus.damageTriggeredBonuses.Count == 0 && player.cardStatus.reactions.Count == 0 && enemy.modifiers.DebuffCount == 0, "Battle reset clears new stances, reactions and debuffs");
            File.WriteAllText("StageReview/CardPool2/verification.txt", "PASS\nChecks: " + checks + "\n20 assets × 3 performances; pool registration; timing/stacking; 128 forecast/runtime combinations.\n");
        }
        finally
        {
            Debug.unityLogger.logEnabled = logging;
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(enemyData); UnityEngine.Object.DestroyImmediate(cardData); UnityEngine.Object.DestroyImmediate(action);
        }
    }
}
