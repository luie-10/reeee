using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace WatermelonSeed.Launch
{
    public class ShopSceneUI : MonoBehaviour
    {
        [System.Serializable]
        public class ShopSlot
        {
            [Tooltip("이 슬롯이 강화할 항목")]
            public UpgradeType type;

            public TMP_Text nameText;
            public TMP_Text levelText;
            public TMP_Text costText;
            public Button buyButton;

            [Tooltip("표시할 이름 (비워두면 type 이름 사용)")]
            public string displayName;
        }

        [Header("Slots")]
        public List<ShopSlot> slots = new List<ShopSlot>();

        [Header("Common UI")]
        public TMP_Text coinText;
        public Button backButton;

        [Header("Return")]
        [Tooltip("저장된 복귀 씬을 찾지 못했을 때 사용할 기본 씬 이름")]
        public string fallbackSceneName = "GameScene";

        private void Start()
        {

            CursorState.ToUI();

            for (int i = 0; i < slots.Count; i++)
            {
                ShopSlot slot = slots[i]; // 클로저 캡처를 위해 지역 변수로 복사
                if (slot.buyButton != null)
                {
                    slot.buyButton.onClick.AddListener(() => Buy(slot.type));
                }

                if (slot.nameText != null)
                {
                    slot.nameText.SetText(string.IsNullOrEmpty(slot.displayName)
                        ? slot.type.ToString()
                        : slot.displayName);
                }
            }

            if (backButton != null) backButton.onClick.AddListener(BackToGame);

            if (UpgradeManager.Instance != null)
            {
                UpgradeManager.Instance.OnDataChanged += Refresh;
            }
            else
            {
                Debug.LogError("[ShopSceneUI] UpgradeManager를 찾을 수 없습니다. 게임 씬에서 진입했는지 확인하세요.");
            }

            Refresh();
        }

        private void OnDestroy()
        {
            if (UpgradeManager.Instance != null)
            {
                UpgradeManager.Instance.OnDataChanged -= Refresh;
            }
        }

        private void Buy(UpgradeType type)
        {
            var manager = UpgradeManager.Instance;
            if (manager == null) return;

            if (!manager.TryUpgrade(type))
            {
                Debug.Log($"[ShopSceneUI] 강화 실패 - 코인 부족 또는 최대 레벨 ({type})");
            }
        }

        private void Refresh()
        {
            var manager = UpgradeManager.Instance;
            if (manager == null) return;

            if (coinText != null) coinText.SetText($"코인 {manager.Coins}");

            for (int i = 0; i < slots.Count; i++)
            {
                ShopSlot slot = slots[i];
                bool isMax = manager.IsMaxLevel(slot.type);

                if (slot.levelText != null)
                {
                    slot.levelText.SetText($"Lv. {manager.GetLevel(slot.type)}");
                }

                if (slot.costText != null)
                {
                    slot.costText.SetText(isMax ? "MAX" : $"{manager.GetCost(slot.type)} 코인");
                }

                if (slot.buyButton != null)
                {
                    slot.buyButton.interactable = manager.CanUpgrade(slot.type);
                }
            }
        }

        public void BackToGame()
        {
            string target = PlayerPrefs.GetString("WS_ReturnScene", fallbackSceneName);
            if (string.IsNullOrEmpty(target)) target = fallbackSceneName;

            SceneManager.LoadScene(target);
        }
    }
}
