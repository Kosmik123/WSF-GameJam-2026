using UnityEngine;

namespace WSFGameJam2026.Vfx
{
    /// <summary>Turns the object around its local axes all the time (parts of orbs, axes, flames).</summary>
    public class VfxSpin : MonoBehaviour
    {
        [SerializeField]
        private Vector3 degreesPerSecond = new Vector3(0f, 200f, 0f);

        public void Init(Vector3 newDegreesPerSecond)
        {
            degreesPerSecond = newDegreesPerSecond;
        }

        private void Update()
        {
            transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
