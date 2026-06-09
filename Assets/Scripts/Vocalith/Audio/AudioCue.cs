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

    [CreateAssetMenu(fileName = "AudioCue", menuName = "Vocalith/Audio/Audio Cue")]
    public sealed class AudioCue : ScriptableObject
    {
        [SerializeField] private AudioClip clip;
        [SerializeField] private string address;
        [SerializeField] private AudioCueKind kind = AudioCueKind.Sfx;
        [SerializeField] private AudioCueCategory category = AudioCueCategory.Default;
        [SerializeField] private int priority;
        [SerializeField, Min(0f)] private float volumeScale = 1f;
        [SerializeField, Min(0f)] private float pitchMin = 1f;
        [SerializeField, Min(0f)] private float pitchMax = 1f;
        [SerializeField, Min(0f)] private float cooldownSeconds;
        [SerializeField, Min(1)] private int maxSimultaneousSelf = 1;
        [SerializeField] private AudioCueSpatialMode spatialMode = AudioCueSpatialMode.TwoDimensional;
        [SerializeField, Min(0f)] private float minDistance = 1f;
        [SerializeField, Min(0.01f)] private float maxDistance = 25f;

        public AudioClip Clip => clip;
        public string Address => address;
        public AudioCueKind Kind => kind;
        public AudioCueCategory Category => category;
        public int Priority => priority;
        public float VolumeScale => volumeScale;
        public float PitchMin => pitchMin;
        public float PitchMax => pitchMax;
        public float CooldownSeconds => cooldownSeconds;
        public int MaxSimultaneousSelf => Mathf.Max(1, maxSimultaneousSelf);
        public AudioCueSpatialMode SpatialMode => spatialMode;
        public float MinDistance => Mathf.Max(0f, minDistance);
        public float MaxDistance => Mathf.Max(Mathf.Max(0.01f, minDistance), maxDistance);

        private void OnValidate()
        {
            volumeScale = Mathf.Max(0f, volumeScale);
            pitchMin = Mathf.Max(0f, pitchMin);
            pitchMax = Mathf.Max(pitchMin, pitchMax);
            cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
            maxSimultaneousSelf = Mathf.Max(1, maxSimultaneousSelf);
            minDistance = Mathf.Max(0f, minDistance);
            maxDistance = Mathf.Max(Mathf.Max(0.01f, minDistance), maxDistance);
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
