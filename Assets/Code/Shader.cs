using UnityEngine;
using System;
using System.Collections;

public class RealTimeSkyboxController : MonoBehaviour
{
    public enum SkyboxMode
    {
        ContinuousRotation, // 연속 회전 모드 (계절에 따른 태양 체류 시간 반영)
        TimeBlockSwap       // 시간대 스카이박스 교체 모드 (계절별 일출/일몰 기준 교체)
    }

    [Header("Mode Selection")]
    public SkyboxMode currentMode = SkyboxMode.TimeBlockSwap;

    [Header("1. Continuous Mode Settings")]
    public Material continuousSkyboxMaterial;
    public float rotationOffset = 0f;

    [Header("2. Time Block Swap Settings")]
    public Material transitionBlendMaterial;
    public float fadeDuration = 2.0f;

    public Cubemap morningCubemap; // 여명 / 아침
    public Cubemap dayCubemap;     // 낮
    public Cubemap eveningCubemap; // 노을 / 저녁
    public Cubemap nightCubemap;   // 밤

    [Header("3. Season Settings (Day/Night Length)")]
    [Tooltip("춘분/추분(06시 일출, 18시 일몰) 대비 여름/겨울의 최대 변동 시간 (단위: 시간)\n예: 1.25 설정 시 여름 일출 04:45 / 겨울 일출 07:15")]
    public float maxSunriseVariationHours = 1.25f;

    [Header("4. Debug / Testing Settings")]
    public bool useDebugTime = false;
    [Range(0f, 24f)]
    public float debugHour = 12f;

    [Tooltip("체크 시 실제 날짜 대신 지정한 월(Month)을 사용합니다.")]
    public bool useDebugDate = false;
    [Range(1, 12)]
    public int debugMonth = 6; // 6 = 여름(낮 길어짐), 12 = 겨울(낮 짧아짐)

    [Header("5. Live Status Monitoring (Read Only)")]
    [SerializeField] private string currentCalculatedDate;
    [SerializeField] private string currentCalculatedTime;
    [SerializeField] private string sunriseTimeStr;
    [SerializeField] private string sunsetTimeStr;
    [SerializeField] private string dayLengthStr; // 계산된 낮 길이
    [SerializeField] private string activeCubemapName = "None";
    [SerializeField] private float currentBlendProgress = 0f;

    [Header("Common Settings")]
    public Light directionalLight;

    [Header("Performance")]
    [Tooltip("크로스페이드 중 DynamicGI.UpdateEnvironment()를 호출할 최소 간격(초). 값이 클수록 성능에 유리하다.")]
    public float giUpdateInterval = 0.1f;

    private Cubemap currentCubemap;
    private Coroutine fadeCoroutine;
    private bool isCrossfading = false;
    private float giUpdateTimer = 0f;

    void Start()
    {
        ValidateSetup();
        InitializeSkybox();
    }

    private void ValidateSetup()
    {
        if (currentMode == SkyboxMode.TimeBlockSwap && transitionBlendMaterial != null)
        {
            if (transitionBlendMaterial.shader == null || transitionBlendMaterial.shader.name != "Skybox/Blend")
            {
                Debug.LogWarning($"[SkyboxController] ⚠️ 머티리얼 셰이더가 'Skybox/Blend'가 아닙니다! (현재: {transitionBlendMaterial.shader?.name})");
            }

            if (morningCubemap == null || dayCubemap == null || eveningCubemap == null || nightCubemap == null)
            {
                Debug.LogWarning("[SkyboxController] ⚠️ morning/day/evening/night Cubemap 중 비어있는 항목이 있습니다. 크로스페이드 중 예외가 발생할 수 있습니다.");
            }
        }
    }

    private void InitializeSkybox()
    {
        if (currentMode == SkyboxMode.TimeBlockSwap && transitionBlendMaterial != null)
        {
            RenderSettings.skybox = transitionBlendMaterial;
        }
    }

    void Update()
    {
        // 1. 현재 날짜 계산 및 계절별 일출/일몰 시간 산출
        DateTime currentDate = GetCurrentDate();
        CalculateSunTimes(currentDate, out float sunriseHour, out float sunsetHour);

        // 2. 현재 시간 가져오기
        TimeSpan currentTime = GetCurrentTimeSpan();
        float currentHourFloat = (float)currentTime.TotalHours;

        // 3. 인스펙터 상태 창 업데이트
        UpdateDebugMonitoring(currentDate, currentTime, sunriseHour, sunsetHour);

        // 4. 선택한 모드로 실행
        switch (currentMode)
        {
            case SkyboxMode.ContinuousRotation:
                UpdateContinuousRotation(currentHourFloat, sunriseHour, sunsetHour);
                break;

            case SkyboxMode.TimeBlockSwap:
                UpdateTimeBlockSwap(currentHourFloat, sunriseHour, sunsetHour);
                break;
        }
    }

    private DateTime GetCurrentDate()
    {
        if (useDebugDate)
        {
            return new DateTime(2026, debugMonth, 15);
        }
        return DateTime.Now;
    }

    private TimeSpan GetCurrentTimeSpan()
    {
        if (useDebugTime)
        {
            float hour = debugHour % 24f;
            return TimeSpan.FromHours(hour);
        }
        return DateTime.Now.TimeOfDay;
    }

    // 계절(날짜)에 따른 일출 및 일몰 시간 계산
    private void CalculateSunTimes(DateTime date, out float sunriseHour, out float sunsetHour)
    {
        int dayOfYear = date.DayOfYear;

        // 춘분(약 80일째)을 기준(0)으로 여름 하지(피크 +1), 겨울 동지(피크 -1) 사인파 계산
        float seasonFactor = Mathf.Sin(((dayOfYear - 80f) / 365f) * Mathf.PI * 2f);

        // 기본 기준: 일출 06:00, 일몰 18:00
        sunriseHour = 6.0f - (maxSunriseVariationHours * seasonFactor);
        sunsetHour = 18.0f + (maxSunriseVariationHours * seasonFactor);
    }

    private void UpdateContinuousRotation(float currentHour, float sunriseHour, float sunsetHour)
    {
        if (continuousSkyboxMaterial == null) return;

        if (RenderSettings.skybox != continuousSkyboxMaterial)
        {
            RenderSettings.skybox = continuousSkyboxMaterial;
        }

        // 스카이박스 Y축 회전
        float dayPercent = currentHour / 24.0f;
        float rotationAngle = (dayPercent * 360f + rotationOffset) % 360f;

        if (continuousSkyboxMaterial.HasProperty("_Rotation"))
        {
            continuousSkyboxMaterial.SetFloat("_Rotation", rotationAngle);
        }

        // 계절별 낮/밤 길이에 맞춘 비선형 태양 각도 계산
        if (directionalLight != null)
        {
            float sunAngle = CalculateSunAngleWithSeasons(currentHour, sunriseHour, sunsetHour);
            directionalLight.transform.rotation = Quaternion.Euler(sunAngle, 170f, 0f);
        }
    }

    private void UpdateTimeBlockSwap(float currentHour, float sunriseHour, float sunsetHour)
    {
        if (transitionBlendMaterial == null) return;

        if (RenderSettings.skybox != transitionBlendMaterial)
        {
            RenderSettings.skybox = transitionBlendMaterial;
        }

        Cubemap targetCubemap = GetSeasonalTargetCubemap(currentHour, sunriseHour, sunsetHour);

        if (targetCubemap != null && targetCubemap != currentCubemap && !isCrossfading)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(CrossfadeSkybox(targetCubemap));
        }

        if (directionalLight != null)
        {
            float sunAngle = CalculateSunAngleWithSeasons(currentHour, sunriseHour, sunsetHour);
            directionalLight.transform.rotation = Quaternion.Euler(sunAngle, 170f, 0f);
        }
    }

    // 계절별 낮/밤 비율에 맞춰 태양 각도(0~360도) 매핑
    private float CalculateSunAngleWithSeasons(float currentHour, float sunriseHour, float sunsetHour)
    {
        if (currentHour >= sunriseHour && currentHour < sunsetHour)
        {
            // 낮 시간대: 일출 시점(0도) ~ 일몰 시점(180도)
            float dayProgress = (currentHour - sunriseHour) / (sunsetHour - sunriseHour);
            return dayProgress * 180f;
        }
        else
        {
            // 밤 시간대: 일몰 시점(180도) ~ 다음날 일출 시점(360도)
            float nightDuration = (24f - sunsetHour) + sunriseHour;
            float nightProgress;

            if (currentHour >= sunsetHour)
            {
                nightProgress = (currentHour - sunsetHour) / nightDuration;
            }
            else
            {
                nightProgress = ((24f - sunsetHour) + currentHour) / nightDuration;
            }

            return 180f + (nightProgress * 180f);
        }
    }

    // 일출/일몰 기준 스카이박스 구역 분할
    private Cubemap GetSeasonalTargetCubemap(float currentHour, float sunriseHour, float sunsetHour)
    {
        float morningStart = sunriseHour - 1.0f; // 일출 1시간 전부터
        float morningEnd = sunriseHour + 2.0f;   // 일출 2시간 후까지 (아침)

        float eveningStart = sunsetHour - 2.0f;  // 일몰 2시간 전부터 (저녁/노을)
        float eveningEnd = sunsetHour + 1.0f;    // 일몰 1시간 후까지

        if (currentHour >= morningStart && currentHour < morningEnd)
        {
            return morningCubemap;
        }
        else if (currentHour >= morningEnd && currentHour < eveningStart)
        {
            return dayCubemap;
        }
        else if (currentHour >= eveningStart && currentHour < eveningEnd)
        {
            return eveningCubemap;
        }
        else
        {
            return nightCubemap;
        }
    }

    private IEnumerator CrossfadeSkybox(Cubemap nextCubemap)
    {
        if (nextCubemap == null)
        {
            Debug.LogWarning("[SkyboxController] 대상 Cubemap이 비어있어 크로스페이드를 건너뜁니다.");
            yield break;
        }

        isCrossfading = true;

        if (currentCubemap == null)
        {
            currentCubemap = nextCubemap;
            activeCubemapName = currentCubemap.name;
            transitionBlendMaterial.SetTexture("_Tex1", currentCubemap);
            transitionBlendMaterial.SetTexture("_Tex2", nextCubemap);
            transitionBlendMaterial.SetFloat("_Blend", 0f);
            DynamicGI.UpdateEnvironment();
            isCrossfading = false;
            yield break;
        }

        transitionBlendMaterial.SetTexture("_Tex1", currentCubemap);
        transitionBlendMaterial.SetTexture("_Tex2", nextCubemap);

        float timer = 0f;
        giUpdateTimer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            giUpdateTimer += Time.deltaTime;
            currentBlendProgress = Mathf.Clamp01(timer / fadeDuration);

            transitionBlendMaterial.SetFloat("_Blend", currentBlendProgress);

            // 매 프레임 갱신 대신 giUpdateInterval 간격으로만 호출해 비용을 줄인다.
            if (giUpdateTimer >= giUpdateInterval)
            {
                DynamicGI.UpdateEnvironment();
                giUpdateTimer = 0f;
            }

            yield return null;
        }

        currentCubemap = nextCubemap;
        activeCubemapName = currentCubemap.name;
        transitionBlendMaterial.SetTexture("_Tex1", currentCubemap);
        transitionBlendMaterial.SetFloat("_Blend", 0f);
        currentBlendProgress = 0f;
        DynamicGI.UpdateEnvironment();

        isCrossfading = false;
    }

    private void UpdateDebugMonitoring(DateTime date, TimeSpan time, float sunrise, float sunset)
    {
        currentCalculatedDate = $"{date.Year}-{date.Month:D2}-{date.Day:D2} ({date.Month}월)";
        currentCalculatedTime = $"{Mathf.FloorToInt((float)time.TotalHours):D2}:{time.Minutes:D2}";

        sunriseTimeStr = FormatHourToTimeString(sunrise);
        sunsetTimeStr = FormatHourToTimeString(sunset);

        float dayLength = sunset - sunrise;
        int hours = Mathf.FloorToInt(dayLength);
        int minutes = Mathf.FloorToInt((dayLength - hours) * 60f);
        dayLengthStr = $"{hours}시간 {minutes}분";
    }

    private string FormatHourToTimeString(float hourFloat)
    {
        int h = Mathf.FloorToInt(hourFloat) % 24;
        if (h < 0) h += 24; // 음수 시간(극단적인 maxSunriseVariationHours 설정 시) 보정
        int m = Mathf.FloorToInt((hourFloat - Mathf.Floor(hourFloat)) * 60f);
        return $"{h:D2}:{m:D2}";
    }
}
