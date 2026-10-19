using System.Collections.Generic;
using UnityEngine;

// Plays area music and combat music, crossfading between them.
// Uses two AudioSources: while one fades in, the other fades out.
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Tracks")]
    public AudioClip combatMusic;

    [Header("Timing")]
    public float areaFadeTime = 2f;       // fade length between area tracks, and back out of combat
    public float combatFadeTime = 0.6f;   // fade length into combat (quick, so it hits)
    public float combatLingerTime = 3f;   // combat music keeps playing this long after the last fight event

    [Range(0f, 1f)] public float musicVolume = 0.6f;

    readonly AudioSource[] sources = new AudioSource[2];
    readonly List<MusicArea> areas = new List<MusicArea>(); // areas the player is currently inside
    int active;              // which source holds the current track
    AudioClip playing;       // the current track (null = silence)
    float combatTimer;

    void Awake()
    {
        Instance = this;

        for (int i = 0; i < sources.Length; i++)
        {
            sources[i] = gameObject.AddComponent<AudioSource>();
            sources[i].loop = true;
            sources[i].playOnAwake = false;
            sources[i].spatialBlend = 0f; // 2D: music sounds the same everywhere
            sources[i].volume = 0f;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---- Called by other scripts ----

    public void EnterArea(MusicArea area)
    {
        if (areas.Contains(area)) return;

        areas.Add(area);
        string track = area.music != null ? area.music.name : "silence";
        Announce($"AREA: entered {area.name} ({track})");
    }

    public void ExitArea(MusicArea area)
    {
        if (areas.Remove(area))
            Announce($"AREA: left {area.name}");
    }

    // Call when something fight-related happens; restarts the 3 second linger timer
    public void TriggerCombat()
    {
        combatTimer = combatLingerTime;
    }

    // ---- Internals ----

    void Update()
    {
        // While any enemy is hunting us, combat music stays on
        foreach (Enemy enemy in Enemy.All)
        {
            if (enemy.IsAlerted)
            {
                combatTimer = combatLingerTime;
                break;
            }
        }
        combatTimer = Mathf.Max(0f, combatTimer - Time.deltaTime);

        // What SHOULD be playing right now?
        AudioClip desired = (combatTimer > 0f && combatMusic != null) ? combatMusic : CurrentAreaClip();
        if (desired != playing) SwitchTo(desired);

        // Fade every source toward its target volume
        float fadeTime = (playing != null && playing == combatMusic) ? combatFadeTime : areaFadeTime;
        float step = musicVolume / Mathf.Max(0.01f, fadeTime) * Time.deltaTime;

        for (int i = 0; i < sources.Length; i++)
        {
            float target = (i == active && playing != null) ? musicVolume : 0f;
            sources[i].volume = Mathf.MoveTowards(sources[i].volume, target, step);

            // Fully faded out? Stop it so it isn't playing silently
            if (target == 0f && sources[i].volume <= 0f && sources[i].isPlaying)
                sources[i].Stop();
        }
    }

    // Prints to the Console and to the on-screen readout, so you can see what's happening
    void Announce(string message)
    {
        Debug.Log(message);

        if (DamageReadout.Instance != null)
            DamageReadout.Instance.ShowMessage(message, DamageReadout.Instance.musicColor);
    }

    AudioClip CurrentAreaClip()
    {
        // If areas overlap, the most recently entered one wins
        return areas.Count > 0 ? areas[areas.Count - 1].music : null;
    }

    void SwitchTo(AudioClip clip)
    {
        playing = clip;

        string reason = clip == null ? "silence" : (clip == combatMusic ? "combat" : "area");
        Announce($"MUSIC: {(clip != null ? clip.name : "(none)")} [{reason}]");

        active = 1 - active; // the old track's source becomes the "fading out" one

        if (clip == null) return;

        sources[active].clip = clip;
        sources[active].volume = 0f; // start silent and fade in
        sources[active].Play();
    }
}
