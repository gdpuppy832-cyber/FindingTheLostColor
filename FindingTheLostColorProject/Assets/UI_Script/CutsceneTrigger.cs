using UnityEngine;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// 콜라이더 영역 접촉 시 대화를 발동시키는 트리거 스크립트.
/// - 컷씬 진행 동안 플레이어의 이동 및 대쉬 입력을 차단하고 고정합니다.
/// - 대사가 모두 완료되면 지정 타일맵 소멸, 스포너 가동, 1.5초간 페이드 및 순간이동(Teleport) 후 조작을 복구합니다.
/// </summary>
public class CutsceneTrigger : MonoBehaviour
{
    [Header("대화 내용")]
    [Tooltip("영역 진입 시 순서대로 말할 대사 목록. 각 요소마다 캐릭터 이름, 이미지, 입 모양 프레임, 텍스트 등을 지정합니다.")]
    public DialogueLine[] dialogues;

    [Header("트리거 설정")]
    [Tooltip("감지할 플레이어 태그 (기본값: Player)")]
    public string playerTag = "Player";

    [Tooltip("한 번 대화가 나온 뒤, 다시 영역에 들어가도 중복 재생을 막을지 여부")]
    public bool triggerOnlyOnce = true;

    [Header("컷씬 및 페이드 연출 설정")]
    [Tooltip("대화 진행 중 플레이어 이동/대쉬 조작을 고정 및 차단할지 여부 (기본값: true)")]
    public bool freezePlayerDuringCutscene = true;

    [Tooltip("대화 종료 후 페이드 연출 전체 소요 시간 (초 단위, 기본값: 1.5s)")]
    public float fadeDurationOnEnd = 1.5f;

    [Header("순간이동 연출 설정 (Teleport Settings)")]
    [Tooltip("페이드가 완전히 어두워진 순간 지정한 위치로 순간이동할지 여부")]
    public bool useTeleportOnFade = true;

    [Tooltip("순간이동할 목표 위치 Transform (빈 오브젝트나 스폰 포인트를 연결)")]
    public Transform teleportDestination;

    [Tooltip("teleportDestination이 비어있을 때 사용할 직접 지정 Vector3 좌표")]
    public Vector3 customTeleportPosition;

    [Header("컷씬 완료 연계 설정 (타일맵 소멸 & 스포너 가동)")]
    [Tooltip("마지막 대사 완료 직후 소멸(비활성화)시킬 타일맵 또는 바닥 게임오브젝트 (선택사항)")]
    public GameObject targetTilemapObj;

    [Tooltip("마지막 대사 완료 직후 충돌을 비활성화할 타일맵 콜라이더 (선택사항)")]
    public Collider2D targetTilemapCollider;

    [Tooltip("컷씬이 종료된 후 가동을 시작할 RandomRangeSpawner (비워두면 씬에서 자동 탐지)")]
    public RandomRangeSpawner spawnerToActivate;

    [Header("이벤트 설정")]
    [Tooltip("대화 및 페이드 연출이 모두 끝났을 때 발동할 이벤트 (필요 시 연결)")]
    public UnityEvent onDialogueEnded;

    private bool hasTriggered = false;
    private PlayerMove cachedPlayerMove;
    private CursorController cachedCursorController;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (triggerOnlyOnce && hasTriggered) return;

        // 플레이어 판정: 태그 비교 및 PlayerMove 컴포넌트 이중 교차 검사로 100% 감지 보장
        bool isPlayer = collision.CompareTag(playerTag) ||
                        collision.transform.root.CompareTag(playerTag) ||
                        collision.GetComponentInParent<PlayerMove>() != null ||
                        collision.GetComponentInChildren<PlayerMove>() != null ||
                        (collision.attachedRigidbody != null && collision.attachedRigidbody.CompareTag(playerTag));

        if (isPlayer)
        {
            hasTriggered = true;
            Debug.Log($"[CutsceneTrigger] {gameObject.name} 플레이어 영역 접촉 감지! 조작을 고정하고 컷씬을 시작합니다.");

            // 1. 플레이어 이동/대쉬/점프 조작 차단 & 위치 고정
            cachedPlayerMove = collision.GetComponentInParent<PlayerMove>();
            if (cachedPlayerMove == null && collision.attachedRigidbody != null)
            {
                cachedPlayerMove = collision.attachedRigidbody.GetComponent<PlayerMove>();
            }
            if (cachedPlayerMove == null)
            {
                cachedPlayerMove = collision.GetComponentInChildren<PlayerMove>();
            }
            if (cachedPlayerMove == null)
            {
                cachedPlayerMove = FindFirstObjectByType<PlayerMove>();
            }

            if (freezePlayerDuringCutscene && cachedPlayerMove != null)
            {
                cachedPlayerMove.SetControl(false);
            }

            // 마우스 공격/붓칠 기능 차단
            cachedCursorController = FindFirstObjectByType<CursorController>();
            if (cachedCursorController != null)
            {
                cachedCursorController.enabled = false;
            }

            // 2. DialogueManager를 통해 대화 시작 (만약 DialogueManager가 없더라도 연출은 멈추지 않고 바로 진행)
            DialogueManager dm = DialogueManager.Instance;
            if (dialogues != null && dialogues.Length > 0 && dm != null)
            {
                dm.StartDialogue(dialogues, HandleDialogueEnded);
            }
            else
            {
                HandleDialogueEnded();
            }
        }
    }

    private void HandleDialogueEnded()
    {
        Debug.Log($"[CutsceneTrigger] {gameObject.name} 대화 완료! 타일맵 소멸, 스포너 가동 및 페이드/순간이동을 개시합니다.");

        // 1. 타일맵/바닥 오브젝트 및 콜라이더 소멸 (비활성화)
        if (targetTilemapObj != null)
        {
            targetTilemapObj.SetActive(false);
        }
        if (targetTilemapCollider != null)
        {
            targetTilemapCollider.enabled = false;
        }

        // 2. 컷씬 대사 종료 후 RandomRangeSpawner 및 NeroInterferenceAttackManager 가동 시작
        if (spawnerToActivate != null)
        {
            spawnerToActivate.StartSpawning();
        }
        else
        {
            RandomRangeSpawner spawner = FindFirstObjectByType<RandomRangeSpawner>();
            if (spawner != null)
            {
                spawner.StartSpawning();
            }
        }

        NeroInterferenceAttackManager nero = FindFirstObjectByType<NeroInterferenceAttackManager>();
        if (nero != null)
        {
            nero.StartAttackLoop();
        }

        // 3. 1.5초 페이드 & 순간이동 시퀀스 코루틴 실행
        StartCoroutine(CutsceneEndSequence());
    }

    private IEnumerator CutsceneEndSequence()
    {
        float halfDuration = (fadeDurationOnEnd > 0f) ? fadeDurationOnEnd * 0.5f : 0.75f;

        // 1단계: 화면이 어두워짐 (Fade Out)
        if (ScreenFader.Instance != null && fadeDurationOnEnd > 0f)
        {
            yield return StartCoroutine(ScreenFader.Instance.FadeOutOnly(halfDuration));
        }
        else if (fadeDurationOnEnd > 0f)
        {
            yield return new WaitForSeconds(halfDuration);
        }

        // 2단계: 화면이 완전히 어두워진 찰나의 순간 지정한 위치로 순간이동(Teleport)!
        PerformTeleport();

        // 3단계: 화면이 다시 밝아짐 (Fade In)
        if (ScreenFader.Instance != null && fadeDurationOnEnd > 0f)
        {
            yield return StartCoroutine(ScreenFader.Instance.FadeInOnly(halfDuration));
        }
        else if (fadeDurationOnEnd > 0f)
        {
            yield return new WaitForSeconds(halfDuration);
        }

        // 4단계: 플레이어 이동 및 대쉬 조작 정상 복구
        if (freezePlayerDuringCutscene && cachedPlayerMove != null)
        {
            cachedPlayerMove.SetControl(true);
        }

        // 마우스 공격/붓칠 기능 정상 복구
        if (cachedCursorController != null)
        {
            cachedCursorController.enabled = true;
        }

        // 5단계: 대화 및 페이드 완료 이벤트 호출
        if (onDialogueEnded != null)
        {
            onDialogueEnded.Invoke();
        }
    }

    /// <summary>
    /// 화면이 어두워졌을 때 플레이어 위치를 지정된 목표 지점으로 이동시킵니다.
    /// </summary>
    private void PerformTeleport()
    {
        if (!useTeleportOnFade) return;

        Transform targetPlayer = (cachedPlayerMove != null) ? cachedPlayerMove.transform : null;
        if (targetPlayer == null)
        {
            GameObject pObj = GameObject.FindWithTag(playerTag);
            if (pObj != null) targetPlayer = pObj.transform;
            if (targetPlayer == null)
            {
                PlayerMove pm = FindFirstObjectByType<PlayerMove>();
                if (pm != null) targetPlayer = pm.transform;
            }
        }

        if (targetPlayer != null)
        {
            Vector3 destPosition = targetPlayer.position;
            bool hasValidDest = false;

            if (teleportDestination != null)
            {
                destPosition = teleportDestination.position;
                hasValidDest = true;
            }
            else if (customTeleportPosition != Vector3.zero)
            {
                destPosition = customTeleportPosition;
                hasValidDest = true;
            }

            if (hasValidDest)
            {
                // 위치 이동
                targetPlayer.position = destPosition;

                // 물리 속도 즉시 동결 (순간이동 후 관성 튕김 방지)
                Rigidbody2D rb = targetPlayer.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }

                Debug.Log($"[CutsceneTrigger] 페이드 암전 순간 {targetPlayer.name}을(를) {destPosition} 위치로 순간이동 완료!");
            }
        }
    }
}
