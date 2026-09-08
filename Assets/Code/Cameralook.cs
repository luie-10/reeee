using UnityEngine;
using WatermelonSeed.Launch;

public class CameraLookController : MonoBehaviour
{
    [Header("Mouse Settings")]
    [Tooltip("마우스 회전 감도")]
    public float mouseSensitivity = 2.0f;

    [Header("Rotation Limits")]
    [Tooltip("좌우 회전 제한 각도 (-Angle ~ +Angle). 0이면 제한 없음")]
    public float yawLimit = 60f;

    [Tooltip("상하 회전 제한 각도 (-Angle ~ +Angle). 0이면 제한 없음")]
    public float pitchLimit = 45f;

    [Header("Cursor Settings")]
    [Tooltip("시점 조작을 켤 때 커서를 잠글지 여부. 실제 처리는 CursorState가 담당한다.")]
    public bool lockCursor = true;

    private float currentYaw = 0f;
    private float currentPitch = 0f;
    private Quaternion initialRotation;
    private Transform cameraTransform;

    /// <summary>현재 시점 각도 (yaw, pitch)</summary>
    public Vector2 CurrentAngles => new Vector2(currentYaw, currentPitch);

    private void Awake()
    {
        cameraTransform = transform;
        initialRotation = transform.rotation;
    }

    private void Update()
    {
        // 이 스크립트가 enabled일 때만 Update가 돌므로 별도 조건이 필요 없다.
        HandleMouseRotation();
    }

    /// <summary>시점 조작 활성화. 각도를 0으로 초기화한다.</summary>
    public void EnableLookControl(Quaternion startRotation)
    {
        EnableLookControl(startRotation, true);
    }

    /// <summary>시점 조작 활성화. resetAngles가 false면 현재 각도를 유지한다.</summary>
    public void EnableLookControl(Quaternion startRotation, bool resetAngles)
    {
        initialRotation = startRotation;

        if (resetAngles)
        {
            currentYaw = 0f;
            currentPitch = 0f;
        }

        enabled = true;

        if (lockCursor) CursorState.ToGameplay();
    }

    /// <summary>시점 조작 비활성화. releaseCursor가 true면 커서를 풀어 UI를 조작할 수 있게 한다.</summary>
    public void DisableLookControl(bool releaseCursor)
    {
        enabled = false;
        if (releaseCursor) CursorState.ToUI();
    }

    public void DisableLookControl() => DisableLookControl(true);

    /// <summary>
    /// 저장해둔 시점 각도를 되돌린다.
    /// activate가 false면 각도만 복원하고 스크립트는 계속 꺼둔 상태로 남긴다.
    /// (결과 UI가 열려 있는 동안 카메라가 돌지 않게 하기 위함)
    /// </summary>
    public void RestoreAngles(Quaternion baseRotation, Vector2 angles, bool activate = true)
    {
        initialRotation = baseRotation;
        currentYaw = angles.x;
        currentPitch = angles.y;

        // 각도만 복원할 때도 카메라 회전은 즉시 반영해둔다.
        ApplyRotation();

        if (activate)
        {
            enabled = true;
            if (lockCursor) CursorState.ToGameplay();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // 윈도우 키 등으로 포커스를 잃었다 돌아왔을 때 마지막 커서 상태를 다시 적용
        if (hasFocus) CursorState.Reapply();
    }

    private void HandleMouseRotation()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        currentYaw += mouseX;
        currentPitch -= mouseY;

        if (pitchLimit > 0f) currentPitch = Mathf.Clamp(currentPitch, -pitchLimit, pitchLimit);
        if (yawLimit > 0f) currentYaw = Mathf.Clamp(currentYaw, -yawLimit, yawLimit);

        ApplyRotation();
    }

    private void ApplyRotation()
    {
        if (cameraTransform == null) cameraTransform = transform;

        Quaternion yawRotation = Quaternion.AngleAxis(currentYaw, Vector3.up);
        Quaternion pitchRotation = Quaternion.AngleAxis(currentPitch, Vector3.right);

        cameraTransform.rotation = initialRotation * yawRotation * pitchRotation;
    }
}
