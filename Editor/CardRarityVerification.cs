using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CardRarityVerification
{
    static CardRarityVerification() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!System.IO.File.Exists("Temp/CardRarity.verify") || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        System.IO.File.Delete("Temp/CardRarity.verify");
        try { Verify(); System.IO.File.WriteAllText("Temp/CardRarity.result", "PASS: rarity probabilities, redistribution and initial/reward draws"); }
        catch (Exception e) { System.IO.File.WriteAllText("Temp/CardRarity.result", "FAIL\n" + e); Debug.LogException(e); }
    }
    [MenuItem("Tools/Cards/Verify Rarity Lottery")]
    public static void Verify()
    {
        for (int mask = 0; mask < 8; mask++)
        {
            var counts = new int[4];
            for (int roll = 0; roll < 100; roll++)
                counts[(int)CardRarityLottery.ResolveRarity(roll, (mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0)]++;
            int uncommon = (mask & 1) != 0 ? 35 : 0;
            int rare = (mask & 2) != 0 ? 10 : 0;
            int superRare = (mask & 4) != 0 ? 5 : 0;
            Require(counts.SequenceEqual(new[] { 100 - uncommon - rare - superRare, uncommon, rare, superRare }), "Probability redistribution " + mask);
        }

        var cards = new List<CardData>();
        var root = new GameObject("RarityVerification");
        root.SetActive(false);
        UnityEngine.Random.State randomState = UnityEngine.Random.state;
        try
        {
            foreach (CardCategory category in Enum.GetValues(typeof(CardCategory)))
                foreach (CardRarity rarity in Enum.GetValues(typeof(CardRarity)))
                {
                    var card = ScriptableObject.CreateInstance<CardData>();
                    card.category = category; card.rarity = rarity;
                    cards.Add(card);
                }
            UnityEngine.Random.InitState(12345);
            var deck = root.AddComponent<DeckManager>();
            deck.balancePool = cards;
            deck.CreateInitialDeck(0);
            Require(deck.currentDeck.Count == 10, "Initial deck size");
            Require(deck.currentDeck.Count(c => c.Data.category == CardCategory.Attack) == 5, "Five attacks");
            Require(deck.currentDeck.Count(c => c.Data.category == CardCategory.Defense) == 2, "Two defenses");
            Require(deck.currentDeck.Count(c => c.Data.category == CardCategory.Buff) == 2, "Two buffs");
            Require(deck.currentDeck.Count(c => c.Data.category == CardCategory.Debuff) == 1, "One debuff");
            var onlyCommon = cards.Where(c => c.rarity == CardRarity.Common).ToList();
            var commonLottery = new CardRarityLottery(onlyCommon);
            for (int i = 0; i < 100; i++) Require(commonLottery.Draw().rarity == CardRarity.Common, "All-common fallback");
            // 上位レアリティが他カテゴリーに存在しても、対象カテゴリーではコモンのみ。
            var mixed = cards.Where(c => c.category != CardCategory.Attack || c.rarity == CardRarity.Common).ToList();
            var attackLottery = new CardRarityLottery(mixed, CardCategory.Attack);
            for (int i = 0; i < 100; i++)
            {
                var card = attackLottery.Draw();
                Require(card.category == CardCategory.Attack && card.rarity == CardRarity.Common, "Category-local fallback");
            }
            var rewardLottery = new CardRarityLottery(cards);
            var seen = new HashSet<CardRarity>();
            for (int i = 0; i < 1000; i++)
            {
                var card = rewardLottery.Draw();
                Require(cards.Contains(card), "Reward membership"); seen.Add(card.rarity);
            }
            Require(seen.Count == 4, "All rarities can be drawn");
            Debug.Log("PASS: rarity probabilities, missing-tier redistribution, initial deck 5/2/2/1, category filtering and reward draws");
        }
        finally
        {
            UnityEngine.Random.state = randomState;
            UnityEngine.Object.DestroyImmediate(root);
            foreach (var card in cards) UnityEngine.Object.DestroyImmediate(card);
        }
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
