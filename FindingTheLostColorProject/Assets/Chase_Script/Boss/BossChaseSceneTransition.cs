using UnityEngine;
using System.Collections;

public class BossChaseSceneTransition : MonoBehaviour
{
    [Tooltip("�߰� ���� �� �� ��ȯ�� �ߵ��Ǳ������ �ð�(��)")]
    public float transitionDelay = 90f;

    [Tooltip("��ȯ�� ���� ���� �̸�")]
    public string nextSceneName;

    [Tooltip("�� ��ȯ �� ���̵� �ƿ�/�� ���� �ð�(��)")]
    public float fadeDuration = 1f;

    [Header("씬 시작 대화 잠금")]
    [Tooltip("true면 다음 씬 전환 카운트다운이 진행되지 않고 그대로 멈춰있음")]
    public bool transitionLocked = true;

    private float elapsedTime = 0f; // 잠금 중에는 누적되지 않는 경과 시간 (WaitForSeconds 대신 직접 누적해서 일시정지 가능하게 함)

    void Start()
    {
        StartCoroutine(TransitionRoutine());
    }

    /// <summary>
    /// 컷씬 컨트롤러(S2_SceneStartCutsceneController 등)에서 호출.
    /// true면 씬 전환 카운트다운이 멈추고, false면 그 시점부터 다시 이어서 진행됨.
    /// </summary>
    public void SetTransitionLocked(bool locked)
    {
        transitionLocked = locked;
    }

    private IEnumerator TransitionRoutine()
    {
        // WaitForSeconds 대신 직접 시간을 누적하는 방식으로 바꿔서,
        // transitionLocked가 true인 동안에는 경과 시간이 전혀 쌓이지 않도록 함
        // (대화 중에는 카운트다운이 멈춰있다가, 잠금이 풀리는 순간부터 이어서 진행됨)
        while (elapsedTime < transitionDelay)
        {
            if (!transitionLocked)
            {
                elapsedTime += Time.deltaTime;
            }
            yield return null;
        }

        if (ScreenFader.Instance != null)
        {
            // ������Ʈ�� �̹� �ִ� ���̵�/����ȯ ���� �ý����� �״�� ���
            ScreenFader.Instance.FadeToScene(nextSceneName, fadeDuration);
        }
        else
        {
            Debug.LogWarning("[BossChaseSceneTransition] ScreenFader.Instance�� �����ϴ�. " +
                "���� ScreenFader�� ��ġ�Ǿ� �ִ��� Ȯ�����ּ���.");
        }
    }
}