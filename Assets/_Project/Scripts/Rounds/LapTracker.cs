using System;
using UnityEngine;
using WSFGameJam2026.Head;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Rounds
{
    /// <summary>
    /// Measures the lap of the player (GDD 6). The player starts in front of the jaw and has to run all the way
    /// around the torus to the place right behind the jaw, at the top of the head. The progress is counted as the
    /// angle between the head and the player along the body, summed up from the changes between frames, so it does
    /// not jump when the angles wrap around. Only theta is used. The lap is won when the player, walking over the head
    /// from behind, crosses its front end (the end set by <c>headFrontOffsetDegrees</c> of <see cref="HeadTerrainBlend"/>).
    /// </summary>
    public class LapTracker : MonoBehaviour
    {
        /// <summary>The player reached the place behind the jaw.</summary>
        public event Action OnLapCompleted;

        [SerializeField]
        private HeadMovement head;

        [SerializeField]
        private TorusObject player;

        [Tooltip("Needed to know where the head ends. Without an active head terrain the goal is behind the head, see Goal Behind Head Degrees.")]
        [SerializeField]
        private TorusWorld world;

        [Tooltip("The lap is won this many degrees of theta before the front end of the head. 0 = at the front end.")]
        [SerializeField]
        [Min(0f)]
        private float goalMarginDegrees;

        [Tooltip("Used only when the head has no terrain (not measured yet): the lap is won when the player is this far behind the head, degrees of theta.")]
        [SerializeField]
        [Min(0f)]
        private float goalBehindHeadDegrees = 20f;

        private bool isRunning;
        private float lastRelativeDegrees;
        private float travelledDegrees;

        /// <summary>Lap progress: 0 at the start, 1 at the goal behind the head. Negative when the head passed the player.</summary>
        public float Progress => Mathf.Min(1f, travelledDegrees / GoalDegrees);

        private float GoalDegrees => Mathf.Max(1f, GetFrontEndDegrees() - goalMarginDegrees);

        private void Reset()
        {
            world = FindAnyObjectByType<TorusWorld>();
            head = FindAnyObjectByType<HeadMovement>();
            player = FindAnyObjectByType<TorusObject>();
        }

        private void Awake()
        {
            if (world == null)
                world = FindAnyObjectByType<TorusWorld>();
        }

        /// <summary>Starts measuring from the current positions of the player and the head.</summary>
        public void Begin()
        {
            lastRelativeDegrees = GetRelativeDegrees();
            travelledDegrees = lastRelativeDegrees;
            isRunning = true;
        }

        /// <summary>Stops measuring, for example after the lap was won or the player died.</summary>
        public void Stop()
        {
            isRunning = false;
        }

        private void Update()
        {
            if (isRunning == false || head == null || player == null)
                return;

            var relativeDegrees = GetRelativeDegrees();
            travelledDegrees += Mathf.DeltaAngle(lastRelativeDegrees, relativeDegrees);
            lastRelativeDegrees = relativeDegrees;

            if (travelledDegrees < GoalDegrees)
                return;

            isRunning = false;
            OnLapCompleted?.Invoke();
        }

        // Progress at which the player is at the front end of the head: one full lap plus the position of the front end relative to the pivot.
        // Before the head terrain is measured the goal is just behind the head.
        private float GetFrontEndDegrees()
        {
            var terrain = world ? world.HeadTerrain : null;
            if (terrain == null || terrain.IsActive == false)
                return 360f - goalBehindHeadDegrees;

            var frontEndRelativeDegrees = (terrain.HeadLength - terrain.NeckOffset) * Mathf.Rad2Deg;
            return 360f + frontEndRelativeDegrees;
        }

        // Position of the player in front of the head, in degrees [0, 360).
        private float GetRelativeDegrees()
        {
            var playerDegrees = player.Angles.ThetaDegrees;
            var headDegrees = head.Theta * Mathf.Rad2Deg;
            return Mathf.Repeat(playerDegrees - headDegrees, 360f);
        }
    }
}
