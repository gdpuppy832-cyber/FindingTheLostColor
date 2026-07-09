using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ResolutionSettings : MonoBehaviour
{
    [Header("UI Element")]
    [SerializeField] private TMP_Dropdown resolutionDropdown; // 씬에 배치한 TMP_Dropdown 컴포넌트

    // 중복 제거(주사율 제외 가로x세로 매칭)된 해상도 리스트
    private List<Resolution> filteredResolutions = new List<Resolution>();
    private bool isInitialized = false;

    private void Start()
    {
        if (resolutionDropdown == null) return;

        // 1. 모니터 지원 해상도 목록을 긁어와 드롭다운에 채워넣음 (최초 1회)
        BuildResolutionOptions();

        // 2. 값 변경 리스너를 1회만 등록
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        isInitialized = true;

        // 3. 저장된 해상도 인덱스로 드롭다운 선택값 초기화 및 동기화
        SyncDropdownToSavedResolution();
    }

    private void OnEnable()
    {
        // 설정창이 켜질 때마다 저장되어 있던 최신 해상도 인덱스를 읽어와 드롭다운을 일치시킵니다.
        if (isInitialized)
        {
            SyncDropdownToSavedResolution();
        }
    }

    // 해상도 옵션 목록을 구성하는 함수
    private void BuildResolutionOptions()
    {
        Resolution[] allResolutions = Screen.resolutions;
        filteredResolutions.Clear();
        List<string> options = new List<string>();

        // 가로x세로 크기가 중복되는 해상도는 필터링 (주사율만 다른 경우를 제거하기 위함)
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
                
                // 가로 1920, 세로 1080일 경우 옆에 (권장) 표시를 붙입니다.
                string optionText;
                if (allResolutions[i].width == 1920 && allResolutions[i].height == 1080)
                {
                    optionText = $"{allResolutions[i].width} x {allResolutions[i].height} (권장)";
                }
                else
                {
                    optionText = $"{allResolutions[i].width} x {allResolutions[i].height}";
                }
                
                options.Add(optionText);
            }
        }

        // 드롭다운 초기화 및 리스트 대입
        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(options);

        // [수정] 게임 생전 처음 구동 시에만 기본 해상도를 1920x1080(권장)으로 자동 세팅하고 저장합니다. (재부팅 시에는 유저 선택 저장값을 로드)
        if (!PlayerPrefs.HasKey("SelectedResolutionIndex"))
        {
            int defaultIndex = 0;
            bool hasFHD = false;
            for (int i = 0; i < filteredResolutions.Count; i++)
            {
                if (filteredResolutions[i].width == 1920 && filteredResolutions[i].height == 1080)
                {
                    Screen.SetResolution(1920, 1080, Screen.fullScreen);
                    defaultIndex = i;
                    hasFHD = true;
                    break;
                }
            }

            // 만약 모니터 사양이 FHD(1920x1080)를 물리적으로 지원 안 하는 화면이라면, 지원하는 가장 높은 해상도로 설정합니다.
            if (!hasFHD && filteredResolutions.Count > 0)
            {
                defaultIndex = filteredResolutions.Count - 1;
                Resolution highest = filteredResolutions[defaultIndex];
                Screen.SetResolution(highest.width, highest.height, Screen.fullScreen);
            }

            // 최초 해상도 인덱스 세팅 저장
            PlayerPrefs.SetInt("SelectedResolutionIndex", defaultIndex);
            PlayerPrefs.Save();
        }
    }

    // 저장된 해상도 인덱스를 기반으로 드롭다운 활성 아이템을 동기화
    private void SyncDropdownToSavedResolution()
    {
        if (resolutionDropdown == null) return;

        // 저장된 해상도 인덱스를 불러옵니다.
        int savedIndex = PlayerPrefs.GetInt("SelectedResolutionIndex", -1);

        // 만약 저장된 데이터가 없는 예외 상황이라면 현재 실제 스크린 크기를 찾아서 보정합니다.
        if (savedIndex == -1)
        {
            savedIndex = 0;
            for (int i = 0; i < filteredResolutions.Count; i++)
            {
                if (filteredResolutions[i].width == Screen.width && filteredResolutions[i].height == Screen.height)
                {
                    savedIndex = i;
                    break;
                }
            }
        }

        // 인덱스가 범위를 벗어나지 않도록 방어 코드
        if (savedIndex >= filteredResolutions.Count)
        {
            savedIndex = filteredResolutions.Count - 1;
        }

        // 이벤트 트리거 없이 안전하게 슬라이더 때처럼 값만 갱신 (무한 리셋 피드백 루프 방지)
        resolutionDropdown.SetValueWithoutNotify(savedIndex);
        resolutionDropdown.RefreshShownValue();
    }

    // 드롭다운 아이템을 선택했을 때 실행되는 함수
    private void OnResolutionChanged(int index)
    {
        if (index < 0 || index >= filteredResolutions.Count) return;

        // 선택된 해상도 인덱스를 PlayerPrefs에 즉시 저장하여 타이틀/인게임 드롭다운이 공유하게 만듭니다.
        PlayerPrefs.SetInt("SelectedResolutionIndex", index);
        PlayerPrefs.Save();

        Resolution selectedResolution = filteredResolutions[index];
        
        // 현재 전체화면 여부를 유지하며 해상도 스위칭
        Screen.SetResolution(selectedResolution.width, selectedResolution.height, Screen.fullScreen);
        
        Debug.Log($"해상도 변경 적용: {selectedResolution.width} x {selectedResolution.height} (인덱스 {index} 저장 완료)");
    }
}
