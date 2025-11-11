using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// AudioSettingsUI
/// - Inisialisasi slider dari AudioSettingsManager
/// - Hook slider -> AudioSettingsManager methods
/// </summary>
public class AudioSettingsUI : MonoBehaviour
{
    [Header("Sliders")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;

    void Start()
    {
        var m = AudioSettingsManager.Instance;
        if (m == null)
        {
            Debug.LogWarning("AudioSettingsManager tidak ditemukan. Pastikan ada AudioManager di scene awal.");
            return;
        }

        // set initial slider positions
        if (masterSlider != null) masterSlider.value = m.GetMasterLinear();
        if (musicSlider != null) musicSlider.value = m.GetMusicLinear();
        if (sfxSlider != null) sfxSlider.value = m.GetSFXLinear();

        // hook OnValueChanged
        if (masterSlider != null) masterSlider.onValueChanged.AddListener(m.SetMasterVolume);
        if (musicSlider != null) musicSlider.onValueChanged.AddListener(m.SetMusicVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(m.SetSFXVolume);
    }

    void OnDestroy()
    {
        // lepaskan listener untuk kebersihan
        if (AudioSettingsManager.Instance != null)
        {
            if (masterSlider != null) masterSlider.onValueChanged.RemoveListener(AudioSettingsManager.Instance.SetMasterVolume);
            if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(AudioSettingsManager.Instance.SetMusicVolume);
            if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(AudioSettingsManager.Instance.SetSFXVolume);
        }
    }
}
