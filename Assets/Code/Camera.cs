using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WatermelonSeed.Launch;

public class CameraPreviewController : MonoBehaviour
{
    [System.Serializable]
    public struct CameraWaypoint
    {
        [Tooltip("카메라가 위치할 좌표 및 기본 회전값")]
        public Transform targetTransform;

        [Tooltip("이동하는 동안 바라볼 시선 대상 (비워두면 TargetTransform의 Rotation 사용)")]
        public Transform lookAtTarget;

        [Tooltip("해당 위치에 도착했을 때 멈춰서 대기할 시간 (초)")]
        public float holdTime;
    }

    [Header("Main Camera & Waypoints")]
    public Camera mainCamera;
    public List<CameraWaypoint> waypoints = new List<CameraWaypoint>();

    [Header("Speed Settings")]
    public float moveSpeed = 10f;
    public float rotationSpeed = 60f;

    [Header("Loop Settings")]
    [Tooltip("마지막 웨이포인트에서 첫 웨이포인트로 되돌아갈 때 즉시 스냅할지 여부")]
    public bool instantReturnToFirstWaypoint = true;

    [Header("Game Start Target")]
    public Transform gameStartTransform;

    [Tooltip("게임 시작 신호를 받으면 서서히 이동하지 않고 곧바로 스냅한다.")]
    public bool instantGameStartTransition = true;

    [Header("Role Separation (역할 분리)")]
    [Tooltip("메인 위치 도착 후 활성화할 마우스 회전 스크립트")]
    public CameraLookController cameraLookController;

    [Tooltip("게임 시작 시 활성화할 플레이어 조작 스크립트들 (SeedAimController, PowerGaugeController 등)")]
    public List<MonoBehaviour> playerControlScripts = new List<MonoBehaviour>();

    [Header("Start UI")]
    [Tooltip("게임 시작 신호를 받으면 즉시 비활성화할 UI 오브젝트 목록")]
    public List<GameObject> startUIObjects = new List<GameObject>();

    [Header("Crosshair")]
    [Tooltip("게임 시작 시 함께 활성화할 조준경(크로스헤어) 컨트롤러")]
    public CrosshairFollowController crosshairController;

    private bool isGameStarted = false;
    private Coroutine previewCoroutine;

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[CameraPreviewController] mainCamera를 찾을 수 없습니다.");
            return;
        }

        if (cameraLookController != null) cameraLookController.enabled = false;

        // 게임 시작 전에는 플레이어 조작 스크립트들을 모두 비활성화
        foreach (var script in playerControlScripts)
        {
            if (script != null) script.enabled = false;
        }

        // 조준경도 게임 시작 전에는 숨겨둔다.
        if (crosshairController != null)
        {
            crosshairController.Hide();
        }

        if (!ValidateWaypoints()) return;

        if (waypoints.Count > 0)
        {
            previewCoroutine = StartCoroutine(PlayInfiniteMapPreviewSequence());
        }
    }

    private bool ValidateWaypoints()
    {
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i].targetTransform == null)
            {
                Debug.LogError($"[CameraPreviewController] waypoints[{i}].targetTransform이 비어있습니다.");
                return false;
            }
        }
        return true;
    }

    public void StartGame()
    {
        if (isGameStarted) return;

        if (gameStartTransform == null)
        {
            Debug.LogError("[CameraPreviewController] gameStartTransform이 비어있어 게임 시작 전환을 진행할 수 없습니다.");
            return;
        }

        isGameStarted = true;
        Debug.Log("게임 시작 신호 수신!");

        StopAllCoroutines();
        previewCoroutine = null;

        DisableStartUI();

        if (instantGameStartTransition)
        {
            mainCamera.transform.position = gameStartTransform.position;
            mainCamera.transform.rotation = gameStartTransform.rotation;
            OnPreviewComplete();
        }
        else
        {
            StartCoroutine(TransitionToGameStartConstantSpeed());
        }
    }

    private void DisableStartUI()
    {
        for (int i = 0; i < startUIObjects.Count; i++)
        {
            if (startUIObjects[i] != null) startUIObjects[i].SetActive(false);
        }
    }

    private IEnumerator PlayInfiniteMapPreviewSequence()
    {
        mainCamera.transform.position = waypoints[0].targetTransform.position;
        mainCamera.transform.rotation = waypoints[0].targetTransform.rotation;

        int currentIndex = 0;

        while (!isGameStarted)
        {
            CameraWaypoint currentWaypoint = waypoints[currentIndex];
            int nextIndex = (currentIndex + 1) % waypoints.Count;
            CameraWaypoint nextWaypoint = waypoints[nextIndex];

            float holdTimer = 0f;
            while (holdTimer < currentWaypoint.holdTime)
            {
                if (isGameStarted) yield break;
                holdTimer += Time.deltaTime;

                if (currentWaypoint.lookAtTarget != null)
                {
                    Vector3 targetDir = currentWaypoint.lookAtTarget.position - mainCamera.transform.position;
                    if (targetDir != Vector3.zero)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(targetDir);
                        mainCamera.transform.rotation = Quaternion.RotateTowards(mainCamera.transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                    }
                }
                yield return null;
            }

            bool isReturningToStart = (nextIndex == 0);
            if (isReturningToStart && instantReturnToFirstWaypoint)
            {
                mainCamera.transform.position = nextWaypoint.targetTransform.position;
                mainCamera.transform.rotation = nextWaypoint.targetTransform.rotation;
            }
            else
            {
                yield return StartCoroutine(MoveAndRotateConstantSpeed(nextWaypoint));
            }

            currentIndex = nextIndex;
        }
    }

    private IEnumerator MoveAndRotateConstantSpeed(CameraWaypoint destination)
    {
        while (!isGameStarted)
        {
            Vector3 targetPos = destination.targetTransform.position;
            Quaternion targetRot;

            if (destination.lookAtTarget != null)
            {
                Vector3 lookDir = destination.lookAtTarget.position - mainCamera.transform.position;
                targetRot = lookDir != Vector3.zero ? Quaternion.LookRotation(lookDir) : destination.targetTransform.rotation;
            }
            else
            {
                targetRot = destination.targetTransform.rotation;
            }

            mainCamera.transform.position = Vector3.MoveTowards(mainCamera.transform.position, targetPos, moveSpeed * Time.deltaTime);
            mainCamera.transform.rotation = Quaternion.RotateTowards(mainCamera.transform.rotation, targetRot, rotationSpeed * Time.deltaTime);

            float distanceLeft = Vector3.Distance(mainCamera.transform.position, targetPos);
            float angleLeft = Quaternion.Angle(mainCamera.transform.rotation, targetRot);

            if (distanceLeft < 0.01f && angleLeft < 0.1f)
            {
                mainCamera.transform.position = targetPos;
                mainCamera.transform.rotation = targetRot;
                break;
            }
            yield return null;
        }
    }

    private IEnumerator TransitionToGameStartConstantSpeed()
    {
        while (true)
        {
            Vector3 targetPos = gameStartTransform.position;
            Quaternion targetRot = gameStartTransform.rotation;

            mainCamera.transform.position = Vector3.MoveTowards(mainCamera.transform.position, targetPos, moveSpeed * 1.5f * Time.deltaTime);
            mainCamera.transform.rotation = Quaternion.RotateTowards(mainCamera.transform.rotation, targetRot, rotationSpeed * 1.5f * Time.deltaTime);

            float distanceLeft = Vector3.Distance(mainCamera.transform.position, targetPos);
            float angleLeft = Quaternion.Angle(mainCamera.transform.rotation, targetRot);

            if (distanceLeft < 0.01f && angleLeft < 0.1f)
            {
                mainCamera.transform.position = targetPos;
                mainCamera.transform.rotation = targetRot;
                break;
            }
            yield return null;
        }

        OnPreviewComplete();
    }

    private void OnPreviewComplete()
    {
        Debug.Log("메인 위치 이동 완료! 플레이어 조작을 활성화합니다.");

        if (cameraLookController != null)
        {
            cameraLookController.EnableLookControl(gameStartTransform.rotation);
        }

        // 게임 시작 시 필요한 모든 조작 스크립트를 한꺼번에 활성화
        foreach (var script in playerControlScripts)
        {
            if (script != null) script.enabled = true;
        }
        CursorState.ToGameplay();
        // 시작 UI가 사라지는 것과 동시에 조준경을 표시
        if (crosshairController != null)
        {
            crosshairController.Show();
        }
    }
}
