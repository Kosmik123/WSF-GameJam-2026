namespace WSFGameJam2026.Skills
{
    /// <summary>Everything a skill needs when it runs: who casts it and with which tier.</summary>
    public readonly struct SkillContext
    {
        /// <summary>The caster. Also runs the coroutines of skills that last (orbs, burning).</summary>
        public SkillUser User { get; }

        public SkillTier Tier { get; }

        public int TierIndex { get; }

        /// <summary>The last tier, which changes how the skill works (not only its numbers).</summary>
        public bool IsEvolved => TierIndex >= SkillDefinition.MaxTier;

        public SkillContext(SkillUser user, SkillTier tier, int tierIndex)
        {
            User = user;
            Tier = tier;
            TierIndex = tierIndex;
        }
    }
}
