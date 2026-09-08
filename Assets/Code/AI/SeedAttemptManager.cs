using System;
using UnityEngine;

namespace WatermelonSeed.Launch
{
    public class SeedAttemptManager : MonoBehaviour
    {
        [Header("References")]
        public SeedLauncher launcher;
        public SeedAimController aimController;
        public CameraLookController cameraLookController;
        public AttemptResultUI resultUI;

        [Tooltip("발사 후 씨앗 시점으로 전환할 카메라 컨트롤러")]
        public SeedFollowCamera followCamera;

        [Header("Rules")]
        [Tooltip("한 라운드의 시도 횟수. 0 이하면 무제한")]
        public int maxAttempts = 3;

        [Tooltip("1미터당 지급 코인")]
        public float coinsPerMeter = 2f;

        [Header("Live Status (Read Only)")]
        [SerializeField] private int attemptCount;
        [SerializeField] private float bestDistance;

        public int AttemptCount => attemptCount;
        public float BestDistance => bestDistance;
        public int RemainingAttempts => maxAttempts > 0 ? Mathf.Max(0, maxAttempts - attemptCount) : -1;

        public event Action<SeedFlightResult> OnAttemptFinished;
        public event Action OnAllAttemptsUsed;

        private void Awake()
        {
            if (resultUI != null) resultUI.HideImmediate();
        }


        private void OnEnable()
        {
            if (launcher != null) launcher.OnSeedLaunched += HandleSeedLaunched;
        }

        private void OnDisable()
        {
            if (launcher != null) launcher.OnSeedLaunched -= HandleSeedLaunched;
        }

        private void HandleSeedLaunched(SeedProjectile seed)
        {
            if (seed == null) return;

            attemptCount++;
            seed.OnFlightFinished += HandleSeedFinished;

            // 씨앗 시점으로 전환
            if (followCamera != null) followCamera.StartFollow(seed);
        }

        private void HandleSeedFinished(SeedProjectile seed, SeedFlightResult result)
        {
            seed.OnFlightFinished -= HandleSeedFinished;

            bestDistance = Mathf.Max(bestDistance, result.distance);

            int reward = Mathf.RoundToInt(result.distance * coinsPerMeter);
            if (UpgradeManager.Instance != null) UpgradeManager.Instance.AddCoins(reward);

            if (aimController != null) aimController.InputLocked = true;

            // 결과 UI를 보는 동안 마우스로 카메라가 돌지 않게 시점 조작을 끈다.
            if (cameraLookController != null) cameraLookController.DisableLookControl(false);

            // 팔로우 카메라가 원위치로 복귀할 때 시점 조작을 다시 켜지 않도록 억제
            if (followCamera != null)
            {
                followCamera.SuppressLookRestore = true;
                followCamera.OnSeedFinished();
            }

            if (resultUI != null)
            {
                // Show 내부에서 CursorState.ToUI()를 호출해 커서를 풀어준다.
                resultUI.Show(result, attemptCount, bestDistance, reward, RemainingAttempts);
            }

            Debug.Log($"[SeedAttemptManager] 시도 {attemptCount} 종료 - " +
                      $"거리 {result.distance:F2}m, 파워 {result.powerStep}칸, 보상 {reward}");

            OnAttemptFinished?.Invoke(result);
        }

        /// <summary>결과 UI의 '다음' 버튼에서 호출</summary>
        public void ContinueNextAttempt()
        {
            if (resultUI != null) resultUI.Hide();

            // 시도 횟수를 모두 썼으면 커서를 UI 상태로 유지한 채 종료 처리
            if (maxAttempts > 0 && attemptCount >= maxAttempts)
            {
                CursorState.ToUI();
                OnAllAttemptsUsed?.Invoke();
                return;
            }

            // 억제를 풀고 카메라를 즉시 원위치로 되돌린다.
            // (returnDelay를 기다리는 중에 눌렸을 수도 있으므로 강제로 마무리)
            if (followCamera != null)
            {
                followCamera.SuppressLookRestore = false;
                followCamera.StopFollowImmediate();   // 내부에서 시점 조작을 다시 켠다
            }
            else if (cameraLookController != null)
            {
                cameraLookController.enabled = true;
            }

            CursorState.ToGameplay();

            if (aimController != null) aimController.InputLocked = false;
        }

        /// <summary>라운드 초기화 (재시작 버튼 등)</summary>
        public void ResetRound()
        {
            attemptCount = 0;
            bestDistance = 0f;

            if (resultUI != null) resultUI.Hide();
            if (aimController != null) aimController.InputLocked = false;

            if (followCamera != null)
            {
                followCamera.SuppressLookRestore = false;
                followCamera.StopFollowImmediate();
            }

            CursorState.ToGameplay();
        }

    }
}
