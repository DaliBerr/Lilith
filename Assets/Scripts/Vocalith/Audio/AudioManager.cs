using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Vocalith.Logging;

namespace Vocalith.Audio
{
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {
        private const int DefaultSfxPoolSize = 16;
        private const float DefaultMusicFadeSeconds = 0.5f;

        private readonly AudioSource[] musicSources = new AudioSource[2];
        private AudioSource[] sfxSources;
        private float[] sfxVolumeScales;

        private Coroutine musicFadeRoutine;
        private AsyncOperationHandle<AudioClip> activeMusicHandle;
        private bool hasActiveMusicHandle;
        private int activeMusicIndex;
        private int nextSfxIndex;
        private float masterVolume = 1f;
        private float musicVolume = 1f;
        private float sfxVolume = 1f;

        public static AudioManager Instance { get; private set; }

        public float MasterVolume => masterVolume;
        public float MusicVolume => musicVolume;
        public float SfxVolume => sfxVolume;
        public float EffectiveMusicVolume => masterVolume * musicVolume;
        public float EffectiveSfxVolume => masterVolume * sfxVolume;
        public int SfxPoolCount => sfxSources?.Length ?? 0;

        public static AudioManager GetOrCreateInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

#if UNITY_2023_1_OR_NEWER
            AudioManager manager = FindFirstObjectByType<AudioManager>();
#else
            AudioManager manager = FindObjectOfType<AudioManager>();
#endif
            if (manager == null)
            {
                GameObject audioObject = new("AudioManager");
                manager = audioObject.AddComponent<AudioManager>();
            }

            if (Instance == null)
            {
                manager.InitializeSingleton();
            }

            return manager;
        }

        public void SetVolumes(float master, float music, float sfx)
        {
            masterVolume = NormalizeVolume(master);
            musicVolume = NormalizeVolume(music);
            sfxVolume = NormalizeVolume(sfx);
            RefreshSourceVolumes();
        }

        public void SetMasterVolume(float value)
        {
            masterVolume = NormalizeVolume(value);
            RefreshSourceVolumes();
        }

        public void SetMusicVolume(float value)
        {
            musicVolume = NormalizeVolume(value);
            RefreshMusicVolumes();
        }

        public void SetSfxVolume(float value)
        {
            sfxVolume = NormalizeVolume(value);
            RefreshSfxVolumes();
        }

        public void PlayMusic(AudioClip clip, float fadeSeconds = DefaultMusicFadeSeconds, bool loop = true)
        {
            if (clip == null)
            {
                GameDebug.LogWarning("[AudioManager] PlayMusic ignored because clip is missing.");
                return;
            }

            ReleaseActiveMusicHandle();
            StartMusicTransition(clip, fadeSeconds, loop);
        }

        public IEnumerator PlayMusicByAddress(string address, float fadeSeconds = DefaultMusicFadeSeconds, bool loop = true)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                GameDebug.LogWarning("[AudioManager] PlayMusicByAddress ignored because address is empty.");
                yield break;
            }

            AsyncOperationHandle<AudioClip> handle = Addressables.LoadAssetAsync<AudioClip>(address.Trim());
            yield return handle;

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                GameDebug.LogWarning($"[AudioManager] Failed to load music address '{address}'.");
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }

                yield break;
            }

            ReleaseActiveMusicHandle();
            activeMusicHandle = handle;
            hasActiveMusicHandle = true;
            StartMusicTransition(handle.Result, fadeSeconds, loop);
        }

        public void StopMusic(float fadeSeconds = DefaultMusicFadeSeconds)
        {
            EnsureSources();

            if (musicFadeRoutine != null)
            {
                StopCoroutine(musicFadeRoutine);
                musicFadeRoutine = null;
            }

            AudioSource activeSource = musicSources[activeMusicIndex];
            StopInactiveMusicSources(activeSource);
            if (activeSource == null || activeSource.clip == null)
            {
                ReleaseActiveMusicHandle();
                return;
            }

            if (fadeSeconds <= 0f || !Application.isPlaying)
            {
                activeSource.Stop();
                activeSource.clip = null;
                activeSource.volume = 0f;
                ReleaseActiveMusicHandle();
                return;
            }

            musicFadeRoutine = StartCoroutine(FadeOutMusic(activeSource, Mathf.Max(0f, fadeSeconds), true));
        }

        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            PlaySfxInternal(clip, volumeScale);
        }

        public IEnumerator PlaySfxByAddress(string address, float volumeScale = 1f)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                GameDebug.LogWarning("[AudioManager] PlaySfxByAddress ignored because address is empty.");
                yield break;
            }

            AsyncOperationHandle<AudioClip> handle = Addressables.LoadAssetAsync<AudioClip>(address.Trim());
            yield return handle;

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                GameDebug.LogWarning($"[AudioManager] Failed to load sfx address '{address}'.");
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }

                yield break;
            }

            AudioSource source = PlaySfxInternal(handle.Result, volumeScale);
            while (source != null && source.isPlaying)
            {
                yield return null;
            }

            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            InitializeSingleton();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            ReleaseActiveMusicHandle();
        }

        private void PromoteToPersistentRoot()
        {
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }

            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void InitializeSingleton()
        {
            Instance = this;
            PromoteToPersistentRoot();
            EnsureSources();
            RefreshSourceVolumes();
        }

        private void EnsureSources()
        {
            EnsureMusicSource(0, "Music Source A");
            EnsureMusicSource(1, "Music Source B");

            if (sfxSources == null || sfxSources.Length != DefaultSfxPoolSize)
            {
                sfxSources = new AudioSource[DefaultSfxPoolSize];
                sfxVolumeScales = new float[DefaultSfxPoolSize];
                for (int i = 0; i < sfxVolumeScales.Length; i++)
                {
                    sfxVolumeScales[i] = 1f;
                }
            }

            for (int i = 0; i < sfxSources.Length; i++)
            {
                if (sfxSources[i] == null)
                {
                    GameObject sourceObject = new($"Sfx Source {i:00}");
                    sourceObject.transform.SetParent(transform, false);
                    sfxSources[i] = CreateAudioSource(sourceObject);
                }
            }
        }

        private void EnsureMusicSource(int index, string sourceName)
        {
            if (musicSources[index] != null)
            {
                return;
            }

            GameObject sourceObject = new(sourceName);
            sourceObject.transform.SetParent(transform, false);
            AudioSource source = CreateAudioSource(sourceObject);
            source.loop = true;
            musicSources[index] = source;
        }

        private static AudioSource CreateAudioSource(GameObject sourceObject)
        {
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        private void StartMusicTransition(AudioClip clip, float fadeSeconds, bool loop)
        {
            EnsureSources();

            AudioSource activeSource = musicSources[activeMusicIndex];
            if (activeSource != null && activeSource.clip == clip && activeSource.isPlaying)
            {
                activeSource.loop = loop;
                activeSource.volume = EffectiveMusicVolume;
                return;
            }

            if (musicFadeRoutine != null)
            {
                StopCoroutine(musicFadeRoutine);
                musicFadeRoutine = null;
            }

            int nextMusicIndex = 1 - activeMusicIndex;
            AudioSource nextSource = musicSources[nextMusicIndex];
            nextSource.Stop();
            nextSource.clip = clip;
            nextSource.loop = loop;
            nextSource.volume = 0f;
            nextSource.Play();

            if (fadeSeconds <= 0f || !Application.isPlaying)
            {
                if (activeSource != null)
                {
                    activeSource.Stop();
                    activeSource.clip = null;
                    activeSource.volume = 0f;
                }

                nextSource.volume = EffectiveMusicVolume;
                activeMusicIndex = nextMusicIndex;
                return;
            }

            activeMusicIndex = nextMusicIndex;
            musicFadeRoutine = StartCoroutine(CrossfadeMusic(activeSource, nextSource, nextMusicIndex, Mathf.Max(0f, fadeSeconds)));
        }

        private IEnumerator CrossfadeMusic(AudioSource fromSource, AudioSource toSource, int nextMusicIndex, float fadeSeconds)
        {
            float elapsed = 0f;
            float fromStartVolume = fromSource != null ? fromSource.volume : 0f;

            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeSeconds);
                float targetVolume = EffectiveMusicVolume;

                if (fromSource != null)
                {
                    fromSource.volume = Mathf.Lerp(fromStartVolume, 0f, t);
                }

                toSource.volume = Mathf.Lerp(0f, targetVolume, t);
                yield return null;
            }

            if (fromSource != null)
            {
                fromSource.Stop();
                fromSource.clip = null;
                fromSource.volume = 0f;
            }

            toSource.volume = EffectiveMusicVolume;
            activeMusicIndex = nextMusicIndex;
            musicFadeRoutine = null;
        }

        private IEnumerator FadeOutMusic(AudioSource source, float fadeSeconds, bool releaseHandleOnComplete)
        {
            float elapsed = 0f;
            float startVolume = source.volume;

            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / fadeSeconds));
                yield return null;
            }

            source.Stop();
            source.clip = null;
            source.volume = 0f;

            if (releaseHandleOnComplete)
            {
                ReleaseActiveMusicHandle();
            }

            musicFadeRoutine = null;
        }

        private void StopInactiveMusicSources(AudioSource keepSource)
        {
            for (int i = 0; i < musicSources.Length; i++)
            {
                AudioSource source = musicSources[i];
                if (source == null || source == keepSource)
                {
                    continue;
                }

                source.Stop();
                source.clip = null;
                source.volume = 0f;
            }
        }

        private AudioSource PlaySfxInternal(AudioClip clip, float volumeScale)
        {
            if (clip == null)
            {
                GameDebug.LogWarning("[AudioManager] PlaySfx ignored because clip is missing.");
                return null;
            }

            EnsureSources();

            int sourceIndex = FindAvailableSfxSourceIndex();
            AudioSource source = sfxSources[sourceIndex];
            float normalizedScale = NormalizeVolume(volumeScale);
            sfxVolumeScales[sourceIndex] = normalizedScale;

            source.Stop();
            source.clip = clip;
            source.loop = false;
            source.volume = EffectiveSfxVolume * normalizedScale;
            source.Play();

            nextSfxIndex = (sourceIndex + 1) % sfxSources.Length;
            return source;
        }

        private int FindAvailableSfxSourceIndex()
        {
            for (int i = 0; i < sfxSources.Length; i++)
            {
                int index = (nextSfxIndex + i) % sfxSources.Length;
                if (!sfxSources[index].isPlaying)
                {
                    return index;
                }
            }

            return nextSfxIndex;
        }

        private void RefreshSourceVolumes()
        {
            RefreshMusicVolumes();
            RefreshSfxVolumes();
        }

        private void RefreshMusicVolumes()
        {
            for (int i = 0; i < musicSources.Length; i++)
            {
                if (musicSources[i] != null && musicSources[i].isPlaying)
                {
                    musicSources[i].volume = i == activeMusicIndex ? EffectiveMusicVolume : musicSources[i].volume;
                }
            }
        }

        private void RefreshSfxVolumes()
        {
            if (sfxSources == null || sfxVolumeScales == null)
            {
                return;
            }

            for (int i = 0; i < sfxSources.Length; i++)
            {
                if (sfxSources[i] != null)
                {
                    sfxSources[i].volume = EffectiveSfxVolume * sfxVolumeScales[i];
                }
            }
        }

        private void ReleaseActiveMusicHandle()
        {
            if (!hasActiveMusicHandle)
            {
                return;
            }

            if (activeMusicHandle.IsValid())
            {
                Addressables.Release(activeMusicHandle);
            }

            hasActiveMusicHandle = false;
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
