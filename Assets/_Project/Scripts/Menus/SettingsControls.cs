using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WSFGameJam2026.Menus
{
    /// <summary>
    /// The controls of the settings screen: master volume and full screen. The volume is saved between sessions and
    /// applied at the start, so keep this component on an object that is active from the start (not on the screen).
    /// </summary>
    public class SettingsControls : MonoBehaviour
    {
        private const string VolumeKey = "WSFGameJam2026.Volume";

        [SerializeField]
        private Slider volumeSlider;

        [Tooltip("Optional. The volume in percent.")]
        [SerializeField]
        private TMP_Text volumeLabel;

        [SerializeField]
        private Toggle fullScreenToggle;

        private void OnEnable()
        {
            if (volumeSlider)
                volumeSlider.onValueChanged.AddListener(VolumeSlider_OnValueChanged);

            if (fullScreenToggle)
                fullScreenToggle.onValueChanged.AddListener(FullScreenToggle_OnValueChanged);
        }

        private void Start()
        {
            float volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
            ApplyVolume(volume);
            if (volumeSlider)
                volumeSlider.SetValueWithoutNotify(volume);

            if (fullScreenToggle)
                fullScreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
        }

        private void VolumeSlider_OnValueChanged(float volume)
        {
            ApplyVolume(volume);
            PlayerPrefs.SetFloat(VolumeKey, volume);
        }

        private void FullScreenToggle_OnValueChanged(bool isOn)
        {
            Screen.fullScreen = isOn;
        }

        private void ApplyVolume(float volume)
        {
            AudioListener.volume = Mathf.Clamp01(volume);
            if (volumeLabel)
                volumeLabel.text = $"{Mathf.RoundToInt(volume * 100f)}%";
        }

        private void OnDisable()
        {
            if (volumeSlider)
                volumeSlider.onValueChanged.RemoveListener(VolumeSlider_OnValueChanged);

            if (fullScreenToggle)
                fullScreenToggle.onValueChanged.RemoveListener(FullScreenToggle_OnValueChanged);

            PlayerPrefs.Save();
        }
    }
}
