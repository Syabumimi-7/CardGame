using UnityEngine;

/// <summary>選択パックと同じ番号のプレイヤーを、編集可能な出現地点に配置します。</summary>
[DisallowMultipleComponent]
public class PlayerVisualManager : MonoBehaviour
{
    [Tooltip("Pack1 / Pack2 / Pack3 の順に設定")]
    public PlayerBattleVisual[] playerPrefabs = new PlayerBattleVisual[3];
    public Transform playerSpawnPoint;
    private PlayerStatus status;
    public PlayerBattleVisual CurrentPlayer { get; private set; }

    public void SpawnPlayer(int packIndex, PlayerStatus playerStatus)
    {
        DespawnPlayer();
        if (playerSpawnPoint == null || packIndex < 0 || packIndex >= playerPrefabs.Length || playerPrefabs[packIndex] == null)
        {
            Debug.LogError("プレイヤーの出現地点または選択パックのPrefabが未設定です。", this);
            return;
        }
        status = playerStatus;
        CurrentPlayer = Instantiate(playerPrefabs[packIndex], playerSpawnPoint.position, playerSpawnPoint.rotation, playerSpawnPoint);
        CurrentPlayer.name = "Player" + (packIndex + 1);
        CurrentPlayer.PlayIdle();
        if (status != null) status.Died += PlayDeath;
    }

    public void PlayDeath() { if (CurrentPlayer != null) CurrentPlayer.PlayDeath(); }

    public void DespawnPlayer()
    {
        if (status != null) status.Died -= PlayDeath;
        status = null;
        if (CurrentPlayer != null)
        {
            CurrentPlayer.gameObject.SetActive(false);
            Destroy(CurrentPlayer.gameObject);
        }
        CurrentPlayer = null;
    }

    private void OnDisable() { DespawnPlayer(); }
}
