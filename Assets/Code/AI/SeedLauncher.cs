using System;
using UnityEngine;

namespace WatermelonSeed.Launch
{
    public class SeedLauncher : MonoBehaviour
    {
        [Header("Prefab & Muzzle")]
        public SeedProjectile seedPrefab;

        [Tooltip("씨앗이 생성될 입 위치. 비워두면 조준 레이의 시작점을 사용")]
        public Transform muzzle;

        [Tooltip("카메라/입에 씨앗이 겹치지 않도록 앞으로 밀어내는 거리")]
        public float spawnForwardOffset = 0.5f;

        [Header("Power → Speed")]
        [Tooltip("게이지 1칸 수준일 때의 발사 속도")]
        public float minLaunchSpeed = 10f;

        [Tooltip("게이지 최대일 때의 발사 속도 (강화 배율이 추가로 곱해진다)")]
        public float maxLaunchSpeed = 38f;

        [Tooltip("조준 방향에서 위로 들어 올릴 각도(도). 포물선을 만든다.")]
        public float upwardAngle = 4f;

        [Tooltip("무작위 산포 각도(도). SeedControl 강화로 줄어든다.")]
        public float randomSpreadDegrees = 1.0f;

        public event Action<SeedProjectile> OnSeedLaunched;

        public SeedProjectile Launch(Ray aimRay, float normalizedPower, int powerStep)
        {
            if (seedPrefab == null)
            {
                Debug.LogError("[SeedLauncher] seedPrefab이 비어있습니다.");
                return null;
            }

            float powerMultiplier = UpgradeManager.Multiplier(UpgradeType.MaxPower);
            float speed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, Mathf.Clamp01(normalizedPower)) * powerMultiplier;

            Vector3 dir = aimRay.direction.sqrMagnitude > 0.0001f
                ? aimRay.direction.normalized
                : transform.forward;

            // 위로 살짝 들어 올리기
            if (Mathf.Abs(upwardAngle) > 0.01f)
            {
                Vector3 rightAxis = Vector3.Cross(Vector3.up, dir);
                if (rightAxis.sqrMagnitude > 0.0001f)
                {
                    dir = Quaternion.AngleAxis(-upwardAngle, rightAxis.normalized) * dir;
                }
            }

            // 산포 (조작 강화 레벨이 높을수록 감소)
            float controlMultiplier = UpgradeManager.Multiplier(UpgradeType.SeedControl);
            float spread = randomSpreadDegrees / Mathf.Max(0.01f, controlMultiplier);
            if (spread > 0.001f)
            {
                dir = Quaternion.Euler(
                    UnityEngine.Random.Range(-spread, spread),
                    UnityEngine.Random.Range(-spread, spread),
                    0f) * dir;
                dir.Normalize();
            }

            Vector3 spawnPos = (muzzle != null ? muzzle.position : aimRay.origin) + dir * spawnForwardOffset;

            SeedProjectile seed = Instantiate(seedPrefab, spawnPos, Quaternion.LookRotation(dir, Vector3.up));
            seed.Launch(dir * speed, normalizedPower, powerStep);

            OnSeedLaunched?.Invoke(seed);
            return seed;
        }
    }
}
