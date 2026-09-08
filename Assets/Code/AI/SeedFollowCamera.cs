using UnityEngine;

namespace WatermelonSeed.Launch
{
    public class SeedFollowCamera : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("이동시킬 카메라. 비워두면 Camera.main")]
        public Camera targetCamera;

        [Tooltip("추적 중 비활성화할 마우스 시선 스크립트")]
        public CameraLookController cameraLookController;

        [Header("Follow Offset")]
        public float distanceBehind = 2.2f;
        public float heightAbove = 0.8f;
        public float lookAheadDistance = 3f;

        [Header("Smoothing")]
        [Range(0f, 0.5f)] public float positionSmoothTime = 0.08f;
        public float rotationLerpSpeed = 10f;

        [Tooltip("추적 시작 시 카메라를 즉시 씨앗 뒤로 스냅한다.")]
        public bool snapOnStart = true;

        [Header("FOV")]
        [Tooltip("추적 중 사용할 FOV. 0 이하면 변경하지 않음")]
        public float followFieldOfView = 70f;
        public float fovLerpSpeed = 6f;

        [Header("Return")]
        [Tooltip("착지 후 원래 시점으로 돌아가기까지 대기 시간(초)")]
        public float returnDelay = 1.2f;

        [Tooltip("원래 시점으로 즉시 스냅한다. 해제하면 부드럽게 복귀")]
        public bool instantReturn = true;

        public float returnMoveSpeed = 30f;

        [Header("Look Restore")]
        [Tooltip("복귀 시 발사 전에 보던 시선 각도를 그대로 되살린다.")]
        public bool restorePreviousLookAngles = true;

        public bool IsFollowing { get; private set; }

        /// <summary>
        /// true면 카메라가 원위치로 복귀할 때 시점 조작을 다시 켜지 않는다.
        /// 결과 UI가 열려 있는 동안 카메라가 마우스로 돌아가는 것을 막는 용도.
        /// </summary>
        public bool SuppressLookRestore { get; set; }


        private Transform seedTransform;
        private Vector3 followVelocity;

        private Vector3 savedPosition;
        private Quaternion savedRotation;
        private Vector2 savedLookAngles;
        private float savedFov;
        private bool hasSavedState;

        private float returnTimer;
        private bool waitingToReturn;
        private bool snapNextFrame;
        private Rigidbody seedRigidbody;
        private Vector3 lastKnownForward = Vector3.forward;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
        }

        /// <summary>SeedAttemptManager가 발사 직후 호출</summary>
        public void StartFollow(SeedProjectile projectile)
        {
            if (projectile == null || targetCamera == null) return;

            seedTransform = projectile.transform;
            seedRigidbody = projectile.GetComponent<Rigidbody>();
            lastKnownForward = seedTransform.forward;

            if (!hasSavedState)
            {
                savedPosition = targetCamera.transform.position;
                savedRotation = targetCamera.transform.rotation;
                savedFov = targetCamera.fieldOfView;

                // 발사 직전 시선 각도를 보관
                savedLookAngles = cameraLookController != null
                    ? cameraLookController.CurrentAngles
                    : Vector2.zero;

                hasSavedState = true;
            }

            if (cameraLookController != null) cameraLookController.enabled = false;

            IsFollowing = true;
            waitingToReturn = false;
            returnTimer = 0f;
            followVelocity = Vector3.zero;

            if (snapOnStart)
            {
                snapNextFrame = true;
                ApplyFollowTransform(0f, 0f);
            }
        }

        /// <summary>착지 판정 시 호출. returnDelay 후 원래 시점으로 복귀</summary>
        public void OnSeedFinished()
        {
            if (!IsFollowing) return;
            waitingToReturn = true;
            returnTimer = 0f;
        }

        /// <summary>즉시 추적을 끊고 원래 시점으로 되돌린다.</summary>
        public void StopFollowImmediate()
        {
            IsFollowing = false;
            waitingToReturn = false;
            snapNextFrame = false;
            seedTransform = null;

            if (hasSavedState && targetCamera != null)
            {
                targetCamera.transform.position = savedPosition;
                targetCamera.transform.rotation = savedRotation;
                targetCamera.fieldOfView = savedFov;
            }

            RestoreLookControl();
            hasSavedState = false;
        }

        private void RestoreLookControl()
        {
            if (cameraLookController == null) return;

            // 결과 UI가 떠 있는 동안에는 각도만 되돌리고 조작은 켜지 않는다.
            bool activate = !SuppressLookRestore;

            if (restorePreviousLookAngles)
            {
                Quaternion baseRotation = savedRotation
                    * Quaternion.AngleAxis(-savedLookAngles.y, Vector3.right)
                    * Quaternion.AngleAxis(-savedLookAngles.x, Vector3.up);

                cameraLookController.RestoreAngles(baseRotation, savedLookAngles, activate);
            }
            else
            {
                cameraLookController.RestoreAngles(savedRotation, Vector2.zero, activate);
            }
        }


        private void LateUpdate()
        {
            if (!IsFollowing || targetCamera == null) return;

            if (seedTransform == null)
            {
                StopFollowImmediate();
                return;
            }

            if (waitingToReturn)
            {
                returnTimer += Time.deltaTime;
                if (returnTimer >= returnDelay)
                {
                    if (instantReturn) StopFollowImmediate();
                    else SmoothReturn();
                    return;
                }
            }

            ApplyFollowTransform(positionSmoothTime, rotationLerpSpeed);
        }

        private void ApplyFollowTransform(float smoothTime, float rotSpeed)
        {
            // seedTransform.forward 는 alignToVelocity 와 랜덤 회전 때문에 불안정하다.
            // 실제 이동 방향(속도)을 기준으로 삼아야 카메라가 앞질러 가지 않는다.
            Vector3 forward = lastKnownForward;

            Rigidbody seedBody = seedRigidbody;
            if (seedBody != null)
            {
                Vector3 v = seedBody.linearVelocity;   // 구버전이면 seedBody.velocity
                if (v.sqrMagnitude > 0.25f) forward = v.normalized;
            }
            else if (seedTransform.forward.sqrMagnitude > 0.0001f)
            {
                forward = seedTransform.forward;
            }

            lastKnownForward = forward;

            Vector3 desiredPos = seedTransform.position
                                 - forward * distanceBehind
                                 + Vector3.up * heightAbove;

            Transform cam = targetCamera.transform;

            if (snapNextFrame || smoothTime <= 0.0001f)
            {
                cam.position = desiredPos;
                followVelocity = Vector3.zero;
            }
            else
            {
                cam.position = Vector3.SmoothDamp(cam.position, desiredPos, ref followVelocity, smoothTime);
            }

            // 씨 자체를 바라본다. lookAheadDistance 는 살짝만 주거나 0 으로 둔다.
            Vector3 lookPoint = seedTransform.position + forward * lookAheadDistance;
            Vector3 lookDir = lookPoint - cam.position;

            if (lookDir.sqrMagnitude > 0.0001f)
            {
                Quaternion desiredRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);

                cam.rotation = snapNextFrame
                    ? desiredRot
                    : Quaternion.Slerp(cam.rotation, desiredRot, 1f - Mathf.Exp(-rotSpeed * Time.deltaTime));
            }

            if (followFieldOfView > 0f)
            {
                targetCamera.fieldOfView = snapNextFrame
                    ? followFieldOfView
                    : Mathf.Lerp(targetCamera.fieldOfView, followFieldOfView, fovLerpSpeed * Time.deltaTime);
            }

            snapNextFrame = false;
        }


        private void SmoothReturn()
        {
            Transform cam = targetCamera.transform;

            cam.position = Vector3.MoveTowards(cam.position, savedPosition, returnMoveSpeed * Time.deltaTime);
            cam.rotation = Quaternion.RotateTowards(cam.rotation, savedRotation, 180f * Time.deltaTime);
            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, savedFov, fovLerpSpeed * Time.deltaTime);

            if (Vector3.Distance(cam.position, savedPosition) < 0.05f &&
                Quaternion.Angle(cam.rotation, savedRotation) < 0.5f)
            {
                StopFollowImmediate();
            }
        }
    }
}
