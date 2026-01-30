using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Gestionnaire audio central.
/// Gere musique, SFX, ambiance et mixage dynamique.
/// Style: Transitions douces inspirees de Genshin Impact.
/// </summary>
public class AudioManager : MonoBehaviour
{
    #region Singleton

    private static AudioManager _instance;
    public static AudioManager Instance
    {
        get => _instance;
        private set => _instance = value;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            SafeDestroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeAudioSources();
        LoadSettings();
    }

    private void SafeDestroy(UnityEngine.Object obj)
    {
        if (obj == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(obj);
        }
        else
        {
            Destroy(obj);
        }
#else
        Destroy(obj);
#endif
    }

    #endregion

    #region Constants

    private const int SFX_POOL_SIZE = 16;
    private const float DEFAULT_FADE_TIME = 1.5f;

    #endregion

    #region Fields

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer _audioMixer;
    [SerializeField] private string _masterVolumeParam = "MasterVolume";
    [SerializeField] private string _musicVolumeParam = "MusicVolume";
    [SerializeField] private string _sfxVolumeParam = "SFXVolume";
    [SerializeField] private string _ambientVolumeParam = "AmbientVolume";

    [Header("Sources")]
    [SerializeField] private AudioSource _musicSourceA;
    [SerializeField] private AudioSource _musicSourceB;
    [SerializeField] private AudioSource _ambientSource;

    [Header("Music Library")]
    [SerializeField] private MusicTrackData[] _musicTracks;

    [Header("SFX Library")]
    [SerializeField] private SFXData[] _sfxLibrary;

    [Header("Settings")]
    [SerializeField] private float _defaultCrossfadeTime = 2f;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = false;

    // Settings actuels
    private AudioSettingsData _settings;

    // Pool de sources SFX
    private List<AudioSource> _sfxPool;

    // Etat musique
    private AudioSource _activeMusicSource;
    private MusicTrackData _currentTrack;
    private Coroutine _musicFadeCoroutine;

    // Zones ambiantes actives
    private List<AmbientZoneData> _activeAmbientZones;

    #endregion

    #region Events

    /// <summary>Declenche lors du changement de piste.</summary>
    public event Action<MusicTrackData> OnMusicChanged;

    /// <summary>Declenche lors du changement de volume.</summary>
    public event Action<AudioChannel, float> OnVolumeChanged;

    #endregion

    #region Properties

    /// <summary>Piste musicale actuelle.</summary>
    public MusicTrackData CurrentTrack => _currentTrack;

    /// <summary>Musique en cours de lecture?</summary>
    public bool IsMusicPlaying => _activeMusicSource != null && _activeMusicSource.isPlaying;

    /// <summary>Settings audio actuels.</summary>
    public AudioSettingsData Settings => _settings;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        UpdateAmbientMixing();
    }

    #endregion

    #region Public Methods - Volume

    /// <summary>
    /// Definit le volume d'un canal.
    /// </summary>
    /// <param name="channel">Canal audio.</param>
    /// <param name="volume">Volume (0-1).</param>
    public void SetVolume(AudioChannel channel, float volume)
    {
        volume = Mathf.Clamp01(volume);

        switch (channel)
        {
            case AudioChannel.Master:
                _settings.masterVolume = volume;
                SetMixerVolume(_masterVolumeParam, volume);
                break;

            case AudioChannel.Music:
                _settings.musicVolume = volume;
                SetMixerVolume(_musicVolumeParam, volume);
                break;

            case AudioChannel.SFX:
                _settings.sfxVolume = volume;
                SetMixerVolume(_sfxVolumeParam, volume);
                break;

            case AudioChannel.Ambient:
                _settings.ambientVolume = volume;
                SetMixerVolume(_ambientVolumeParam, volume);
                break;

            case AudioChannel.Voice:
                _settings.voiceVolume = volume;
                break;

            case AudioChannel.UI:
                _settings.uiVolume = volume;
                break;
        }

        SaveSettings();
        OnVolumeChanged?.Invoke(channel, volume);
        Log($"Volume {channel}: {volume:P0}");
    }

    /// <summary>
    /// Obtient le volume d'un canal.
    /// </summary>
    /// <param name="channel">Canal audio.</param>
    /// <returns>Volume (0-1).</returns>
    public float GetVolume(AudioChannel channel)
    {
        return channel switch
        {
            AudioChannel.Master => _settings.masterVolume,
            AudioChannel.Music => _settings.musicVolume,
            AudioChannel.SFX => _settings.sfxVolume,
            AudioChannel.Ambient => _settings.ambientVolume,
            AudioChannel.Voice => _settings.voiceVolume,
            AudioChannel.UI => _settings.uiVolume,
            _ => 1f
        };
    }

    /// <summary>
    /// Mute/unmute un canal.
    /// </summary>
    /// <param name="channel">Canal.</param>
    /// <param name="muted">Mute?</param>
    public void SetMuted(AudioChannel channel, bool muted)
    {
        float volume = muted ? 0f : GetVolume(channel);
        string param = channel switch
        {
            AudioChannel.Master => _masterVolumeParam,
            AudioChannel.Music => _musicVolumeParam,
            AudioChannel.SFX => _sfxVolumeParam,
            AudioChannel.Ambient => _ambientVolumeParam,
            _ => null
        };

        if (param != null)
        {
            SetMixerVolume(param, muted ? 0f : volume);
        }
    }

    #endregion

    #region Public Methods - Music

    /// <summary>
    /// Joue une piste musicale.
    /// </summary>
    /// <param name="trackName">Nom de la piste.</param>
    /// <param name="transition">Type de transition.</param>
    /// <param name="fadeTime">Temps de transition.</param>
    public void PlayMusic(string trackName, MusicTransitionType transition = MusicTransitionType.Crossfade, float fadeTime = -1f)
    {
        var track = GetMusicTrack(trackName);
        if (track == null)
        {
            LogError($"Music track not found: {trackName}");
            return;
        }

        PlayMusic(track, transition, fadeTime);
    }

    /// <summary>
    /// Joue une piste musicale.
    /// </summary>
    /// <param name="track">Piste.</param>
    /// <param name="transition">Transition.</param>
    /// <param name="fadeTime">Temps de fade.</param>
    public void PlayMusic(MusicTrackData track, MusicTransitionType transition = MusicTransitionType.Crossfade, float fadeTime = -1f)
    {
        if (track == null || track.clip == null) return;

        if (fadeTime < 0) fadeTime = _defaultCrossfadeTime;

        // Arreter le fade en cours
        if (_musicFadeCoroutine != null)
        {
            StopCoroutine(_musicFadeCoroutine);
        }

        switch (transition)
        {
            case MusicTransitionType.Instant:
                PlayMusicInstant(track);
                break;

            case MusicTransitionType.Crossfade:
                _musicFadeCoroutine = StartCoroutine(CrossfadeMusic(track, fadeTime));
                break;

            case MusicTransitionType.FadeOutThenIn:
                _musicFadeCoroutine = StartCoroutine(FadeOutThenIn(track, fadeTime));
                break;

            case MusicTransitionType.Stinger:
                _musicFadeCoroutine = StartCoroutine(PlayStingerThenMusic(track));
                break;
        }

        _currentTrack = track;
        OnMusicChanged?.Invoke(track);
        Log($"Playing music: {track.trackName}");
    }

    /// <summary>
    /// Arrete la musique.
    /// </summary>
    /// <param name="fadeTime">Temps de fade out.</param>
    public void StopMusic(float fadeTime = -1f)
    {
        if (fadeTime < 0) fadeTime = DEFAULT_FADE_TIME;

        if (_musicFadeCoroutine != null)
        {
            StopCoroutine(_musicFadeCoroutine);
        }

        _musicFadeCoroutine = StartCoroutine(FadeOutMusic(fadeTime));
        Log("Music stopped");
    }

    /// <summary>
    /// Pause/resume la musique.
    /// </summary>
    /// <param name="paused">Pause?</param>
    public void PauseMusic(bool paused)
    {
        if (_activeMusicSource == null) return;

        if (paused)
            _activeMusicSource.Pause();
        else
            _activeMusicSource.UnPause();
    }

    #endregion

    #region Public Methods - SFX

    /// <summary>
    /// Joue un effet sonore.
    /// </summary>
    /// <param name="sfxName">Nom du SFX.</param>
    /// <param name="position">Position 3D (optionnel).</param>
    /// <returns>Source audio utilisee.</returns>
    public AudioSource PlaySFX(string sfxName, Vector3? position = null)
    {
        var sfx = GetSFXData(sfxName);
        if (sfx == null)
        {
            LogError($"SFX not found: {sfxName}");
            return null;
        }

        return PlaySFX(sfx, position);
    }

    /// <summary>
    /// Joue un effet sonore.
    /// </summary>
    /// <param name="sfx">Donnees SFX.</param>
    /// <param name="position">Position 3D.</param>
    /// <returns>Source audio.</returns>
    public AudioSource PlaySFX(SFXData sfx, Vector3? position = null)
    {
        if (sfx == null || sfx.clips == null || sfx.clips.Length == 0) return null;

        var source = GetAvailableSFXSource();
        if (source == null) return null;

        // Choisir un clip aleatoire
        var clip = sfx.clips[UnityEngine.Random.Range(0, sfx.clips.Length)];

        // Configurer la source
        source.clip = clip;
        source.volume = UnityEngine.Random.Range(sfx.volumeMin, sfx.volumeMax) * _settings.sfxVolume;
        source.pitch = UnityEngine.Random.Range(sfx.pitchMin, sfx.pitchMax);
        source.spatialBlend = sfx.is3D ? 1f : 0f;
        source.minDistance = sfx.minDistance;
        source.maxDistance = sfx.maxDistance;
        source.priority = sfx.priority;

        if (position.HasValue && sfx.is3D)
        {
            source.transform.position = position.Value;
        }

        source.Play();
        return source;
    }

    /// <summary>
    /// Joue un clip audio simple.
    /// </summary>
    /// <param name="clip">Clip audio.</param>
    /// <param name="volume">Volume.</param>
    /// <param name="pitch">Pitch.</param>
    public void PlayOneShot(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;

        var source = GetAvailableSFXSource();
        if (source == null) return;

        source.pitch = pitch;
        source.PlayOneShot(clip, volume * _settings.sfxVolume);
    }

    #endregion

    #region Public Methods - Ambient

    /// <summary>
    /// Joue un son d'ambiance.
    /// </summary>
    /// <param name="clip">Clip audio.</param>
    /// <param name="fadeTime">Temps de fade.</param>
    public void PlayAmbient(AudioClip clip, float fadeTime = 2f)
    {
        if (clip == null || _ambientSource == null) return;

        StartCoroutine(FadeAmbient(_ambientSource, clip, fadeTime));
    }

    /// <summary>
    /// Arrete l'ambiance.
    /// </summary>
    /// <param name="fadeTime">Temps de fade.</param>
    public void StopAmbient(float fadeTime = 2f)
    {
        if (_ambientSource == null) return;

        StartCoroutine(FadeOutSource(_ambientSource, fadeTime));
    }

    /// <summary>
    /// Entre dans une zone ambiante.
    /// </summary>
    /// <param name="zone">Zone.</param>
    public void EnterAmbientZone(AmbientZoneData zone)
    {
        if (zone == null) return;
        if (_activeAmbientZones == null) _activeAmbientZones = new List<AmbientZoneData>();

        if (!_activeAmbientZones.Contains(zone))
        {
            _activeAmbientZones.Add(zone);
            UpdateAmbientMixing();
            Log($"Entered ambient zone: {zone.zoneName}");
        }
    }

    /// <summary>
    /// Quitte une zone ambiante.
    /// </summary>
    /// <param name="zone">Zone.</param>
    public void ExitAmbientZone(AmbientZoneData zone)
    {
        if (zone == null || _activeAmbientZones == null) return;

        if (_activeAmbientZones.Remove(zone))
        {
            UpdateAmbientMixing();
            Log($"Exited ambient zone: {zone.zoneName}");
        }
    }

    #endregion

    #region Public Methods - Dynamic Mixing

    /// <summary>
    /// Applique un snapshot de mixage.
    /// </summary>
    /// <param name="snapshotName">Nom du snapshot.</param>
    /// <param name="transitionTime">Temps de transition.</param>
    public void ApplyMixerSnapshot(string snapshotName, float transitionTime = 1f)
    {
        if (_audioMixer == null) return;

        var snapshot = _audioMixer.FindSnapshot(snapshotName);
        if (snapshot != null)
        {
            snapshot.TransitionTo(transitionTime);
            Log($"Applied mixer snapshot: {snapshotName}");
        }
    }

    /// <summary>
    /// Duck (baisse) un canal pour mettre en valeur un autre.
    /// </summary>
    /// <param name="channel">Canal a duck.</param>
    /// <param name="amount">Montant (0-1).</param>
    /// <param name="duration">Duree.</param>
    public void DuckChannel(AudioChannel channel, float amount, float duration)
    {
        StartCoroutine(DuckChannelCoroutine(channel, amount, duration));
    }

    #endregion

    #region Private Methods - Initialization

    private void InitializeAudioSources()
    {
        // Creer les sources music si necessaires
        if (_musicSourceA == null)
        {
            _musicSourceA = CreateAudioSource("Music_A");
            _musicSourceA.loop = true;
        }
        if (_musicSourceB == null)
        {
            _musicSourceB = CreateAudioSource("Music_B");
            _musicSourceB.loop = true;
        }
        if (_ambientSource == null)
        {
            _ambientSource = CreateAudioSource("Ambient");
            _ambientSource.loop = true;
        }

        _activeMusicSource = _musicSourceA;

        // Pool SFX
        _sfxPool = new List<AudioSource>();
        for (int i = 0; i < SFX_POOL_SIZE; i++)
        {
            var source = CreateAudioSource($"SFX_{i}");
            _sfxPool.Add(source);
        }

        _settings = new AudioSettingsData();
        _activeAmbientZones = new List<AmbientZoneData>();
    }

    private AudioSource CreateAudioSource(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        return go.AddComponent<AudioSource>();
    }

    private void LoadSettings()
    {
        _settings.masterVolume = PlayerPrefs.GetFloat("Audio_Master", 1f);
        _settings.musicVolume = PlayerPrefs.GetFloat("Audio_Music", 0.8f);
        _settings.sfxVolume = PlayerPrefs.GetFloat("Audio_SFX", 1f);
        _settings.ambientVolume = PlayerPrefs.GetFloat("Audio_Ambient", 0.7f);
        _settings.voiceVolume = PlayerPrefs.GetFloat("Audio_Voice", 1f);
        _settings.uiVolume = PlayerPrefs.GetFloat("Audio_UI", 0.8f);

        // Appliquer
        SetMixerVolume(_masterVolumeParam, _settings.masterVolume);
        SetMixerVolume(_musicVolumeParam, _settings.musicVolume);
        SetMixerVolume(_sfxVolumeParam, _settings.sfxVolume);
        SetMixerVolume(_ambientVolumeParam, _settings.ambientVolume);
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat("Audio_Master", _settings.masterVolume);
        PlayerPrefs.SetFloat("Audio_Music", _settings.musicVolume);
        PlayerPrefs.SetFloat("Audio_SFX", _settings.sfxVolume);
        PlayerPrefs.SetFloat("Audio_Ambient", _settings.ambientVolume);
        PlayerPrefs.SetFloat("Audio_Voice", _settings.voiceVolume);
        PlayerPrefs.SetFloat("Audio_UI", _settings.uiVolume);
        PlayerPrefs.Save();
    }

    #endregion

    #region Private Methods - Music

    private void PlayMusicInstant(MusicTrackData track)
    {
        if (_activeMusicSource != null)
        {
            _activeMusicSource.Stop();
        }

        _activeMusicSource = _musicSourceA;
        _activeMusicSource.clip = track.clip;
        _activeMusicSource.volume = track.baseVolume * _settings.musicVolume;
        _activeMusicSource.loop = track.loop;
        _activeMusicSource.Play();
    }

    private IEnumerator CrossfadeMusic(MusicTrackData track, float fadeTime)
    {
        var oldSource = _activeMusicSource;
        var newSource = _activeMusicSource == _musicSourceA ? _musicSourceB : _musicSourceA;

        // Configurer nouvelle source
        newSource.clip = track.clip;
        newSource.volume = 0f;
        newSource.loop = track.loop;
        if (track.loop && track.loopStartTime > 0)
        {
            newSource.time = track.loopStartTime;
        }
        newSource.Play();

        float targetVolume = track.baseVolume * _settings.musicVolume;
        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / fadeTime;

            newSource.volume = Mathf.Lerp(0f, targetVolume, t);
            if (oldSource != null && oldSource.isPlaying)
            {
                oldSource.volume = Mathf.Lerp(targetVolume, 0f, t);
            }

            yield return null;
        }

        newSource.volume = targetVolume;
        if (oldSource != null)
        {
            oldSource.Stop();
            oldSource.volume = 0f;
        }

        _activeMusicSource = newSource;
    }

    private IEnumerator FadeOutThenIn(MusicTrackData track, float fadeTime)
    {
        float halfTime = fadeTime / 2f;

        // Fade out
        yield return StartCoroutine(FadeOutMusic(halfTime));

        // Fade in
        _activeMusicSource.clip = track.clip;
        _activeMusicSource.Play();

        float targetVolume = track.baseVolume * _settings.musicVolume;
        float elapsed = 0f;

        while (elapsed < halfTime)
        {
            elapsed += Time.unscaledDeltaTime;
            _activeMusicSource.volume = Mathf.Lerp(0f, targetVolume, elapsed / halfTime);
            yield return null;
        }

        _activeMusicSource.volume = targetVolume;
    }

    private IEnumerator PlayStingerThenMusic(MusicTrackData track)
    {
        // Jouer le stinger si disponible
        if (track.stingerClip != null)
        {
            var stingerSource = GetAvailableSFXSource();
            if (stingerSource != null)
            {
                stingerSource.PlayOneShot(track.stingerClip);
                yield return new WaitForSeconds(track.stingerClip.length);
            }
        }

        // Puis la musique
        PlayMusicInstant(track);
    }

    private IEnumerator FadeOutMusic(float fadeTime)
    {
        if (_activeMusicSource == null || !_activeMusicSource.isPlaying) yield break;

        float startVolume = _activeMusicSource.volume;
        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            _activeMusicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeTime);
            yield return null;
        }

        _activeMusicSource.Stop();
        _activeMusicSource.volume = 0f;
        _currentTrack = null;
    }

    private MusicTrackData GetMusicTrack(string name)
    {
        if (_musicTracks == null) return null;

        foreach (var track in _musicTracks)
        {
            if (track.trackName == name) return track;
        }

        return null;
    }

    #endregion

    #region Private Methods - SFX

    private AudioSource GetAvailableSFXSource()
    {
        if (_sfxPool == null) return null;

        foreach (var source in _sfxPool)
        {
            if (!source.isPlaying) return source;
        }

        // Toutes occupees - voler la moins prioritaire
        AudioSource lowest = _sfxPool[0];
        foreach (var source in _sfxPool)
        {
            if (source.priority > lowest.priority)
            {
                lowest = source;
            }
        }

        lowest.Stop();
        return lowest;
    }

    private SFXData GetSFXData(string name)
    {
        if (_sfxLibrary == null) return null;

        foreach (var sfx in _sfxLibrary)
        {
            if (sfx.sfxName == name) return sfx;
        }

        return null;
    }

    #endregion

    #region Private Methods - Ambient

    private void UpdateAmbientMixing()
    {
        // TODO: Mixer les zones ambiantes actives selon la distance/priorite
    }

    private IEnumerator FadeAmbient(AudioSource source, AudioClip clip, float fadeTime)
    {
        // Fade out ancien
        if (source.isPlaying)
        {
            yield return StartCoroutine(FadeOutSource(source, fadeTime / 2f));
        }

        // Fade in nouveau
        source.clip = clip;
        source.Play();

        float targetVolume = _settings.ambientVolume;
        float elapsed = 0f;

        while (elapsed < fadeTime / 2f)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(0f, targetVolume, elapsed / (fadeTime / 2f));
            yield return null;
        }

        source.volume = targetVolume;
    }

    private IEnumerator FadeOutSource(AudioSource source, float fadeTime)
    {
        float startVolume = source.volume;
        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeTime);
            yield return null;
        }

        source.Stop();
        source.volume = 0f;
    }

    #endregion

    #region Private Methods - Mixer

    private void SetMixerVolume(string parameter, float linearVolume)
    {
        if (_audioMixer == null) return;

        // Convertir en dB (-80dB a 0dB)
        float dB = linearVolume > 0.0001f ? Mathf.Log10(linearVolume) * 20f : -80f;
        _audioMixer.SetFloat(parameter, dB);
    }

    private IEnumerator DuckChannelCoroutine(AudioChannel channel, float amount, float duration)
    {
        float originalVolume = GetVolume(channel);
        float duckedVolume = originalVolume * (1f - amount);

        SetVolume(channel, duckedVolume);

        yield return new WaitForSeconds(duration);

        // Fade back
        float elapsed = 0f;
        float fadeTime = 0.5f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float vol = Mathf.Lerp(duckedVolume, originalVolume, elapsed / fadeTime);
            SetVolume(channel, vol);
            yield return null;
        }

        SetVolume(channel, originalVolume);
    }

    #endregion

    #region Private Methods - Utility

    private void Log(string message)
    {
        if (_debugMode)
        {
            Debug.Log($"[Audio] {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[Audio] {message}");
    }

    #endregion
}

/// <summary>
/// Canaux audio.
/// </summary>
public enum AudioChannel
{
    Master,
    Music,
    SFX,
    Ambient,
    Voice,
    UI
}

/// <summary>
/// Settings audio.
/// </summary>
[System.Serializable]
public class AudioSettingsData
{
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.8f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float ambientVolume = 0.7f;
    [Range(0f, 1f)] public float voiceVolume = 1f;
    [Range(0f, 1f)] public float uiVolume = 0.8f;
}

/// <summary>
/// Types de transition musicale.
/// </summary>
public enum MusicTransitionType
{
    /// <summary>Changement instantane.</summary>
    Instant,

    /// <summary>Fondu croise.</summary>
    Crossfade,

    /// <summary>Fade out puis fade in.</summary>
    FadeOutThenIn,

    /// <summary>Joue un stinger puis la musique.</summary>
    Stinger
}

/// <summary>
/// Donnees d'une piste musicale.
/// </summary>
[System.Serializable]
public class MusicTrackData
{
    [Tooltip("Nom de la piste")]
    public string trackName;

    [Tooltip("Clip audio principal")]
    public AudioClip clip;

    [Tooltip("Volume de base")]
    [Range(0f, 1f)]
    public float baseVolume = 1f;

    [Tooltip("Boucler la piste?")]
    public bool loop = true;

    [Tooltip("Point de boucle (secondes)")]
    public float loopStartTime = 0f;

    [Tooltip("BPM de la piste (pour sync)")]
    public float bpm = 120f;

    [Tooltip("Clip stinger (intro)")]
    public AudioClip stingerClip;

    [Tooltip("Categorie musicale")]
    public MusicCategory category;
}

/// <summary>
/// Categories musicales.
/// </summary>
public enum MusicCategory
{
    Exploration,
    Combat,
    Boss,
    Town,
    Cinematic,
    Menu
}

/// <summary>
/// Donnees d'un effet sonore.
/// </summary>
[System.Serializable]
public class SFXData
{
    [Tooltip("Nom du SFX")]
    public string sfxName;

    [Tooltip("Clips audio (choix aleatoire)")]
    public AudioClip[] clips;

    [Header("Volume")]
    [Range(0f, 1f)]
    public float volumeMin = 0.9f;
    [Range(0f, 1f)]
    public float volumeMax = 1f;

    [Header("Pitch")]
    [Range(0.5f, 2f)]
    public float pitchMin = 0.95f;
    [Range(0.5f, 2f)]
    public float pitchMax = 1.05f;

    [Header("3D Audio")]
    public bool is3D = false;
    public float minDistance = 1f;
    public float maxDistance = 50f;

    [Header("Priority")]
    [Range(0, 256)]
    public int priority = 128;
}

/// <summary>
/// Zone ambiante.
/// </summary>
[System.Serializable]
public class AmbientZoneData
{
    public string zoneName;
    public AudioClip ambientClip;
    public float baseVolume = 1f;
    public float blendDistance = 10f;
    public int priority = 0;
}
