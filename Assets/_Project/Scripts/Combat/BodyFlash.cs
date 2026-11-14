using UnityEngine;

namespace WSFGameJam2026.Combat
{
    /// <summary>
    /// Colours all renderers of the object for a moment through a MaterialPropertyBlock: white flash on damage
    /// (port of the LD57 DamageFlash) and a coloured warning before an enemy attack (telegraph).
    /// The damage flash wins over the telegraph.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class BodyFlash : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField]
        private Health health;

        [SerializeField]
        [Min(0f)]
        private float damageFlashDuration = 0.1f;

        [SerializeField]
        private Color damageFlashColor = Color.white;

        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private float damageFlashUntil = -1f;
        private float telegraphUntil = -1f;
        private Color telegraphColor;
        private bool isTinted;

        private void Reset()
        {
            health = GetComponent<Health>();
        }

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (health != null)
                health.OnDamaged += Health_OnDamaged;
        }

        private void OnDisable()
        {
            if (health != null)
                health.OnDamaged -= Health_OnDamaged;

            Clear();
        }

        /// <summary>Colours the body for the given time (game time). Used for attack warnings.</summary>
        public void Telegraph(Color color, float seconds)
        {
            telegraphColor = color;
            telegraphUntil = Time.time + seconds;
        }

        public void StopTelegraph()
        {
            telegraphUntil = -1f;
        }

        private void Health_OnDamaged(Health damagedHealth, DamageInfo damage)
        {
            // Real time, so the flash is visible during the hit stop.
            damageFlashUntil = Time.unscaledTime + damageFlashDuration;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < damageFlashUntil)
            {
                Apply(damageFlashColor);
            }
            else if (Time.time < telegraphUntil)
            {
                // Pulses, so the warning reads as "something is coming".
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 30f);
                Apply(Color.Lerp(telegraphColor * 0.6f, telegraphColor, pulse));
            }
            else if (isTinted)
            {
                Clear();
            }
        }

        private void Apply(Color color)
        {
            block.Clear();
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            block.SetColor(EmissionColorId, color);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null)
                    renderers[i].SetPropertyBlock(block);

            isTinted = true;
        }

        private void Clear()
        {
            if (renderers == null)
                return;

            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null)
                    renderers[i].SetPropertyBlock(null);

            isTinted = false;
        }
    }
}
