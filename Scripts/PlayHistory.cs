using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Versioned, local history shared by standalone and WebGL (IndexedDB PlayerPrefs).</summary>
public static class PlayHistory
{
    public const int Capacity = 30;
    public const string StorageKey = "PlayHistory.v1";
    [Serializable] public class Card
    {
        public string assetName, name, description;
        public int pattern = -1, cost;
    }
    [Serializable] public class Record
    {
        public string id, endedAt, enemyName;
        public int pack, battle;
        public bool cleared, archived;
        public List<Card> deck = new List<Card>();
    }
    [Serializable] private class Document
    {
        public int version = 1;
        public List<Record> records = new List<Record>();
    }
    public static string Notice { get; private set; }
    public static List<Record> Read()
    {
        try
        {
            string json = PlayerPrefs.GetString(StorageKey, "");
            if (string.IsNullOrEmpty(json)) return new List<Record>();
            var document = JsonUtility.FromJson<Document>(json);
            if (document == null || document.version != 1 || document.records == null || document.records.Count > Capacity)
                throw new FormatException("Unsupported history data");
            foreach (var record in document.records)
                if (record == null || record.deck == null || string.IsNullOrEmpty(record.id))
                    throw new FormatException("Invalid history record");
            return document.records;
        }
        catch (Exception exception)
        {
            Notice = "プレイ記録を読み込めませんでした。保存済みデータは保持されています。";
            Debug.LogWarning("Play history load failed: " + exception.Message);
            return null;
        }
    }
    public static bool Add(Record record)
    {
        var records = Read();
        if (records == null) return false;
        if (records.Exists(item => item.id == record.id)) return true;
        if (records.Count >= Capacity)
        {
            // Stored in completion order; changing archive state never changes that order.
            int oldest = records.FindIndex(item => !item.archived);
            if (oldest < 0)
            {
                Notice = "30件すべてが保管中のため、今回のプレイ記録は保存されませんでした。";
                return false;
            }
            records.RemoveAt(oldest);
        }
        records.Add(record);
        return Save(records);
    }
    public static bool SetArchived(string id, bool archived)
    {
        var records = Read();
        var record = records == null ? null : records.Find(item => item.id == id);
        if (record == null) return false;
        record.archived = archived;
        return Save(records);
    }
    public static bool Delete(string id)
    {
        var records = Read();
        if (records == null || records.RemoveAll(record => record.id == id) == 0) return false;
        return Save(records);
    }
    private static bool Save(List<Record> records)
    {
        string previous = PlayerPrefs.GetString(StorageKey, "");
        try
        {
            string json = JsonUtility.ToJson(new Document { records = records });
            // Leave room for the other preferences within WebGL's 1MB budget.
            if (System.Text.Encoding.UTF8.GetByteCount(json) > 800000)
                throw new InvalidOperationException("History storage budget exceeded");
            PlayerPrefs.SetString(StorageKey, json);
            PlayerPrefs.Save();
            Notice = null;
            return true;
        }
        catch (Exception exception)
        {
            PlayerPrefs.SetString(StorageKey, previous);
            Notice = "プレイ記録を保存できませんでした。保存容量・ブラウザーの設定を確認してください。";
            Debug.LogWarning("Play history save failed: " + exception.Message);
            return false;
        }
    }
    public static Record Capture(int pack, int battle, bool cleared, string enemy, List<CardInstance> deck)
    {
        var result = new Record { id = Guid.NewGuid().ToString("N"),
            endedAt = DateTimeOffset.Now.ToString("o"), pack = pack, battle = battle,
            cleared = cleared, enemyName = enemy };
        foreach (var card in deck)
        {
            if (card == null || card.Data == null) continue;
            int pattern = !card.IsPerformanceDetermined ? -1 :
                ReferenceEquals(card.CurrentPerformance.effects, card.Data.lowPerformance.effects) ? 0 :
                ReferenceEquals(card.CurrentPerformance.effects, card.Data.mediumPerformance.effects) ? 1 : 2;
            // Snapshot the permanent card, excluding temporary battle buffs and use counts.
            var permanent = pattern < 0 ? new CardInstance(card.Data) : CardInstance.CreateCollectionPreview(card.Data, pattern);
            result.deck.Add(new Card { assetName = card.Data.name, name = card.Data.cardName,
                pattern = pattern, cost = permanent.CurrentPerformance.cost, description = permanent.GetDescription() });
        }
        return result;
    }
}
