using System.Collections.Generic;
using UnityEngine;

//Sonidos del jugador:
// - Pasos al caminar, correr y caminar agachado, sincronizados con el momento en que cada pie toca el suelo.
//   Los audios traen varias pisadas seguidas; al empezar se separan en pisadas sueltas automaticamente.
// - Golpe al aterrizar de un salto o una caida.
// - Respiracion agitada cuando el jugador se agota de correr.
// - Respiracion al contener el aire con el catalejo (Shift apuntando): inspira al empezar
//   y espira al soltar Shift o cuando se acaba el aire.
//Todos los audios se normalizan al cargarse, asi los volumenes del Inspector son comparables entre si.
public class PlayerAudio : MonoBehaviour
{
    public playerController player;
    public WeaponShooter shooter;

    [Header("Pasos")]
    [Tooltip("Audio con varias pisadas caminando")]
    public AudioClip stepSound;
    [Tooltip("Audio con varias pisadas corriendo")]
    public AudioClip runSound;
    [Range(0f, 1f)] public float walkVolume = 0.35f;
    [Range(0f, 1f)] public float runVolume = 0.5f;
    [Range(0f, 1f)] public float crouchVolume = 0.18f;
    [Range(0f, 1f)] public float landVolume = 0.55f;
    [Range(0f, 0.2f)] public float stepPitchVariation = 0.07f;
    [Tooltip("Momentos del ciclo de caminar/correr (0-1) en que cada pie toca el suelo")]
    public Vector2 walkContacts = new Vector2(0.43f, 0.93f);
    [Tooltip("Momentos del ciclo de caminar agachado (0-1) en que cada pie toca el suelo")]
    public Vector2 crouchContacts = new Vector2(0f, 0.5f);

    [Header("Respiracion")]
    public AudioClip breathFastSound;
    public AudioClip breathInSound;
    public AudioClip breathOutSound;
    [Range(0f, 1f)] public float breathFastVolume = 0.5f;
    [Range(0f, 1f)] public float aimBreathVolume = 0.45f;
    [Tooltip("La respiracion agitada se apaga cuando la resistencia vuelve a este valor")]
    [Range(0f, 1f)] public float breathFastStopAt = 0.7f;
    [Tooltip("Segundos de fundido al empezar y terminar la respiracion agitada")]
    public float breathFastFade = 1.2f;

    static readonly int LocomotionHash = Animator.StringToHash("BaseAnimation");
    static readonly int CrouchWalkHash = Animator.StringToHash("caminar_agachado");

    private Animator animator;
    private AudioSource stepSource;
    private AudioSource breathFastSource;
    private AudioSource aimBreathSource;

    private List<AudioClip> walkSteps = new List<AudioClip>();
    private List<AudioClip> runSteps = new List<AudioClip>();
    private AudioClip breathFast, breathIn, breathOut;
    private float breathInStart, breathOutStart;

    private float previousPhase = -1f;
    private int previousState;
    private int lastStep = -1;
    private bool wasGrounded = true;
    private float airTime;
    private bool panting;
    private bool wasHoldingBreath;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        stepSource = CreateSource("SonidoPasos", false);
        breathFastSource = CreateSource("SonidoRespiracionAgitada", true);
        aimBreathSource = CreateSource("SonidoRespiracionApuntar", false);

        walkSteps = SliceSteps(stepSound);
        runSteps = SliceSteps(runSound);
        if (runSteps.Count == 0) runSteps = walkSteps;
        breathFast = Normalize(breathFastSound);
        breathIn = Normalize(breathInSound);
        breathOut = Normalize(breathOutSound);
        breathInStart = SoundStart(breathIn);
        breathOutStart = SoundStart(breathOut);
        breathFastSource.clip = breathFast;
    }

    void Update()
    {
        if (player == null) return;
        UpdateFootsteps();
        UpdatePanting();
        UpdateAimBreath();
    }

    // ---------- Pasos ----------

    void UpdateFootsteps()
    {
        if (!player.IsGrounded)
        {
            airTime += Time.deltaTime;
            wasGrounded = false;
            previousPhase = -1f;
            return;
        }

        //Aterrizaje tras un salto o una caida (no por pequeños saltitos del CharacterController)
        if (!wasGrounded)
        {
            if (airTime > 0.3f) PlayStep(walkSteps, landVolume);
            wasGrounded = true;
        }
        airTime = 0f;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        bool crouchWalk = state.shortNameHash == CrouchWalkHash;
        bool locomotion = state.shortNameHash == LocomotionHash;
        if ((!crouchWalk && !locomotion) || player.MoveAmount < 0.2f || animator.IsInTransition(0))
        {
            previousPhase = -1f;
            return;
        }

        //Fase del ciclo de la animacion: suena una pisada cada vez que pasa por el contacto de un pie
        float phase = state.normalizedTime % 1f;
        Vector2 contacts = crouchWalk ? crouchContacts : walkContacts;
        if (previousPhase >= 0f && previousState == state.shortNameHash &&
            (Crossed(previousPhase, phase, contacts.x) || Crossed(previousPhase, phase, contacts.y)))
        {
            if (crouchWalk) PlayStep(walkSteps, crouchVolume);
            else if (player.IsRunning) PlayStep(runSteps, runVolume);
            else PlayStep(walkSteps, walkVolume);
        }
        previousPhase = phase;
        previousState = state.shortNameHash;
    }

    static bool Crossed(float from, float to, float point)
    {
        if (to >= from) return from < point && point <= to;
        return point > from || point <= to;   //la fase dio la vuelta (de 0.98 a 0.02)
    }

    void PlayStep(List<AudioClip> steps, float volume)
    {
        if (steps.Count == 0 || volume <= 0f) return;
        //Una pisada al azar, sin repetir la anterior
        int index = Random.Range(0, steps.Count);
        if (steps.Count > 1 && index == lastStep) index = (index + 1) % steps.Count;
        lastStep = index;
        stepSource.pitch = 1f + Random.Range(-stepPitchVariation, stepPitchVariation);
        stepSource.PlayOneShot(steps[index], volume);
    }

    // ---------- Respiracion agitada (cansancio) ----------

    void UpdatePanting()
    {
        if (breathFast == null) return;
        //Empieza al agotarse y sigue hasta haber recuperado buena parte de la resistencia
        panting = player.IsExhausted || (panting && player.Stamina < breathFastStopAt);

        float target = panting ? breathFastVolume : 0f;
        float step = breathFastVolume / Mathf.Max(breathFastFade, 0.01f) * Time.deltaTime;
        breathFastSource.volume = Mathf.MoveTowards(breathFastSource.volume, target, step);

        if (panting && !breathFastSource.isPlaying)
        {
            breathFastSource.time = Random.Range(0f, breathFast.length * 0.5f);
            breathFastSource.Play();
        }
        else if (!panting && breathFastSource.isPlaying && breathFastSource.volume <= 0f)
        {
            breathFastSource.Stop();
        }
    }

    // ---------- Contener la respiracion con el catalejo ----------

    //Apuntando con el catalejo, Shift contiene el aire para estabilizar la mira:
    //al empezar suena la inspiracion y al soltar Shift (o quedarse sin aire) la espiracion
    void UpdateAimBreath()
    {
        bool holding = shooter != null && shooter.HoldingBreath;
        if (holding == wasHoldingBreath) return;
        wasHoldingBreath = holding;

        AudioClip clip = holding ? breathIn : breathOut;
        if (clip == null) return;
        aimBreathSource.Stop();
        aimBreathSource.clip = clip;
        aimBreathSource.volume = aimBreathVolume;
        aimBreathSource.time = holding ? breathInStart : breathOutStart;
        aimBreathSource.Play();
    }

    // ---------- Utilidades de audio ----------

    AudioSource CreateSource(string sourceName, bool loop)
    {
        var go = new GameObject(sourceName);
        go.transform.SetParent(transform, false);
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        if (loop) source.volume = 0f;
        return source;
    }

    static float[] ReadSamples(AudioClip clip)
    {
        var data = new float[clip.samples * clip.channels];
        clip.GetData(data, 0);
        return data;
    }

    static float Peak(float[] data)
    {
        float peak = 0f;
        for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        return peak;
    }

    //Copia del audio con el pico a 0.95 (algunos audios vienen muy bajos)
    static AudioClip Normalize(AudioClip clip)
    {
        if (clip == null) return null;
        float[] data = ReadSamples(clip);
        float peak = Peak(data);
        if (peak <= 0.0001f) return clip;
        float gain = 0.95f / peak;
        for (int i = 0; i < data.Length; i++) data[i] *= gain;
        var result = AudioClip.Create(clip.name + "_normalizado", clip.samples, clip.channels, clip.frequency, false);
        result.SetData(data, 0);
        return result;
    }

    //Segundo en el que empieza a oirse el audio (para saltar el silencio inicial)
    static float SoundStart(AudioClip clip)
    {
        if (clip == null) return 0f;
        float[] data = ReadSamples(clip);
        float threshold = Peak(data) * 0.05f;
        for (int i = 0; i < data.Length; i++)
        {
            if (Mathf.Abs(data[i]) > threshold)
                return Mathf.Max(0f, (float)(i / clip.channels) / clip.frequency - 0.03f);
        }
        return 0f;
    }

    //Separa un audio con varias pisadas en pisadas sueltas: busca los picos de volumen
    //(separados al menos 0.18 s) y corta un trozo corto alrededor de cada uno, normalizado.
    static List<AudioClip> SliceSteps(AudioClip clip)
    {
        var steps = new List<AudioClip>();
        if (clip == null) return steps;
        float[] data = ReadSamples(clip);
        int channels = clip.channels;
        int frame = clip.frequency / 100;                 //10 ms
        int frames = clip.samples / frame;
        float peak = Peak(data);
        if (peak <= 0.0001f || frames < 5) return steps;

        //Envolvente: volumen maximo de cada tramo de 10 ms
        var env = new float[frames];
        for (int f = 0; f < frames; f++)
        {
            int end = Mathf.Min(data.Length, (f + 1) * frame * channels);
            for (int i = f * frame * channels; i < end; i++) env[f] = Mathf.Max(env[f], Mathf.Abs(data[i]));
        }

        //Picos de al menos el 25 % del maximo, separados 18 tramos (0.18 s)
        var peaks = new List<int>();
        for (int f = 0; f < frames; f++)
        {
            if (env[f] < peak * 0.25f) continue;
            bool isMax = true;
            for (int k = Mathf.Max(0, f - 9); k <= Mathf.Min(frames - 1, f + 9); k++)
            {
                if (env[k] > env[f]) { isMax = false; break; }
            }
            if (isMax && (peaks.Count == 0 || f - peaks[peaks.Count - 1] >= 18)) peaks.Add(f);
        }

        float gain = 0.95f / peak;
        for (int p = 0; p < peaks.Count; p++)
        {
            int startFrame = Mathf.Max(0, peaks[p] - 5);                      //50 ms antes del golpe
            int endFrame = p + 1 < peaks.Count ? peaks[p + 1] - 6 : frames;  //hasta antes del siguiente
            endFrame = Mathf.Min(endFrame, startFrame + 40);                  //maximo 0.4 s
            int length = (endFrame - startFrame) * frame;
            if (length <= frame) continue;

            var slice = new float[length * channels];
            int offset = startFrame * frame * channels;
            int fadeIn = frame / 2, fadeOut = frame * 4;
            for (int s = 0; s < length; s++)
            {
                float fade = Mathf.Min(1f, Mathf.Min((float)s / fadeIn, (float)(length - s) / fadeOut));
                for (int c = 0; c < channels; c++)
                {
                    int src = offset + s * channels + c;
                    slice[s * channels + c] = src < data.Length ? data[src] * gain * fade : 0f;
                }
            }
            var stepClip = AudioClip.Create(clip.name + "_pisada" + (p + 1), length, channels, clip.frequency, false);
            stepClip.SetData(slice, 0);
            steps.Add(stepClip);
        }
        return steps;
    }

    //Numero de pisadas detectadas en cada audio (para comprobarlo desde el editor)
    public int WalkStepCount => walkSteps.Count;
    public int RunStepCount => runSteps.Count;
}
