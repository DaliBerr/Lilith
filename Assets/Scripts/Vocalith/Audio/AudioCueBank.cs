using System.Collections.Generic;
using UnityEngine;

namespace Vocalith.Audio
{
    [CreateAssetMenu(fileName = "AudioCueBank", menuName = "Vocalith/Audio/Audio Cue Bank")]
    public sealed class AudioCueBank : ScriptableObject
    {
        [SerializeField] private List<AudioCue> cues = new();
        [SerializeField] private bool releaseOnSceneUnload;

        public IReadOnlyList<AudioCue> Cues => cues;
        public bool ReleaseOnSceneUnload => releaseOnSceneUnload;
    }
}
