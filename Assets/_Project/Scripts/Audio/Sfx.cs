using UnityEngine;

namespace WSFGameJam2026.Audio
{
    /// <summary>Static entry point for sound effects (like the LD57 Sfx). Does nothing without an <see cref="SfxPlayer"/> in the scene.</summary>
    public static class Sfx
    {
        /// <summary>Plays the sound at a world position.</summary>
        public static void Play(SoundEffect sound, Vector3 position, float volume = 1f)
        {
            if (SfxPlayer.Instance)
                SfxPlayer.Instance.Play(sound, position, volume);
        }

        /// <summary>Plays the sound without a position. Use for sounds with a spatial blend of 0 (2D: stingers, UI).</summary>
        public static void Play(SoundEffect sound)
        {
            Play(sound, Vector3.zero);
        }
    }
}
