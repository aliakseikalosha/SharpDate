# Sound

The sound engine: file and sample playback, synthesizers, sequencing, mixing and effects. Full member
list: [Sound.md](../api/Sound.md).

All sound objects are handle structs: copying one copies the handle, and you `Dispose()` what you
create. Volumes and levels are 0-1, times are seconds unless a parameter says samples (44,100 per
second), and pitches are Hz or MIDI note numbers (60 = middle C). Everything plays on the default
channel unless you add it to another `SoundChannel`.

| Type | Use it for | Lua |
|---|---|---|
| `FilePlayer` | music and long sounds, streamed from storage | `snd.fileplayer` |
| `AudioSample` + `SamplePlayer` | short sounds loaded into memory, played often | `snd.sample`, `snd.sampleplayer` |
| `Synth` | one synthesizer voice (waveform, sample, wavetable or custom) | `snd.synth` |
| `Instrument` | several voices for chords; plays sequence tracks | `snd.instrument` |
| `Envelope`, `LFO`, `ControlSignal`, `SynthSignal` | modulation sources (`SynthSignalValue`) | `snd.envelope`, `snd.lfo`, `snd.controlsignal` |
| `SoundSequence`, `SequenceTrack` | songs, usually from MIDI files | `snd.sequence`, `snd.track` |
| `SoundChannel` | a mixer bus with volume, pan and effects | `snd.channel` |
| `SoundEffect` | filters, bitcrusher, ring modulator, overdrive, delay, custom | `snd.twopolefilter` ... |
| `SoundSource` | what all players, synths and instruments convert to | `snd.source` |
| `Sound` | the engine: default channel, custom sources, microphone, headphones | `playdate.sound` |

## FilePlayer: music

pdc compiles `.wav`/`.aif` in your Source folder to `.pda` (ADPCM); `.mp3` files are played as they are.
Paths have no extension.

```csharp
public sealed class MusicDemo : Game
{
    private FilePlayer _music;

    protected override void Initialize()
    {
        _music = FilePlayer.Load("music/theme");    // Source/music/theme.wav
        _music.SetVolume(0.8f);
        _music.SetLoopRange(4.0f, 36.0f);          // loop the middle part (seconds)
        _music.SetFinishCallback(() => Sys.Log("music stopped"));   // runs on the main thread
        _music.Play(0);                             // 0 = loop forever, 1 = once, n = n times
    }

    public override bool Update()
    {
        if (Sys.JustPressed(Buttons.B))
        {
            if (_music.IsPlaying) _music.Pause();
            else _music.Play(0);                    // resumes where it paused
        }
        if (Sys.JustPressed(Buttons.A))
            _music.FadeVolume(0, 0, 44100 * 2);     // fade out over two seconds (in samples)

        _music.Rate = 1 + Sys.CrankChange / 100;    // crank = turntable (rate can't be negative)

        Graphics.Clear(SolidColor.White);
        Graphics.DrawText("Position " + ((int)_music.Offset) + " / " + ((int)_music.Length) + " s", 10, 10);
        return true;
    }
}
```

## AudioSample and SamplePlayer: sound effects

A sample is loaded once into memory; any number of players can play it.

```csharp
public sealed class SfxDemo : Game
{
    private AudioSample _laserSample;
    private SamplePlayer _laser, _laserLow;

    protected override void Initialize()
    {
        _laserSample = AudioSample.Load("sfx/laser");            // Source/sfx/laser.wav
        _laser = SamplePlayer.Create(_laserSample);
        _laserLow = SamplePlayer.Create(_laserSample);           // same data, second player
    }

    public override bool Update()
    {
        if (Sys.JustPressed(Buttons.A))
            _laser.Play();                                       // once, normal speed
        if (Sys.JustPressed(Buttons.B))
            _laserLow.Play(1, 0.5f);                             // once, an octave lower
        if (Sys.JustPressed(Buttons.Up))
            _laser.Play(1, -1);                                  // backwards (PCM only; see Decompress)
        return false;
    }

    protected override void OnEvent(SystemEvent ev, uint arg)
    {
        if (ev == SystemEvent.Terminate)
        {
            _laser.Dispose();
            _laserLow.Dispose();
            _laserSample.Dispose();                              // after the players that use it
        }
    }
}
```

Samples compiled by pdc are ADPCM; call `sample.Decompress()` before playing them backwards or using
them in a synth. `SamplePlayer.Play(-1)` ping-pongs between the ends (or the `SetPlayRange` frames).

## Synth: making sounds from scratch

A synth plays one note at a time. Give it a waveform (`Square`, `Triangle`, `Sine`, `Noise`,
`Sawtooth`, or the Pocket Operator voices `POPhase`, `PODigital`, `POVosim`), shape its volume with
the ADSR envelope, and play notes by frequency or MIDI number. `length` -1 holds the note until
`NoteOff`.

```csharp
public sealed class SynthDemo : Game
{
    private Synth _lead, _drum;
    private LFO _vibrato;

    protected override void Initialize()
    {
        _lead = Synth.Create(SoundWaveform.Square);
        _lead.AttackTime = 0.01f;      // seconds
        _lead.DecayTime = 0.1f;
        _lead.SustainLevel = 0.6f;     // 0-1
        _lead.ReleaseTime = 0.3f;
        _lead.SetParameter(1, 0.25f);  // square wave parameter 1 = pulse width

        // Vibrato: an LFO on the frequency modulator (a value of 1 = one octave).
        _vibrato = LFO.Create(LFOType.Sine);
        _vibrato.Rate = 5;             // 5 Hz
        _vibrato.Depth = 0.02f;        // a quarter of a half step either way
        _lead.FrequencyModulator = _vibrato;

        _drum = Synth.Create(SoundWaveform.Noise);
        _drum.AttackTime = 0;
        _drum.DecayTime = 0.15f;
        _drum.SustainLevel = 0;        // a short burst
    }

    public override bool Update()
    {
        // Crank position picks the note: one octave over a full turn.
        float note = PD.NOTE_C4 + Sys.CrankAngle / 30;

        if (Sys.JustPressed(Buttons.A))
            _lead.PlayMIDINote(note, 0.8f);          // held until NoteOff
        if (Sys.JustReleased(Buttons.A))
            _lead.NoteOff();
        if (Sys.JustPressed(Buttons.B))
            _drum.PlayNote(200, 1, 0.1f);            // 200 Hz, full velocity, 0.1 s

        // Schedule a note exactly half a second from now on the audio clock.
        if (Sys.JustPressed(Buttons.Down))
            _lead.PlayNote(Sound.NoteToFrequency(72), 1, 0.2f, Sound.CurrentTime + 22050);
        return false;
    }
}
```

`Synth.SetSample(sample)` plays a (decompressed) sample at the note's pitch, looping a sustain range;
`SetWavetable` plays a wavetable; `SetGenerator` lets native callbacks render the audio.
`synth.Envelope` is the synth's own envelope for settings like `Curvature` and `Legato`; it belongs
to the synth (don't dispose it).

## Modulators: Envelope, LFO, ControlSignal, SynthSignal

Anything that takes a `SynthSignalValue` (frequency, amplitude, rate, parameters, channel volume and
pan, effect mix) can be driven by a modulator:

| Modulator | Output |
|---|---|
| `LFO` | a repeating wave: `Center` + wave x `Depth`, at `Rate` Hz; also arpeggios (`SetArpeggiation`) and sample-and-hold |
| `Envelope` | attack/decay/sustain/release, started by notes |
| `ControlSignal` | values at sequence steps (`AddEvent(step, value, interpolate)`) |
| `SynthSignal.ForValue(other)` | another signal with `SetValueScale` / `SetValueOffset` applied |
| `SynthSignal.Create(...)` | your own native callbacks |

```csharp
public sealed class ModulationDemo : Game
{
    protected override void Initialize()
    {
        Synth pad = Synth.Create(SoundWaveform.Sawtooth);

        // Tremolo: an LFO on the amplitude.
        LFO tremolo = LFO.Create(LFOType.Triangle);
        tremolo.Rate = 4;
        tremolo.Center = 0.7f;
        tremolo.Depth = 0.3f;
        pad.AmplitudeModulator = tremolo;

        // An arpeggio: the LFO steps through a major chord (half steps above the played note).
        LFO arp = LFO.Create(LFOType.Arpeggiator);
        arp.SetArpeggiation(new float[] { 0, 4, 7, 12 });
        arp.Rate = 8;                                   // steps per second
        Synth arpSynth = Synth.Create(SoundWaveform.Triangle);
        arpSynth.FrequencyModulator = arp;

        pad.PlayMIDINote(48, 0.5f, 4);                  // C3 for four seconds
        arpSynth.PlayMIDINote(60, 0.5f, 4);
        // These synths play for the rest of the game; dispose synths and LFOs when no longer needed.
    }

    public override bool Update() => false;
}
```

## Instrument: chords and polyphony

An instrument owns several synth voices and gives each note to a free one. `Synth.Copy()` makes extra
voices with the same settings.

```csharp
public sealed class ChordDemo : Game
{
    private Instrument _piano;

    protected override void Initialize()
    {
        Synth voice = Synth.Create(SoundWaveform.Triangle);
        voice.ReleaseTime = 0.5f;

        _piano = Instrument.Create();
        _piano.AddVoice(voice);                    // the instrument now owns the synth
        for (int i = 0; i < 3; i++)
            _piano.AddVoice(voice.Copy());         // four voices = four-note chords
        _piano.SetVolume(0.5f);
    }

    public override bool Update()
    {
        if (Sys.JustPressed(Buttons.A))
            foreach (float note in new float[] { 60, 64, 67, 72 })   // C major
                _piano.PlayMIDINote(note, 0.8f, 1.0f);
        _piano.PitchBend = Sys.IsPressed(Buttons.Up) ? 0.5f : 0;     // half of the bend range (12 half steps)
        return false;
    }
}
```

## SoundSequence: songs and MIDI files

A sequence has tracks; each track has notes (in steps) and an instrument. Tempo is in steps per
second. Every track needs an instrument before playing.

```csharp
public sealed class SongDemo : Game
{
    private SoundSequence _song, _beat;

    protected override void Initialize()
    {
        // 1. From a MIDI file in the pdx: give every track an instrument.
        _song = SoundSequence.Create();
        if (_song.LoadMIDIFile("music/song.mid"))
        {
            for (int i = 0; i < _song.TrackCount; i++)
            {
                Instrument instrument = Instrument.Create();
                Synth voice = Synth.Create(SoundWaveform.Square);
                instrument.AddVoice(voice);
                for (int v = 1; v < _song.GetTrack(i).Polyphony; v++)
                    instrument.AddVoice(voice.Copy());
                _song.GetTrack(i).Instrument = instrument;
            }
            _song.Play(() => Sys.Log("song finished"));
        }

        // 2. Built in code: a four-step drum loop.
        _beat = SoundSequence.Create();
        SequenceTrack drums = _beat.AddTrack();
        Instrument kit = Instrument.Create();
        kit.AddVoice(Synth.Create(SoundWaveform.Noise));
        drums.Instrument = kit;
        for (uint step = 0; step < 16; step += 4)
            drums.AddNoteEvent(step, 1, 40, step == 0 ? 1.0f : 0.6f);   // step, length, MIDI note, velocity
        _beat.Tempo = 8;                    // 8 steps per second
        _beat.SetLoops(0, 16, 0);           // loop steps 0-16 forever
        _beat.Play();
    }

    public override bool Update()
    {
        Graphics.Clear(SolidColor.White);
        Graphics.DrawText("Step " + _beat.CurrentStep, 10, 10);
        return true;
    }
}
```

`ControlSignal` sequences parameter changes: `track.GetSignalForController(7, true)` (MIDI controller 7
is volume) or `ControlSignal.Create()` used as a modulator, with `AddEvent(step, value, interpolate)`.

## SoundChannel and SoundEffect: mixing

A channel is a bus with its own volume, pan and effects. Create it, add it to the engine, then move
sources onto it. Effects: `TwoPoleFilter` (low/high/band-pass, notch, EQ, shelves), `OnePoleFilter`,
`BitCrusher`, `RingModulator`, `Overdrive`, `DelayLine` (with taps), or a custom native effect.
`Mix` sets wet/dry (0-1).

```csharp
public sealed class EffectsDemo : Game
{
    private SoundChannel _fx;
    private SoundEffect _filter, _delay;
    private Synth _synth;
    private bool _echo = true;

    protected override void Initialize()
    {
        _fx = SoundChannel.Create();
        Sound.AddChannel(_fx);

        _synth = Synth.Create(SoundWaveform.Sawtooth);
        _fx.AddSource(_synth);                                   // moves the synth to this channel

        _filter = SoundEffect.TwoPoleFilter(TwoPoleFilterType.LowPass, 800, 0.5f);   // 800 Hz, resonance 0.5
        _fx.AddEffect(_filter);

        // An echo: a quarter-second delay line, fed back at 40%, mixed in at 50%.
        _delay = SoundEffect.DelayLine(44100 / 4, false, 0.4f);
        _delay.Mix = 0.5f;
        _fx.AddEffect(_delay);

        _fx.Volume = 0.7f;
        _fx.Pan = -0.5f;                                         // a bit to the left
    }

    public override bool Update()
    {
        if (Sys.JustPressed(Buttons.A))
            _synth.PlayMIDINote(57, 1, 0.2f);
        if (Sys.JustPressed(Buttons.B))
        {
            _echo = !_echo;
            _delay.Mix = _echo ? 0.5f : 0;                       // 0 = effect out of the mix
        }
        return false;
    }

    protected override void OnEvent(SystemEvent ev, uint arg)
    {
        if (ev != SystemEvent.Terminate)
            return;
        _fx.RemoveEffect(_delay);                                // remove before disposing
        _fx.RemoveEffect(_filter);
        _delay.Dispose();
        _filter.Dispose();
        Sound.RemoveChannel(_fx);
        _fx.Dispose();
    }
}
```

`delay.AddDelayTap(samples)` returns a `SoundSource` that plays the delayed signal at another point;
add it to any channel. `channel.OutputAsSource` routes a whole channel into another one.

## Custom audio: sources, effects and the microphone

These callbacks run on the audio thread, 512 samples at a time (`PD.AUDIO_FRAMES_PER_CYCLE`). They must
be `[UnmanagedCallersOnly]` static methods, must be fast, and must not allocate managed objects or call
most of the API. Share data through static fields or native memory.

```csharp
public static unsafe class SineSource
{
    private static float s_phase;
    private static float s_frequency = 440;
    private static SoundSource s_source;

    public static void Start() => s_source = Sound.AddSource(&Render, null, false);   // mono

    public static void Stop()
    {
        Sound.RemoveSource(s_source);
        PD.System->realloc(s_source.Handle, 0);          // custom sources are freed like this
    }

    // Change the pitch from the game loop; the audio thread just reads the field.
    public static void SetFrequency(float hz) => s_frequency = hz;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Render(void* context, short* left, short* right, int length)
    {
        float step = s_frequency / 44100f;
        for (int i = 0; i < length; i++)
        {
            left[i] = (short)(MathF.Sin(s_phase * 2 * MathF.PI) * 8000);   // 16-bit samples
            s_phase += step;
            if (s_phase >= 1) s_phase -= 1;
        }
        return 1;                                        // 1 = produced sound, 0 = silent
    }
}
```

Recording: ask for permission, then set a microphone callback. It receives mono 16-bit samples and
returns 1 to keep recording.

```csharp
public sealed unsafe class MicDemo : Game
{
    private static int s_peak;

    protected override void Initialize()
    {
        Sound.RequestMicAccess("to measure how loud you are", allowed =>
        {
            if (allowed)
                Sound.SetMicCallback(&OnAudio, null, MicSource.Autodetect);
        });
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int OnAudio(void* context, short* samples, int length)
    {
        int peak = 0;
        for (int i = 0; i < length; i++)
            peak = Math.Max(peak, Math.Abs((int)samples[i]));
        s_peak = peak;                                   // read by the game loop
        return 1;                                        // keep recording
    }

    public override bool Update()
    {
        Graphics.Clear(SolidColor.White);
        Graphics.FillRect(10, 100, s_peak * 380 / 32768, 40, SolidColor.Black);
        return true;
    }
}
```

`SoundEffect.Create(process, userdata)` is a custom effect: `process` receives the channel's left and
right buffers in Q8.24 fixed point and returns 1 if it changed them.

## Engine settings

`Sound.CurrentTime` is the audio clock in samples (for scheduling notes). `Sound.GetHeadphoneState`
tells whether headphones and a headset mic are plugged in; `Sound.SetOutputsActive(headphone, speaker)`
forces the output. `Sound.NoteToFrequency` / `FrequencyToNote` convert between MIDI notes and Hz.
