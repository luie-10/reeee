using UnityEngine;

namespace WatermelonSeed.Launch
{
    /// <summary>
    /// 커서 잠금/표시 상태를 한 곳에서만 관리한다.
    /// 다른 스크립트에서 Cursor.lockState / Cursor.visible 을 직접 건드리지 말 것.
    /// </summary>
    public static class CursorState
    {
        public static bool IsGameplayMode { get; private set; }

        /// <summary>조준·발사 중. 커서 잠금 + 숨김.</summary>
        public static void ToGameplay()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            IsGameplayMode = true;
        }

        /// <summary>결과창·상점·일시정지. 커서 해제 + 표시.</summary>
        public static void ToUI()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            IsGameplayMode = false;
        }

        /// <summary>포커스 복귀 시 마지막 상태를 다시 적용.</summary>
        public static void Reapply()
        {
            if (IsGameplayMode) ToGameplay();
            else ToUI();
        }
    }
}
