using UnityEngine;
using WSFGameJam2026.World;

public class CenterSunRotation : MonoBehaviour
{
    [SerializeField]
    private TorusObject target;

    private void Update()
    {
        float angle = target.Angles.ThetaDegrees;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.up);
    }
}
