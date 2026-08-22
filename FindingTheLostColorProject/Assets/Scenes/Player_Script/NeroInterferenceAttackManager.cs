using System.Collections;
using UnityEngine;

/// <summary>
/// 네로의 방해 공격 패턴 컨트롤러.
/// 5초에 한 번씩 네로 말풍선 애니메이션 트리거와 함께 플레이어를 추격하는 경고 신호를 2초간 띄우고,
/// 경고 위치 위에서 아래로 암흑 구슬(NeroDarkOrb)을 소환하여 공격합니다.
/// X좌표가 설정한 기준값 이상일 때만 발동합니다.
/// </summary>
public class NeroInterferenceAttackManager : MonoBehaviour
{
    [Header("1. 말풍선 애니메이션 설정")]
    [Tooltip("네로 말풍선 UI/오브젝트의 Animator")]
    public Animator speechBubbleAnimator;

    [Tooltip("말풍선 애니메이션 트리거 파라미터 이름")]
    public string speechBubbleTriggerName = "19_nero_SpeechBubbleTriger";

    [Tooltip("말풍선 표시 시간 (초)")]
    public float speechBubbleDuration = 2.0f;

    [Header("2. 빨간 경고 신호 (Warning) 설정")]
    [Tooltip("플레이어 위치를 추격할 빨간 경고 표시 오브젝트/프리팹")]
    public GameObject warningIndicatorPrefab;

    [Tooltip("미리 배치된 경고 표시 오브젝트가 있다면 여기에 연결 (없으면 프리팹으로 생성)")]
    public GameObject warningIndicatorInstance;

    [Tooltip("경고 표시가 플레이어를 추적하고 유지되는 시간 (초)")]
    public float warningDuration = 2.0f;

    [Header("3. 암흑 구슬 (Dark Orb) 공격 설정")]
    [Tooltip("위에서 떨어질 암흑 구슬 공격 프리팹 (NeroDarkOrb 스크립트 부착)")]
    public GameObject darkOrbPrefab;

    [Tooltip("경고 위치 기준으로 암흑 구슬이 생겨날 위쪽 Y 오프셋 거리")]
    public float spawnTopOffsetY = 8.0f;

    [Header("4. 주기 설정")]
    [Tooltip("방해 공격이 발생하는 시간 주기 (초 단위, 기본 5초)")]
    public float attackInterval = 5.0f;

    [Tooltip("자동 공격 가동 여부 (기본값: false - 컷씬 이후 스포너와 함께 가동)")]
    public bool isAttacking = false;

    [Header("5. 발동 조건 설정 (X좌표 제한)")]
    [Tooltip("일정 X좌표 이상일 때만 방해 공격이 작동할지 여부")]
    public bool useMinXThreshold = true;

    [Tooltip("공격이 시작되는 최소 플레이어 X좌표 (기본값: 266)")]
    public float startAttackMinX = 266.0f;

    [Header("대상 설정")]
    [Tooltip("추격 대상 플레이어 Transform (비워둘 경우 자동 검색)")]
    public Transform playerTransform;

    private Coroutine attackRoutine;

    void Awake()
    {
        // 씬 시작 시 무조건 방해 공격 루프를 정지 및 비활성화 상태로 시작
        isAttacking = false;
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
    }

    void Start()
    {
        // 플레이어 검색
        if (playerTransform == null)
        {
            PlayerHealth player = FindFirstObjectByType<PlayerHealth>();
            if (player != null) playerTransform = player.transform;
        }

        // 인스턴스가 지정되지 않았고 프리팹이 있다면 미리 하나 생성 후 비활성화
        if (warningIndicatorInstance == null && warningIndicatorPrefab != null)
        {
            warningIndicatorInstance = Instantiate(warningIndicatorPrefab, transform);
            warningIndicatorInstance.SetActive(false);
        }

        // isAttacking이 true일 때만 5초 주기 공격 루프 가동
        if (isAttacking)
        {
            StartAttackLoop();
        }
    }

    /// <summary>
    /// 공격 루프 가동
    /// </summary>
    public void StartAttackLoop()
    {
        if (attackRoutine != null) StopCoroutine(attackRoutine);
        isAttacking = true;
        attackRoutine = StartCoroutine(InterferenceAttackRoutine());
    }

    /// <summary>
    /// 공격 루프 정지
    /// </summary>
    public void StopAttackLoop()
    {
        isAttacking = false;
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        if (warningIndicatorInstance != null)
        {
            warningIndicatorInstance.SetActive(false);
        }
    }

    /// <summary>
    /// 5초 주기 방해 공격 코루틴 메인 시퀀스
    /// </summary>
    private IEnumerator InterferenceAttackRoutine()
    {
        while (isAttacking)
        {
            // 5초 간격 대기
            yield return new WaitForSeconds(attackInterval);

            if (!isAttacking) break;

            // X좌표 발동 조건 검사 (플레이어가 설정된 X좌표 미만이면 공격 안 함)
            if (useMinXThreshold)
            {
                // 플레이어가 검색되지 않았을 경우 다시 찾기 시도
                if (playerTransform == null)
                {
                    PlayerHealth player = FindFirstObjectByType<PlayerHealth>();
                    if (player != null) playerTransform = player.transform;
                }

                if (playerTransform != null && playerTransform.position.x < startAttackMinX)
                {
                    // 아직 플레이어가 기준 X좌표에 도달하지 않았으면 공격을 건너뜀
                    continue;
                }
            }

            // ===== [ 1단계 ] 네로 말풍선 트리거 실행 (2초) =====
            if (speechBubbleAnimator != null && !string.IsNullOrEmpty(speechBubbleTriggerName))
            {
                speechBubbleAnimator.SetTrigger(speechBubbleTriggerName);
            }

            // ===== [ 2단계 ] 플레이어 추격 빨간 경고 신호 가동 (2초) =====
            Vector3 targetAttackPosition = transform.position; // 기본값

            if (warningIndicatorInstance != null)
            {
                warningIndicatorInstance.SetActive(true);

                float elapsed = 0f;
                // 2초 동안 플레이어 위치를 실시간으로 추적
                while (elapsed < warningDuration)
                {
                    elapsed += Time.deltaTime;

                    if (playerTransform != null)
                    {
                        // 경고 지점을 플레이어의 현재 위치(X, Y)로 동기화
                        targetAttackPosition = playerTransform.position;
                        warningIndicatorInstance.transform.position = targetAttackPosition;
                    }

                    yield return null;
                }

                // 2초 경과 후 최종 공격 지점 확정 및 경고 비활성화
                warningIndicatorInstance.SetActive(false);
            }
            else
            {
                // 경고 인스턴스가 없을 경우 2초 대기만 수행
                if (playerTransform != null) targetAttackPosition = playerTransform.position;
                yield return new WaitForSeconds(warningDuration);
            }

            // ===== [ 3단계 ] 확정된 경고 위치 위쪽에서 암흑 구슬 소환 ➔ 하강 공격 =====
            if (darkOrbPrefab != null)
            {
                Vector3 spawnPos = new Vector3(
                    targetAttackPosition.x,
                    targetAttackPosition.y + spawnTopOffsetY,
                    targetAttackPosition.z
                );

                Instantiate(darkOrbPrefab, spawnPos, Quaternion.identity);
                Debug.Log($"[NeroInterferenceAttack] X: {targetAttackPosition.x} 위치로 암흑 구슬 하강 공격 발사!");
            }
        }
    }
}
