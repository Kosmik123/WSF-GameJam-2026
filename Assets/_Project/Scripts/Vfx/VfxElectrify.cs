using UnityEngine;

namespace WSFGameJam2026.Vfx
{
    /// <summary>A body hit by lightning (port of the LD57 MageElectrify): 4 small bright solids jump around it at random.</summary>
    public class VfxElectrify : MonoBehaviour
    {
        private const int BitCount = 4;
        private const float HopInterval = 0.06f;

        [Tooltip("Size of the area around the pivot of the target where the sparks jump.")]
        [SerializeField]
        private Vector2 area = new Vector2(0.45f, 0.5f);

        private Transform target;
        private Transform[] bits;
        private float duration;
        private float elapsed;
        private float hopTime;

        public void Play(Transform newTarget, Color color, float seconds)
        {
            target = newTarget;
            duration = seconds;
            bits = new Transform[BitCount];
            for (int i = 0; i < BitCount; i++)
            {
                var bitColor = Color.Lerp(color, Color.white, 0.5f + 0.3f * Random.value);
                bits[i] = StylizedFx.MakeShape(transform, "Spark Bit", VfxShape.Octa, bitColor, 0.12f, 1f, 4f).transform;
            }

            Hop();
        }

        private void LateUpdate()
        {
            elapsed += Time.deltaTime;
            if (target == null || elapsed >= duration)
            {
                Destroy(gameObject);
                return;
            }

            hopTime += Time.deltaTime;
            if (hopTime < HopInterval)
                return;

            hopTime = 0f;
            Hop();
        }

        private void Hop()
        {
            var right = target.right;
            var up = target.up;
            var forward = target.forward;
            for (int i = 0; i < bits.Length; i++)
            {
                var direction = Random.onUnitSphere;
                var position = target.position + (right * direction.x + forward * direction.z) * area.x + up * direction.y * area.y;
                bits[i].SetPositionAndRotation(position, Random.rotation);
                bits[i].localScale = Vector3.one * Random.Range(0.09f, 0.16f);
            }
        }
    }
}
