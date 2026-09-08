using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WatermelonSeed.Launch
{
    public class GaugeDebugger : MonoBehaviour
    {
        public PowerGaugeController gauge;
        public KeyCode dumpKey = KeyCode.F3;
        [Header("강제 표시 테스트")]
        public KeyCode forceShowKey = KeyCode.F4;

        private void ForceShow()
        {
            if (gauge == null) return;

            CanvasGroup cg = gauge.GetComponentInChildren<CanvasGroup>(true);
            if (cg != null)
            {
                cg.alpha = 1f;
                Debug.Log("CanvasGroup alpha 를 1 로 강제했습니다.");
            }

            RectTransform rt = gauge.gaugeRect != null
                ? gauge.gaugeRect
                : gauge.GetComponent<RectTransform>();

            if (rt != null)
            {
                rt.position = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
                Debug.Log($"게이지를 화면 중앙으로 이동: {rt.position}");
            }

            IList<Image> cells = gauge.cellImages;
            if (cells != null)
            {
                for (int i = 0; i < cells.Count; i++)
                    if (cells[i] != null) cells[i].fillAmount = (i < 3) ? 1f : 0f;
                Debug.Log("테스트용으로 3칸만 채웠습니다.");
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(dumpKey)) Dump();
            if (Input.GetKeyDown(forceShowKey)) ForceShow();

        }

        private void Dump()
        {
            Debug.Log("===== 파워 게이지 진단 =====");

            if (gauge == null)
            {
                Debug.LogError("gauge 슬롯이 비어 있습니다. PowerGauge 오브젝트를 넣으세요.");
                return;
            }

            Debug.Log($"컨트롤러 오브젝트 = {gauge.name} / " +
                      $"activeInHierarchy = {gauge.gameObject.activeInHierarchy} / " +
                      $"컴포넌트 enabled = {gauge.enabled}");

            if (!gauge.gameObject.activeInHierarchy)
                Debug.LogError("컨트롤러 오브젝트가 비활성입니다. Update 가 절대 돌지 않습니다.");

            if (gauge.gaugeRoot == null)
            {
                Debug.LogWarning("gaugeRoot 가 비어 있습니다.");
            }
            else
            {
                bool self = gauge.gaugeRoot == gauge.gameObject;
                Debug.Log($"gaugeRoot = {gauge.gaugeRoot.name} / " +
                          $"activeSelf = {gauge.gaugeRoot.activeSelf}" +
                          (self ? "   ← 자기 자신입니다. 자식 오브젝트로 바꾸세요." : ""));
            }

            CanvasGroup cg = gauge.GetComponentInChildren<CanvasGroup>(true);
            if (cg != null)
            {
                Debug.Log($"CanvasGroup '{cg.name}' alpha = {cg.alpha}");
                if (cg.alpha <= 0.01f)
                    Debug.LogWarning("alpha 가 0 입니다. BeginCharge 가 호출되지 않았거나 " +
                                     "SeedAimController.powerGauge 가 비어 있습니다.");
            }

            IList<Image> cells = gauge.cellImages;

            if (cells == null || cells.Count == 0)
            {
                Debug.LogError("cellImages 가 비어 있습니다. Cell 이미지 5개를 할당하세요.");
            }
            else
            {
                Debug.Log($"cellImages 개수 = {cells.Count}");

                for (int i = 0; i < cells.Count; i++)
                {
                    Image img = cells[i];
                    if (img == null)
                    {
                        Debug.LogError($"cellImages[{i}] 가 null 입니다.");
                        continue;
                    }

                    string warn = "";
                    if (img.type != Image.Type.Filled)
                        warn += "  ← Type 이 Filled 가 아니라 fillAmount 가 무시됩니다.";
                    if (img.sprite == null)
                        warn += "  ← sprite 가 없습니다.";
                    if (img.color.a <= 0.01f)
                        warn += "  ← color.a 가 0 입니다.";
                    if (!img.gameObject.activeInHierarchy)
                        warn += "  ← 오브젝트가 비활성입니다.";

                    Debug.Log($"cellImages[{i}] = {img.name} / " +
                              $"active={img.gameObject.activeInHierarchy} / " +
                              $"type={img.type} / fillAmount={img.fillAmount:F2} / " +
                              $"color={img.color} / enabled={img.enabled}{warn}");
                }
            }

            RectTransform rt = gauge.gaugeRect != null
                ? gauge.gaugeRect
                : gauge.GetComponent<RectTransform>();

            if (rt != null)
            {
                Debug.Log($"gaugeRect 크기 = {rt.rect.size} / scale = {rt.lossyScale}");

                if (rt.rect.width < 1f || rt.rect.height < 1f)
                    Debug.LogWarning("Rect 크기가 0 에 가깝습니다. Width/Height 를 지정하세요.");
                if (rt.lossyScale.x < 0.01f)
                    Debug.LogWarning("스케일이 0 에 가깝습니다.");

                Vector3[] corners = new Vector3[4];
                rt.GetWorldCorners(corners);

                Canvas canvas = gauge.GetComponentInParent<Canvas>();
                Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    ? canvas.worldCamera
                    : null;

                Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
                Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

                Debug.Log($"화면 영역 = ({min.x:F0},{min.y:F0}) ~ ({max.x:F0},{max.y:F0}) / " +
                          $"해상도 = {Screen.width}x{Screen.height}");

                bool off = max.x < 0f || min.x > Screen.width || max.y < 0f || min.y > Screen.height;
                if (off)
                    Debug.LogError("게이지가 화면 밖에 있습니다. followOffset 을 (60,0) 정도로 줄이세요.");

                if (canvas != null)
                    Debug.Log($"Canvas = {canvas.name} / renderMode = {canvas.renderMode} / " +
                              $"worldCamera = {(canvas.worldCamera == null ? "없음" : canvas.worldCamera.name)}");
            }
        }
    }
}
