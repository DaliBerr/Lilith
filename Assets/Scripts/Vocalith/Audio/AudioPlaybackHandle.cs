namespace Vocalith.Audio
{
    public readonly struct AudioPlaybackHandle
    {
        private readonly AudioManager manager;
        private readonly int id;
        private readonly int generation;

        internal AudioPlaybackHandle(AudioManager manager, int id, int generation)
        {
            this.manager = manager;
            this.id = id;
            this.generation = generation;
        }

        public static AudioPlaybackHandle Invalid => default;
        public bool IsValid => manager != null && manager.IsHandleValid(id, generation);
        public bool IsPlaying => manager != null && manager.IsHandlePlaying(id, generation);

        public bool Stop(float fadeOutOverride = -1f)
        {
            return manager != null && manager.StopHandle(id, generation, fadeOutOverride);
        }

        public bool Pause()
        {
            return manager != null && manager.PauseHandle(id, generation);
        }

        public bool Resume()
        {
            return manager != null && manager.ResumeHandle(id, generation);
        }

        public bool SetVolumeScale(float volumeScale)
        {
            return manager != null && manager.SetHandleVolumeScale(id, generation, volumeScale);
        }

        public bool SetPitch(float pitch)
        {
            return manager != null && manager.SetHandlePitch(id, generation, pitch);
        }
    }
}
