using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Vocalith.Logging;

namespace Vocalith.Audio
{
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {
        private const int DefaultSfxPoolSize = 32;
        private const float DefaultMusicFadeSeconds = 0.5f;

        public const int HighestPriority = 1000;

        private static readonly Vocalith.Random RandomSource = new();

        private readonly AudioSource[] musicSources = new AudioSource[2];
        private readonly Dictionary<string, CachedAddressClip> cachedAddressClips = new(StringComparer.Ordinal);
        private readonly Dictionary<AudioCue, HashSet<string>> retainedCueAddresses = new();
        private readonly Dictionary<AudioCue, int> retainedCueCounts = new();
        private readonly HashSet<AudioCueBank> retainedBanks = new();
        private readonly Dictionary<AudioCue, float> lastCuePlayTimes = new();
        private readonly Dictionary<AudioCue, AudioCueVariant> lastSelectedVariants = new();
        private readonly Dictionary<int, HandleBinding> handleBindings = new();

        private AudioSource[] sfxSources;
        private PlaybackState[] sfxStates;
        private MusicPlaybackState musicState;

        private Coroutine musicFadeRoutine;
        private Coroutine musicSequenceRoutine;
        private int activeMusicIndex;
        private int nextSfxIndex;
        private int nextHandleId = 1;
        private int nextGeneration = 1;
        private float masterVolume = 1f;
        private float musicVolume = 1f;
        private float sfxVolume = 1f;
        private bool isAudioPaused;

        private int acceptedCount;
        private int rejectedCount;
        private int stolenCount;
        private int cacheHitCount;
        private int cacheMissCount;

        public static AudioManager Instance { get; private set; }

        public float MasterVolume => masterVolume;
        public float MusicVolume => musicVolume;
        public float SfxVolume => sfxVolume;
        public float EffectiveMusicVolume => masterVolume * musicVolume;
        public float EffectiveSfxVolume => masterVolume * sfxVolume;
        public int SfxPoolCount => sfxSources?.Length ?? 0;
        public bool IsAudioPaused => isAudioPaused;

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

        public AudioPlaybackHandle PlayCue(AudioCue cue, AudioCuePlayRequest request = default)
        {
            return PlayCueInternal(cue, null, request);
        }

        public AudioPlaybackHandle PlayCueAt(AudioCue cue, Vector3 position, AudioCuePlayRequest request = default)
        {
            request.HasPosition = true;
            request.Position = position;
            return PlayCue(cue, request);
        }

        public AudioPlaybackHandle PlayCueFollow(AudioCue cue, Transform target, AudioCuePlayRequest request = default)
        {
            if (target == null)
            {
                GameDebug.LogWarning("[AudioManager] PlayCueFollow ignored because target is missing.");
                rejectedCount++;
                return AudioPlaybackHandle.Invalid;
            }

            request.HasPosition = true;
            request.Position = target.position;
            return PlayCueInternal(cue, target, request);
        }

        public void StopMusic(float fadeSeconds = DefaultMusicFadeSeconds)
        {
            EnsureSources();

            if (!musicState.Active)
            {
                StopAllMusicSources();
                return;
            }

            StopMusicState(fadeSeconds);
        }

        public void SetAudioPaused(bool paused)
        {
            if (isAudioPaused == paused)
            {
                return;
            }

            isAudioPaused = paused;
            EnsureSources();
            RefreshSfxPlaybackStates();

            if (paused)
            {
                ApplyPauseToActiveSources();
                return;
            }

            ResumeSourcesPausedByManager();
        }

        public IEnumerator PreloadCue(AudioCue cue)
        {
            if (!ValidateCue(cue))
            {
                yield break;
            }

            if (!cue.HasPlayableVariant)
            {
                GameDebug.LogWarning($"[AudioManager] PreloadCue ignored because cue '{cue.name}' has no playable variant.");
                yield break;
            }

            if (retainedCueCounts.TryGetValue(cue, out int retainCount))
            {
                retainedCueCounts[cue] = retainCount + 1;
                yield break;
            }

            HashSet<string> addresses = CollectVariantAddresses(cue);
            retainedCueCounts[cue] = 1;
            retainedCueAddresses[cue] = addresses;

            foreach (string address in addresses)
            {
                if (cachedAddressClips.TryGetValue(address, out CachedAddressClip cached))
                {
                    cached.RetainCount++;
                    continue;
                }

                AsyncOperationHandle<AudioClip> handle = Addressables.LoadAssetAsync<AudioClip>(address);
                yield return handle;

                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
                    GameDebug.LogWarning($"[AudioManager] Failed to preload audio cue '{cue.name}' at '{address}'.");
                    if (handle.IsValid())
                    {
                        Addressables.Release(handle);
                    }

                    continue;
                }

                cachedAddressClips[address] = new CachedAddressClip(handle);
            }
        }

        public void ReleaseCue(AudioCue cue)
        {
            if (cue == null || !retainedCueCounts.TryGetValue(cue, out int retainCount))
            {
                return;
            }

            if (retainCount > 1)
            {
                retainedCueCounts[cue] = retainCount - 1;
                return;
            }

            retainedCueCounts.Remove(cue);

            if (!retainedCueAddresses.TryGetValue(cue, out HashSet<string> addresses))
            {
                return;
            }

            foreach (string address in addresses)
            {
                ReleaseAddress(address);
            }

            retainedCueAddresses.Remove(cue);
        }

        public IEnumerator PreloadBank(AudioCueBank bank)
        {
            if (bank == null || retainedBanks.Contains(bank))
            {
                yield break;
            }

            retainedBanks.Add(bank);
            IReadOnlyList<AudioCue> cues = bank.Cues;
            for (int i = 0; i < cues.Count; i++)
            {
                if (cues[i] != null)
                {
                    yield return PreloadCue(cues[i]);
                }
            }
        }

        public void ReleaseBank(AudioCueBank bank)
        {
            if (bank == null || !retainedBanks.Remove(bank))
            {
                return;
            }

            IReadOnlyList<AudioCue> cues = bank.Cues;
            for (int i = 0; i < cues.Count; i++)
            {
                ReleaseCue(cues[i]);
            }
        }

        public void ReleaseAllCachedCues()
        {
            retainedBanks.Clear();
            retainedCueCounts.Clear();
            retainedCueAddresses.Clear();

            foreach (CachedAddressClip cached in cachedAddressClips.Values)
            {
                cached.Release();
            }

            cachedAddressClips.Clear();
        }

        public AudioManagerStats GetStats()
        {
            RefreshSfxPlaybackStates();

            AudioManagerStats stats = new()
            {
                AcceptedCount = acceptedCount,
                RejectedCount = rejectedCount,
                StolenCount = stolenCount,
                CacheHitCount = cacheHitCount,
                CacheMissCount = cacheMissCount,
            };

            for (int i = 0; i < sfxStates.Length; i++)
            {
                PlaybackState state = sfxStates[i];
                if (!state.Active)
                {
                    continue;
                }

                stats.ActiveVoiceCount++;
                if (state.IsLoop)
                {
                    stats.ActiveLoopCount++;
                }

                stats.AddCategory(state.Category);
            }

            if (musicState.Active)
            {
                stats.ActiveVoiceCount++;
                stats.ActiveLoopCount++;
            }

            return stats;
        }

        public void ResetStats()
        {
            acceptedCount = 0;
            rejectedCount = 0;
            stolenCount = 0;
            cacheHitCount = 0;
            cacheMissCount = 0;
        }

        internal bool IsHandleValid(int id, int generation)
        {
            if (!handleBindings.TryGetValue(id, out HandleBinding binding) || binding.Generation != generation)
            {
                return false;
            }

            return binding.IsMusic
                ? musicState.Active && musicState.HandleId == id && musicState.Generation == generation
                : IsSfxHandleValid(binding.Index, id, generation);
        }

        internal bool IsHandlePlaying(int id, int generation)
        {
            if (!IsHandleValid(id, generation))
            {
                return false;
            }

            HandleBinding binding = handleBindings[id];
            AudioSource source = binding.IsMusic ? musicState.Source : sfxSources[binding.Index];
            return source != null && source.isPlaying;
        }

        internal bool StopHandle(int id, int generation, float fadeOutOverride)
        {
            if (!IsHandleValid(id, generation))
            {
                return false;
            }

            HandleBinding binding = handleBindings[id];
            if (binding.IsMusic)
            {
                float fade = fadeOutOverride >= 0f ? fadeOutOverride : musicState.FadeOutSeconds;
                StopMusicState(fade);
                return true;
            }

            float sfxFade = fadeOutOverride >= 0f ? fadeOutOverride : sfxStates[binding.Index].FadeOutSeconds;
            StopSfxState(binding.Index, sfxFade);
            return true;
        }

        internal bool PauseHandle(int id, int generation)
        {
            if (!IsHandleValid(id, generation))
            {
                return false;
            }

            HandleBinding binding = handleBindings[id];
            AudioSource source = binding.IsMusic ? musicState.Source : sfxSources[binding.Index];
            if (source == null)
            {
                return false;
            }

            source.Pause();
            if (binding.IsMusic)
            {
                musicState.ManuallyPaused = true;
            }
            else
            {
                sfxStates[binding.Index].ManuallyPaused = true;
            }

            return true;
        }

        internal bool ResumeHandle(int id, int generation)
        {
            if (!IsHandleValid(id, generation))
            {
                return false;
            }

            HandleBinding binding = handleBindings[id];
            AudioSource source = binding.IsMusic ? musicState.Source : sfxSources[binding.Index];
            if (source == null)
            {
                return false;
            }

            source.UnPause();
            if (binding.IsMusic)
            {
                musicState.ManuallyPaused = false;
            }
            else
            {
                sfxStates[binding.Index].ManuallyPaused = false;
            }

            return true;
        }

        internal bool SetHandleVolumeScale(int id, int generation, float volumeScale)
        {
            if (!IsHandleValid(id, generation))
            {
                return false;
            }

            float normalized = NormalizeVolume(volumeScale);
            HandleBinding binding = handleBindings[id];
            if (binding.IsMusic)
            {
                musicState.VolumeScale = normalized;
                if (musicState.Source != null)
                {
                    musicState.Source.volume = EffectiveMusicVolume * normalized;
                }

                return true;
            }

            sfxStates[binding.Index].VolumeScale = normalized;
            if (sfxSources[binding.Index] != null)
            {
                sfxSources[binding.Index].volume = EffectiveSfxVolume * normalized;
            }

            return true;
        }

        internal bool SetHandlePitch(int id, int generation, float pitch)
        {
            if (!IsHandleValid(id, generation))
            {
                return false;
            }

            HandleBinding binding = handleBindings[id];
            AudioSource source = binding.IsMusic ? musicState.Source : sfxSources[binding.Index];
            if (source == null)
            {
                return false;
            }

            source.pitch = SanitizePitch(pitch);
            return true;
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

        private void LateUpdate()
        {
            EnsureSources();
            RefreshSfxPlaybackStates();
            UpdateFollowTargets();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            ReleaseAllCachedCues();
        }

        private void InitializeSingleton()
        {
            Instance = this;
            PromoteToPersistentRoot();
            EnsureSources();
            RefreshSourceVolumes();
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

        private void EnsureSources()
        {
            EnsureMusicSource(0, "Music Source A");
            EnsureMusicSource(1, "Music Source B");

            if (sfxSources == null || sfxSources.Length != DefaultSfxPoolSize)
            {
                sfxSources = new AudioSource[DefaultSfxPoolSize];
                sfxStates = new PlaybackState[DefaultSfxPoolSize];
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
            musicSources[index] = CreateAudioSource(sourceObject);
        }

        private static AudioSource CreateAudioSource(GameObject sourceObject)
        {
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        private AudioPlaybackHandle PlayCueInternal(AudioCue cue, Transform followTarget, AudioCuePlayRequest request)
        {
            if (!ValidateCue(cue))
            {
                rejectedCount++;
                return AudioPlaybackHandle.Invalid;
            }

            if (!cue.HasPlayableVariant)
            {
                GameDebug.LogWarning($"[AudioManager] Cue '{cue.name}' has no playable variant.");
                rejectedCount++;
                return AudioPlaybackHandle.Invalid;
            }

            AudioCueCategory category = request.HasCategoryOverride ? request.CategoryOverride : cue.Category;
            AudioCuePausePolicy pausePolicy = ResolvePausePolicy(cue.Kind, category, cue.PausePolicy);
            if (isAudioPaused && pausePolicy != AudioCuePausePolicy.Continue)
            {
                rejectedCount++;
                return AudioPlaybackHandle.Invalid;
            }

            return cue.Kind == AudioCueKind.Music
                ? PlayMusicCue(cue, category, pausePolicy, request)
                : PlaySfxCue(cue, category, pausePolicy, followTarget, request);
        }

        private AudioPlaybackHandle PlayMusicCue(
            AudioCue cue,
            AudioCueCategory category,
            AudioCuePausePolicy pausePolicy,
            AudioCuePlayRequest request)
        {
            IReadOnlyList<AudioCueVariant> loopVariants = HasPlayableVariant(cue.LoopVariants) ? cue.LoopVariants : cue.Variants;
            AudioCueVariant introVariant = SelectVariant(cue, cue.IntroVariants);
            AudioCueVariant loopVariant = SelectVariant(cue, loopVariants);

            if (introVariant == null && loopVariant == null)
            {
                GameDebug.LogWarning($"[AudioManager] Music cue '{cue.name}' has no intro or loop variant.");
                rejectedCount++;
                return AudioPlaybackHandle.Invalid;
            }

            if (loopVariant == null)
            {
                loopVariant = introVariant;
                introVariant = null;
            }

            if (!TryResolveVariantClip(cue, loopVariant, out AudioClip loopClip)
                || (introVariant != null && !TryResolveVariantClip(cue, introVariant, out _)))
            {
                rejectedCount++;
                return AudioPlaybackHandle.Invalid;
            }

            AudioClip introClip = null;
            if (introVariant != null && !TryResolveVariantClip(cue, introVariant, out introClip))
            {
                rejectedCount++;
                return AudioPlaybackHandle.Invalid;
            }

            int handleId = AllocateHandleId();
            int generation = AllocateGeneration();
            float volumeScale = ResolveVolumeScale(cue, request);
            musicState = new MusicPlaybackState
            {
                Active = true,
                HandleId = handleId,
                Generation = generation,
                Cue = cue,
                Category = category,
                PausePolicy = pausePolicy,
                VolumeScale = volumeScale,
                FadeOutSeconds = cue.FadeOutSeconds,
            };
            handleBindings[handleId] = new HandleBinding(true, -1, generation);

            if (musicSequenceRoutine != null)
            {
                StopCoroutine(musicSequenceRoutine);
                musicSequenceRoutine = null;
            }

            if (introClip != null)
            {
                AudioSource introSource = StartMusicTransition(introClip, cue.FadeInSeconds, false, volumeScale);
                if (Application.isPlaying)
                {
                    musicSequenceRoutine = StartCoroutine(PlayMusicLoopAfterIntro(handleId, generation, introSource, loopClip, cue.FadeInSeconds, volumeScale));
                }
                else
                {
                    StartMusicTransition(loopClip, cue.FadeInSeconds, true, volumeScale);
                }
            }
            else
            {
                StartMusicTransition(loopClip, cue.FadeInSeconds, true, volumeScale);
            }

            acceptedCount++;
            return new AudioPlaybackHandle(this, handleId, generation);
        }

        private IEnumerator PlayMusicLoopAfterIntro(
            int handleId,
            int generation,
            AudioSource introSource,
            AudioClip loopClip,
            float fadeSeconds,
            float volumeScale)
        {
            while (IsHandleValid(handleId, generation)
                && introSource != null
                && introSource.clip != null
                && introSource.isPlaying)
            {
                yield return null;
            }

            if (IsHandleValid(handleId, generation))
            {
                StartMusicTransition(loopClip, fadeSeconds, true, volumeScale);
            }

            musicSequenceRoutine = null;
        }

        private AudioSource StartMusicTransition(AudioClip clip, float fadeSeconds, bool loop, float volumeScale)
        {
            EnsureSources();

            if (musicFadeRoutine != null)
            {
                StopCoroutine(musicFadeRoutine);
                musicFadeRoutine = null;
            }

            AudioSource fromSource = musicState.Source;
            if (fromSource == null)
            {
                fromSource = musicSources[activeMusicIndex];
            }

            int nextMusicIndex = 1 - activeMusicIndex;
            AudioSource toSource = musicSources[nextMusicIndex];
            toSource.Stop();
            toSource.clip = clip;
            toSource.loop = loop;
            toSource.pitch = 1f;
            toSource.spatialBlend = 0f;
            toSource.volume = 0f;
            toSource.Play();

            activeMusicIndex = nextMusicIndex;
            musicState.Source = toSource;

            if (fadeSeconds <= 0f || !Application.isPlaying)
            {
                if (fromSource != null && fromSource != toSource)
                {
                    fromSource.Stop();
                    fromSource.clip = null;
                    fromSource.volume = 0f;
                }

                toSource.volume = EffectiveMusicVolume * volumeScale;
                return toSource;
            }

            musicFadeRoutine = StartCoroutine(CrossfadeMusic(fromSource, toSource, Mathf.Max(0f, fadeSeconds), volumeScale));
            return toSource;
        }

        private IEnumerator CrossfadeMusic(AudioSource fromSource, AudioSource toSource, float fadeSeconds, float volumeScale)
        {
            float elapsed = 0f;
            float fromStartVolume = fromSource != null ? fromSource.volume : 0f;

            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeSeconds);

                if (fromSource != null && fromSource != toSource)
                {
                    fromSource.volume = Mathf.Lerp(fromStartVolume, 0f, t);
                }

                toSource.volume = Mathf.Lerp(0f, EffectiveMusicVolume * volumeScale, t);
                yield return null;
            }

            if (fromSource != null && fromSource != toSource)
            {
                fromSource.Stop();
                fromSource.clip = null;
                fromSource.volume = 0f;
            }

            toSource.volume = EffectiveMusicVolume * volumeScale;
            musicFadeRoutine = null;
        }

        private AudioPlaybackHandle PlaySfxCue(
            AudioCue cue,
            AudioCueCategory category,
            AudioCuePausePolicy pausePolicy,
            Transform followTarget,
            AudioCuePlayRequest request)
        {
            AudioCueVariant variant = SelectVariant(cue, cue.Variants);
            if (variant == null || !TryResolveVariantClip(cue, variant, out AudioClip clip))
            {
                rejectedCount++;
                return AudioPlaybackHandle.Invalid;
            }

            EnsureSources();
            RefreshSfxPlaybackStates();

            int priority = cue.Priority + request.PriorityOffset;
            if (IsBlockedByCooldown(cue, priority))
            {
                rejectedCount++;
                return AudioPlaybackHandle.Invalid;
            }

            if (!TryResolveCueSourceIndex(cue, category, priority, out int sourceIndex))
            {
                rejectedCount++;
                return AudioPlaybackHandle.Invalid;
            }

            if (sfxStates[sourceIndex].Active)
            {
                stolenCount++;
                StopSfxState(sourceIndex, 0f);
            }

            AudioSource source = sfxSources[sourceIndex];
            float volumeScale = ResolveVolumeScale(cue, request);
            float pitch = ResolvePitch(cue);
            bool useWorldPosition = cue.SpatialMode == AudioCueSpatialMode.WorldPosition && request.HasPosition;
            if (cue.SpatialMode == AudioCueSpatialMode.WorldPosition && !request.HasPosition)
            {
                GameDebug.LogWarning($"[AudioManager] Cue '{cue.name}' requested world audio but no position was provided. Playing as 2D.");
            }

            int handleId = AllocateHandleId();
            int generation = AllocateGeneration();
            bool isLoop = cue.PlaybackMode == AudioCuePlaybackMode.Loop;
            ConfigureSfxSource(source, clip, volumeScale, pitch, request.Position, useWorldPosition, cue.MinDistance, cue.MaxDistance, isLoop);
            SetSfxState(sourceIndex, cue, category, priority, volumeScale, cue.FadeOutSeconds, isLoop, pausePolicy, followTarget, handleId, generation);
            handleBindings[handleId] = new HandleBinding(false, sourceIndex, generation);
            lastCuePlayTimes[cue] = Time.unscaledTime;
            nextSfxIndex = (sourceIndex + 1) % sfxSources.Length;
            acceptedCount++;

            if (cue.FadeInSeconds > 0f && Application.isPlaying)
            {
                source.volume = 0f;
                sfxStates[sourceIndex].FadeRoutine = StartCoroutine(FadeSfxVolume(sourceIndex, EffectiveSfxVolume * volumeScale, cue.FadeInSeconds));
            }

            return new AudioPlaybackHandle(this, handleId, generation);
        }

        private void ConfigureSfxSource(
            AudioSource source,
            AudioClip clip,
            float volumeScale,
            float pitch,
            Vector3 position,
            bool useWorldPosition,
            float minDistance,
            float maxDistance,
            bool loop)
        {
            source.Stop();
            source.clip = clip;
            source.loop = loop;
            source.pitch = pitch;
            source.volume = EffectiveSfxVolume * volumeScale;
            source.spatialBlend = useWorldPosition ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = Mathf.Max(0f, minDistance);
            source.maxDistance = Mathf.Max(source.minDistance + 0.01f, maxDistance);

            if (useWorldPosition)
            {
                source.transform.position = position;
            }
            else
            {
                source.transform.SetParent(transform, false);
                source.transform.localPosition = Vector3.zero;
            }

            source.Play();
        }

        private IEnumerator FadeSfxVolume(int index, float targetVolume, float fadeSeconds)
        {
            AudioSource source = sfxSources[index];
            float elapsed = 0f;
            float startVolume = source != null ? source.volume : 0f;

            while (elapsed < fadeSeconds && index >= 0 && index < sfxStates.Length && sfxStates[index].Active)
            {
                elapsed += Time.unscaledDeltaTime;
                if (source != null)
                {
                    source.volume = Mathf.Lerp(startVolume, targetVolume, Mathf.Clamp01(elapsed / fadeSeconds));
                }

                yield return null;
            }

            if (source != null && index >= 0 && index < sfxStates.Length && sfxStates[index].Active)
            {
                source.volume = targetVolume;
            }

            if (index >= 0 && index < sfxStates.Length)
            {
                sfxStates[index].FadeRoutine = null;
            }
        }

        private void SetSfxState(
            int index,
            AudioCue cue,
            AudioCueCategory category,
            int priority,
            float volumeScale,
            float fadeOutSeconds,
            bool isLoop,
            AudioCuePausePolicy pausePolicy,
            Transform followTarget,
            int handleId,
            int generation)
        {
            sfxStates[index] = new PlaybackState
            {
                Active = true,
                Cue = cue,
                Category = category,
                Priority = priority,
                StartTime = Time.unscaledTime,
                VolumeScale = volumeScale,
                FadeOutSeconds = fadeOutSeconds,
                IsLoop = isLoop,
                PausePolicy = pausePolicy,
                FollowTarget = followTarget,
                HasFollowTarget = followTarget != null,
                HandleId = handleId,
                Generation = generation,
            };
        }

        private void StopSfxState(int index, float fadeSeconds)
        {
            if (index < 0 || index >= sfxStates.Length || !sfxStates[index].Active)
            {
                return;
            }

            AudioSource source = sfxSources[index];
            if (sfxStates[index].FadeRoutine != null)
            {
                StopCoroutine(sfxStates[index].FadeRoutine);
                sfxStates[index].FadeRoutine = null;
            }

            if (fadeSeconds > 0f && Application.isPlaying && source != null && source.clip != null)
            {
                sfxStates[index].FadeRoutine = StartCoroutine(FadeOutSfxAndClear(index, source, fadeSeconds));
                return;
            }

            ClearSfxState(index, true);
        }

        private IEnumerator FadeOutSfxAndClear(int index, AudioSource source, float fadeSeconds)
        {
            float elapsed = 0f;
            float startVolume = source != null ? source.volume : 0f;

            while (elapsed < fadeSeconds && index >= 0 && index < sfxStates.Length && sfxStates[index].Active)
            {
                elapsed += Time.unscaledDeltaTime;
                if (source != null)
                {
                    source.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / fadeSeconds));
                }

                yield return null;
            }

            ClearSfxState(index, true);
        }

        private void ClearSfxState(int index, bool stopSource)
        {
            if (index < 0 || index >= sfxStates.Length)
            {
                return;
            }

            PlaybackState state = sfxStates[index];
            if (state.HandleId != 0)
            {
                handleBindings.Remove(state.HandleId);
            }

            AudioSource source = sfxSources[index];
            if (stopSource && source != null)
            {
                source.Stop();
                source.clip = null;
                source.volume = 0f;
                source.loop = false;
            }

            sfxStates[index] = default;
        }

        private void StopMusicState(float fadeSeconds)
        {
            if (!musicState.Active)
            {
                return;
            }

            if (musicSequenceRoutine != null)
            {
                StopCoroutine(musicSequenceRoutine);
                musicSequenceRoutine = null;
            }

            if (musicFadeRoutine != null)
            {
                StopCoroutine(musicFadeRoutine);
                musicFadeRoutine = null;
            }

            AudioSource source = musicState.Source;
            if (fadeSeconds > 0f && Application.isPlaying && source != null && source.clip != null)
            {
                musicFadeRoutine = StartCoroutine(FadeOutMusicAndClear(source, fadeSeconds, musicState.HandleId));
                return;
            }

            ClearMusicState(true);
        }

        private IEnumerator FadeOutMusicAndClear(AudioSource source, float fadeSeconds, int handleId)
        {
            float elapsed = 0f;
            float startVolume = source != null ? source.volume : 0f;

            while (elapsed < fadeSeconds && musicState.Active && musicState.HandleId == handleId)
            {
                elapsed += Time.unscaledDeltaTime;
                if (source != null)
                {
                    source.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / fadeSeconds));
                }

                yield return null;
            }

            if (musicState.Active && musicState.HandleId == handleId)
            {
                ClearMusicState(true);
            }

            musicFadeRoutine = null;
        }

        private void ClearMusicState(bool stopSources)
        {
            if (musicState.HandleId != 0)
            {
                handleBindings.Remove(musicState.HandleId);
            }

            if (stopSources)
            {
                StopAllMusicSources();
            }

            musicState = default;
        }

        private void StopAllMusicSources()
        {
            for (int i = 0; i < musicSources.Length; i++)
            {
                AudioSource source = musicSources[i];
                if (source == null)
                {
                    continue;
                }

                source.Stop();
                source.clip = null;
                source.volume = 0f;
            }
        }

        private bool TryResolveCueSourceIndex(AudioCue cue, AudioCueCategory category, int priority, out int sourceIndex)
        {
            sourceIndex = -1;

            int selfCount = CountPlayingCue(cue);
            if (selfCount >= cue.MaxSimultaneousSelf)
            {
                int candidate = FindLowestPriorityCueSource(cue);
                if (candidate < 0 || priority <= sfxStates[candidate].Priority)
                {
                    return false;
                }

                sourceIndex = candidate;
            }

            int categoryCount = CountPlayingCategory(category);
            if (categoryCount >= GetCategoryLimit(category))
            {
                int candidate = FindLowestPriorityCategorySource(category);
                if (candidate < 0 || priority <= sfxStates[candidate].Priority)
                {
                    return false;
                }

                if (sourceIndex < 0 || sfxStates[candidate].Priority < sfxStates[sourceIndex].Priority)
                {
                    sourceIndex = candidate;
                }
            }

            if (sourceIndex >= 0)
            {
                return true;
            }

            sourceIndex = FindAvailableSfxSourceIndex();
            if (sourceIndex >= 0)
            {
                return true;
            }

            int globalCandidate = FindLowestPrioritySfxSource();
            if (globalCandidate >= 0 && priority > sfxStates[globalCandidate].Priority)
            {
                sourceIndex = globalCandidate;
                return true;
            }

            sourceIndex = -1;
            return false;
        }

        private int FindAvailableSfxSourceIndex()
        {
            for (int i = 0; i < sfxSources.Length; i++)
            {
                int index = (nextSfxIndex + i) % sfxSources.Length;
                if (!sfxStates[index].Active)
                {
                    return index;
                }
            }

            return -1;
        }

        private int CountPlayingCue(AudioCue cue)
        {
            int count = 0;
            for (int i = 0; i < sfxStates.Length; i++)
            {
                if (sfxStates[i].Active && sfxStates[i].Cue == cue)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountPlayingCategory(AudioCueCategory category)
        {
            int count = 0;
            for (int i = 0; i < sfxStates.Length; i++)
            {
                if (sfxStates[i].Active && sfxStates[i].Category == category)
                {
                    count++;
                }
            }

            return count;
        }

        private int FindLowestPriorityCueSource(AudioCue cue)
        {
            return FindLowestPrioritySource(state => state.Cue == cue);
        }

        private int FindLowestPriorityCategorySource(AudioCueCategory category)
        {
            return FindLowestPrioritySource(state => state.Category == category);
        }

        private int FindLowestPrioritySfxSource()
        {
            return FindLowestPrioritySource(_ => true);
        }

        private int FindLowestPrioritySource(Predicate<PlaybackState> predicate)
        {
            int index = -1;
            int priority = int.MaxValue;
            float startTime = float.MaxValue;

            for (int i = 0; i < sfxStates.Length; i++)
            {
                PlaybackState state = sfxStates[i];
                if (!state.Active || !predicate(state))
                {
                    continue;
                }

                if (state.Priority < priority || (state.Priority == priority && state.StartTime < startTime))
                {
                    priority = state.Priority;
                    startTime = state.StartTime;
                    index = i;
                }
            }

            return index;
        }

        private bool IsBlockedByCooldown(AudioCue cue, int priority)
        {
            if (cue.CooldownSeconds <= 0f || priority >= HighestPriority)
            {
                return false;
            }

            if (!lastCuePlayTimes.TryGetValue(cue, out float lastTime))
            {
                return false;
            }

            return Time.unscaledTime - lastTime < cue.CooldownSeconds;
        }

        private static int GetCategoryLimit(AudioCueCategory category)
        {
            return category switch
            {
                AudioCueCategory.Ui => 6,
                AudioCueCategory.Combat => 18,
                AudioCueCategory.Ambience => 4,
                AudioCueCategory.Voice => 3,
                AudioCueCategory.System => 4,
                _ => 12,
            };
        }

        private void RefreshSfxPlaybackStates()
        {
            if (sfxSources == null || sfxStates == null)
            {
                return;
            }

            for (int i = 0; i < sfxSources.Length; i++)
            {
                AudioSource source = sfxSources[i];
                if (!sfxStates[i].Active)
                {
                    continue;
                }

                bool sourceStillOwned = source != null && source.clip != null;
                bool shouldKeepInactiveSource = sfxStates[i].IsLoop || sfxStates[i].PausedByManager || sfxStates[i].ManuallyPaused;
                if (!sourceStillOwned || (!source.isPlaying && !shouldKeepInactiveSource))
                {
                    ClearSfxState(i, false);
                }
            }
        }

        private void UpdateFollowTargets()
        {
            if (sfxStates == null)
            {
                return;
            }

            for (int i = 0; i < sfxStates.Length; i++)
            {
                PlaybackState state = sfxStates[i];
                if (!state.Active || !state.HasFollowTarget)
                {
                    continue;
                }

                if (!state.FollowTarget)
                {
                    StopSfxState(i, state.FadeOutSeconds);
                    continue;
                }

                sfxSources[i].transform.position = state.FollowTarget.position;
            }
        }

        private void ApplyPauseToActiveSources()
        {
            for (int i = 0; i < sfxStates.Length; i++)
            {
                if (!sfxStates[i].Active)
                {
                    continue;
                }

                if (sfxStates[i].PausePolicy == AudioCuePausePolicy.Stop)
                {
                    StopSfxState(i, sfxStates[i].FadeOutSeconds);
                    continue;
                }

                if (sfxStates[i].PausePolicy != AudioCuePausePolicy.Pause)
                {
                    continue;
                }

                AudioSource source = sfxSources[i];
                if (source != null && source.isPlaying)
                {
                    source.Pause();
                    sfxStates[i].PausedByManager = true;
                }
            }

            if (musicState.Active && musicState.PausePolicy == AudioCuePausePolicy.Pause && musicState.Source != null)
            {
                musicState.Source.Pause();
                musicState.PausedByManager = true;
            }
        }

        private void ResumeSourcesPausedByManager()
        {
            for (int i = 0; i < sfxStates.Length; i++)
            {
                if (!sfxStates[i].Active || !sfxStates[i].PausedByManager)
                {
                    continue;
                }

                AudioSource source = sfxSources[i];
                if (source != null)
                {
                    source.UnPause();
                }

                sfxStates[i].PausedByManager = false;
            }

            if (musicState.Active && musicState.PausedByManager && musicState.Source != null)
            {
                musicState.Source.UnPause();
                musicState.PausedByManager = false;
            }
        }

        private static AudioCuePausePolicy ResolvePausePolicy(AudioCueKind kind, AudioCueCategory category, AudioCuePausePolicy policy)
        {
            if (policy != AudioCuePausePolicy.UseCategoryDefault)
            {
                return policy;
            }

            if (kind == AudioCueKind.Music || category == AudioCueCategory.Ui || category == AudioCueCategory.System)
            {
                return AudioCuePausePolicy.Continue;
            }

            return AudioCuePausePolicy.Pause;
        }

        private AudioCueVariant SelectVariant(AudioCue cue, IReadOnlyList<AudioCueVariant> variants)
        {
            if (!HasPlayableVariant(variants))
            {
                return null;
            }

            AudioCueVariant lastVariant = lastSelectedVariants.TryGetValue(cue, out AudioCueVariant last) ? last : null;
            float totalWeight = 0f;
            int playableCount = 0;
            for (int i = 0; i < variants.Count; i++)
            {
                AudioCueVariant variant = variants[i];
                if (variant == null || !variant.HasPlayableReference)
                {
                    continue;
                }

                if (variant == lastVariant && CountPlayableVariants(variants) > 1)
                {
                    continue;
                }

                totalWeight += variant.Weight > 0f ? variant.Weight : 0f;
                playableCount++;
            }

            if (playableCount <= 0)
            {
                lastVariant = null;
                return SelectVariant(cue, variants);
            }

            float roll = totalWeight > 0f ? (float)RandomSource.NextDouble() * totalWeight : (float)RandomSource.NextDouble() * playableCount;
            float cursor = 0f;
            for (int i = 0; i < variants.Count; i++)
            {
                AudioCueVariant variant = variants[i];
                if (variant == null || !variant.HasPlayableReference || (variant == lastVariant && CountPlayableVariants(variants) > 1))
                {
                    continue;
                }

                cursor += totalWeight > 0f ? Mathf.Max(0f, variant.Weight) : 1f;
                if (roll <= cursor)
                {
                    lastSelectedVariants[cue] = variant;
                    return variant;
                }
            }

            for (int i = 0; i < variants.Count; i++)
            {
                AudioCueVariant variant = variants[i];
                if (variant != null && variant.HasPlayableReference)
                {
                    lastSelectedVariants[cue] = variant;
                    return variant;
                }
            }

            return null;
        }

        private bool TryResolveVariantClip(AudioCue cue, AudioCueVariant variant, out AudioClip clip)
        {
            clip = null;
            if (variant == null)
            {
                return false;
            }

            if (variant.Clip != null)
            {
                clip = variant.Clip;
                return true;
            }

            string address = NormalizeAddress(variant.Address);
            if (string.IsNullOrEmpty(address))
            {
                GameDebug.LogWarning($"[AudioManager] Cue '{cue.name}' variant has no clip or address.");
                return false;
            }

            if (cachedAddressClips.TryGetValue(address, out CachedAddressClip cached) && cached.Clip != null)
            {
                cacheHitCount++;
                clip = cached.Clip;
                return true;
            }

            cacheMissCount++;
            GameDebug.LogWarning($"[AudioManager] Cue '{cue.name}' address '{address}' has not been preloaded.");
            return false;
        }

        private static HashSet<string> CollectVariantAddresses(AudioCue cue)
        {
            HashSet<string> addresses = new(StringComparer.Ordinal);
            AddVariantAddresses(cue.Variants, addresses);
            AddVariantAddresses(cue.IntroVariants, addresses);
            AddVariantAddresses(cue.LoopVariants, addresses);
            return addresses;
        }

        private static void AddVariantAddresses(IReadOnlyList<AudioCueVariant> variants, HashSet<string> addresses)
        {
            if (variants == null)
            {
                return;
            }

            for (int i = 0; i < variants.Count; i++)
            {
                string address = NormalizeAddress(variants[i]?.Address);
                if (!string.IsNullOrEmpty(address))
                {
                    addresses.Add(address);
                }
            }
        }

        private void ReleaseAddress(string address)
        {
            if (string.IsNullOrEmpty(address) || !cachedAddressClips.TryGetValue(address, out CachedAddressClip cached))
            {
                return;
            }

            cached.RetainCount--;
            if (cached.RetainCount > 0)
            {
                return;
            }

            cached.Release();
            cachedAddressClips.Remove(address);
        }

        private static bool HasPlayableVariant(IReadOnlyList<AudioCueVariant> variants)
        {
            return CountPlayableVariants(variants) > 0;
        }

        private static int CountPlayableVariants(IReadOnlyList<AudioCueVariant> variants)
        {
            if (variants == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < variants.Count; i++)
            {
                if (variants[i] != null && variants[i].HasPlayableReference)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool ValidateCue(AudioCue cue)
        {
            if (cue != null)
            {
                return true;
            }

            GameDebug.LogWarning("[AudioManager] PlayCue ignored because cue is missing.");
            return false;
        }

        private bool IsSfxHandleValid(int index, int id, int generation)
        {
            return index >= 0
                && index < sfxStates.Length
                && sfxStates[index].Active
                && sfxStates[index].HandleId == id
                && sfxStates[index].Generation == generation;
        }

        private int AllocateHandleId()
        {
            if (nextHandleId == int.MaxValue)
            {
                nextHandleId = 1;
            }

            return nextHandleId++;
        }

        private int AllocateGeneration()
        {
            if (nextGeneration == int.MaxValue)
            {
                nextGeneration = 1;
            }

            return nextGeneration++;
        }

        private float ResolveVolumeScale(AudioCue cue, AudioCuePlayRequest request)
        {
            return NormalizeVolume(request.HasVolumeScaleOverride ? request.VolumeScaleOverride : cue.VolumeScale);
        }

        private static float ResolvePitch(AudioCue cue)
        {
            float min = SanitizePitch(cue.PitchMin);
            float max = Mathf.Max(min, SanitizePitch(cue.PitchMax));
            if (Mathf.Approximately(min, max))
            {
                return min;
            }

            return min + (float)RandomSource.NextDouble() * (max - min);
        }

        private static float SanitizePitch(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                return 1f;
            }

            return value;
        }

        private void RefreshSourceVolumes()
        {
            RefreshMusicVolumes();
            RefreshSfxVolumes();
        }

        private void RefreshMusicVolumes()
        {
            if (musicState.Active && musicState.Source != null)
            {
                musicState.Source.volume = EffectiveMusicVolume * musicState.VolumeScale;
            }
        }

        private void RefreshSfxVolumes()
        {
            if (sfxSources == null || sfxStates == null)
            {
                return;
            }

            for (int i = 0; i < sfxSources.Length; i++)
            {
                if (sfxSources[i] != null && sfxStates[i].Active)
                {
                    sfxSources[i].volume = EffectiveSfxVolume * sfxStates[i].VolumeScale;
                }
            }
        }

        private static float NormalizeVolume(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            return Mathf.Clamp01(value);
        }

        private static string NormalizeAddress(string address)
        {
            return string.IsNullOrWhiteSpace(address) ? string.Empty : address.Trim();
        }

        private sealed class CachedAddressClip
        {
            private readonly AsyncOperationHandle<AudioClip> handle;

            public CachedAddressClip(AsyncOperationHandle<AudioClip> handle)
            {
                this.handle = handle;
                RetainCount = 1;
            }

            public int RetainCount { get; set; }
            public AudioClip Clip => handle.IsValid() ? handle.Result : null;

            public void Release()
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }

        private readonly struct HandleBinding
        {
            public HandleBinding(bool isMusic, int index, int generation)
            {
                IsMusic = isMusic;
                Index = index;
                Generation = generation;
            }

            public bool IsMusic { get; }
            public int Index { get; }
            public int Generation { get; }
        }

        private struct PlaybackState
        {
            public bool Active;
            public AudioCue Cue;
            public AudioCueCategory Category;
            public AudioCuePausePolicy PausePolicy;
            public Transform FollowTarget;
            public Coroutine FadeRoutine;
            public int Priority;
            public int HandleId;
            public int Generation;
            public float StartTime;
            public float VolumeScale;
            public float FadeOutSeconds;
            public bool IsLoop;
            public bool PausedByManager;
            public bool ManuallyPaused;
            public bool HasFollowTarget;
        }

        private struct MusicPlaybackState
        {
            public bool Active;
            public AudioSource Source;
            public AudioCue Cue;
            public AudioCueCategory Category;
            public AudioCuePausePolicy PausePolicy;
            public int HandleId;
            public int Generation;
            public float VolumeScale;
            public float FadeOutSeconds;
            public bool PausedByManager;
            public bool ManuallyPaused;
        }
    }

    public struct AudioManagerStats
    {
        public int ActiveVoiceCount;
        public int UiActiveCount;
        public int CombatActiveCount;
        public int AmbienceActiveCount;
        public int VoiceActiveCount;
        public int SystemActiveCount;
        public int DefaultActiveCount;
        public int ActiveLoopCount;
        public int AcceptedCount;
        public int RejectedCount;
        public int StolenCount;
        public int CacheHitCount;
        public int CacheMissCount;

        public void AddCategory(AudioCueCategory category)
        {
            switch (category)
            {
                case AudioCueCategory.Ui:
                    UiActiveCount++;
                    break;
                case AudioCueCategory.Combat:
                    CombatActiveCount++;
                    break;
                case AudioCueCategory.Ambience:
                    AmbienceActiveCount++;
                    break;
                case AudioCueCategory.Voice:
                    VoiceActiveCount++;
                    break;
                case AudioCueCategory.System:
                    SystemActiveCount++;
                    break;
                default:
                    DefaultActiveCount++;
                    break;
            }
        }
    }
}
