using System.Collections.Generic;
using UnityEngine;

namespace WatermelonSeed.Launch
{
    public enum UpgradeType
    {
        GaugeSpeed,   // 게이지가 차오르는 속도
        MaxPower,     // 발사 파워(속도) 배율
        SeedControl   // 발사 방향 흔들림 감소
    }

    [CreateAssetMenu(menuName = "WatermelonSeed/Upgrade Table", fileName = "UpgradeTable")]
    public class UpgradeTable : ScriptableObject
    {
        [System.Serializable]
        public class UpgradeEntry
        {
            public UpgradeType type;

            [Tooltip("최대 강화 레벨")]
            public int maxLevel = 5;

            [Tooltip("레벨 1당 증가하는 배율 (0.15 = 레벨당 +15%)")]
            public float valuePerLevel = 0.15f;

            [Tooltip("0레벨 → 1레벨 강화 비용")]
            public int baseCost = 100;

            [Tooltip("레벨이 오를 때마다 비용에 곱해지는 값")]
            public float costGrowth = 1.6f;

            public float GetMultiplier(int level)
            {
                return 1f + valuePerLevel * Mathf.Max(0, level);
            }

            public int GetCost(int currentLevel)
            {
                return Mathf.RoundToInt(baseCost * Mathf.Pow(costGrowth, Mathf.Max(0, currentLevel)));
            }
        }

        public List<UpgradeEntry> entries = new List<UpgradeEntry>();

        public UpgradeEntry Get(UpgradeType type)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].type == type) return entries[i];
            }
            return null;
        }
    }
}
