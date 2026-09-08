using System;
using UnityEngine;

namespace WatermelonSeed.Launch
{
    public struct SeedFlightResult
    {
        public float normalizedPower;
        public int powerStep;
        public float launchSpeed;
        public float flightTime;
        public float distance;
        public float maxHeight;
        public Vector3 landPosition;
        public bool timedOut;
    }

    [RequireComponent(typeof(Rigidbody))]
    public class SeedProjectile : MonoBehaviour
    {
        [Header("Stop Detection (시도 종료 판정)")]
        [Tooltip("이 속도(m/s) 이하로 느려지면 멈춘 것으로 본다.")]
        public float stopSpeedThreshold = 0.6f;

        [Tooltip("위 속도 이하가 이 시간(초)만큼 유지되면 종료 처리")]
        public float stopConfirmTime = 0.4f;

        [Tooltip("발사 직후 오판을 막기 위한 최소 비행 시간(초)")]
        public float minFlightTimeBeforeCheck = 0.3f;

        [Tooltip("무언가에 한 번 닿은 뒤에만 정지 판정을 시작한다.")]
        public bool requireContactBeforeStop = true;

        [Tooltip("어딘가에 끼었을 때를 대비한 최대 비행 시간(초)")]
        public float maxLifeTime = 20f;

        [Header("In-Flight Steering (비행 중 방향키 조종)")]
        [Tooltip("비행 중 방향키로 조종 가능 여부")]
        public bool allowSteering = true;

        [Tooltip("좌우 조종 강도 (초당 회전 각도). SeedControl 강화로 증가")]
        public float steerYawSpeed = 35f;

        [Tooltip("상하 조종 강도 (초당 회전 각도)")]
        public float steerPitchSpeed = 25f;

        [Tooltip("누적 조종 각도 한계(도). 0 이하면 무제한")]
        public float maxTotalSteerAngle = 45f;

        [Tooltip("발사 후 조종이 가능한 시간(초). 0 이하면 착지까지 계속 가능")]
        public float steerDuration = 2.5f;

        [Tooltip("땅에 닿은 뒤에는 조종을 막는다.")]
        public bool disableSteeringAfterContact = true;

        [Tooltip("조종 시 속도 크기를 유지한다. 해제하면 살짝 감속된다.")]
        public bool preserveSpeedWhileSteering = true;

        [Tooltip("preserveSpeed 해제 시 조종 1초당 잃는 속도 비율")]
        [Range(0f, 1f)] public float steerSpeedLossPerSecond = 0.15f;

        [Header("Presentation")]
        [Tooltip("진행 방향으로 씨앗을 회전시킨다.")]
        public bool alignToVelocity = true;

        [Tooltip("종료 후 오브젝트를 파괴하기까지의 지연(초). 음수면 파괴하지 않음")]
        public float destroyDelayAfterFinish = 3f;
        [Header("Fallback Ground Check (충돌이 안 잡힐 때 대비)")]
        [Tooltip("충돌 이벤트 대신 스피어캐스트로 지면 접촉을 판정한다. 터널링 대비용")]
        public bool useRaycastGroundCheck = true;

        [Tooltip("지면으로 판정할 레이어. 기본은 전체")]
        public LayerMask groundMask = ~0;

        [Tooltip("스피어캐스트 반지름. 콜라이더보다 크게 잡는다.")]
        public float groundProbeRadius = 0.08f;

        [Tooltip("이 높이보다 아래로 떨어지면 지면을 통과한 것으로 보고 즉시 종료")]
        public float killHeight = -50f;

        [Tooltip("접지 후 이 값 이상으로 감속을 강제한다. 구르는 구체가 안 멈추는 문제 대비")]
        [Range(0f, 5f)] public float groundedExtraDrag = 1.5f;

        [Tooltip("종료 판정에 수평 속도만 사용한다. 미끄러짐 판정에 유리")]
        public bool useHorizontalSpeedOnly = false;

        [Tooltip("콘솔에 상태를 출력한다.")]
        public bool debugLog = false;

        public bool IsFinished { get; private set; }
        public bool IsLaunched { get; private set; }

        /// <summary>현재 조종 입력을 받을 수 있는 상태인지</summary>
        public bool CanSteer => IsLaunched && !IsFinished && allowSteering &&
                                (steerDuration <= 0f || flightTime <= steerDuration) &&
                                !(disableSteeringAfterContact && contacted) &&
                                (maxTotalSteerAngle <= 0f || usedSteerAngle < maxTotalSteerAngle);

        /// <summary>남은 조종 여력 (0~1). UI 게이지에 쓸 수 있다.</summary>
        public float SteerBudgetRemaining
        {
            get
            {
                if (maxTotalSteerAngle <= 0f) return 1f;
                return Mathf.Clamp01(1f - usedSteerAngle / maxTotalSteerAngle);
            }
        }

        public event Action<SeedProjectile, SeedFlightResult> OnFlightFinished;

        private Rigidbody rb;
        private Vector3 startPosition;
        private float flightTime;
        private float slowTimer;
        private float maxHeight;
        private bool contacted;
        private float normalizedPower;
        private int powerStep;
        private float launchSpeed;
        private float usedSteerAngle;
        private Vector2 steerInput;

        private Vector3 Velocity
        {
            get => rb.linearVelocity;
            set => rb.linearVelocity = value;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        public void Launch(Vector3 velocity, float power01, int step)
        {
            normalizedPower = Mathf.Clamp01(power01);
            powerStep = step;
            launchSpeed = velocity.magnitude;

            startPosition = transform.position;
            flightTime = 0f;
            slowTimer = 0f;
            maxHeight = 0f;
            usedSteerAngle = 0f;
            steerInput = Vector2.zero;
            contacted = false;
            IsFinished = false;
            IsLaunched = true;

            rb.isKinematic = false;
            Velocity = velocity;
            rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 8f;
        }

        private void Update()
        {
            if (!IsLaunched || IsFinished) return;

            // 입력은 Update에서 읽고, 물리 적용은 FixedUpdate에서 처리
            if (allowSteering)
            {
                steerInput = new Vector2(
                    Input.GetAxisRaw("Horizontal"),
                    Input.GetAxisRaw("Vertical"));
            }
        }

        private void FixedUpdate()
        {
            if (!IsLaunched || IsFinished) return;

            float dt = Time.fixedDeltaTime;
            flightTime += dt;
            maxHeight = Mathf.Max(maxHeight, transform.position.y - startPosition.y);

            // 지면을 통과해 무한 낙하하는 경우 즉시 종료
            if (transform.position.y < killHeight)
            {
                if (debugLog) Debug.LogWarning($"[Seed] killHeight({killHeight}) 아래로 떨어져 종료. 지면 콜라이더를 확인하세요.");
                contacted = true;
                Finish(true);
                return;
            }

            // 충돌 이벤트를 놓쳤을 때를 대비한 스피어캐스트 접지 판정
            if (useRaycastGroundCheck && !contacted)
            {
                if (Physics.CheckSphere(transform.position, groundProbeRadius, groundMask,
                                        QueryTriggerInteraction.Ignore))
                {
                    contacted = true;
                    if (debugLog) Debug.Log("[Seed] 스피어캐스트로 접지 감지 (충돌 이벤트 누락)");
                }
            }

            ApplySteering(dt);

            // 접지 후에는 감속을 강제해 구르는 구체가 멈추게 한다
            if (contacted && groundedExtraDrag > 0f)
            {
                rb.linearDamping = Mathf.Max(rb.linearDamping, groundedExtraDrag);
                rb.angularDamping = Mathf.Max(rb.angularDamping, groundedExtraDrag);
            }

            Vector3 velocity = Velocity;

            if (alignToVelocity && velocity.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
            }

            float checkSpeed = useHorizontalSpeedOnly
                ? new Vector2(velocity.x, velocity.z).magnitude
                : velocity.magnitude;

            bool canCheckStop = flightTime >= minFlightTimeBeforeCheck &&
                                (!requireContactBeforeStop || contacted);

            if (canCheckStop && checkSpeed <= stopSpeedThreshold)
            {
                slowTimer += dt;
                if (slowTimer >= stopConfirmTime)
                {
                    Finish(false);
                    return;
                }
            }
            else
            {
                slowTimer = 0f;
            }

            if (debugLog && Time.frameCount % 30 == 0)
            {
                Debug.Log($"[Seed] t={flightTime:F1}s y={transform.position.y:F1} " +
                          $"speed={checkSpeed:F2} contacted={contacted} slowTimer={slowTimer:F2}");
            }

            if (flightTime >= maxLifeTime)
            {
                if (debugLog) Debug.LogWarning("[Seed] maxLifeTime 초과로 종료 (timedOut)");
                Finish(true);
            }
        }


        private void ApplySteering(float dt)
        {
            if (!CanSteer) return;
            if (steerInput.sqrMagnitude < 0.0001f) return;

            Vector3 velocity = Velocity;
            float speed = velocity.magnitude;
            if (speed < 0.5f) return;

            float controlMultiplier = UpgradeManager.Multiplier(UpgradeType.SeedControl);

            float yawDelta = steerInput.x * steerYawSpeed * controlMultiplier * dt;
            float pitchDelta = steerInput.y * steerPitchSpeed * controlMultiplier * dt;

            // 누적 조종량 한계 처리
            float requested = Mathf.Abs(yawDelta) + Mathf.Abs(pitchDelta);
            if (maxTotalSteerAngle > 0f)
            {
                float remaining = maxTotalSteerAngle - usedSteerAngle;
                if (remaining <= 0f) return;

                if (requested > remaining)
                {
                    float scale = remaining / requested;
                    yawDelta *= scale;
                    pitchDelta *= scale;
                    requested = remaining;
                }
            }
            usedSteerAngle += requested;

            Vector3 dir = velocity / speed;

            // 좌우: 월드 Y축 기준 회전
            if (Mathf.Abs(yawDelta) > 0.0001f)
            {
                dir = Quaternion.AngleAxis(yawDelta, Vector3.up) * dir;
            }

            // 상하: 진행 방향의 오른쪽 축 기준 회전
            if (Mathf.Abs(pitchDelta) > 0.0001f)
            {
                Vector3 rightAxis = Vector3.Cross(Vector3.up, dir);
                if (rightAxis.sqrMagnitude > 0.0001f)
                {
                    dir = Quaternion.AngleAxis(-pitchDelta, rightAxis.normalized) * dir;
                }
            }

            if (!preserveSpeedWhileSteering)
            {
                speed *= Mathf.Max(0f, 1f - steerSpeedLossPerSecond * dt);
            }

            Velocity = dir.normalized * speed;
        }

        private void OnCollisionEnter(Collision collision)
        {
            contacted = true;
            if (debugLog) Debug.Log($"[Seed] 충돌: {collision.collider.name}");
        }

        private void OnTriggerEnter(Collider other)
        {
            // 바닥이 Is Trigger로 설정된 경우에도 접지로 인정
            contacted = true;
            if (debugLog) Debug.Log($"[Seed] 트리거 접촉: {other.name}");
        }


        private void Finish(bool timedOut)
        {
            if (IsFinished) return;
            IsFinished = true;

            Vector3 flat = transform.position - startPosition;
            flat.y = 0f;

            SeedFlightResult result = new SeedFlightResult
            {
                normalizedPower = normalizedPower,
                powerStep = powerStep,
                launchSpeed = launchSpeed,
                flightTime = flightTime,
                distance = flat.magnitude,
                maxHeight = maxHeight,
                landPosition = transform.position,
                timedOut = timedOut
            };

            OnFlightFinished?.Invoke(this, result);

            if (destroyDelayAfterFinish >= 0f)
            {
                Destroy(gameObject, destroyDelayAfterFinish);
            }
        }
    }
}
