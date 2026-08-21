using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class CutsceneDialogueManager : MonoBehaviour
{
    public static CutsceneDialogueManager Instance { get; private set; }

    [System.Serializable]
    public class DialogueData
    {
        [Header("대사 정보")]
        public string speakerName;

        [Multiline(3)]
        public string sentence;

        public Color speakerColor = Color.white;

        [Header("애니메이션 설정 (통짜 애니메이션 1개)")]
        [Tooltip("대화 말할 때 재생되는 통짜 애니메이션 State 이름 (예: 00_Player_Profile_IDLE_T_0000)")]
        public string talkingAnimState;

        [Tooltip("말을 안 할 때(대사 완료 후 2초 대기) 표시할 정지 프레임/State 이름 (예: 00_Player_Profile_IDLE_M_0000)")]
        public string idleAnimState;

        [Tooltip("정적 스프라이트 이미지 (옵션)")]
        public Sprite dialogueSprite;
    }

    [Header("UI 컴포넌트 연결")]
    [Tooltip("하단 대화창 부모 패널 (통짜 대화창 오브젝트 / Animator 부착 오브젝트)")]
    public GameObject dialoguePanelObj;

    [Tooltip("대화창 등장/퇴장 애니메이터 (비워둘 시 dialoguePanelObj의 Animator 자동 캐싱)")]
    public Animator dialogueAnimator;

    [Header("통짜 대화창 스왑 설정 (플레이어/NPC 색상 및 프레임 교체)")]
    [Tooltip("통짜 대화창 Image 컴포넌트 (비워둘 시 dialoguePanelObj의 Image 자동 캐싱)")]
    public Image dialogueFrameImage;

    [Header("프로필 / 초상화 UI (옵션: 쪼개진 뷰일 경우 사용)")]
    [Tooltip("왼쪽에 분리 배치된 프로필/스파인 Image 컴포넌트 (통짜 프레임 사용 시 비워둠)")]
    public Image portraitImage;

    [Tooltip("왼쪽 프로필/스파인 애니메이터 (통짜 프레임 사용 시 비워둠)")]
    public Animator portraitAnimator;

    [Header("텍스트 UI 연결")]
    [Tooltip("화자 이름을 출력할 TextMeshProUGUI (또는 UI Text)")]
    public TextMeshProUGUI speakerTmpText;
    public Text speakerLegacyText;

    [Tooltip("대사 내용을 출력할 TextMeshProUGUI (또는 UI Text)")]
    public TextMeshProUGUI sentenceTmpText;
    public Text sentenceLegacyText;

    [Tooltip("스페이스바 진행 안내 텍스트/아이콘 (옵션)")]
    public GameObject spacePromptObj;

    [Header("대사 유지 및 자동 진행 설정")]
    [Tooltip("한 문장이 완성된 후 다음 대사로 자동 진행되기까지 대기시간 (초 단위, 기본값: 2.0초)")]
    public float autoAdvanceDelay = 2.0f;

    [Header("3번 기능: 8번 '아.' 대사 시 충돌 제거할 타일맵/바닥")]
    [Tooltip("8번 '아.' 대사 출력 시 충돌 판정을 비활성화할 타일맵 콜라이더")]
    public Collider2D targetTilemapCollider;

    [Tooltip("8번 '아.' 대사 출력 시 비활성화할 바닥 게임오브젝트 (선택사항)")]
    public GameObject targetFloorObj;

    [Header("4번 기능: 컷씬 종료 페이드 아웃 설정")]
    [Tooltip("컷씬 종료 후 화면이 까맣게 꺼지는 페이드아웃 시간 (초 단위, 기본값: 2.0초)")]
    public float fadeOutDurationOnEnd = 2.0f;

    [Header("인스펙터 직접 입력용 대사 리스트 (코드 없이 사용할 때)")]
    [Tooltip("에디터 인스펙터 창에서 직접 타이핑하여 등록하는 대사 목록")]
    public List<DialogueData> inspectorDialogues = new List<DialogueData>();

    [Header("타이핑 연출 설정")]
    [Tooltip("한 글자씩 출력되는 타이핑 속도 (초 단위, 0이면 즉시 전체 출력)")]
    public float typingSpeed = 0.03f;

    [Header("애니메이터 파라미터 이름")]
    public string openTriggerName = "Open";
    public string closeTriggerName = "Close";

    private List<DialogueData> currentDialogueList = new List<DialogueData>();
    private int currentIndex = 0;
    private bool isDialogueActive = false;
    private bool isTyping = false;
    private bool isWaitingForAutoAdvance = false;

    private Coroutine typingCoroutine;
    private Coroutine autoAdvanceCoroutine;
    private System.Action onDialogueComplete;
    private float originalPlayerGravity = -1f;
    private float originalTimeScale = 1.0f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (dialoguePanelObj != null && dialogueAnimator == null)
        {
            dialogueAnimator = dialoguePanelObj.GetComponent<Animator>();
        }

        if (portraitImage != null && portraitAnimator == null)
        {
            portraitAnimator = portraitImage.GetComponent<Animator>();
        }

        // 인스펙터 대사 리스트가 비어 있는 경우 1~8번 대사 및 애니메이션 State 자동 내장
        if (inspectorDialogues == null || inspectorDialogues.Count == 0)
        {
            InitDefaultDialogues();
        }

        // 시작 시 대화창 비활성화
        if (dialoguePanelObj != null)
        {
            dialoguePanelObj.SetActive(false);
        }
    }

    /// <summary>
    /// 제시받은 1번~8번 대사 및 애니메이션 State 기본 내장 프리셋
    /// </summary>
    private void InitDefaultDialogues()
    {
        inspectorDialogues = new List<DialogueData>()
        {
            new DialogueData { speakerName = "치즈", sentence = "어?색채 구슬이다!", talkingAnimState = "00_Player_Profile_IDLE_T_0000", idleAnimState = "00_Player_Profile_IDLE_M_0000" },
            new DialogueData { speakerName = "치즈", sentence = "그렇다는 건... 네가 네로구나!", talkingAnimState = "00_Player_Profile_Anger_T_0000", idleAnimState = "00_Player_Profile_Anger_M_0000" },
            new DialogueData { speakerName = "치즈", sentence = "당장 돌려줘!", talkingAnimState = "00_Player_Profile_Anger_T_0000", idleAnimState = "00_Player_Profile_Anger_M_0000" },
            new DialogueData { speakerName = "네로", sentence = "돌려달라고?", talkingAnimState = "09_Nero_Profile_Sneer_T_0000", idleAnimState = "09_Nero_Profile_Sneer_M_0000" },
            new DialogueData { speakerName = "네로", sentence = "이건 이제 내 꺼야.", talkingAnimState = "09_Nero_Profile_IDLE_T_0000", idleAnimState = "09_Nero_Profile_IDLE_M_0000" },
            new DialogueData { speakerName = "네로", sentence = "누구에게도 넘길 생각은 없어.", talkingAnimState = "09_Nero_Profile_Anger_T_0000", idleAnimState = "09_Nero_Profile_Anger_M_0000" },
            new DialogueData { speakerName = "치즈", sentence = "뭐하는...", talkingAnimState = "00_Player_Profile_IDLE_T_0000", idleAnimState = "00_Player_Profile_IDLE_M_0000" },
            new DialogueData { speakerName = "치즈", sentence = "아.", talkingAnimState = "00_Player_Profile_Suprised_T_0000", idleAnimState = "00_Player_Profile_Suprised_M_0000" }
        };
    }

    private void Update()
    {
        if (!isDialogueActive) return;

        // 스페이스바 키 입력으로 다음 대사 진행
        if (Input.GetKeyDown(KeyCode.Space))
        {
            OnSpaceKeyPressed();
        }
    }

    /// <summary>
    /// 인스펙터 창에서 직접 작성해둔 대사 리스트(inspectorDialogues)를 즉시 개시하는 함수 (버튼 / 이벤트 연동용)
    /// </summary>
    public void PlayInspectorDialogues()
    {
        StartDialogue(inspectorDialogues);
    }

    /// <summary>
    /// 외부(타임라인 / 스크립트 / 컷씬 트리거)에서 대화창을 개시할 때 호출하는 메인 API
    /// </summary>
    /// <param name="dialogues">대사 목록 데이터</param>
    /// <param name="onComplete">모든 대화 완료 시 실행할 콜백 함수</param>
    public void StartDialogue(List<DialogueData> dialogues, System.Action onComplete = null)
    {
        if (dialogues == null || dialogues.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        currentDialogueList = dialogues;
        currentIndex = 0;
        onDialogueComplete = onComplete;
        isDialogueActive = true;

        // [수정] Time.timeScale을 0으로 강제 멈추지 않고 1.0 정상 유지! (대사 및 애니메이션이 멈추는 근본 원인 해결)
        // 컷씬 도중 플레이어 조작(이동, 점프, 공격)만 깔끔하게 잠급니다.
        PlayerMove playerMove = FindFirstObjectByType<PlayerMove>();
        if (playerMove != null)
        {
            playerMove.SetControl(false);
        }

        // 마우스 붓칠/드로잉 시스템 차단 (마우스 이동은 가능하되 칠하기 금지)
        CursorController cursorCtrl = FindFirstObjectByType<CursorController>();
        if (cursorCtrl != null)
        {
            cursorCtrl.enabled = false;
        }

        if (dialoguePanelObj != null)
        {
            dialoguePanelObj.SetActive(true);

            if (dialogueAnimator != null && !string.IsNullOrEmpty(openTriggerName))
            {
                dialogueAnimator.ResetTrigger(closeTriggerName);
                dialogueAnimator.SetTrigger(openTriggerName);
            }
        }

        DisplayCurrentSentence();
    }

    /// <summary>
    /// 스페이스바 입력 처리:
    /// 1) 타이핑 중이면 즉시 전체 글자 완성 ➔ 2초 자동 진행 대기 타이머 개시
    /// 2) 2초 대기시간 도중 스페이스바 클릭 ➔ 2초 대기시간 즉시 스킵(Skip)하고 다음 대사로 진행
    /// </summary>
    public void OnSpaceKeyPressed()
    {
        if (!isDialogueActive) return;

        if (isTyping)
        {
            // 타이핑 출력 중 ➔ 즉시 문장 완성
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            
            DialogueData data = currentDialogueList[currentIndex];
            SetSentenceText(data.sentence);
            isTyping = false;

            StartAutoAdvanceTimer();
        }
        else if (isWaitingForAutoAdvance)
        {
            // 2초 대기 중 ➔ 2초 대기시간 즉시 스킵!
            if (autoAdvanceCoroutine != null) StopCoroutine(autoAdvanceCoroutine);
            isWaitingForAutoAdvance = false;

            GoToNextSentence();
        }
        else
        {
            GoToNextSentence();
        }
    }

    private void GoToNextSentence()
    {
        currentIndex++;
        if (currentIndex < currentDialogueList.Count)
        {
            DisplayCurrentSentence();
        }
        else
        {
            EndDialogue();
        }
    }

    /// <summary>
    /// 현재 인덱스의 대사, 화자 이름, 및 좌측 프로필/애니메이션 출력
    /// </summary>
    private void DisplayCurrentSentence()
    {
        if (currentIndex >= currentDialogueList.Count) return;

        // 이전 2초 대기 타이머 취소
        if (autoAdvanceCoroutine != null) StopCoroutine(autoAdvanceCoroutine);
        isWaitingForAutoAdvance = false;

        DialogueData data = currentDialogueList[currentIndex];

        // 3번 기능: 8번 대사 ("아." 대사) 출력 시 특정 타일맵 콜라이더 충돌 판정 제거!
        if (data.sentence.Contains("아.") || currentIndex == 7)
        {
            DisableTargetTilemapCollider();
        }

        // 1. 화자 이름 및 색상 세팅
        SetSpeakerText(data.speakerName, data.speakerColor);

        // 2. 상단/하단 애니메이션 스왑
        UpdatePortrait(data);

        // 3. 대사 출력 (타이핑 연출 적용 여부)
        if (typingSpeed > 0f)
        {
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            typingCoroutine = StartCoroutine(TypeSentenceRoutine(data.sentence));
        }
        else
        {
            SetSentenceText(data.sentence);
            isTyping = false;
            StartAutoAdvanceTimer();
        }
    }

    /// <summary>
    /// 3번 기능 구현: 8번 '아.' 대사 시 특정 타일맵 콜라이더 충돌 비활성화 및 공중 체공 유지
    /// </summary>
    private void DisableTargetTilemapCollider()
    {
        Debug.Log("[CutsceneDialogueManager] 8번 대사 '아.' 출력! 지정된 바닥 타일맵 콜라이더 충돌을 제거하되 플레이어는 공중 체공을 유지합니다.");

        // 1. 플레이어 중력 0 및 속도 0으로 맞춰 컷씬 도중 추락하는 것을 방지 (공중에 둥둥 떠있게 유지)
        PlayerMove playerMove = FindFirstObjectByType<PlayerMove>();
        if (playerMove != null)
        {
            Rigidbody2D rb = playerMove.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                if (originalPlayerGravity < 0f) originalPlayerGravity = rb.gravityScale;
                rb.gravityScale = 0f;
                rb.linearVelocity = Vector2.zero;
            }
        }

        // 2. 인스펙터에 지정된 타일맵 콜라이더 끄기
        if (targetTilemapCollider != null)
        {
            targetTilemapCollider.enabled = false;
        }

        // 3. 인스펙터에 지정된 바닥 게임오브젝트 비활성화
        if (targetFloorObj != null)
        {
            targetFloorObj.SetActive(false);
        }

        // 4. 씬 내 태그가 BreakableFloor 인 콜라이더 자동 탐색 및 충돌 비활성화
        GameObject[] breakableFloors = GameObject.FindGameObjectsWithTag("BreakableFloor");
        foreach (GameObject floor in breakableFloors)
        {
            Collider2D col = floor.GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
        }
    }

    private void StartAutoAdvanceTimer()
    {
        if (spacePromptObj != null) spacePromptObj.SetActive(true);

        // 대사가 다 출력되어 2초 대기할 때는 말을 하지 않는 정지 프레임(idleAnimState)으로 스왑!
        PlayIdleAnim();

        if (autoAdvanceDelay > 0f)
        {
            if (autoAdvanceCoroutine != null) StopCoroutine(autoAdvanceCoroutine);
            autoAdvanceCoroutine = StartCoroutine(AutoAdvanceRoutine());
        }
    }

    private IEnumerator AutoAdvanceRoutine()
    {
        isWaitingForAutoAdvance = true;
        yield return new WaitForSeconds(autoAdvanceDelay);
        isWaitingForAutoAdvance = false;

        GoToNextSentence();
    }

    /// <summary>
    /// 대화 말할 때 통짜 애니메이션(talkingAnimState) 재생 (3중 안전 재생 지원)
    /// </summary>
    private void UpdatePortrait(DialogueData data)
    {
        if (dialogueAnimator != null && !string.IsNullOrEmpty(data.talkingAnimState))
        {
            dialogueAnimator.ResetTrigger(closeTriggerName);

            // 1. Play(StateName) 시도
            dialogueAnimator.Play(data.talkingAnimState, -1, 0f);
            
            // 2. 만약 State 이름 대신 파라미터 Trigger 일 경우를 대비해 Trigger 세팅도 시도
            dialogueAnimator.SetTrigger(data.talkingAnimState);
        }

        if (dialogueFrameImage != null && data.dialogueSprite != null)
        {
            dialogueFrameImage.sprite = data.dialogueSprite;
        }
    }

    /// <summary>
    /// 대사가 끝나고 2초 대기할 때는 말을 하지 않는 정지 프레임(idleAnimState)으로 스왑
    /// </summary>
    private void PlayIdleAnim()
    {
        if (currentIndex >= currentDialogueList.Count) return;
        DialogueData data = currentDialogueList[currentIndex];

        if (dialogueAnimator != null && !string.IsNullOrEmpty(data.idleAnimState))
        {
            dialogueAnimator.Play(data.idleAnimState, -1, 0f);
            dialogueAnimator.SetTrigger(data.idleAnimState);
        }
    }

    private IEnumerator TypeSentenceRoutine(string sentence)
    {
        isTyping = true;
        if (spacePromptObj != null) spacePromptObj.SetActive(false);

        SetSentenceText("");

        foreach (char letter in sentence.ToCharArray())
        {
            AppendSentenceText(letter);
            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;
        StartAutoAdvanceTimer();
    }

    private void SetSpeakerText(string name, Color color)
    {
        if (speakerTmpText != null)
        {
            speakerTmpText.text = name;
            speakerTmpText.color = color;
        }
        if (speakerLegacyText != null)
        {
            speakerLegacyText.text = name;
            speakerLegacyText.color = color;
        }
    }

    private void SetSentenceText(string text)
    {
        if (sentenceTmpText != null) sentenceTmpText.text = text;
        if (sentenceLegacyText != null) sentenceLegacyText.text = text;
    }

    private void AppendSentenceText(char letter)
    {
        if (sentenceTmpText != null) sentenceTmpText.text += letter;
        if (sentenceLegacyText != null) sentenceLegacyText.text += letter;
    }

    /// <summary>
    /// 대화창 닫기 및 애니메이션 완료 후 컷씬 재개
    /// </summary>
    private void EndDialogue()
    {
        isDialogueActive = false;

        StartCoroutine(EndDialogueRoutine());
    }

    private IEnumerator EndDialogueRoutine()
    {
        // 1. 퇴장(Close) 애니메이션 실행
        if (dialogueAnimator != null && !string.IsNullOrEmpty(closeTriggerName))
        {
            dialogueAnimator.ResetTrigger(openTriggerName);
            dialogueAnimator.SetTrigger(closeTriggerName);
            yield return new WaitForSeconds(0.35f);
        }

        // 2. 대화창 비활성화 (대화창 소멸)
        if (dialoguePanelObj != null)
        {
            dialoguePanelObj.SetActive(false);
        }

        // 3. 2초간 화면 까맣게 페이드아웃 연출
        if (ScreenFader.Instance != null)
        {
            Debug.Log("[CutsceneDialogueManager] 컷씬 대화 완료! 대화창 소멸 후 2초간 화면 페이드아웃을 진행합니다.");
            yield return ScreenFader.Instance.FadeOutOnly(fadeOutDurationOnEnd);
        }
        else
        {
            yield return new WaitForSeconds(fadeOutDurationOnEnd);
        }

        // 4. 대화창이 소멸하고 2초 페이드아웃이 끝난 시점에서 비로소 중력 복원 ➔ 추락/낙하 시작!
        PlayerMove playerMove = FindFirstObjectByType<PlayerMove>();
        if (playerMove != null)
        {
            Rigidbody2D rb = playerMove.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.gravityScale = (originalPlayerGravity > 0f) ? originalPlayerGravity : 3.0f;
            }
            playerMove.SetControl(true);
        }

        // 5. 물감 붓칠/드로잉 시스템 복원
        CursorController cursorCtrl = FindFirstObjectByType<CursorController>();
        if (cursorCtrl != null)
        {
            cursorCtrl.enabled = true;
        }

        // 6. 멈췄던 게임 세계 시간 복구 (1.0)
        Time.timeScale = (originalTimeScale > 0f) ? originalTimeScale : 1.0f;

        // 7. 완료 콜백 실행 및 화면 페이드인 복귀
        System.Action callback = onDialogueComplete;
        onDialogueComplete = null;
        callback?.Invoke();

        if (ScreenFader.Instance != null)
        {
            yield return ScreenFader.Instance.FadeInOnly(1.0f);
        }
    }
}
