using System.Collections.Generic;
using UnityEngine;
using TMPro; // TextMeshPro Dropdown을 사용하기 위함

public class ResolutionSettings : MonoBehaviour
{
    [Header("UI Element")]
    [SerializeField] private TMP_Dropdown resolutionDropdown; // 씬에 배치한 TMP_Dropdown 컴포넌트

    // 중복 제거(주사율 제외 가로x세로 매칭)된 해상도 리스트
    private List<Resolution> filteredResolutions = new List<Resolution>();

    private void Start()
    {
        if (resolutionDropdown == null) return;

        InitializeResolutions();
    }

    private void InitializeResolutions()
    {
        // 1. 현재 모니터가 지원하는 전체 해상도 목록 가져오기
        Resolution[] allResolutions = Screen.resolutions;
        
        filteredResolutions.Clear();
        List<string> options = new List<string>();
        int currentResolutionIndex = 0;

        // 2. 가로x세로 크기가 중복되는 해상도는 필터링 (주사율만 다른 경우를 제거하기 위함)
        for (int i = 0; i < allResolutions.Length; i++)
        {
            bool isDuplicate = false;
            foreach (var res in filteredResolutions)
            {
                if (res.width == allResolutions[i].width && res.height == allResolutions[i].height)
                {
                    isDuplicate = true;
                    break;
                }
            }

            if (!isDuplicate)
            {
                filteredResolutions.Add(allResolutions[i]);
                
                // 드롭다운에 표시할 텍스트 형식 (예: "1920 x 1080")
                string optionText = $"{allResolutions[i].width} x {allResolutions[i].height}";
                options.Add(optionText);

                // 현재 사용 중인 화면 해상도와 일치하는 항목의 인덱스 저장
                if (allResolutions[i].width == Screen.width && allResolutions[i].height == Screen.height)
                {
                    currentResolutionIndex = filteredResolutions.Count - 1;
                }
            }
        }

        // 3. 드롭다운 초기화 및 리스트 대입
        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(options);

        // 4. 현재 해상도 인덱스를 기본값으로 선택 표시
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();

        // 5. 드롭다운 값이 바뀔 때 작동할 이벤트 함수 연결
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
    }

    // 드롭다운 아이템을 선택했을 때 실행되는 함수
    private void OnResolutionChanged(int index)
    {
        if (index < 0 || index >= filteredResolutions.Count) return;

        Resolution selectedResolution = filteredResolutions[index];
        
        // 현재 전체화면 여부(Screen.fullScreen)를 유지하면서 해상도를 변경합니다.
        Screen.SetResolution(selectedResolution.width, selectedResolution.height, Screen.fullScreen);
        
        Debug.Log($"해상도 변경 적용: {selectedResolution.width} x {selectedResolution.height}");
    }
}
