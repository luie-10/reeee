using UnityEngine;
using UnityEngine.UI;

namespace WatermelonSeed.Launch
{
    public class CrosshairFollowController : MonoBehaviour
    {
        public enum FollowMode { ScreenCenter, Pointer }
        public enum ClampMode { None, ScreenRect, RadiusFromCenter }

        [Header("참조")]
        public GameObject crosshairRoot;
        public RectTransform crosshairRect;
        public Canvas uiCanvas;
        public Image crosshairImage;
        public Camera aimCamera;

        [Header("추적 방식")]
        [Tooltip("커서 잠금(CursorState.ToGameplay)을 쓰면 반드시 ScreenCenter 여야 한다.")]
        public FollowMode followMode = FollowMode.ScreenCenter;
        [Range(0f, 0.3f)] public float followSmoothTime = 0.04f;
        public Vector2 pointerOffset = Vector2.zero;

        [Header("화면 제한")]
        public ClampMode clampMode = ClampMode.ScreenRect;
        public float screenPadding = 40f;
        public float maxRadiusPixels = 300f;

        [Header("조준 레이캐스트")]
        public bool useAimRaycast = false;
        public float raycastDistance = 200f;
        public LayerMask raycastMask = ~0;

        [Header("색상 / 스케일")]
        public Color normalColor = new Color(1f, 1f, 1f, 0.85f);
        public Color targetColor = new Color(1f, 0.3f, 0.3f, 1f);
        public float chargingScale = 1.25f;
        public float scaleLerpSpeed = 10f;

        private Vector2 currentScreenPos;
        private Vector2 posVelocity;
        private bool isCharging;
        private bool isVisible;

        private void Awake()
        {
            if (crosshairRect == null) crosshairRect = GetComponent<RectTransform>();
            if (crosshairRoot == null) crosshairRoot = gameObject;
            if (crosshairImage == null) crosshairImage = GetComponent<Image>();
            if (uiCanvas == null) uiCanvas = GetComponentInParent<Canvas>();
            if (aimCamera == null) aimCamera = Camera.main;

            // 조준경은 절대 클릭을 가로채면 안 된다.
            if (crosshairImage != null) crosshairImage.raycastTarget = false;

            currentScreenPos = ScreenCenter();
            Hide();
        }

        private void LateUpdate()
        {
            if (!isVisible) return;

            Vector2 target = (followMode == FollowMode.Pointer)
                ? (Vector2)Input.mousePosition + pointerOffset
                : ScreenCenter() + pointerOffset;

            target = ApplyClamp(target);

            currentScreenPos = (followSmoothTime > 0.0001f && followMode == FollowMode.Pointer)
                ? Vector2.SmoothDamp(currentScreenPos, target, ref posVelocity, followSmoothTime)
                : target;

            ApplyScreenPosition(currentScreenPos);
            UpdateRaycastColor();
            UpdateScale();
        }

        private Vector2 ScreenCenter() => new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        private Vector2 ApplyClamp(Vector2 p)
        {
            switch (clampMode)
            {
                case ClampMode.ScreenRect:
                    p.x = Mathf.Clamp(p.x, screenPadding, Screen.width - screenPadding);
                    p.y = Mathf.Clamp(p.y, screenPadding, Screen.height - screenPadding);
                    break;
                case ClampMode.RadiusFromCenter:
                    Vector2 c = ScreenCenter();
                    Vector2 d = p - c;
                    if (d.magnitude > maxRadiusPixels) p = c + d.normalized * maxRadiusPixels;
                    break;
            }
            return p;
        }

        private void ApplyScreenPosition(Vector2 screenPos)
        {
            if (crosshairRect == null || uiCanvas == null) return;

            if (uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                crosshairRect.position = screenPos;
            }
            else
            {
                Camera cam = uiCanvas.worldCamera != null ? uiCanvas.worldCamera : aimCamera;
                RectTransform parent = crosshairRect.parent as RectTransform;
                if (parent == null) return;

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        parent, screenPos, cam, out Vector2 local))
                {
                    crosshairRect.anchoredPosition = local;
                }
            }
        }

        private void UpdateRaycastColor()
        {
            if (crosshairImage == null) return;

            if (!useAimRaycast || aimCamera == null)
            {
                crosshairImage.color = normalColor;
                return;
            }

            Ray ray = GetAimRay();
            bool hit = Physics.Raycast(ray, raycastDistance, raycastMask, QueryTriggerInteraction.Ignore);
            crosshairImage.color = hit ? targetColor : normalColor;
        }

        private void UpdateScale()
        {
            if (crosshairRect == null) return;
            float t = isCharging ? chargingScale : 1f;
            Vector3 goal = Vector3.one * t;
            crosshairRect.localScale = Vector3.Lerp(
                crosshairRect.localScale, goal, Time.deltaTime * scaleLerpSpeed);
        }

        /// <summary>조준경이 가리키는 방향의 Ray. SeedLauncher 가 사용.</summary>
        public Ray GetAimRay()
        {
            if (aimCamera == null) return new Ray(transform.position, transform.forward);
            Vector3 sp = new Vector3(currentScreenPos.x, currentScreenPos.y, 0f);
            return aimCamera.ScreenPointToRay(sp);
        }

        public Vector2 GetScreenPoint() => currentScreenPos;

        public void Show()
        {
            isVisible = true;
            if (crosshairRoot != null) crosshairRoot.SetActive(true);
            currentScreenPos = ApplyClamp(
                followMode == FollowMode.Pointer
                    ? (Vector2)Input.mousePosition + pointerOffset
                    : ScreenCenter() + pointerOffset);
            posVelocity = Vector2.zero;
            ApplyScreenPosition(currentScreenPos);
        }

        public void Hide()
        {
            isVisible = false;
            isCharging = false;
            if (crosshairRoot != null) crosshairRoot.SetActive(false);
        }

        public void SetCharging(bool charging) => isCharging = charging;
    }
}
