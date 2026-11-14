using UnityEngine;
using WSFGameJam2026.World;

public class HeadMovement : MonoBehaviour
{
    [SerializeField]
    private TorusWorld world;

    [SerializeField]
    private float angularMoveSpeed;

    [SerializeField]
    private float startTheta;

    [Header("States")]
    [SerializeField]
    private float theta;

    private void Start()
    {
        theta = startTheta;
    }

    private void Refresh()
    {

    }
}
