using System;
using UnityEngine;

namespace WSFGameJam2026.Menus
{
    /// <summary>
    /// Markers over the torus seen from above in the main menu: where the hero starts, where the jaw is
    /// and where the goal (the top of the head) is. Each marker follows the screen position of its target.
    /// Runs after the camera moved, so the markers do not lag behind.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class OverviewMarkers : MonoBehaviour
    {
        [SerializeField]
        private Camera viewCamera;

        [Tooltip("The rect the markers live in (stretched over the screen, anchored in the middle).")]
        [SerializeField]
        private RectTransform area;

        [SerializeField]
        private Marker[] markers = new Marker[0];

        private void Reset()
        {
            viewCamera = Camera.main;
            area = transform as RectTransform;
        }

        private void LateUpdate()
        {
            if (viewCamera == null || area == null)
                return;

            foreach (var marker in markers)
                marker.Follow(viewCamera, area);
        }

        /// <summary>One marker and the object it points at.</summary>
        [Serializable]
        public class Marker
        {
            [SerializeField]
            private Transform target;

            [SerializeField]
            private RectTransform view;

            [Tooltip("Lifts the marker above the target along its up direction, in metres.")]
            [SerializeField]
            private float heightOffset = 1f;

            public void Follow(Camera viewCamera, RectTransform area)
            {
                if (view == null)
                    return;

                if (target == null)
                {
                    view.gameObject.SetActive(false);
                    return;
                }

                var screenPoint = viewCamera.WorldToScreenPoint(target.position + target.up * heightOffset);
                bool isVisible = screenPoint.z > 0f;
                view.gameObject.SetActive(isVisible);
                if (isVisible && RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screenPoint, null, out var localPoint))
                    view.anchoredPosition = localPoint;
            }
        }
    }
}
