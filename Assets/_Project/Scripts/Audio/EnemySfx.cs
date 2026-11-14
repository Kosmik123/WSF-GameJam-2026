using UnityEngine;
using WSFGameJam2026.Enemies;

namespace WSFGameJam2026.Audio
{
    /// <summary>
    /// Plays the attack telegraph (windup) and release sounds of an enemy, from its <see cref="EnemyBehaviour"/> events.
    /// Put on enemy prefabs next to the behaviour.
    /// </summary>
    public class EnemySfx : MonoBehaviour
    {
        [SerializeField]
        private SoundEffect windup;

        [Tooltip("The attack is released (lunge, shot, slam).")]
        [SerializeField]
        private SoundEffect attack;

        private EnemyBehaviour behaviour;

        private void Awake()
        {
            TryGetComponent(out behaviour);
        }

        private void OnEnable()
        {
            if (behaviour == null)
                return;

            behaviour.OnWindupStarted += Behaviour_OnWindupStarted;
            behaviour.OnAttackReleased += Behaviour_OnAttackReleased;
        }

        private void Behaviour_OnWindupStarted()
        {
            Sfx.Play(windup, transform.position);
        }

        private void Behaviour_OnAttackReleased()
        {
            Sfx.Play(attack, transform.position);
        }

        private void OnDisable()
        {
            if (behaviour == null)
                return;

            behaviour.OnWindupStarted -= Behaviour_OnWindupStarted;
            behaviour.OnAttackReleased -= Behaviour_OnAttackReleased;
        }
    }
}
