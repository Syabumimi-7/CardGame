using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Music follows battle state, so pause/help overlays never change the track.</summary>
public class GameAudio : MonoBehaviour
{
    private static GameAudio instance;
    private GameAudioSettings settings;
    private AudioSource music, sounds;
    private int buttonSoundFrame = -1;
    private readonly Dictionary<Button, Button.ButtonClickedEvent> boundButtons = new Dictionary<Button, Button.ButtonClickedEvent>();
    private readonly List<Button> destroyedButtons = new List<Button>();
    private const string MusicKey = "Audio.MusicVolume";
    private const string SoundKey = "Audio.SoundVolume";
    public static int MusicVolume => instance != null ? Mathf.RoundToInt(instance.music.volume * 100) : 50;
    public static int SoundVolume => instance != null ? Mathf.RoundToInt(instance.sounds.volume * 100) : 80;

    public static void SetMusicVolume(float value)
    {
        int volume = Mathf.Clamp(Mathf.RoundToInt(value), 0, 100);
        if (instance != null) instance.music.volume = volume / 100f;
        PlayerPrefs.SetInt(MusicKey, volume);
    }
    public static void SetSoundVolume(float value)
    {
        int volume = Mathf.Clamp(Mathf.RoundToInt(value), 0, 100);
        if (instance != null) instance.sounds.volume = volume / 100f;
        PlayerPrefs.SetInt(SoundKey, volume);
    }
    public static void SaveVolumeSettings() { PlayerPrefs.Save(); }
    private void OnApplicationQuit() { SaveVolumeSettings(); }
    private void OnApplicationPause(bool paused) { if (paused) SaveVolumeSettings(); }

    public static void Install(GameObject owner)
    {
        if (instance == null) owner.AddComponent<GameAudio>();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;
        settings = Resources.Load<GameAudioSettings>("GameAudioSettings");
        music = Source("Music", true);
        sounds = Source("Sound Effects", false);
        if (settings == null) { Debug.LogError("GameAudioSettings is missing.", this); return; }
        music.volume = Mathf.Clamp(PlayerPrefs.GetInt(MusicKey, Mathf.RoundToInt(settings.musicVolume * 100)), 0, 100) / 100f;
        sounds.volume = Mathf.Clamp(PlayerPrefs.GetInt(SoundKey, Mathf.RoundToInt(settings.soundVolume * 100)), 0, 100) / 100f;
        PlayMusic(settings.title);
        BindButtons();
    }

    private AudioSource Source(string sourceName, bool loop)
    {
        var obj = new GameObject(sourceName); obj.transform.SetParent(transform, false);
        var source = obj.AddComponent<AudioSource>();
        source.playOnAwake = false; source.loop = loop; source.spatialBlend = 0;
        source.ignoreListenerPause = true;
        return source;
    }

    private void LateUpdate()
    {
        // Includes new reward/help buttons and serialized buttons whose listeners were replaced.
        BindButtons();
    }

    private void BindButtons()
    {
        destroyedButtons.Clear();
        foreach (var entry in boundButtons)
            if (entry.Key == null) destroyedButtons.Add(entry.Key);
        foreach (var button in destroyedButtons) boundButtons.Remove(button);
        foreach (var button in FindObjectsOfType<Button>(true))
        {
            if (boundButtons.TryGetValue(button, out var previous) && previous == button.onClick) continue;
            // A static listener also stays safe when a configured button is cloned.
            button.onClick.RemoveListener(PlayButton);
            button.onClick.AddListener(PlayButton);
            boundButtons[button] = button.onClick;
        }
    }

    private static void PlayButton()
    {
        if (instance == null || instance.settings == null || instance.buttonSoundFrame == Time.frameCount) return;
        instance.buttonSoundFrame = Time.frameCount;
        instance.PlaySound(instance.settings.button);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null) sounds.PlayOneShot(clip);
    }

    private void PlayMusic(AudioClip clip)
    {
        if (clip == null || (music.clip == clip && music.isPlaying)) return;
        music.clip = clip; music.Play();
    }

    public static void PlayTitle()
    {
        if (instance != null && instance.settings != null) instance.PlayMusic(instance.settings.title);
    }

    public static void PlayBattle(bool boss)
    {
        if (instance != null && instance.settings != null)
            instance.PlayMusic(boss ? instance.settings.boss : instance.settings.enemy);
    }

    public static void PlayDamage()
    {
        if (Application.isPlaying && instance != null && instance.settings != null) instance.PlaySound(instance.settings.damage);
    }

    public static void PlayRecovery()
    {
        if (instance != null && instance.settings != null) instance.PlaySound(instance.settings.recovery);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
