using System;
using System.Collections.Generic;
using UnityEngine;

namespace WatermelonSeed.Launch
{
    public class UpgradeManager : MonoBehaviour
    {
        public static UpgradeManager Instance { get; private set; }

        [Header("Data")]
        public UpgradeTable table;

        [Tooltip("PlayerPrefs로 강화 레벨과 코인을 저장/복원한다.")]
        public bool useSaveData = true;

        [Tooltip("저장 데이터가 없을 때 지급할 시작 코인")]
        public int startCoins = 0;

        [Tooltip("씬이 바뀌어도 유지할지 여부")]
        public bool persistAcrossScenes = true;

        [Header("Live Status (Read Only)")]
        [SerializeField] private int coins;
        [SerializeField] private string levelSummary;

        private readonly Dictionary<UpgradeType, int> levels = new Dictionary<UpgradeType, int>();

        public int Coins => coins;
        public event Action OnDataChanged;

        private const string CoinKey = "WS_Coins";
        private const string LevelKeyPrefix = "WS_UpgradeLevel_";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (persistAcrossScenes) DontDestroyOnLoad(gameObject);

            Load();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>매니저가 없어도 안전하게 배율을 얻는 헬퍼</summary>
        public static float Multiplier(UpgradeType type)
        {
            return Instance != null ? Instance.GetMultiplier(type) : 1f;
        }

        public int GetLevel(UpgradeType type)
        {
            return levels.TryGetValue(type, out int level) ? level : 0;
        }

        public float GetMultiplier(UpgradeType type)
        {
            var entry = table != null ? table.Get(type) : null;
            if (entry == null) return 1f;
            return entry.GetMultiplier(GetLevel(type));
        }

        public int GetCost(UpgradeType type)
        {
            var entry = table != null ? table.Get(type) : null;
            return entry != null ? entry.GetCost(GetLevel(type)) : int.MaxValue;
        }

        public bool IsMaxLevel(UpgradeType type)
        {
            var entry = table != null ? table.Get(type) : null;
            return entry == null || GetLevel(type) >= entry.maxLevel;
        }

        public bool CanUpgrade(UpgradeType type)
        {
            return !IsMaxLevel(type) && coins >= GetCost(type);
        }

        /// <summary>상점에서 호출. 성공하면 true</summary>
        public bool TryUpgrade(UpgradeType type)
        {
            if (!CanUpgrade(type)) return false;

            coins -= GetCost(type);
            levels[type] = GetLevel(type) + 1;

            Save();
            OnDataChanged?.Invoke();
            return true;
        }

        public void AddCoins(int amount)
        {
            if (amount == 0) return;
            coins = Mathf.Max(0, coins + amount);

            Save();
            OnDataChanged?.Invoke();
        }

        private void Load()
        {
            levels.Clear();

            foreach (UpgradeType type in Enum.GetValues(typeof(UpgradeType)))
            {
                int saved = useSaveData ? PlayerPrefs.GetInt(LevelKeyPrefix + type, 0) : 0;
                levels[type] = saved;
            }

            coins = useSaveData ? PlayerPrefs.GetInt(CoinKey, startCoins) : startCoins;
            RefreshSummary();
        }

        private void Save()
        {
            RefreshSummary();
            if (!useSaveData) return;

            foreach (var pair in levels)
            {
                PlayerPrefs.SetInt(LevelKeyPrefix + pair.Key, pair.Value);
            }
            PlayerPrefs.SetInt(CoinKey, coins);
            PlayerPrefs.Save();
        }

        private void RefreshSummary()
        {
            levelSummary = $"GaugeSpeed Lv.{GetLevel(UpgradeType.GaugeSpeed)} / " +
                           $"MaxPower Lv.{GetLevel(UpgradeType.MaxPower)} / " +
                           $"Control Lv.{GetLevel(UpgradeType.SeedControl)}";
        }

        [ContextMenu("Reset Save Data")]
        public void ResetSaveData()
        {
            foreach (UpgradeType type in Enum.GetValues(typeof(UpgradeType)))
            {
                PlayerPrefs.DeleteKey(LevelKeyPrefix + type);
            }
            PlayerPrefs.DeleteKey(CoinKey);
            Load();
            OnDataChanged?.Invoke();
        }
    }
}
