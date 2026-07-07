using UnityEngine;
using UnityEngine.UI;

public class BossHPbar : MonoBehaviour
{
    private CanvasGroup canvasGroup;

    void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    void Update()
    {
        bool bossExists = GameObject.FindGameObjectWithTag("Boss") != null;

        canvasGroup.alpha = bossExists ? 1f : 0f;
        canvasGroup.interactable = bossExists;
        canvasGroup.blocksRaycasts = bossExists;
    }
}
