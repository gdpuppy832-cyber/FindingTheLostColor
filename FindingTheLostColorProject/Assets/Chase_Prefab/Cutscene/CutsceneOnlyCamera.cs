using UnityEngine;

/// <summary>
/// 씬 시작 컷씬(S2_SceneStartCutsceneController) 동안에만 활성화되어야 하는 카메라(혹은 오브젝트)에 붙임.
/// 씬이 시작되면 평소엔 꺼져있고, 컷씬이 시작될 때 켜지며, 컷씬이 끝나면 다시 꺼짐.
/// 이 스크립트 자체는 아무 동작도 하지 않고, S2_SceneStartCutsceneController가
/// ShowForCutscene()/HideAfterCutscene()을 호출해 켜고 끄는 용도로만 쓰임.
/// </summary>
public class CutsceneOnlyCamera : MonoBehaviour
{
    [Tooltip("씬이 로드되는 즉시(컷씬 시작 전) 평소 상태로 꺼둘지 여부")]
    public bool startHidden = true;

    private void Awake()
    {
        if (startHidden)
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>컷씬이 시작되는 시점에 호출. 이 오브젝트를 켠다.</summary>
    public void ShowForCutscene()
    {
        gameObject.SetActive(true);
    }

    /// <summary>컷씬이 완전히 끝나는 시점에 호출. 이 오브젝트를 다시 끈다.</summary>
    public void HideAfterCutscene()
    {
        gameObject.SetActive(false);
    }
}