using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace WatermelonSeed.Launch
{
    public class AttemptResultUI : MonoBehaviour
    {
        [Header("Panel")]
        public GameObject panelRoot;

        [Tooltip("panelRoot에 붙은 CanvasGroup. 비워두면 자동으로 추가한다.")]
        public CanvasGroup panelGroup;

        [Tooltip("패널이 뜨기까지 기다리는 시간(초). 카메라 복귀와 겹치지 않게 한다.")]
        public float showDelay = 1.0f;

        [Tooltip("페이드 인/아웃에 걸리는 시간(초). 0이면 즉시")]
        public float fadeDuration = 0.25f;

        [Tooltip("나타날 때 아래에서 살짝 올라오는 거리(px). 0이면 이동 없음")]
        public float slideUpDistance = 40f;

        [Tooltip("나타날 때 살짝 커지는 시작 배율. 1이면 스케일 연출 없음")]
        [Range(0.8f, 1f)] public float popStartScale = 0.96f;

        [Header("Texts (TextMeshPro)")]
        public TMP_Text distanceText;
        public TMP_Text powerText;
        public TMP_Text flightTimeText;
        public TMP_Text bestDistanceText;
        public TMP_Text rewardText;
        public TMP_Text attemptText;
        public TMP_Text coinText;

        [Header("Buttons")]
        public Button nextButton;
        public Button shopButton;

        [Header("Shop Scene")]
        [Tooltip("상점 씬 이름. Build Settings의 Scenes In Build에 반드시 추가되어 있어야 한다.")]
        public string shopSceneName = "ShopScene";

        [Tooltip("상점에서 돌아올 때 불러올 씬 이름. 비워두면 현재 씬 이름을 자동으로 사용한다.")]
        public string returnSceneName = "";

        [Header("Refs")]
        public SeedAttemptManager attemptManager;

        /// <summary>패널이 열려 있거나 열리는 중인지</summary>
        public bool IsOpen { get; private set; }

        private RectTransform panelRect;
        private Vector2 basePosition;
        private Coroutine animRoutine;
        private bool selfInsidePanel;   // 이 스크립트가 panelRoot 내부에 있는지
        private void Awake()
        {
            if (panelRoot == null)
            {
                Debug.LogError("[AttemptResultUI] panelRoot가 비어 있습니다.");
                return;
            }

            // 이 스크립트가 panelRoot 자신에 붙어 있으면
            // SetActive(false) 시 코루틴이 죽으므로 알파 방식만 사용한다.
            panelRect = panelRoot.GetComponent<RectTransform>();
            if (panelRect != null) basePosition = panelRect.anchoredPosition;

            // 이 스크립트가 panelRoot 자신이나 그 자식에 붙어 있으면
            // SetActive(false)로 자신을 끄게 되어 코루틴을 시작할 수 없다.
            selfInsidePanel = transform == panelRoot.transform ||
                              transform.IsChildOf(panelRoot.transform);

            if (selfInsidePanel)
            {
                Debug.LogWarning("[AttemptResultUI] 스크립트가 panelRoot 내부에 있습니다. " +
                                 "SetActive 대신 알파로만 숨깁니다. " +
                                 "가능하면 컴포넌트를 Canvas나 GameManager로 옮기세요.");
            }


            if (panelGroup == null) panelGroup = panelRoot.GetComponent<CanvasGroup>();
            if (panelGroup == null) panelGroup = panelRoot.AddComponent<CanvasGroup>();

            if (nextButton != null && attemptManager != null)
            {
                nextButton.onClick.AddListener(attemptManager.ContinueNextAttempt);
            }
            else if (nextButton != null)
            {
                Debug.LogError("[AttemptResultUI] attemptManager가 비어 있어 다음 버튼이 동작하지 않습니다. " +
                               "인스펙터에서 GameManager를 할당하세요.");
            }

            if (shopButton != null) shopButton.onClick.AddListener(OpenShopScene);

            HideImmediate();
        }

        public void Show(SeedFlightResult result, int attemptNumber, float bestDistance,
                         int reward, int remainingAttempts)
        {
            FillTexts(result, attemptNumber, bestDistance, reward, remainingAttempts);

            IsOpen = true;

            if (animRoutine != null) StopCoroutine(animRoutine);
            animRoutine = StartCoroutine(ShowRoutine());
        }

        private void FillTexts(SeedFlightResult result, int attemptNumber, float bestDistance,
                               int reward, int remainingAttempts)
        {
            SetText(distanceText, $"{result.distance:F2} m");
            SetText(powerText, $"파워 {result.powerStep}칸 ({result.normalizedPower * 100f:F0}%)");
            SetText(flightTimeText, $"비행 시간 {result.flightTime:F2}초");
            SetText(bestDistanceText, $"최고 기록 {bestDistance:F2} m");
            SetText(rewardText, $"+{reward} 코인");

            if (UpgradeManager.Instance != null)
            {
                SetText(coinText, $"보유 코인 {UpgradeManager.Instance.Coins}");
            }

            SetText(attemptText, remainingAttempts < 0
                ? $"{attemptNumber}번째 시도"
                : $"{attemptNumber}번째 시도 (남은 횟수 {remainingAttempts})");
        }

        private IEnumerator ShowRoutine()
        {
            // 패널을 미리 켜두되 완전히 투명한 상태로 둔다.
            panelRoot.SetActive(true);
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;

            if (panelRect != null)
            {
                panelRect.anchoredPosition = basePosition + new Vector2(0f, -slideUpDistance);
                panelRect.localScale = Vector3.one * popStartScale;
            }

            // 카메라가 원위치로 돌아올 시간을 준 뒤에 뜨게 한다.
            if (showDelay > 0f) yield return new WaitForSecondsRealtime(showDelay);

            // 커서는 패널이 실제로 보이기 시작할 때 풀어준다.
            CursorState.ToUI();
            panelGroup.blocksRaycasts = true;

            float t = 0f;
            float dur = Mathf.Max(0.0001f, fadeDuration);

            while (t < dur)
            {
                // Time.timeScale이 0이어도 동작하도록 unscaled 사용
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                float eased = 1f - (1f - k) * (1f - k);   // ease-out quad

                panelGroup.alpha = eased;

                if (panelRect != null)
                {
                    panelRect.anchoredPosition = basePosition +
                        new Vector2(0f, Mathf.Lerp(-slideUpDistance, 0f, eased));
                    panelRect.localScale = Vector3.one * Mathf.Lerp(popStartScale, 1f, eased);
                }

                yield return null;
            }

            panelGroup.alpha = 1f;
            panelGroup.interactable = true;

            if (panelRect != null)
            {
                panelRect.anchoredPosition = basePosition;
                panelRect.localScale = Vector3.one;
            }

            animRoutine = null;
        }

        /// <summary>부드럽게 닫는다. 다음 시도 버튼에서 호출되는 경로.</summary>
        public void Hide()
        {
            if (!IsOpen)
            {
                HideImmediate();
                return;
            }

            IsOpen = false;

            if (animRoutine != null) StopCoroutine(animRoutine);
            animRoutine = StartCoroutine(HideRoutine());
        }

        private IEnumerator HideRoutine()
        {
            // 닫기 시작하는 즉시 클릭을 막아 중복 입력을 방지한다.
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;

            float start = panelGroup.alpha;
            float t = 0f;
            float dur = Mathf.Max(0.0001f, fadeDuration);

            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);

                panelGroup.alpha = Mathf.Lerp(start, 0f, k);

                if (panelRect != null)
                {
                    panelRect.anchoredPosition = basePosition +
                        new Vector2(0f, Mathf.Lerp(0f, -slideUpDistance * 0.5f, k));
                }

                yield return null;
            }

            HideImmediate();
            animRoutine = null;
        }

        /// <summary>연출 없이 즉시 닫는다.</summary>
        public void HideImmediate()
        {
            IsOpen = false;

            if (animRoutine != null)
            {
                StopCoroutine(animRoutine);
                animRoutine = null;
            }

            if (panelGroup != null)
            {
                panelGroup.alpha = 0f;
                panelGroup.interactable = false;
                panelGroup.blocksRaycasts = false;
            }

            if (panelRect != null)
            {
                panelRect.anchoredPosition = basePosition;
                panelRect.localScale = Vector3.one;
            }

            if (panelRoot != null) panelRoot.SetActive(false);
        }

        /// <summary>상점 씬으로 이동. 돌아올 씬 이름을 저장해둔다.</summary>
        public void OpenShopScene()
        {
            if (string.IsNullOrEmpty(shopSceneName))
            {
                Debug.LogError("[AttemptResultUI] shopSceneName이 비어 있습니다.");
                return;
            }

            string backTo = string.IsNullOrEmpty(returnSceneName)
                ? SceneManager.GetActiveScene().name
                : returnSceneName;

            // 커서를 잠긴 상태로 씬을 넘기면 상점 조작이 불가능하므로 반드시 해제
            CursorState.ToUI();
            Time.timeScale = 1f;

            PlayerPrefs.SetString("WS_ReturnScene", backTo);
            PlayerPrefs.Save();

            SceneManager.LoadScene(shopSceneName);
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null) target.SetText(value);
        }
    }
}
