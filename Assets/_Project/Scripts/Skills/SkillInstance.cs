using UnityEngine;

namespace WSFGameJam2026.Skills
{
    /// <summary>An owned skill: its definition, the current tier (0-3) and the cooldown.</summary>
    public class SkillInstance
    {
        public SkillDefinition Definition { get; }

        public int Tier { get; private set; }

        public float CooldownRemaining { get; private set; }

        public SkillTier Current => Definition.GetTier(Tier);
        public bool IsMaxTier => Tier >= SkillDefinition.MaxTier;
        public bool IsReady => CooldownRemaining <= 0f;

        /// <summary>Remaining part of the cooldown, 1 right after the use, 0 when ready.</summary>
        public float CooldownNormalized => Current.Cooldown > 0f ? Mathf.Clamp01(CooldownRemaining / Current.Cooldown) : 0f;

        public SkillInstance(SkillDefinition definition)
        {
            Definition = definition;
        }

        /// <summary>Raises the tier by one. Returns false at the last tier.</summary>
        public bool Upgrade()
        {
            if (IsMaxTier)
                return false;

            Tier++;
            return true;
        }

        public void StartCooldown()
        {
            CooldownRemaining = Current.Cooldown;
        }

        public void Tick(float deltaTime)
        {
            if (CooldownRemaining > 0f)
                CooldownRemaining = Mathf.Max(0f, CooldownRemaining - deltaTime);
        }
    }
}
