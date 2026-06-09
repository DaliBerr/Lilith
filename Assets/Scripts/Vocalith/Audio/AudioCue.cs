using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vocalith.Audio
{
    public enum AudioCueKind
    {
        Music = 0,
        Sfx = 1,
    }

    public enum AudioCueCategory
    {
        Default = 0,
        Ui = 1,
        Combat = 2,
        Ambience = 3,
        Voice = 4,
        System = 5,
    }

    public enum AudioCueSpatialMode
    {
        TwoDimensional = 0,
        WorldPosition = 1,
    }

    public enum AudioCuePlaybackMode
    {
        OneShot = 0,
        Loop = 1,
    }

    public enum AudioCuePausePolicy
    {
        UseCategoryDefault = 0,
        Continue = 1,
        Pause = 2,
        Stop = 3,
    }

    [Serializable]
    public sealed class AudioCueVariant
    {
        [SerializeField] private AudioClip clip;
        [SerializeField] private string address;
        [SerializeField, Min(0f)] private float weight = 1f;

        public AudioClip Clip => clip;
        public string Address => address;
        public float Weight => Mathf.Max(0f, weight);
        public bool HasClip => clip != null;
        public bool HasAddress => !string.IsNullOrWhiteSpace(address);
        public bool HasPlayableReference => HasClip || HasAddress;
    }

    [CreateAssetMenu(fileName = "AudioCue", menuName = "Vocalith/Audio/Audio Cue")]
    public sealed class AudioCue : ScriptableObject
    {
        [SerializeField] private AudioCueKind kind = AudioCueKind.Sfx;
        [SerializeField] private AudioCueCategory category = AudioCueCategory.Default;
        [SerializeField] private AudioCuePlaybackMode playbackMode = AudioCuePlaybackMode.OneShot;
        [SerializeField] private AudioCuePausePolicy pausePolicy = AudioCuePausePolicy.UseCategoryDefault;
        [SerializeField] private int priority;
        [SerializeField, Min(0f)] private float volumeScale = 1f;
        [SerializeField, Min(0f)] private float pitchMin = 1f;
        [SerializeField, Min(0f)] private float pitchMax = 1f;
        [SerializeField, Min(0f)] private float cooldownSeconds;
        [SerializeField, Min(1)] private int maxSimultaneousSelf = 1;
        [SerializeField, Min(0f)] private float fadeInSeconds;
        [SerializeField, Min(0f)] private float fadeOutSeconds;
        [SerializeField] private AudioCueSpatialMode spatialMode = AudioCueSpatialMode.TwoDimensional;
        [SerializeField, Min(0f)] private float minDistance = 1f;
        [SerializeField, Min(0.01f)] private float maxDistance = 25f;
        [SerializeField] private List<AudioCueVariant> variants = new();
        [SerializeField] private List<AudioCueVariant> introVariants = new();
        [SerializeField] private List<AudioCueVariant> loopVariants = new();

        public AudioCueKind Kind => kind;
        public AudioCueCategory Category => category;
        public AudioCuePlaybackMode PlaybackMode => playbackMode;
        public AudioCuePausePolicy PausePolicy => pausePolicy;
        public int Priority => priority;
        public float VolumeScale => volumeScale;
        public float PitchMin => pitchMin;
        public float PitchMax => pitchMax;
        public float CooldownSeconds => cooldownSeconds;
        public int MaxSimultaneousSelf => Mathf.Max(1, maxSimultaneousSelf);
        public float FadeInSeconds => Mathf.Max(0f, fadeInSeconds);
        public float FadeOutSeconds => Mathf.Max(0f, fadeOutSeconds);
        public AudioCueSpatialMode SpatialMode => spatialMode;
        public float MinDistance => Mathf.Max(0f, minDistance);
        public float MaxDistance => Mathf.Max(Mathf.Max(0.01f, minDistance), maxDistance);
        public IReadOnlyList<AudioCueVariant> Variants => variants;
        public IReadOnlyList<AudioCueVariant> IntroVariants => introVariants;
        public IReadOnlyList<AudioCueVariant> LoopVariants => loopVariants;

        public bool HasPlayableVariant
        {
            get
            {
                return ContainsPlayableVariant(variants)
                    || ContainsPlayableVariant(introVariants)
                    || ContainsPlayableVariant(loopVariants);
            }
        }

        private void OnValidate()
        {
            volumeScale = Mathf.Max(0f, volumeScale);
            pitchMin = Mathf.Max(0f, pitchMin);
            pitchMax = Mathf.Max(pitchMin, pitchMax);
            cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
            maxSimultaneousSelf = Mathf.Max(1, maxSimultaneousSelf);
            fadeInSeconds = Mathf.Max(0f, fadeInSeconds);
            fadeOutSeconds = Mathf.Max(0f, fadeOutSeconds);
            minDistance = Mathf.Max(0f, minDistance);
            maxDistance = Mathf.Max(Mathf.Max(0.01f, minDistance), maxDistance);
        }

        private static bool ContainsPlayableVariant(IReadOnlyList<AudioCueVariant> list)
        {
            if (list == null)
            {
                return false;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].HasPlayableReference)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public struct AudioCuePlayRequest
    {
        public bool HasVolumeScaleOverride;
        public float VolumeScaleOverride;
        public int PriorityOffset;
        public bool HasCategoryOverride;
        public AudioCueCategory CategoryOverride;
        public bool HasPosition;
        public Vector3 Position;

        public static AudioCuePlayRequest WithPosition(Vector3 position)
        {
            return new AudioCuePlayRequest
            {
                HasPosition = true,
                Position = position,
            };
        }
    }
}
