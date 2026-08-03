using UnityEngine;
using System.Collections;

public class BossChaseSceneTransition : MonoBehaviour
{
    [Tooltip("추격 시작 후 씬 전환이 발동되기까지의 시간(초)")]
    public float transitionDelay = 90f;

    [Tooltip("전환할 다음 씬의 이름")]
    public string nextSceneName;

    [Tooltip("씬 전환 시 페이드 아웃/인 연출 시간(초)")]
    public float fadeDuration = 1f;

    void Start()
    {
        StartCoroutine(TransitionRoutine());
    }

    private IEnumerator TransitionRoutine()
    {
        // Time.timeScale 영향 없이 정확히 90초(기본값) 대기
        yield return new WaitForSecondsRealtime(transitionDelay);

        if (ScreenFader.Instance != null)
        {
            // 프로젝트에 이미 있는 페이드/씬전환 통합 시스템을 그대로 사용
            ScreenFader.Instance.FadeToScene(nextSceneName, fadeDuration);
        }
        else
        {
            Debug.LogWarning("[BossChaseSceneTransition] ScreenFader.Instance가 없습니다. " +
                "씬에 ScreenFader가 배치되어 있는지 확인해주세요.");
        }
    }
}