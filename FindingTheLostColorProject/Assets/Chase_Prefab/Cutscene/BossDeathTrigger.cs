using UnityEngine;

[System.Obsolete("보스 오브젝트가 아닌 독립된 GameObject에 배치해야 합니다. " +
    "보스와 같은 오브젝트/부모/자식 관계에 있으면 NormalMonster.Purify()가 " +
    "이 스크립트를 강제로 비활성화시켜 컷씬 감지가 절대 되지 않습니다.")]
public class BossDeathTrigger : MonoBehaviour
{
    [Tooltip("반드시 씬에서 직접 드래그해서 연결. GetComponent로 자동 탐색하지 않음 " +
             "(이 스크립트는 보스와 부모/자식 관계가 아닌 별도 오브젝트에 있어야 하므로)")]
    public NormalMonster bossHealth;

    [Tooltip("비워두면 같은 오브젝트에서 자동으로 찾음")]
    public BossDeathCutsceneTrigger cutsceneTrigger;

    private bool cutsceneStarted = false;

    void Awake()
    {
        if (cutsceneTrigger == null) cutsceneTrigger = GetComponent<BossDeathCutsceneTrigger>();
    }

    void Update()
    {
        if (cutsceneStarted) return;
        if (bossHealth == null || cutsceneTrigger == null) return;
        if (bossHealth.IsPurified)
        {
            cutsceneStarted = true;
            cutsceneTrigger.StartDeathCutscene();
        }
    }
}