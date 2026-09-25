using UnityEngine;

namespace Ceffy.Demos.SharedInstance
{
    /// <summary>
    /// Positions a UI element in a screen-space canvas over a world Transform each frame,
    /// after camera/object LateUpdate motion via <see cref="Canvas.preWillRenderCanvases"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class DemoFollowTarget : MonoBehaviour
    {
        [Tooltip("World object this element should follow.")]
        public Transform Target;

        [Tooltip("Offset applied in the target's local space before projecting to screen.")]
        public Vector3 WorldOffset = new Vector3(0f, 1.2f, 0f);

        [Tooltip("Additional offset in screen/canvas pixels after projection.")]
        public Vector2 ScreenOffset;

        [Tooltip("Hide the element when the target is behind the camera.")]
        public bool HideWhenBehindCamera = true;

        [Tooltip("Camera used for WorldToScreenPoint. Defaults to Camera.main.")]
        public Camera Camera;

        private RectTransform rectTransform;
        private Canvas canvas;
        private CanvasGroup canvasGroup;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>();
            if (!TryGetComponent(out canvasGroup))
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            // This component owns placement, so anchor to the parent's centre and drive
            // anchoredPosition. The pivot is left alone so callers can offset the element.
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

            Canvas.preWillRenderCanvases += UpdateFollow;
        }

        private void OnDisable()
        {
            Canvas.preWillRenderCanvases -= UpdateFollow;
            if (canvasGroup)
                SetVisible(true);
        }

        private void UpdateFollow()
        {
            if (!Target)
                return;

            var cam = Camera ? Camera : Camera.main;
            if (!cam)
                return;

            Vector3 screen = cam.WorldToScreenPoint(Target.TransformPoint(WorldOffset));

            bool behind = screen.z <= 0f;
            if (HideWhenBehindCamera)
                SetVisible(!behind);

            if (behind)
                return;

            Camera eventCamera = null;
            if (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                eventCamera = canvas.worldCamera ? canvas.worldCamera : cam;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)rectTransform.parent, screen, eventCamera, out Vector2 local))
                rectTransform.anchoredPosition = local + ScreenOffset;
        }

        private void SetVisible(bool visible)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }
    }
}
