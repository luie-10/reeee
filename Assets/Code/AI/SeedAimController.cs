using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WatermelonSeed.Launch
{
    public class SeedAimController : MonoBehaviour
    {
        [Header("References")]
        public Camera aimCamera;
        public SeedLauncher launcher;
        public PowerGaugeController powerGauge;
        public CrosshairFollowController crosshairController;

        [Header("Aim Origin")]
        [Tooltip("조준점 위치를 기준으로 조준할지 여부. 해제하면 화면 정중앙 기준")]
        public bool useCrosshairScreenPoint = true;

        [Tooltip("조준점 RectTransform")]
        public RectTransform crosshairRect;

        [Tooltip("조준점이 들어있는 Canvas (Overlay가 아닐 때 좌표 변환에 사용)")]
        public Canvas uiCanvas;

        [Header("Input Rules")]
        [Tooltip("UI 위를 눌렀을 때는 충전을 시작하지 않는다.")]
        public bool ignoreWhenPointerOverUI = true;

        [Tooltip("씨앗이 날아가는 중에는 새로 발사하지 못하게 한다.")]
        public bool blockWhileSeedInAir = true;

        [Tooltip("체크 시 충전 중에만 조준점을 표시한다.")]
        public bool showCrosshairOnlyWhileAiming = false;

        /// <summary>결과 UI 표시 중 등 외부에서 입력을 잠글 때 사용</summary>
        public bool InputLocked { get; set; }

        public bool IsCharging => powerGauge != null && powerGauge.IsCharging;
        public bool IsSeedInAir { get; private set; }

        public event Action OnChargeStarted;
        public event Action<float, int> OnFired; // (정규화 파워, 칸 수)

        private void OnEnable()
        {
            if (aimCamera == null) aimCamera = Camera.main;

            if (launcher != null) launcher.OnSeedLaunched += HandleSeedLaunched;

            if (showCrosshairOnlyWhileAiming && crosshairController != null)
            {
                crosshairController.Hide();
            }
        }

        private void OnDisable()
        {
            if (launcher != null) launcher.OnSeedLaunched -= HandleSeedLaunched;
            if (powerGauge != null) powerGauge.CancelCharge();
        }

        private void Update()
        {
            bool blocked = InputLocked || (blockWhileSeedInAir && IsSeedInAir);

            if (blocked)
            {
                if (IsCharging) powerGauge.CancelCharge();
                return;
            }

            if (!IsCharging)
            {
                if (PointerDown() && !(ignoreWhenPointerOverUI && IsPointerOverUI()))
                {
                    BeginCharge();
                }
            }
            else if (!PointerHeld())
            {
                Fire();
            }
        }

        private void BeginCharge()
        {
            if (powerGauge == null)
            {
                Debug.LogError("[SeedAimController] powerGauge가 비어있습니다.");
                return;
            }

            if (crosshairController != null)
            {
                crosshairController.Show();
                crosshairController.SetCharging(true);
            }

            powerGauge.BeginCharge();
            OnChargeStarted?.Invoke();
        }

        private void Fire()
        {
            float power = powerGauge.EndCharge();
            int step = powerGauge.CurrentStep;

            if (launcher != null)
            {
                launcher.Launch(GetAimRay(), power, step);
            }

            if (crosshairController != null)
            {
                crosshairController.SetCharging(false);
                if (showCrosshairOnlyWhileAiming) crosshairController.Hide();
            }

            OnFired?.Invoke(power, step);
        }

        // 이 스크립트는 '씨앗이 공중에 있는지'만 추적한다.
        // 시도 횟수, 보상, 결과 UI는 SeedAttemptManager가 담당한다.
        private void HandleSeedLaunched(SeedProjectile seed)
        {
            if (seed == null) return;

            IsSeedInAir = true;
            seed.OnFlightFinished += HandleSeedFinished;
        }

        private void HandleSeedFinished(SeedProjectile seed, SeedFlightResult result)
        {
            seed.OnFlightFinished -= HandleSeedFinished;
            IsSeedInAir = false;
        }

        public Ray GetAimRay()
        {
            if (aimCamera == null) return new Ray(transform.position, transform.forward);

            if (useCrosshairScreenPoint && crosshairRect != null)
            {
                Camera uiCam = (uiCanvas != null && uiCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    ? uiCanvas.worldCamera
                    : null;

                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCam, crosshairRect.position);
                return aimCamera.ScreenPointToRay(screenPoint);
            }

            return aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }

        private bool PointerDown()
        {
            if (Input.GetMouseButtonDown(0)) return true;

            if (Input.touchCount > 0)
            {
                return Input.GetTouch(0).phase == TouchPhase.Began;
            }
            return false;
        }

        private bool PointerHeld()
        {
            if (Input.GetMouseButton(0)) return true;

            if (Input.touchCount > 0)
            {
                TouchPhase phase = Input.GetTouch(0).phase;
                return phase == TouchPhase.Began || phase == TouchPhase.Moved || phase == TouchPhase.Stationary;
            }
            return false;
        }

        private bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
