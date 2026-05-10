using UnityEngine;
using Vocalith.Audio;

namespace Kernel.Audio
{
    public static class LilithAudioSettings
    {
        public const string MasterVolumePrefsKey = "Options.Audio.MasterVolume";
        public const string MusicVolumePrefsKey = "Options.Audio.MusicVolume";
        public const string SfxVolumePrefsKey = "Options.Audio.SfxVolume";

        public const float DefaultMasterVolume = 0.8f;
        public const float DefaultMusicVolume = 0.8f;
        public const float DefaultSfxVolume = 0.8f;

        public static AudioManager ApplyStoredSettings()
        {
            AudioManager audioManager = AudioManager.GetOrCreateInstance();
            audioManager.SetVolumes(ReadMasterVolume(), ReadMusicVolume(), ReadSfxVolume());
            return audioManager;
        }

        public static float ReadMasterVolume()
        {
            return NormalizeVolume(PlayerPrefs.GetFloat(MasterVolumePrefsKey, DefaultMasterVolume));
        }

        public static float ReadMusicVolume()
        {
            return NormalizeVolume(PlayerPrefs.GetFloat(MusicVolumePrefsKey, DefaultMusicVolume));
        }

        public static float ReadSfxVolume()
        {
            return NormalizeVolume(PlayerPrefs.GetFloat(SfxVolumePrefsKey, DefaultSfxVolume));
        }

        private static float NormalizeVolume(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            return Mathf.Clamp01(value);
        }
    }
}
