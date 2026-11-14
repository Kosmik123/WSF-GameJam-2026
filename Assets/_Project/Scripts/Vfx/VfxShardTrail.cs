using UnityEngine;

namespace WSFGameJam2026.Vfx
{
    /// <summary>
    /// Trail of glowing shards left along the movement of the object, one every 0.15 m (port of the LD57 MageShardTrail).
    /// Embers drift up along the local up of the object. With a lifetime it removes itself (dodge, charge).
    /// </summary>
    public class VfxShardTrail : MonoBehaviour
    {
        private const float Spacing = 0.15f;
        private const float TeleportDistance = 8f;
        private const int MaxPerFrame = 20;

        [SerializeField]
        private Color color = Color.white;

        [SerializeField]
        [Min(0.01f)]
        private float size = 0.12f;

        [SerializeField]
        [Min(0.05f)]
        private float life = 0.45f;

        [SerializeField]
        private bool isEmber;

        [Tooltip("Height of the trail above the pivot, along the local up.")]
        [SerializeField]
        private float height;

        private Vector3 lastPosition;
        private float removeTime = -1f;

        private Vector3 TrailPosition => transform.position + transform.up * height;

        private void OnEnable()
        {
            lastPosition = TrailPosition;
        }

        /// <summary>Sets the look of the trail. A positive <paramref name="seconds"/> removes the trail after that time.</summary>
        public void Configure(Color newColor, float newSize, float newLife, bool newIsEmber, float newHeight = 0f, float seconds = 0f)
        {
            color = newColor;
            size = newSize;
            life = newLife;
            isEmber = newIsEmber;
            height = newHeight;
            removeTime = seconds > 0f ? Time.time + seconds : -1f;
            lastPosition = TrailPosition;
        }

        private void LateUpdate()
        {
            if (removeTime > 0f && Time.time >= removeTime)
            {
                Destroy(this);
                return;
            }

            var position = TrailPosition;
            var offset = position - lastPosition;
            float distance = offset.magnitude;
            if (distance < Spacing)
                return;

            if (distance > TeleportDistance)
            {
                lastPosition = position;
                return;
            }

            int count = Mathf.Min(MaxPerFrame, (int)(distance / Spacing));
            var step = offset / distance * Spacing;
            for (int i = 1; i <= count; i++)
                EmitShard(lastPosition + step * i);

            lastPosition += step * count;
        }

        private void EmitShard(Vector3 position)
        {
            var velocity = Random.insideUnitSphere * 0.5f;
            if (isEmber)
                velocity += transform.up * Random.Range(0.2f, 0.8f);

            float shardSize = size * Random.Range(0.6f, 1.2f);
            VfxParticles.Shard(position + Random.insideUnitSphere * size * 0.5f, velocity, StylizedFx.Tint(color, 0.3f),
                new Vector3(shardSize, shardSize * 1.4f, shardSize), life * Random.Range(0.7f, 1.2f));
        }
    }
}
