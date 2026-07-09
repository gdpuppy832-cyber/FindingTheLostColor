using UnityEngine;

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

    private void Awake()
    {
        // 씬 전환 시 파괴되지 않는 싱글톤 구현
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadVolumeSettings(); // 저장된 볼륨 값 불러오기
        }
        else
        {
            Destroy(gameObject);
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
    }

    // [수정] 게임 생전 처음 구동 시에만 기본값 50%로 세팅하고, 그 이후 재부팅할 때는 유저가 마지막으로 수정한 값을 정상 로드합니다.
    private void LoadVolumeSettings()
    {
        // MasterVolume 키가 없다는 것은 게임을 처음 켠 상태를 의미합니다.
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
            // 이전에 설정된 볼륨이 이미 있다면 그 수치를 고스란히 불러옵니다.
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
}
