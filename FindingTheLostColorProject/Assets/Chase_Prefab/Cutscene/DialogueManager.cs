using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

[System.Serializable]
public class DialogueLine
{
    [Header("캐릭터")]
    [Tooltip("대화창 상단에 표시될 캐릭터 이름")]
    public string speakerName;

    [Tooltip("현재 대화에서 표시할 기본 캐릭터 이미지")]
    public Sprite characterImage;

    [Tooltip("말하지 않을 때 사용할 입 닫힘(기본) 이미지")]
    public Sprite characterMouthClosedImage;

    [Tooltip("말하는 동안 순서대로 반복 재생할 입 모양 프레임들. 배열 크기로 프레임 개수를 자유롭게 조절할 수 있음 (예: 입 살짝 벌림 -> 크게 벌림 -> 살짝 벌림 순서로 3장 이상도 가능)")]
    public Sprite[] characterMouthFrames;

    [Tooltip("켜두면, 이 대사의 타이핑이 끝난 뒤에도 입 애니메이션이 멈추지 않고 계속 반복 재생됨. 다음 대사로 넘어가거나 대화가 종료될 때 비로소 멈춤")]
    public bool keepMouthAnimatingAfterTyping = false;

    [Header("대사")]
    [TextArea(2, 4)]
    public string text;
}

/// <summary>
/// 메이플스토리 스타일 대화 시스템.
/// 대화창 UI는 하나만 존재하며, 대사가 바뀔 때마다 그 안의 Sprite/텍스트만 교체한다.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("대화 UI")]
    [Tooltip("대화창 전체 오브젝트 (대화 시작 시 활성화, 종료 시 비활성화)")]
    public GameObject dialogueBox;

    [Tooltip("대화창 안의 캐릭터 이미지 (하나만 존재하며 Sprite만 계속 교체됨)")]
    public Image characterImage;

    [Tooltip("대화창 안의 캐릭터 이름 텍스트")]
    public TextMeshProUGUI nameText;

    [Tooltip("대화창 안의 대사 내용 텍스트")]
    public TextMeshProUGUI dialogueText;

    [Header("타자기 설정")]
    [Tooltip("대사가 한 글자씩 출력되는 간격 (초)")]
    public float typeSpeed = 0.1f;

    [Header("입 모양 설정")]
    [Tooltip("타이핑 중 입 모양이 열림/닫힘으로 전환되는 간격 (초)")]
    public float mouthChangeInterval = 0.05f;

    private DialogueLine[] currentLines;
    private int currentLineIndex = -1;

    private Coroutine typingCoroutine;
    private Coroutine mouthCoroutine;

    private bool isTyping = false;
    private bool isDialogueActive = false;
    private bool keepMouthAnimating = false;
    private System.Action onDialogueEndedCallback; // 이 대화가 끝났을 때 호출할 콜백 (예: 보스 2페이즈 시작)

    void Awake()
    {
        // 간단한 싱글톤. 이미 인스턴스가 있으면 새로 생긴 쪽을 정리한다.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (dialogueBox != null)
            dialogueBox.SetActive(false);
    }

    void Update()
    {
        if (!isDialogueActive) return;

        // 요구사항: ENTER를 항상 먼저 검사한다. ENTER는 "현재 대사 스킵"이 아니라 "전체 대화 종료".
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            EndDialogue();
            return;
        }

        // ENTER가 눌리지 않았을 때만 SPACE를 검사한다.
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isTyping)
            {
                // 타이핑 중 SPACE -> 현재 대사를 즉시 완성
                CompleteCurrentLineInstantly();
            }
            else
            {
                // 타이핑이 이미 끝난 상태에서 SPACE -> 다음 대사로 진행
                AdvanceToNextLine();
            }
        }
    }

    /// <summary>
    /// 외부 스크립트에서 대화를 시작할 때 호출하는 함수.
    /// 예: DialogueManager.Instance.StartDialogue(dialogues);
    /// </summary>
    public void StartDialogue(DialogueLine[] lines, System.Action onComplete = null)
    {
        if (lines == null || lines.Length == 0) return;

        // 혹시 이전 대화가 남아있다면 완전히 정리하고 새로 시작
        StopAllDialogueCoroutines();

        currentLines = lines;
        currentLineIndex = -1;
        isDialogueActive = true;
        onDialogueEndedCallback = onComplete;

        if (dialogueBox != null)
            dialogueBox.SetActive(true);

        AdvanceToNextLine();
    }

    /// <summary>
    /// 다음 DialogueLine으로 넘어간다. 더 이상 대사가 없으면 대화를 종료한다.
    /// </summary>
    private void AdvanceToNextLine()
    {
        currentLineIndex++;

        if (currentLines == null || currentLineIndex >= currentLines.Length)
        {
            EndDialogue();
            return;
        }

        DialogueLine line = currentLines[currentLineIndex];

        // 이름/캐릭터 이미지 교체 (대화창 UI 자체는 그대로, 내부 내용만 갱신)
        if (nameText != null)
            nameText.text = line.speakerName;

        // 대사 시작 시 기본은 입 닫힘 이미지
        if (characterImage != null)
        {
            characterImage.sprite = line.characterMouthClosedImage != null
                ? line.characterMouthClosedImage
                : line.characterImage;
        }
        // (characterMouthOpenImage 참조는 더 이상 없음 - characterMouthFrames 배열 사용)

        if (dialogueText != null)
            dialogueText.text = "";

        // 이전 대사에서 돌던 코루틴들을 확실히 끊고 새로 시작
        StopAllDialogueCoroutines();

        typingCoroutine = StartCoroutine(TypeLineRoutine(line));
        mouthCoroutine = StartCoroutine(MouthAnimationRoutine(line));
    }

    /// <summary>
    /// 한 글자씩 대사를 출력하는 타자기 효과 코루틴.
    /// </summary>
    private IEnumerator TypeLineRoutine(DialogueLine line)
    {
        isTyping = true;

        if (dialogueText != null)
        {
            dialogueText.text = "";

            foreach (char c in line.text)
            {
                dialogueText.text += c;
                yield return new WaitForSeconds(typeSpeed);
            }
        }

        FinishTyping();
    }

    /// <summary>
    /// 타이핑 중 입 모양을 닫힘/열림으로 반복 전환하는 코루틴.
    /// 타이핑이 끝나면(또는 스킵되면) 반드시 함께 멈춰야 한다.
    /// </summary>
    private IEnumerator MouthAnimationRoutine(DialogueLine line)
    {
        // 순환할 프레임이 없으면(빈 배열) 애니메이션 없이 그냈 대기
        if (line.characterMouthFrames == null || line.characterMouthFrames.Length == 0)
            yield break;

        int frameIndex = 0;

        // 타이핑 중이거나, "타이핑 끝나도 계속" 옵션이 켜져서 keepMouthAnimating이 true인 동안 계속 반복
        while (isTyping || keepMouthAnimating)
        {
            if (characterImage != null)
            {
                Sprite target = line.characterMouthFrames[frameIndex];
                if (target != null)
                    characterImage.sprite = target;

                frameIndex = (frameIndex + 1) % line.characterMouthFrames.Length;
            }

            yield return new WaitForSeconds(mouthChangeInterval);
        }
    }

    /// <summary>
    /// SPACE로 타이핑을 스킵했을 때: 대사 전체를 즉시 표시하고, 관련 코루틴을 정확히 정리한다.
    /// </summary>
    private void CompleteCurrentLineInstantly()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        if (currentLines != null && currentLineIndex >= 0 && currentLineIndex < currentLines.Length)
        {
            DialogueLine line = currentLines[currentLineIndex];
            if (dialogueText != null)
                dialogueText.text = line.text;
        }

        FinishTyping();
    }

    /// <summary>
    /// 타이핑이 끝났을 때(자연 종료든 스킵이든) 공통으로 처리하는 마무리 로직.
    /// 입 모양 코루틴을 멈추고 입 닫힘 이미지로 되돌린다.
    /// </summary>
    private void FinishTyping()
    {
        isTyping = false;

        DialogueLine line = (currentLines != null && currentLineIndex >= 0 && currentLineIndex < currentLines.Length)
            ? currentLines[currentLineIndex]
            : null;

        if (line != null && line.keepMouthAnimatingAfterTyping)
        {
            // 이 대사는 타이핑이 끝나도 입 애니메이션을 계속 돌림 (mouthCoroutine을 멈추지 않고,
            // 입 닫힘 이미지로도 되돌리지 않음 - 다음 대사로 넘어갈 때 StopAllDialogueCoroutines에서 정리됨)
            keepMouthAnimating = true;
        }
        else
        {
            if (mouthCoroutine != null)
            {
                StopCoroutine(mouthCoroutine);
                mouthCoroutine = null;
            }

            if (line != null && characterImage != null)
            {
                Sprite closed = line.characterMouthClosedImage != null
                    ? line.characterMouthClosedImage
                    : line.characterImage;
                characterImage.sprite = closed;
            }
        }

        typingCoroutine = null;
    }

    private void StopAllDialogueCoroutines()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        if (mouthCoroutine != null)
        {
            StopCoroutine(mouthCoroutine);
            mouthCoroutine = null;
        }

        isTyping = false;
        keepMouthAnimating = false; // "타이핑 끝나도 계속" 옵션으로 남아있던 애니메이션도 여기서 확실히 종료
    }

    /// <summary>
    /// 전체 대화를 종료한다. (ENTER를 눌렀거나, 마지막 대사에서 SPACE를 눌렀을 때 호출됨)
    /// </summary>
    private void EndDialogue()
    {
        StopAllDialogueCoroutines();

        isDialogueActive = false;
        currentLines = null;
        currentLineIndex = -1;

        if (dialogueBox != null)
            dialogueBox.SetActive(false);

        if (dialogueText != null)
            dialogueText.text = "";

        if (nameText != null)
            nameText.text = "";

        if (characterImage != null)
            characterImage.sprite = null;

        // 콜백은 상태 정리가 다 끝난 뒤 마지막에 호출 (콜백 안에서 새 대화를 다시 시작해도 안전하도록)
        System.Action callback = onDialogueEndedCallback;
        onDialogueEndedCallback = null;
        callback?.Invoke();
    }
}