using UnityEngine;
using UnityEngine.Playables;
using Unity.Cinemachine;

[RequireComponent(typeof(Collider2D))]
public class CutsceneTrigger : MonoBehaviour
{
    [Header("컷씬 설정")]
    [Tooltip("재생할 Timeline이 붙어있는 PlayableDirector")]
    public PlayableDirector director;

    [Tooltip("한 번 재생한 뒤에는 다시 트리거되지 않게 할지 여부")]
    public bool playOnlyOnce = true;

    [Header("플레이어 조작 제어")]
    [Tooltip("컷씬 재생 중 플레이어 이동을 막을지 여부")]
    public bool lockPlayerControl = true;

    [Header("카메라 전환 설정")]
    [Tooltip("컷씬 재생 중 활성화할 Cinemachine 가상 카메라 (비워두면 카메라 전환 없음)")]
    public CinemachineCamera cutsceneCamera;

    [Tooltip("컷씬 카메라에 부여할 임시 우선순위 (평소 게임플레이 카메라보다 높아야 함)")]
    public int cutsceneCameraPriority = 20;

    private int originalCameraPriority;
    private bool hasPlayed = false;

    void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (playOnlyOnce && hasPlayed) return;
        if (director == null) return;

        hasPlayed = true;

        if (lockPlayerControl)
        {
            PlayerMove playerMove = other.GetComponent<PlayerMove>();
            if (playerMove == null) playerMove = other.GetComponentInParent<PlayerMove>();
            if (playerMove != null) playerMove.SetControl(false);
        }

        // 컷씬 시작 직전, 원래 우선순위를 기억해두고 컷씬 카메라로 전환
        if (cutsceneCamera != null)
        {
            originalCameraPriority = cutsceneCamera.Priority;
            cutsceneCamera.Priority = cutsceneCameraPriority;
        }

        director.stopped += OnCutsceneFinished;
        director.Play();
    }

    private void OnCutsceneFinished(PlayableDirector finishedDirector)
    {
        director.stopped -= OnCutsceneFinished; // 한 번 쓰고 반드시 구독 해제 (중복 방지)

        if (lockPlayerControl)
        {
            PlayerMove playerMove = FindFirstObjectByType<PlayerMove>();
            if (playerMove != null) playerMove.SetControl(true);
        }

        // 컷씬 종료 시 원래 우선순위로 복원 -> 자동으로 원래 게임플레이 카메라로 블렌딩 복귀
        if (cutsceneCamera != null)
        {
            cutsceneCamera.Priority = originalCameraPriority;
        }
    }
}