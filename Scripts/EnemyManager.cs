using System.Collections.Generic;
using UnityEngine;

/// <summary>連戦数に応じた敵の選択と生成を管理します。</summary>
public class EnemyManager : MonoBehaviour
{
    [Header("敵データ")]
    public List<EnemyData> normalEnemies = new List<EnemyData>();
    public List<EnemyData> bossEnemies = new List<EnemyData>();

    [Header("生成設定")]
    public Transform enemySpawnPoint;

    public EnemyInstance CurrentEnemy { get; private set; }
    readonly List<EnemyData> normalEnemyBag = new List<EnemyData>();
    EnemyData lastNormalEnemy;

    public EnemyData SelectEnemyData(int battleCount)
    {
        if (battleCount == 1)
        {
            normalEnemyBag.Clear();
            lastNormalEnemy = null;
        }
        if (battleCount == 10)
            return bossEnemies == null || bossEnemies.Count == 0 ? null : bossEnemies[Random.Range(0, bossEnemies.Count)];

        normalEnemyBag.RemoveAll(e => e == null || normalEnemies == null || !normalEnemies.Contains(e));
        if (normalEnemyBag.Count == 0 && normalEnemies != null)
        {
            foreach (var entry in normalEnemies)
                if (entry != null && !normalEnemyBag.Contains(entry)) normalEnemyBag.Add(entry);
        }
        if (normalEnemyBag.Count == 0) return null;
        var choices = normalEnemyBag.FindAll(e => e != lastNormalEnemy);
        if (choices.Count == 0) choices.AddRange(normalEnemyBag);
        var selected = choices[Random.Range(0, choices.Count)];
        normalEnemyBag.Remove(selected);
        lastNormalEnemy = selected;
        return selected;
    }

    public void DespawnCurrentEnemy()
    {
        if (CurrentEnemy != null) Destroy(CurrentEnemy.gameObject);
        CurrentEnemy = null;
        if (enemySpawnPoint == null) return;
        for (int i = enemySpawnPoint.childCount - 1; i >= 0; i--)
            Destroy(enemySpawnPoint.GetChild(i).gameObject);
    }

    public void SpawnEnemy(int battleCount)
    {
        DespawnCurrentEnemy();

        List<EnemyData> candidates = battleCount == 10 ? bossEnemies : normalEnemies;
        string enemyTypeName = battleCount == 10 ? "ボス" : "通常敵";
        if (candidates == null || candidates.Count == 0)
        {
            Debug.LogError($"{enemyTypeName}が登録されていません。");
            return;
        }

        EnemyData selectedData = SelectEnemyData(battleCount);
        if (selectedData == null || selectedData.enemyPrefab == null)
        {
            Debug.LogError($"{enemyTypeName}のEnemyDataまたはenemyPrefabが未設定です。");
            return;
        }

        Vector3 spawnPosition = enemySpawnPoint != null ? enemySpawnPoint.position : Vector3.zero;
        Quaternion spawnRotation = enemySpawnPoint != null
            ? enemySpawnPoint.rotation * selectedData.enemyPrefab.transform.rotation
            : selectedData.enemyPrefab.transform.rotation;
        GameObject enemyObject = Instantiate(selectedData.enemyPrefab, spawnPosition, spawnRotation, enemySpawnPoint);
        enemyObject.transform.localPosition = Vector3.zero;
        CurrentEnemy = enemyObject.GetComponent<EnemyInstance>();
        if (CurrentEnemy == null) CurrentEnemy = enemyObject.GetComponentInChildren<EnemyInstance>();
        if (CurrentEnemy == null)
        {
            Debug.LogWarning($"{selectedData.enemyName}のPrefabにEnemyInstanceがないため、ルートへ自動追加しました。");
            CurrentEnemy = enemyObject.AddComponent<EnemyInstance>();
        }

        CurrentEnemy.Setup(selectedData, battleCount);
        Debug.Log($"第{battleCount}戦：{selectedData.enemyName}が現れた！（HP:{CurrentEnemy.currentHP}）");
    }
}
