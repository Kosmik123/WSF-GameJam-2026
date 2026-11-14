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
    /// not jump when the angles wrap around. Only theta is used.
    /// </summary>
    public class LapTracker : MonoBehaviour
    {
        /// <summary>The player reached the place behind the jaw.</summary>
        public event Action OnLapCompleted;

        [SerializeField]
        private HeadMovement head;

        [SerializeField]
        private TorusObject player;

        [Tooltip("The lap is won when the player is this far behind the head (degrees of theta). More than the reach of the kill zone behind the head.")]
        [SerializeField]
        [Min(0f)]
        private float goalBehindHeadDegrees = 20f;
        public float GoalBehindHeadDegrees => goalBehindHeadDegrees;

        private bool isRunning;
        private float lastRelativeDegrees;
        private float travelledDegrees;

        /// <summary>Lap progress: 0 at the start, 1 at the goal behind the head. Negative when the head passed the player.</summary>
        public float Progress => Mathf.Min(1f, travelledDegrees / GoalDegrees);

        /// <summary>True between <see cref="Begin"/> and the end of the lap (won or stopped).</summary>
        public bool IsRunning => isRunning;

        /// <summary>
        /// How far the player is in front of the jaw, in metres along the centre line of the torus.
        /// The lead the player has over the jaw; negative when the jaw passed the player.
        /// </summary>
        public float MetersAheadOfHead
        {
            get
            {
                if (head == null || head.World == null)
                    return 0f;

                return travelledDegrees * Mathf.Deg2Rad * head.World.MajorRadius;
            }
        }

        private float GoalDegrees => Mathf.Max(1f, 360f - goalBehindHeadDegrees);

        private void Reset()
        {
            head = FindAnyObjectByType<HeadMovement>();
            player = FindAnyObjectByType<TorusObject>();
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

        // Position of the player in front of the head, in degrees [0, 360).
        private float GetRelativeDegrees()
        {
            var playerDegrees = player.Angles.ThetaDegrees;
            var headDegrees = head.Theta * Mathf.Rad2Deg;
            return Mathf.Repeat(playerDegrees - headDegrees, 360f);
        }
    }
}
