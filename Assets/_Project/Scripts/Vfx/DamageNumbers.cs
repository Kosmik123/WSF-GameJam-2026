using System.Collections.Generic;
using TMPro;
using UnityEngine;
using WSFGameJam2026.Combat;

namespace WSFGameJam2026.Vfx
{
    /// <summary>
    /// Damage numbers: every hit (enemies, the weak point of the snake, the player) spawns a number above the body that floats
    /// up and fades out. Only listens to <see cref="Health.OnAnyDamaged"/>, the fight knows nothing about it. One per scene.
    /// The numbers are world space text, always facing the camera and drawn on top of the world, and are reused (pooled).
    /// </summary>
    public class DamageNumbers : MonoBehaviour
    {
        private class Number
        {
            public TextMeshPro Text;
            public Vector3 Velocity;
            public float Age;
            public float Lifetime;
            public Color Color;
        }

        [Header("Colors")]
        [Tooltip("Damage dealt to enemies and to the weak point.")]
        [SerializeField]
        private Color enemyColor = new Color(1f, 0.92f, 0.35f);

        [Tooltip("Damage taken by the player.")]
        [SerializeField]
        private Color playerColor = new Color(1f, 0.25f, 0.2f);

        [Header("Look")]
        [Tooltip("Size of the text for a hit of 'Reference Damage'. Bigger hits are bigger.")]
        [SerializeField]
        [Min(0.1f)]
        private float baseFontSize = 4f;

        [SerializeField]
        [Min(1f)]
        private float referenceDamage = 10f;

        [Tooltip("Size of the biggest hits relative to the base size.")]
        [SerializeField]
        [Min(1f)]
        private float maxSizeMultiplier = 2f;

        [Tooltip("Height of the number above the pivot of the body, on top of its radius.")]
        [SerializeField]
        [Min(0f)]
        private float heightOffset = 1.4f;

        [Header("Motion")]
        [SerializeField]
        [Min(0.1f)]
        private float lifetime = 0.9f;

        [SerializeField]
        [Min(0f)]
        private float riseSpeed = 2.2f;

        [Tooltip("Random sideways speed, so the numbers of quick hits do not lie on top of each other.")]
        [SerializeField]
        [Min(0f)]
        private float sideSpeed = 1f;

        private readonly List<Number> active = new List<Number>();
        private readonly Stack<Number> pool = new Stack<Number>();
        private Camera viewCamera;

        private void OnEnable()
        {
            Health.OnAnyDamaged += Health_OnAnyDamaged;
        }

        private void Health_OnAnyDamaged(Health health, DamageInfo damage)
        {
            var number = GetNumber();
            var up = health.transform.up;
            var sideways = Vector3.Cross(up, GetCamera().transform.forward).normalized;

            float size = baseFontSize * Mathf.Lerp(1f, maxSizeMultiplier, Mathf.Clamp01((damage.Amount / referenceDamage - 1f) / 2f));
            number.Age = 0f;
            number.Lifetime = lifetime;
            number.Color = health.Team == CombatTeam.Player ? playerColor : enemyColor;
            number.Velocity = up * riseSpeed + sideways * Random.Range(-sideSpeed, sideSpeed);
            number.Text.text = damage.Amount.ToString();
            number.Text.fontSize = size;
            number.Text.color = number.Color;
            number.Text.transform.position = health.transform.position + up * (health.BodyRadius + heightOffset);
            number.Text.gameObject.SetActive(true);
            active.Add(number);
        }

        private void LateUpdate()
        {
            var cameraRotation = GetCamera().transform.rotation;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var number = active[i];
                number.Age += Time.unscaledDeltaTime;
                if (number.Age >= number.Lifetime)
                {
                    number.Text.gameObject.SetActive(false);
                    pool.Push(number);
                    active.RemoveAt(i);
                    continue;
                }

                float progress = number.Age / number.Lifetime;
                var color = number.Color;
                color.a = 1f - progress * progress;
                number.Text.color = color;
                number.Text.transform.position += number.Velocity * Time.unscaledDeltaTime;
                number.Text.transform.rotation = cameraRotation;
            }
        }

        private Camera GetCamera()
        {
            if (viewCamera == null)
                viewCamera = Camera.main;

            return viewCamera;
        }

        private Number GetNumber()
        {
            if (pool.Count > 0)
                return pool.Pop();

            var textObject = new GameObject("Damage Number");
            textObject.transform.SetParent(transform, false);
            var text = textObject.AddComponent<TextMeshPro>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.raycastTarget = false;

            // The overlay shader draws over the world, so the number is not hidden by the terrain or the body.
            var overlay = Shader.Find("TextMeshPro/Distance Field Overlay");
            if (overlay)
                text.fontMaterial.shader = overlay;

            textObject.SetActive(false);
            return new Number { Text = text };
        }

        private void OnDisable()
        {
            Health.OnAnyDamaged -= Health_OnAnyDamaged;
            foreach (var number in active)
            {
                number.Text.gameObject.SetActive(false);
                pool.Push(number);
            }

            active.Clear();
        }
    }
}
