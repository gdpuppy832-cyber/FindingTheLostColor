using UnityEngine;
using UnityEngine.UI;

public class OptionUI : MonoBehaviour
{
    [Header("Close Button")]
    [SerializeField] private Button closeButton; // 옵션창을 닫는 버튼

    private void Start()
    {
        // 닫기 버튼이 인스펙터에서 연결되어 있다면 클릭 이벤트 자동 리스너 등록
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }
    }

    // 옵션창 열기
    public void Open()
    {
        gameObject.SetActive(true);
    }

    // 옵션창 닫기
    public void Close()
    {
        gameObject.SetActive(false);

        // 볼륨 등 플레이어가 수정한 설정을 기기에 안전하게 강제 저장(Save)합니다.
        PlayerPrefs.Save();
        Debug.Log("설정 저장 완료");
    }

    // 옵션창 상태 토글 (켜져있으면 끄고, 꺼져있으면 켬)
    public void Toggle()
    {
        if (gameObject.activeSelf)
        {
            Close();
        }
        else
        {
            Open();
        }
    }
}
