using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WatermelonSeed.Launch
{
    public class PowerGaugeController : MonoBehaviour
    {
        public enum GaugeColorMode
        {
            KeepAssignedColor,
            DimWhenEmpty,
            PowerGradient
        }

        public enum VisibilityMode
        {
            [Tooltip("CanvasGroup 알파로 숨긴다. 오브젝트가 계속 활성이라 안전")]
            CanvasGroupAlpha,

            [Tooltip("gaugeRoot를 SetActive로 켜고 끈다. gaugeRoot는 반드시 자식이어야 함")]
            SetActive
        }

        [Header("Gauge Rule")]
        [Tooltip("게이지 칸 수")]
        [Min(1)] public int cellCount = 5;

        [Tooltip("1초에 채워지는 칸 수 (상점 강화 전 기본값)")]
        public float baseFillSpeedPerSecond = 2.5f;

        [Tooltip("끝까지 차면 다시 내려오는 왕복 방식 사용")]
        public bool pingPong = true;

        [Tooltip("체크 시 손을 뗀 순간의 값을 칸 단위(1~5)로 올림 처리")]
        public bool snapToCell = false;

        [Tooltip("아주 짧게 눌렀을 때 파워가 0이 되지 않도록 하는 하한값")]
        [Range(0f, 1f)] public float minimumPower = 0.1f;

        [Header("Visibility")]
        [Tooltip("CanvasGroupAlpha 권장. SetActive는 gaugeRoot가 자식일 때만 사용")]
        public VisibilityMode visibilityMode = VisibilityMode.CanvasGroupAlpha;

        [Tooltip("숨김/표시 대상 루트. 비워두면 이 오브젝트를 사용")]
        public GameObject gaugeRoot;

        [Tooltip("표시/숨김 전환에 걸리는 시간(초). 0이면 즉시")]
        public float fadeDuration = 0.08f;

        [Header("Cells")]
        [Tooltip("아래쪽 칸부터 순서대로 넣는다. Image Type은 Filled 권장")]
        public List<Image> cellImages = new List<Image>();

        [Header("Color Handling")]
        [Tooltip("이미 칸마다 색을 입혀둔 경우 KeepAssignedColor를 사용한다.")]
        public GaugeColorMode colorMode = GaugeColorMode.KeepAssignedColor;

        [Tooltip("DimWhenEmpty 모드에서 비어있는 칸의 알파 배율")]
        [Range(0f, 1f)] public float emptyCellAlpha = 0.25f;

        public Color lowPowerColor = new Color(0.4f, 0.9f, 0.4f);
        public Color highPowerColor = new Color(0.95f, 0.35f, 0.25f);

        [Header("Follow Crosshair")]
        [Tooltip("게이지 자신의 RectTransform")]
        public RectTransform gaugeRect;

        [Tooltip("따라갈 조준점 RectTransform")]
        public RectTransform crosshairRect;

        [Tooltip("조준점 기준 오프셋 (px)")]
        public Vector2 followOffset = new Vector2(120f, 0f);

        [Tooltip("Canvas가 Screen Space - Overlay가 아니면 체크")]
        public bool followByAnchoredPosition = false;

        [Header("Live Status (Read Only)")]
        [SerializeField] private bool debugIsCharging;
        [SerializeField] private float debugCurrentValue;

        public bool IsCharging { get; private set; }
        public float CurrentValue { get; private set; }

        public float NormalizedPower => cellCount > 0 ? Mathf.Clamp01(CurrentValue / cellCount) : 0f;
        public int CurrentStep => Mathf.Clamp(Mathf.CeilToInt(CurrentValue), 1, cellCount);

        private int direction = 1;
        private readonly List<Color> originalColors = new List<Color>();
        private CanvasGroup canvasGroup;
        private float targetAlpha;

        public float CurrentFillSpeed =>
            baseFillSpeedPerSecond * UpgradeManager.Multiplier(UpgradeType.GaugeSpeed);

        private void Awake()
        {
            if (gaugeRoot == gameObject)
            {
                Debug.LogError($"[PowerGaugeController] gaugeRoot 가 자기 자신({name})입니다. " +
                               "자식 오브젝트를 할당하세요. 참조를 해제합니다.");
                gaugeRoot = null;
            }
            if (gaugeRoot == null) gaugeRoot = gameObject;
            if (gaugeRect == null) gaugeRect = transform as RectTransform;

            CacheOriginalColors();
            SetupCanvasGroup();
            ApplyVisibility(false, true);
        }

        private void SetupCanvasGroup()
        {
            if (visibilityMode != VisibilityMode.CanvasGroupAlpha) return;

            canvasGroup = gaugeRoot.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gaugeRoot.AddComponent<CanvasGroup>();
            }

            // 게이지는 클릭을 절대 가로채면 안 된다
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void CacheOriginalColors()
        {
            originalColors.Clear();
            for (int i = 0; i < cellImages.Count; i++)
            {
                originalColors.Add(cellImages[i] != null ? cellImages[i].color : Color.white);
            }
        }

        private void OnDisable()
        {
            IsCharging = false;
            CurrentValue = 0f;
            ApplyVisibility(false, true);
        }

        public void BeginCharge()
        {
            Debug.Log($"[PowerGauge] BeginCharge 호출됨 / gaugeRoot active = " +
              $"{(gaugeRoot != null ? gaugeRoot.activeSelf.ToString() : "null")}");
            IsCharging = true;
            CurrentValue = 0f;
            direction = 1;

            ApplyVisibility(true, fadeDuration <= 0f);
            UpdateVisual();
            FollowCrosshair();
        }

        public float EndCharge()
        {
            if (!IsCharging) return 0f;

            IsCharging = false;

            float value = CurrentValue;
            if (snapToCell) value = Mathf.Clamp(Mathf.Ceil(value), 1f, cellCount);

            float normalized = cellCount > 0 ? Mathf.Clamp01(value / cellCount) : 0f;
            normalized = Mathf.Max(normalized, minimumPower);

            CurrentValue = normalized * cellCount;
            UpdateVisual();
            ApplyVisibility(false, fadeDuration <= 0f);

            return normalized;
        }

        public void CancelCharge()
        {
            IsCharging = false;
            CurrentValue = 0f;
            ApplyVisibility(false, fadeDuration <= 0f);
        }

        private void Update()
        {
            UpdateFade();

            debugIsCharging = IsCharging;
            debugCurrentValue = CurrentValue;

            if (!IsCharging) return;

            CurrentValue += direction * CurrentFillSpeed * Time.deltaTime;

            if (CurrentValue >= cellCount)
            {
                CurrentValue = cellCount;
                if (pingPong) direction = -1;
            }
            else if (CurrentValue <= 0f)
            {
                CurrentValue = 0f;
                direction = 1;
            }

            UpdateVisual();
            FollowCrosshair();
        }

        private void ApplyVisibility(bool visible, bool immediate)
        {
            targetAlpha = visible ? 1f : 0f;

            if (visibilityMode == VisibilityMode.SetActive)
            {
                if (gaugeRoot == gameObject)
                {
                    Debug.LogWarning("[PowerGaugeController] SetActive 모드에서 gaugeRoot가 자기 자신입니다.");
                    return;
                }
                if (gaugeRoot.activeSelf != visible) gaugeRoot.SetActive(visible);
                return;
            }

            // CanvasGroupAlpha 모드: 오브젝트는 항상 켜두고 alpha 로만 숨긴다.
            if (gaugeRoot != null && !gaugeRoot.activeSelf) gaugeRoot.SetActive(true);

            // CanvasGroupAlpha 모드: 오브젝트는 항상 켜두고 alpha로만 숨긴다.
            if (gaugeRoot != null && !gaugeRoot.activeSelf) gaugeRoot.SetActive(true);

            if (canvasGroup == null) return;
            if (immediate) canvasGroup.alpha = targetAlpha;


            if (canvasGroup == null) return;
            if (immediate) canvasGroup.alpha = targetAlpha;
        }
        private void OnEnable()
        {
            if (visibilityMode == VisibilityMode.CanvasGroupAlpha &&
                gaugeRoot != null && gaugeRoot != gameObject && !gaugeRoot.activeSelf)
            {
                gaugeRoot.SetActive(true);
            }
            ApplyVisibility(false, true);
        }


        private void UpdateFade()
        {
            if (visibilityMode != VisibilityMode.CanvasGroupAlpha) return;
            if (canvasGroup == null) return;
            if (Mathf.Approximately(canvasGroup.alpha, targetAlpha)) return;

            if (fadeDuration <= 0f)
            {
                canvasGroup.alpha = targetAlpha;
                return;
            }

            canvasGroup.alpha = Mathf.MoveTowards(
                canvasGroup.alpha, targetAlpha, Time.deltaTime / fadeDuration);
        }

        private void UpdateVisual()
        {
            for (int i = 0; i < cellImages.Count; i++)
            {
                Image cell = cellImages[i];
                if (cell == null) continue;

                float fill = Mathf.Clamp01(CurrentValue - i);
                cell.fillAmount = fill;

                ApplyCellColor(cell, i, fill);
            }
        }

        private void ApplyCellColor(Image cell, int index, float fill)
        {
            switch (colorMode)
            {
                case GaugeColorMode.KeepAssignedColor:
                    break;

                case GaugeColorMode.DimWhenEmpty:
                    Color origin = index < originalColors.Count ? originalColors[index] : cell.color;
                    origin.a = fill > 0f ? origin.a : origin.a * emptyCellAlpha;
                    cell.color = origin;
                    break;

                case GaugeColorMode.PowerGradient:
                    Color powerColor = Color.Lerp(lowPowerColor, highPowerColor, NormalizedPower);
                    powerColor.a = fill > 0f ? powerColor.a : powerColor.a * emptyCellAlpha;
                    cell.color = powerColor;
                    break;
            }
        }

        private void FollowCrosshair()
        {
            if (gaugeRect == null || crosshairRect == null) return;

            if (followByAnchoredPosition)
            {
                gaugeRect.anchoredPosition = crosshairRect.anchoredPosition + followOffset;
            }
            else
            {
                gaugeRect.position = crosshairRect.position + (Vector3)followOffset;
            }
        }
    }
}
