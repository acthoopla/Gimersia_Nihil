using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// AudioSettingsManager
/// - Singleton, DontDestroyOnLoad
/// - Convert slider linear (0..1) -> dB, set ke AudioMixer
/// - Simpan ke PlayerPrefs agar persist antar scene
/// </summary>
public class AudioSettingsManager : MonoBehaviour
{
    public static AudioSettingsManager Instance { get; private set; }

    [Header("Mixer")]
    public AudioMixer audioMixer; // assign AudioMixer di Inspector

    // NAMA exposed parameter di AudioMixer (pastikan exact)
    const string MASTER_PARAM = "MasterVolume";
    const string MUSIC_PARAM = "MusicVolume";
    const string SFX_PARAM = "SFXVolume";

    // PlayerPrefs keys
    const string PREF_MASTER = "pref_master";
    const string PREF_MUSIC = "pref_music";
    const string PREF_SFX = "pref_sfx";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // default 1 jika belum ada
        if (!PlayerPrefs.HasKey(PREF_MASTER)) PlayerPrefs.SetFloat(PREF_MASTER, 1f);
        if (!PlayerPrefs.HasKey(PREF_MUSIC)) PlayerPrefs.SetFloat(PREF_MUSIC, 1f);
        if (!PlayerPrefs.HasKey(PREF_SFX)) PlayerPrefs.SetFloat(PREF_SFX, 1f);

        ApplyAllFromPrefs();
    }

    // convert linear 0..1 ke dB
    float LinearToDb(float linear)
    {
        linear = Mathf.Clamp(linear, 0f, 1f);
        if (linear <= 0.0001f) return -80f; // floor untuk "silent"
        return Mathf.Log10(linear) * 20f;
    }

    void ApplyAllFromPrefs()
    {
        float master = PlayerPrefs.GetFloat(PREF_MASTER, 1f);
        float music = PlayerPrefs.GetFloat(PREF_MUSIC, 1f);
        float sfx = PlayerPrefs.GetFloat(PREF_SFX, 1f);

        audioMixer.SetFloat(MASTER_PARAM, LinearToDb(master));
        audioMixer.SetFloat(MUSIC_PARAM, LinearToDb(music));
        audioMixer.SetFloat(SFX_PARAM, LinearToDb(sfx));
    }

    // API publik, dipanggil dari slider OnValueChanged
    public void SetMasterVolume(float linear)
    {
        PlayerPrefs.SetFloat(PREF_MASTER, linear);
        audioMixer.SetFloat(MASTER_PARAM, LinearToDb(linear));
    }

    public void SetMusicVolume(float linear)
    {
        PlayerPrefs.SetFloat(PREF_MUSIC, linear);
        audioMixer.SetFloat(MUSIC_PARAM, LinearToDb(linear));
    }

    public void SetSFXVolume(float linear)
    {
        PlayerPrefs.SetFloat(PREF_SFX, linear);
        audioMixer.SetFloat(SFX_PARAM, LinearToDb(linear));
    }

    // helper: ambil nilai linear untuk inisialisasi slider
    public float GetMasterLinear() => PlayerPrefs.GetFloat(PREF_MASTER, 1f);
    public float GetMusicLinear() => PlayerPrefs.GetFloat(PREF_MUSIC, 1f);
    public float GetSFXLinear() => PlayerPrefs.GetFloat(PREF_SFX, 1f);
}
