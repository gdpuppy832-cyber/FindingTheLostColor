using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource; // 배경음용 오디오 소스 (루프 재생)
    [SerializeField] private AudioSource sfxSource; // 효과음용 오디오 소스 (PlayOneShot 재생)

    // 내부 저장용 볼륨 값 (0.0 ~ 1.0)
    private float masterVolume = 0.5f;
    private float bgmVolume = 0.5f;
    private float sfxVolume = 0.5f;

    // [추가] 효과음 분류용 Enum
    public enum SFXType
    {
        Jump,
        PlayerHit,
        NoPaint,
        PaintRecover,
        Painting,
        EnemyRecover,
        ButtonClick,
        TextInfo,
        Warning,
        CrystalBroke,
        EnemyShoot
    }

    // [추가] 런타임 캐싱용 딕셔너리 및 연속 재생 필터링 변수
    private Dictionary<SFXType, AudioClip> sfxClips = new Dictionary<SFXType, AudioClip>();
    private AudioSource paintingAudioSource; // 붓칠 전용 오디오 소스 (중복 겹침 방지용)
    private float lastPaintingSoundTime = 0f;
    private const float PAINTING_SOUND_INTERVAL = 0.12f;

    private void Awake()
    {
        // 씬 전환 시 파괴되지 않는 싱글톤 구현
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadVolumeSettings(); // 저장된 볼륨 값 불러오기
            PreloadSFX();         // 효과음 미리 로드
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // [추가] 효과음 프리로드 함수
    private void PreloadSFX()
    {
        LoadAndCache(SFXType.Jump, "cheese_sound/jump");
        LoadAndCache(SFXType.PlayerHit, "cheese_sound/player_hit");
        LoadAndCache(SFXType.NoPaint, "cheese_sound/no_paint");
        LoadAndCache(SFXType.PaintRecover, "cheese_sound/paint_recover");
        LoadAndCache(SFXType.Painting, "cheese_sound/painting");
        LoadAndCache(SFXType.EnemyRecover, "cheese_sound/enemy_recover");
        LoadAndCache(SFXType.ButtonClick, "cheese_sound/button_click");
        LoadAndCache(SFXType.TextInfo, "cheese_sound/text_info");
        LoadAndCache(SFXType.Warning, "cheese_sound/warning");
        LoadAndCache(SFXType.CrystalBroke, "cheese_sound/crystal_broke");
        LoadAndCache(SFXType.EnemyShoot, "cheese_sound/enemy_shoot");
    }

    private void LoadAndCache(SFXType type, string resourcePath)
    {
        AudioClip clip = Resources.Load<AudioClip>(resourcePath);
        if (clip != null)
        {
            sfxClips[type] = clip;
        }
        else
        {
            Debug.LogWarning($"[SoundManager] 리소스 로드 실패: {resourcePath} (파일명을 확인해 주세요)");
        }
    }

    // 1. 마스터 볼륨 변경 (0.0 ~ 1.0)
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat("MasterVolume", masterVolume);
        UpdateVolume();
    }

    // 2. BGM 볼륨 변경 (0.0 ~ 1.0)
    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat("BGMVolume", bgmVolume);
        UpdateVolume();
    }

    // 3. SFX 볼륨 변경 (0.0 ~ 1.0)
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
        UpdateVolume();
    }

    // VolumeSettings 스크립트에서 저장된 값을 가져가기 위한 함수들
    public float GetMasterVolume() => masterVolume;
    public float GetBGMVolume() => bgmVolume;
    public float GetSFXVolume() => sfxVolume;

    // 실제 오디오 소스에 볼륨을 업데이트
    private void UpdateVolume()
    {
        if (bgmSource != null)
        {
            bgmSource.volume = bgmVolume * masterVolume;
        }
        if (sfxSource != null)
        {
            sfxSource.volume = sfxVolume * masterVolume;
        }
        if (paintingAudioSource != null)
        {
            paintingAudioSource.volume = sfxVolume * masterVolume;
        }
    }

    private void LoadVolumeSettings()
    {
        // 붓칠 전용 오디오 소스 생성 및 세팅
        if (paintingAudioSource == null)
        {
            paintingAudioSource = gameObject.AddComponent<AudioSource>();
            paintingAudioSource.playOnAwake = false;
            paintingAudioSource.loop = false;
        }

        if (!PlayerPrefs.HasKey("MasterVolume"))
        {
            masterVolume = 0.5f;
            bgmVolume = 0.5f;
            sfxVolume = 0.5f;

            PlayerPrefs.SetFloat("MasterVolume", masterVolume);
            PlayerPrefs.SetFloat("BGMVolume", bgmVolume);
            PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
            PlayerPrefs.Save();
        }
        else
        {
            masterVolume = PlayerPrefs.GetFloat("MasterVolume", 0.5f);
            bgmVolume = PlayerPrefs.GetFloat("BGMVolume", 0.5f);
            sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 0.5f);
        }

        UpdateVolume();
    }

    // ==========================================
    // [BGM 재생 기능]
    // ==========================================

    public void PlayBGM(AudioClip clip, bool loop = true)
    {
        if (bgmSource == null || clip == null) return;
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.Play();
    }

    public void PlayBGM(string bgmFileName, bool loop = true)
    {
        AudioClip clip = Resources.Load<AudioClip>($"SoundResource/{bgmFileName}");
        if (clip == null)
        {
            Debug.LogWarning($"[SoundManager] BGM 파일을 찾을 수 없습니다: Resources/SoundResource/{bgmFileName}");
            return;
        }
        PlayBGM(clip, loop);
    }

    public void StopBGM()
    {
        if (bgmSource != null)
        {
            bgmSource.Stop();
        }
    }

    // ==========================================
    // [SFX 재생 기능]
    // ==========================================

    public void PlaySFX(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip);
    }

    /// <summary>
    /// [신규] 특정 시작 오프셋 시간(startTime)부터 오디오 클립을 재생합니다. (앞부분 자르기 대응)
    /// </summary>
    public void PlaySFX(AudioClip clip, float startTime)
    {
        PlaySFXWithOffset(clip, startTime, 1.0f);
    }

    /// <summary>
    /// [신규] 특정 시작 오프셋 및 볼륨 배율로 오디오 클립을 재생하는 헬퍼 메서드
    /// </summary>
    public void PlaySFXWithOffset(AudioClip clip, float startTime, float volumeMultiplier = 1.0f)
    {
        if (clip == null) return;

        GameObject tempGO = new GameObject("TempSFX_" + clip.name);
        AudioSource tempSource = tempGO.AddComponent<AudioSource>();

        tempSource.clip = clip;
        // 마스터 볼륨 및 사운드매니저 효과음 볼륨을 종합하여 반영
        tempSource.volume = volumeMultiplier * sfxVolume * masterVolume;
        tempSource.time = Mathf.Clamp(startTime, 0f, clip.length - 0.01f);
        tempSource.Play();

        // 재생이 종료되면 임시 게임오브젝트 자동 소멸
        Destroy(tempGO, clip.length - startTime + 0.2f);
    }

    public void PlaySFX(string sfxFileName)
    {
        AudioClip clip = Resources.Load<AudioClip>($"SoundResource/{sfxFileName}");
        if (clip == null)
        {
            Debug.LogWarning($"[SoundManager] SFX 파일을 찾을 수 없습니다: Resources/SoundResource/{sfxFileName}");
            return;
        }
        PlaySFX(clip);
    }

    /// <summary>
    /// [추가] Enum 타입을 이용한 2D 효과음 단발성 재생
    /// </summary>
    public void PlaySFX(SFXType type, float volume = 1.0f)
    {
        if (!sfxClips.ContainsKey(type)) return;

        AudioClip clip = sfxClips[type];
 
        // 붓칠(Painting) 소리는 소리가 끝나기 약 0.15초 전에 다음 소리가 살짝 겹쳐서 흘러나오도록
        // 쿨타임 간격을 사운드 클립 길이에 비례해 동적으로 계산합니다.
        if (type == SFXType.Painting && clip != null)
        {
            float dynamicInterval = Mathf.Max(0.05f, clip.length - 0.15f);
            if (Time.time - lastPaintingSoundTime < dynamicInterval) return;
            lastPaintingSoundTime = Time.time;
        }
 
        if (clip != null)
        {
            // [수정] ButtonClick 타입 효과음의 경우, 항상 앞부분 0.03초를 자르고 재생 (즉각적 반응감 극대화)
            if (type == SFXType.ButtonClick)
            {
                PlaySFXWithOffset(clip, 0.03f, volume);
            }
            else if (sfxSource != null)
            {
                // SoundManager 고유의 SFX 볼륨 비율을 함께 감안하여 볼륨 결정
                float finalVolume = volume * sfxVolume * masterVolume;
                sfxSource.PlayOneShot(clip, finalVolume);
            }
        }
    }

    /// <summary>
    /// [추가] 3D 공간 효과음 재생
    /// </summary>
    public void PlaySFXAtPoint(SFXType type, Vector3 worldPosition, float volume = 1.0f)
    {
        if (!sfxClips.ContainsKey(type)) return;

        AudioClip clip = sfxClips[type];
        if (clip != null)
        {
            float finalVolume = volume * sfxVolume * masterVolume;
            AudioSource.PlayClipAtPoint(clip, worldPosition, finalVolume);
        }
    }

    /// <summary>
    /// [추가] 캐싱된 오디오 클립을 외부로 가져오기 위한 헬퍼 게터
    /// </summary>
    public AudioClip GetCachedClip(SFXType type)
    {
        if (sfxClips.ContainsKey(type)) return sfxClips[type];
        return null;
    }
}
