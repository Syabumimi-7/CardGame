using UnityEngine;

[CreateAssetMenu(menuName = "Game/Audio Settings")]
public class GameAudioSettings : ScriptableObject
{
    public AudioClip title, enemy, boss;
    public AudioClip button, damage, recovery;
    [Range(0, 1)] public float musicVolume = .5f;
    [Range(0, 1)] public float soundVolume = .8f;
}
