using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class MapPortal : InteractableObject
{
    [Header("포탈 씬 이동 설정")]
    [Tooltip("조건(모든 고양이 정화) 충족 시 이동할 다음 씬 이름")]
    public string nextSceneName = "NextStage";
    [Tooltip("씬 전환 시 페이드 아웃 연출 시간 (초)")]
    public float fadeDuration = 1.0f;

    [Header("시작지점 복귀 설정")]
    [Tooltip("조건 미달 시 플레이어가 튕겨나갈 시작지점 위치 (비워둘 시 Start에서 플레이어 현재 위치로 자동 백업)")]
    public Vector3 startSpawnPoint;

    [Header("화면 전환 UI 설정 (선택사항)")]
    [Tooltip("화면 전환용 암전 페이드 이미지 (비워둘 시 씬 내에서 자동 검색)")]
    public Image customFadeImage;

    [Header("효과음 설정")]
    [Tooltip("다음 맵으로 갈 때 재생할 효과음 (비워둘 시 무음)")]
    public AudioClip nextSceneSFX;
    [Tooltip("시작지점으로 퇴출당할 때 재생할 효과음 (비워둘 시 무음)")]
    public AudioClip teleportSFX;

    [Header("커스텀 텍스트 출력 설정 (신규)")]
    [Tooltip("텍스트를 출력할 3D TextMeshPro 컴포넌트")]
    public TMPro.TextMeshPro customTmp3DText;
    [Tooltip("텍스트를 출력할 UI TextMeshProUGUI 컴포넌트 (Canvas 내)")]
    public TMPro.TextMeshProUGUI customTmpUGUIText;
    [Tooltip("텍스트를 출력할 Legacy TextMesh 컴포넌트")]
    public TextMesh customLegacyTextMesh;
    [Tooltip("커스텀 텍스트를 출력할 때 발밑의 기본 안내판을 숨길지 여부 (기본값: true)")]
    public bool hideDefaultPrompt = true;

    [Header("정화 몹 개수 UI 설정 (신규)")]
    [Tooltip("정화 현황(예: 정화 고양이 2 / 5)을 표시할 상단 UI TextMeshProUGUI")]
    public TMPro.TextMeshProUGUI statusTmpText;
    [Tooltip("정화 현황을 표시할 레거시 UI Text")]
    public UnityEngine.UI.Text statusLegacyText;
    [Tooltip("상태 텍스트의 접두사 (기본값: '고양이 ')")]
    public string statusPrefix = "고양이 ";

    private PlayerMove playerCache;
    private bool isTransitioning = false;

    void Start()
    {
        // 1. 시작지점 스폰 포인트가 미지정되어 있다면 플레이어의 최초 위치로 자동 캐싱
        if (startSpawnPoint == Vector3.zero)
        {
            playerCache = FindFirstObjectByType<PlayerMove>();
            if (playerCache != null)
            {
                startSpawnPoint = playerCache.transform.position;
            }
        }
        else
        {
            playerCache = FindFirstObjectByType<PlayerMove>();
        }

        // 2. 페이드 이미지 자동 할당 방어 처리
        if (customFadeImage == null)
        {
            // 씬 내에 배치된 CaveEntrance가 쓰고 있는 Fade Image를 우선 검색하여 복제 할당
            CaveEntrance cave = FindFirstObjectByType<CaveEntrance>();
            if (cave != null && cave.customFadeImage != null)
            {
                customFadeImage = cave.customFadeImage;
            }
            else
            {
                // 차선책으로 전체 Image 컴포넌트 중 검은색 Fade용 UI 검색
                Image[] allImages = FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var img in allImages)
                {
                    if (img.gameObject.name.Contains("Fade") || img.gameObject.name.Contains("Black"))
                    {
                        customFadeImage = img;
                        break;
                    }
                }
            }
        }

        // 3. 기본 안내판 숨김이 켜져있다면 아예 처음부터 promptMessage를 비워두어 1프레임 번쩍임 차단
        if (hideDefaultPrompt)
        {
            promptMessage = "";
        }
    }

    void Update()
    {
        if (isTransitioning) return;

        // 1. 맵 내 모든 고양이 몬스터의 정화 완료 상태 검사
        bool isAllPurified = false;
        if (PurificationManager.Instance != null)
        {
            isAllPurified = PurificationManager.Instance.IsAllPurified;
        }
        else
        {
            NormalMonster[] monsters = FindObjectsByType<NormalMonster>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            isAllPurified = monsters.Length == 0;
        }

        // 2. 조건 충족 여부에 따라 다이내믹하게 상호작용 W키 팝업 문구 스왑
        string message = isAllPurified ? "W를 눌러 다음 맵으로!" : "W를 눌러 시작지점으로 돌아가기";

        // 3. 플레이어와의 실시간 상호작용 범위 측정
        if (playerCache == null) playerCache = FindFirstObjectByType<PlayerMove>();
        bool inRange = playerCache != null && IsInRange(playerCache.transform.position);

        // 4. 발밑 기본 UI 은닉 조율 (진입/탈출 프레임 시점 차로 인한 1프레임 번쩍임 지터링 원천 차단)
        if (hideDefaultPrompt)
        {
            promptMessage = ""; // 항상 비워둠
        }
        else
        {
            promptMessage = inRange ? message : "";
        }

        // 5. 지정한 커스텀 텍스트메쉬 컴포넌트들에 실시간 문구 쓰기 및 비우기
        string displayText = inRange ? message : "";
        if (customTmp3DText != null) customTmp3DText.text = displayText;
        if (customTmpUGUIText != null) customTmpUGUIText.text = displayText;
        if (customLegacyTextMesh != null) customLegacyTextMesh.text = displayText;

        // 6. [신규] 실시간 정화 몹 갯수 스캔 및 상단 UI 텍스트 출력
        if (statusTmpText != null || statusLegacyText != null)
        {
            int total = 0;
            int purified = 0;

            // 씬 상의 모든 몬스터를 스캔하여 정화 수치 파악
            NormalMonster[] allMonsters = FindObjectsByType<NormalMonster>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            total = allMonsters.Length;
            foreach (var m in allMonsters)
            {
                if (m != null && m.IsPurified)
                {
                    purified++;
                }
            }

            string statusText = $"{statusPrefix}{purified} / {total}";
            if (statusTmpText != null) statusTmpText.text = statusText;
            if (statusLegacyText != null) statusLegacyText.text = statusText;
        }
    }

    /// <summary>
    /// 플레이어가 상호작용 영역 내에서 W키를 눌러 상호작용을 실행할 때 트리거
    /// </summary>
    public override void OnInteract()
    {
        if (isTransitioning) return;

        // 고양이 정화 완료 상태 재점검
        bool isAllPurified = false;
        if (PurificationManager.Instance != null)
        {
            isAllPurified = PurificationManager.Instance.IsAllPurified;
        }
        else
        {
            NormalMonster[] monsters = FindObjectsByType<NormalMonster>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            isAllPurified = monsters.Length == 0;
        }

        if (isAllPurified)
        {
            // 모든 고양이 정화 완료 -> 다음 씬으로 전환
            StartCoroutine(TransitionToNextSceneRoutine());
        }
        else
        {
            // 아직 미완료 -> 화면 암전 페이드 후 시작 지점으로 퇴출 순간이동
            StartCoroutine(TeleportToStartPointRoutine());
        }
    }

    /// <summary>
    /// 화면을 페이드아웃시키고 다음 씬을 로드합니다.
    /// </summary>
    private IEnumerator TransitionToNextSceneRoutine()
    {
        isTransitioning = true;

        // 플레이어 조작 차단
        PlayerInteraction playerInt = FindFirstObjectByType<PlayerInteraction>();
        if (playerInt != null) playerInt.SetInteractionLock(true);

        PlayerMove playerMove = FindFirstObjectByType<PlayerMove>();
        if (playerMove != null) playerMove.SetControl(false);

        // 씬 이동 진입 사운드 재생 (인스펙터 등록 클립 직접 재생)
        if (nextSceneSFX != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(nextSceneSFX);
        }

        // 화면 검은색 페이드 아웃
        if (customFadeImage != null)
        {
            customFadeImage.gameObject.SetActive(true);
            float elapsed = 0f;
            Color c = customFadeImage.color;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Clamp01(elapsed / fadeDuration);
                customFadeImage.color = c;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(fadeDuration);
        }

        // 다음 씬 전환 실행
        SceneManager.LoadScene(nextSceneName);
    }

    /// <summary>
    /// 화면을 부드럽게 까맣게 페이드아웃 후, 시작지점으로 플레이어를 텔레포트하고 다시 화면을 밝힙니다.
    /// </summary>
    private IEnumerator TeleportToStartPointRoutine()
    {
        isTransitioning = true;

        PlayerMove playerMove = FindFirstObjectByType<PlayerMove>();
        PlayerInteraction playerInt = FindFirstObjectByType<PlayerInteraction>();

        if (playerInt != null) playerInt.SetInteractionLock(true);
        if (playerMove != null) playerMove.SetControl(false);

        // 퇴출 실패 경보 사운드 재생 (인스펙터 등록 클립 직접 재생)
        if (teleportSFX != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(teleportSFX);
        }

        // 1. 화면 검은색 페이드 아웃 (0.4초)
        if (customFadeImage != null)
        {
            customFadeImage.gameObject.SetActive(true);
            float elapsed = 0f;
            Color c = customFadeImage.color;
            while (elapsed < 0.4f)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Clamp01(elapsed / 0.4f);
                customFadeImage.color = c;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(0.4f);
        }

        // 2. 플레이어 위치를 시작 지점으로 순간이동 및 낙하 속도 초기화
        if (playerMove != null)
        {
            playerMove.transform.position = startSpawnPoint;
            Rigidbody2D rb = playerMove.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero; // 순간이동 후 굳어버리거나 낙하 속도로 인한 충격 방지
            }
        }

        // 순간이동 후 프레임이 안전하게 수습되도록 미세 대기
        yield return new WaitForSeconds(0.2f);

        // 3. 화면 페이드 인 (0.4초)
        if (customFadeImage != null)
        {
            float elapsed = 0f;
            Color c = customFadeImage.color;
            while (elapsed < 0.4f)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Clamp01(1f - (elapsed / 0.4f));
                customFadeImage.color = c;
                yield return null;
            }
            customFadeImage.gameObject.SetActive(false);
        }

        // 플레이어 제어 복원
        if (playerInt != null) playerInt.SetInteractionLock(false);
        if (playerMove != null) playerMove.SetControl(true);

        isTransitioning = false;
    }
}
