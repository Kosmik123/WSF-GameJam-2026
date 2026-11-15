using TMPro;
using UnityEngine;
using WSFGameJam2026.Rounds;

namespace WSFGameJam2026.Hud
{
    /// <summary>Shows the record (the best number of completed rounds), refreshed when a run ends.</summary>
    public class RecordLabel : MonoBehaviour
    {
        [SerializeField]
        private RoundController controller;

        [SerializeField]
        private TMP_Text label;

        [Tooltip("{0} is replaced with the record.")]
        [SerializeField]
        private string format = "rekord: {0}";

        private void Reset()
        {
            controller = FindAnyObjectByType<RoundController>();
            label = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            if (controller == null)
                return;

            controller.OnRunEnded += RoundController_OnRunEnded;
            Show(controller.BestRounds);
        }

        private void RoundController_OnRunEnded(int completedRounds, int bestRounds, bool isNewBest)
        {
            Show(bestRounds);
        }

        private void Show(int record)
        {
            if (label)
                label.text = string.Format(format, record);
        }

        private void OnDisable()
        {
            if (controller == null)
                return;

            controller.OnRunEnded -= RoundController_OnRunEnded;
        }
    }
}
