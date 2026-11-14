using UnityEngine;
using WSFGameJam2026.Skills;

namespace WSFGameJam2026.Hud
{
    /// <summary>Skill slots of the player with their keys and cooldowns (port of the LD57 SkillHud, without the XP bar).</summary>
    public class SkillBar : MonoBehaviour
    {
        [SerializeField]
        private SkillUser skillUser;

        [Tooltip("One view per slot, in slot order.")]
        [SerializeField]
        private SkillSlotView[] views = new SkillSlotView[0];

        private void Reset()
        {
            skillUser = FindAnyObjectByType<SkillUser>();
            views = GetComponentsInChildren<SkillSlotView>(true);
        }

        private void Update()
        {
            if (skillUser == null)
                return;

            var owned = skillUser.Slots.Slots;
            for (int i = 0; i < views.Length; i++)
                if (views[i])
                    views[i].Show(i < owned.Count ? owned[i] : null, skillUser.GetSlotKeyLabel(i));
        }
    }
}
