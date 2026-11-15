namespace WSFGameJam2026.Audio
{
    /// <summary>
    /// Synthesis algorithms of <see cref="SfxSynthesizer"/> (ported from the LD57 ProceduralSfxBank).
    /// A closed set: each value is code. New sounds are new <see cref="SoundEffect"/> assets, not new values.
    /// Serialized as a number, so add new values only at the end.
    /// </summary>
    public enum SfxRecipe
    {
        None = 0,
        Dash = 1,
        Attack = 2,
        Hit = 3,
        PlayerHurt = 4,
        Windup = 5,
        Shot = 6,
        GateOpen = 7,
        Heart = 8,
        LevelUp = 9,
        CardPick = 10,
        WaveCleared = 11,
        Death = 12,
        Win = 13,
        BossTelegraph = 14,
        EnemyDie = 15,
        Cast = 16,
    }
}
