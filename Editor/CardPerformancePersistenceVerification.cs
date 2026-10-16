using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CardPerformancePersistenceVerification
{
    static CardPerformancePersistenceVerification() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists("Temp/CardPerformance.verify") || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete("Temp/CardPerformance.verify");
        try { Verify(); }
        catch (Exception e) { File.WriteAllText("Temp/CardPerformance.result", "FAIL\n" + e); Debug.LogException(e); }
    }
    [MenuItem("Tools/Cards/Verify Performance Persistence")]
    public static void Verify()
    {
        int checks = 0;
        Action<bool, string> check = (valid, message) => { checks++; if (!valid) throw new Exception(message); };
        var root = new GameObject("CardPerformanceVerification"); root.SetActive(false);
        var owned = new List<CardData>();
        var previousRandom = UnityEngine.Random.state;
        try
        {
            var deck = root.AddComponent<DeckManager>();
            foreach (CardCategory category in Enum.GetValues(typeof(CardCategory)))
            {
                var data = ScriptableObject.CreateInstance<CardData>(); owned.Add(data);
                data.category = category; data.rarity = CardRarity.Common; data.description = "{value1}/{duration1}";
                Func<int, CardPerformance> tier = i => new CardPerformance { cost = i, effects = new List<BattleEffect>
                    { new BattleEffect { effectType = BattleEffectType.GrowingDamage, value = 5 + i, damageIncrease = 2, duration = i + 1, target = EffectTarget.Enemy } } };
                data.lowPerformance = tier(0); data.mediumPerformance = tier(1); data.highPerformance = tier(2);
            }
            deck.balancePool = owned; deck.CreateInitialDeck(0);
            var card = deck.currentDeck[0]; card.RollPerformance();
            var fixedTier = card.CurrentPerformance; string fixedDescription = card.GetDescription();
            var unrolled = deck.currentDeck[1];
            for (int battle = 1; battle <= 10; battle++)
            {
                card.RecordPlayed(); card.CostReduction = 5;
                deck.ResetCardsForBattle();
                check(card.IsPerformanceDetermined && card.CurrentPerformance.cost == fixedTier.cost
                    && ReferenceEquals(card.CurrentPerformance.effects, fixedTier.effects), "Keep exact performance across battle " + battle);
                check(card.TimesPlayed == 0 && card.CostReduction == 0 && card.GetDescription() == fixedDescription, "Reset battle-only growth and cost reduction " + battle);
                check(!unrolled.IsPerformanceDetermined, "Do not prematurely roll undrawn cards " + battle);
                var random = UnityEngine.Random.state; card.RollPerformance();
                check(random.Equals(UnityEngine.Random.state), "No re-roll or RNG consumption " + battle);
            }
            var reward = new CardInstance(owned[0]); deck.currentDeck.Add(reward); deck.ResetCardsForBattle();
            check(!reward.IsPerformanceDetermined, "Acquired card remains unknown before first draw");
            reward.RollPerformance(); var rewardTier = reward.CurrentPerformance; deck.ResetCardsForBattle(); reward.RollPerformance();
            check(reward.IsPerformanceDetermined && ReferenceEquals(rewardTier.effects, reward.CurrentPerformance.effects), "Acquired card keeps first result");
            var oldDeck = deck.currentDeck.ToArray(); deck.CreateInitialDeck(0);
            check(deck.currentDeck.Count == 10 && deck.currentDeck.All(c => !c.IsPerformanceDetermined && !oldDeck.Contains(c)), "New game creates fresh undetermined card instances");
            foreach (string path in new[] { "Assets/Script/BattleManager.cs", "Assets/Script/BattleProgressionUI.cs" })
                check(!File.ReadAllText(path).Contains("ResetAllCardPerformances()") && File.ReadAllText(path).Contains("ResetCardsForBattle()"), "Battle start and rewards preserve performance: " + path);
            var cardGuids = AssetDatabase.FindAssets("t:CardData", new[] { "Assets/ScriptableObject/CardData" });
            check(cardGuids.Length == 70, "All 70 card definitions are present");
            var performanceProperty = typeof(CardInstance).GetProperty("CurrentPerformance");
            var determinedProperty = typeof(CardInstance).GetProperty("IsPerformanceDetermined");
            foreach (var guid in cardGuids)
            {
                var definition = AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
                var instance = new CardInstance(definition);
                check(!instance.GetDescription().Contains("{"), "Undetermined description resolves: " + definition.name);
                foreach (var tier in new[] { definition.lowPerformance, definition.mediumPerformance, definition.highPerformance })
                {
                    performanceProperty.SetValue(instance, tier);
                    determinedProperty.SetValue(instance, true);
                    string description = instance.GetDescription();
                    check(!string.IsNullOrWhiteSpace(description) && !description.Contains("{") && !description.Contains("?"), "Determined description resolves: " + definition.name);
                }
            }
            File.WriteAllText("Temp/CardPerformance.result", "PASS\nChecks: " + checks + "\nTen-battle persistence, no extra rolls, battle-only resets, reward cards and new-game reset.\n");
        }
        finally
        {
            UnityEngine.Random.state = previousRandom;
            UnityEngine.Object.DestroyImmediate(root); foreach (var data in owned) UnityEngine.Object.DestroyImmediate(data);
        }
    }
}
