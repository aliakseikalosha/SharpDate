using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Playdate.Native;

namespace Playdate
{
    /// <summary>
    /// Wraps <c>pd->sound</c>. Sound objects are thin handles over native pointers; free the ones you
    /// create with <c>Dispose()</c>.
    /// </summary>
    /// <remarks>
    /// Audio callbacks (custom sources, effects, signals, mic input) run on the audio thread/interrupt.
    /// They must be <c>[UnmanagedCallersOnly]</c> functions and must not allocate managed objects.
    /// Finish/loop callbacks set through these wrappers are delivered on the main thread.
    /// </remarks>
    public static unsafe class Sound
    {
        internal static playdate_sound* Api => PD.Sound;

        /// <summary>Current audio time in samples (44.1 kHz).</summary>
        public static uint CurrentTime => Api->getCurrentTime();

        public static SoundChannel DefaultChannel => new SoundChannel(Api->getDefaultChannel());

        public static bool AddChannel(SoundChannel channel) => Api->addChannel(channel.Handle) != 0;
        public static bool RemoveChannel(SoundChannel channel) => Api->removeChannel(channel.Handle) != 0;

        /// <summary>Adds a custom audio source to the default channel (callback runs on the audio thread).</summary>
        public static SoundSource AddSource(delegate* unmanaged[Cdecl]<void*, short*, short*, int, int> callback, void* context, bool stereo)
            => new SoundSource(Api->addSource(callback, context, stereo ? 1 : 0));

        public static bool RemoveSource(SoundSource source) => Api->removeSource(source.Handle) != 0;

        /// <summary>Starts recording from the microphone (callback runs on the audio thread). Pass null to stop.</summary>
        public static int SetMicCallback(delegate* unmanaged[Cdecl]<void*, short*, int, int> callback, void* context, MicSource source = MicSource.Autodetect)
            => Api->setMicCallback(callback, context, source);

        public static void GetHeadphoneState(out bool headphone, out bool headsetMic)
        {
            int h, m;
            Api->getHeadphoneState(&h, &m, null);
            headphone = h != 0;
            headsetMic = m != 0;
        }

        public static void SetOutputsActive(bool headphone, bool speaker) => Api->setOutputsActive(headphone ? 1 : 0, speaker ? 1 : 0);

        public static string GetError() => Utf8.ToString(Api->getError());

        public static float NoteToFrequency(float note) => 440f * MathF.Pow(2f, (note - 69f) / 12f);
        public static float FrequencyToNote(float frequency) => 12f * MathF.Log2(frequency) - 36.376316562f;

        // Finish/loop callbacks keyed by source pointer (userdata is not needed).
        internal static readonly Dictionary<nint, Action> s_finishCallbacks = new Dictionary<nint, Action>();
        internal static readonly Dictionary<nint, Action> s_loopCallbacks = new Dictionary<nint, Action>();

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static void FinishTrampoline(Native.SoundSource* source, void* userdata)
        {
            if (s_finishCallbacks.TryGetValue((nint)source, out Action action))
                action();
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static void LoopTrampoline(Native.SoundSource* source, void* userdata)
        {
            if (s_loopCallbacks.TryGetValue((nint)source, out Action action))
                action();
        }

        internal static delegate* unmanaged[Cdecl]<Native.SoundSource*, void*, void> FinishProc(Action action)
            => action == null ? null : &FinishTrampoline;

        internal static delegate* unmanaged[Cdecl]<Native.SoundSource*, void*, void> LoopProc(Action action)
            => action == null ? null : &LoopTrampoline;

        internal static void SetCallback(Dictionary<nint, Action> table, void* source, Action action)
        {
            if (action == null)
                table.Remove((nint)source);
            else
                table[(nint)source] = action;
        }
    }

    /// <summary>Any object that produces audio: file/sample players, synths, instruments, delay taps.</summary>
    public readonly unsafe struct SoundSource
    {
        public readonly Native.SoundSource* Handle;

        public SoundSource(Native.SoundSource* handle) { Handle = handle; }

        public void SetVolume(float left, float right) => Sound.Api->source->setVolume(Handle, left, right);

        public void GetVolume(out float left, out float right)
        {
            float l, r;
            Sound.Api->source->getVolume(Handle, &l, &r);
            left = l;
            right = r;
        }

        public bool IsPlaying => Sound.Api->source->isPlaying(Handle) != 0;

        public void SetFinishCallback(Action callback)
        {
            Sound.SetCallback(Sound.s_finishCallbacks, Handle, callback);
            Sound.Api->source->setFinishCallback(Handle, Sound.FinishProc(callback), null);
        }
    }

    /// <summary>Streams audio from a file (<c>FilePlayer</c>): .pda, .wav, .mp3.</summary>
    public readonly unsafe struct FilePlayer : IDisposable
    {
        public readonly Native.FilePlayer* Handle;
        private static playdate_sound_fileplayer* Api => Sound.Api->fileplayer;

        public FilePlayer(Native.FilePlayer* handle) { Handle = handle; }

        public static FilePlayer Create() => new FilePlayer(Api->newPlayer());

        /// <summary>Creates a player and loads <paramref name="path"/>; reports an error if the file can't be opened.</summary>
        public static FilePlayer Load(string path)
        {
            FilePlayer player = Create();
            if (!player.LoadIntoPlayer(path))
                Sys.Error(string.Concat("Couldn't open audio file ", path));
            return player;
        }

        public bool LoadIntoPlayer(string path) => Api->loadIntoPlayer(Handle, Utf8.Arg(path)) != 0;

        public static implicit operator SoundSource(FilePlayer p) => new SoundSource((Native.SoundSource*)p.Handle);

        public void SetBufferLength(float seconds) => Api->setBufferLength(Handle, seconds);

        /// <summary>Plays the file <paramref name="repeat"/> times (0 = loop forever).</summary>
        public bool Play(int repeat = 1) => Api->play(Handle, repeat) != 0;
        public bool IsPlaying => Api->isPlaying(Handle) != 0;
        public void Pause() => Api->pause(Handle);
        public void Stop() => Api->stop(Handle);
        public void SetVolume(float left, float right) => Api->setVolume(Handle, left, right);
        public void SetVolume(float volume) => Api->setVolume(Handle, volume, volume);

        public void GetVolume(out float left, out float right)
        {
            float l, r;
            Api->getVolume(Handle, &l, &r);
            left = l;
            right = r;
        }

        /// <summary>Length of the file in seconds.</summary>
        public float Length => Api->getLength(Handle);

        public float Offset
        {
            get => Api->getOffset(Handle);
            set => Api->setOffset(Handle, value);
        }

        public float Rate
        {
            get => Api->getRate(Handle);
            set => Api->setRate(Handle, value);
        }

        public void SetLoopRange(float start, float end) => Api->setLoopRange(Handle, start, end);
        public bool DidUnderrun => Api->didUnderrun(Handle) != 0;
        public void SetStopOnUnderrun(bool flag) => Api->setStopOnUnderrun(Handle, flag ? 1 : 0);

        public void FadeVolume(float left, float right, int lengthInSamples) => Api->fadeVolume(Handle, left, right, lengthInSamples, null, null);

        public void SetFinishCallback(Action callback)
        {
            Sound.SetCallback(Sound.s_finishCallbacks, Handle, callback);
            Api->setFinishCallback(Handle, Sound.FinishProc(callback), null);
        }

        public void SetLoopCallback(Action callback)
        {
            Sound.SetCallback(Sound.s_loopCallbacks, Handle, callback);
            Api->setLoopCallback(Handle, Sound.LoopProc(callback), null);
        }

        public SynthSignalValue RateModulator
        {
            get => new SynthSignalValue(Api->getRateModulator(Handle));
            set => Api->setRateModulator(Handle, value.Handle);
        }

        public void Dispose()
        {
            if (Handle == null)
                return;
            Sound.s_finishCallbacks.Remove((nint)Handle);
            Sound.s_loopCallbacks.Remove((nint)Handle);
            Api->freePlayer(Handle);
        }
    }

    /// <summary>Audio data in memory (<c>AudioSample</c>).</summary>
    public readonly unsafe struct AudioSample : IDisposable
    {
        public readonly Native.AudioSample* Handle;
        private static playdate_sound_sample* Api => Sound.Api->sample;

        public AudioSample(Native.AudioSample* handle) { Handle = handle; }

        public bool IsNull => Handle == null;

        /// <summary>Loads a sound file (e.g. "piano" for piano.wav compiled to piano.pda).</summary>
        public static AudioSample Load(string path)
        {
            var sample = new AudioSample(Api->load(Utf8.Arg(path)));
            if (sample.IsNull)
                Sys.Error(string.Concat("Couldn't load audio sample ", path));
            return sample;
        }

        public static AudioSample Create(int byteCount) => new AudioSample(Api->newSampleBuffer(byteCount));

        /// <summary>Wraps raw PCM data; if <paramref name="shouldFreeData"/> the sample frees it with the Playdate allocator.</summary>
        public static AudioSample FromData(byte* data, SoundFormat format, uint sampleRate, int byteCount, bool shouldFreeData)
            => new AudioSample(Api->newSampleFromData(data, format, sampleRate, byteCount, shouldFreeData ? 1 : 0));

        public bool LoadInto(string path) => Api->loadIntoSample(Handle, Utf8.Arg(path)) != 0;

        public void GetData(out byte* data, out SoundFormat format, out uint sampleRate, out uint byteLength)
        {
            byte* d;
            SoundFormat f;
            uint sr, len;
            Api->getData(Handle, &d, &f, &sr, &len);
            data = d;
            format = f;
            sampleRate = sr;
            byteLength = len;
        }

        /// <summary>Length in seconds.</summary>
        public float Length => Api->getLength(Handle);

        /// <summary>Decompresses ADPCM data in place for faster playback.</summary>
        public bool Decompress() => Api->decompress(Handle) != 0;

        public void Dispose()
        {
            if (Handle != null)
                Api->freeSample(Handle);
        }
    }

    /// <summary>Plays an <see cref="AudioSample"/> (<c>SamplePlayer</c>).</summary>
    public readonly unsafe struct SamplePlayer : IDisposable
    {
        public readonly Native.SamplePlayer* Handle;
        private static playdate_sound_sampleplayer* Api => Sound.Api->sampleplayer;

        public SamplePlayer(Native.SamplePlayer* handle) { Handle = handle; }

        public static SamplePlayer Create() => new SamplePlayer(Api->newPlayer());

        public static SamplePlayer Create(AudioSample sample)
        {
            SamplePlayer player = Create();
            player.Sample = sample;
            return player;
        }

        public static implicit operator SoundSource(SamplePlayer p) => new SoundSource((Native.SoundSource*)p.Handle);

        public AudioSample Sample
        {
            set => Api->setSample(Handle, value.Handle);
        }

        /// <summary>Plays the sample <paramref name="repeat"/> times (0 = loop forever, negative = ping-pong) at <paramref name="rate"/>.</summary>
        public bool Play(int repeat = 1, float rate = 1) => Api->play(Handle, repeat, rate) != 0;
        public bool IsPlaying => Api->isPlaying(Handle) != 0;
        public void Stop() => Api->stop(Handle);
        public void SetVolume(float left, float right) => Api->setVolume(Handle, left, right);
        public void SetVolume(float volume) => Api->setVolume(Handle, volume, volume);

        public void GetVolume(out float left, out float right)
        {
            float l, r;
            Api->getVolume(Handle, &l, &r);
            left = l;
            right = r;
        }

        public float Length => Api->getLength(Handle);

        public float Offset
        {
            get => Api->getOffset(Handle);
            set => Api->setOffset(Handle, value);
        }

        public float Rate
        {
            get => Api->getRate(Handle);
            set => Api->setRate(Handle, value);
        }

        public void SetPlayRange(int start, int end) => Api->setPlayRange(Handle, start, end);
        public void SetPaused(bool paused) => Api->setPaused(Handle, paused ? 1 : 0);

        public void SetFinishCallback(Action callback)
        {
            Sound.SetCallback(Sound.s_finishCallbacks, Handle, callback);
            Api->setFinishCallback(Handle, Sound.FinishProc(callback), null);
        }

        public void SetLoopCallback(Action callback)
        {
            Sound.SetCallback(Sound.s_loopCallbacks, Handle, callback);
            Api->setLoopCallback(Handle, Sound.LoopProc(callback), null);
        }

        public SynthSignalValue RateModulator
        {
            get => new SynthSignalValue(Api->getRateModulator(Handle));
            set => Api->setRateModulator(Handle, value.Handle);
        }

        public void Dispose()
        {
            if (Handle == null)
                return;
            Sound.s_finishCallbacks.Remove((nint)Handle);
            Sound.s_loopCallbacks.Remove((nint)Handle);
            Api->freePlayer(Handle);
        }
    }

    /// <summary>A modulation input (LFO, envelope, control signal, ...): <c>PDSynthSignalValue</c>.</summary>
    public readonly unsafe struct SynthSignalValue
    {
        public readonly PDSynthSignalValue* Handle;

        public SynthSignalValue(PDSynthSignalValue* handle) { Handle = handle; }

        public bool IsNull => Handle == null;
    }

    /// <summary>A custom or derived signal (<c>PDSynthSignal</c>).</summary>
    public readonly unsafe struct SynthSignal : IDisposable
    {
        public readonly PDSynthSignal* Handle;
        private static playdate_sound_signal* Api => Sound.Api->signal;

        public SynthSignal(PDSynthSignal* handle) { Handle = handle; }

        /// <summary>Creates a signal driven by native callbacks (they run on the audio thread).</summary>
        public static SynthSignal Create(
            delegate* unmanaged[Cdecl]<void*, int*, float*, float> step,
            delegate* unmanaged[Cdecl]<void*, float, float, float, void> noteOn,
            delegate* unmanaged[Cdecl]<void*, int, int, void> noteOff,
            delegate* unmanaged[Cdecl]<void*, void> dealloc,
            void* userdata)
            => new SynthSignal(Api->newSignal(step, noteOn, noteOff, dealloc, userdata));

        /// <summary>Creates a signal that follows another signal value, with its own scale and offset.</summary>
        public static SynthSignal ForValue(SynthSignalValue value) => new SynthSignal(Api->newSignalForValue(value.Handle));

        public static implicit operator SynthSignalValue(SynthSignal s) => new SynthSignalValue((PDSynthSignalValue*)s.Handle);

        public float Value => Api->getValue(Handle);
        public void SetValueScale(float scale) => Api->setValueScale(Handle, scale);
        public void SetValueOffset(float offset) => Api->setValueOffset(Handle, offset);

        public void Dispose()
        {
            if (Handle != null)
                Api->freeSignal(Handle);
        }
    }

    /// <summary>Low-frequency oscillator (<c>PDSynthLFO</c>).</summary>
    public readonly unsafe struct LFO : IDisposable
    {
        public readonly PDSynthLFO* Handle;
        private static playdate_sound_lfo* Api => Sound.Api->lfo;

        public LFO(PDSynthLFO* handle) { Handle = handle; }

        public static LFO Create(LFOType type) => new LFO(Api->newLFO(type));

        public static implicit operator SynthSignalValue(LFO lfo) => new SynthSignalValue((PDSynthSignalValue*)lfo.Handle);

        public LFOType Type { set => Api->setType(Handle, value); }
        public float Rate { set => Api->setRate(Handle, value); }
        public float Phase { set => Api->setPhase(Handle, value); }
        public float StartPhase { set => Api->setStartPhase(Handle, value); }
        public float Center { set => Api->setCenter(Handle, value); }
        public float Depth { set => Api->setDepth(Handle, value); }
        public bool Retrigger { set => Api->setRetrigger(Handle, value ? 1 : 0); }
        public bool Global { set => Api->setGlobal(Handle, value ? 1 : 0); }
        public ushort RandomSeed { set => Api->setRandomSeed(Handle, value); }
        public float Value => Api->getValue(Handle);

        public void SetDelay(float holdoff, float rampTime) => Api->setDelay(Handle, holdoff, rampTime);

        /// <summary>Sets the steps (in half steps) for an arpeggiator LFO.</summary>
        public void SetArpeggiation(ReadOnlySpan<float> steps)
        {
            fixed (float* p = steps)
                Api->setArpeggiation(Handle, steps.Length, p);
        }

        /// <summary>Uses a native function to compute values (runs on the audio thread).</summary>
        public void SetFunction(delegate* unmanaged[Cdecl]<PDSynthLFO*, void*, float> function, void* userdata, bool interpolate)
            => Api->setFunction(Handle, function, userdata, interpolate ? 1 : 0);

        public void Dispose()
        {
            if (Handle != null)
                Api->freeLFO(Handle);
        }
    }

    /// <summary>ADSR envelope (<c>PDSynthEnvelope</c>).</summary>
    public readonly unsafe struct Envelope : IDisposable
    {
        public readonly PDSynthEnvelope* Handle;
        private static playdate_sound_envelope* Api => Sound.Api->envelope;

        public Envelope(PDSynthEnvelope* handle) { Handle = handle; }

        public static Envelope Create(float attack, float decay, float sustain, float release) => new Envelope(Api->newEnvelope(attack, decay, sustain, release));

        public static implicit operator SynthSignalValue(Envelope e) => new SynthSignalValue((PDSynthSignalValue*)e.Handle);

        public float Attack { set => Api->setAttack(Handle, value); }
        public float Decay { set => Api->setDecay(Handle, value); }
        public float Sustain { set => Api->setSustain(Handle, value); }
        public float Release { set => Api->setRelease(Handle, value); }
        public bool Legato { set => Api->setLegato(Handle, value ? 1 : 0); }
        public bool Retrigger { set => Api->setRetrigger(Handle, value ? 1 : 0); }
        public float Curvature { set => Api->setCurvature(Handle, value); }
        public float VelocitySensitivity { set => Api->setVelocitySensitivity(Handle, value); }
        public float Value => Api->getValue(Handle);

        public void SetRateScaling(float scaling, float startNote, float endNote) => Api->setRateScaling(Handle, scaling, startNote, endNote);

        public void Dispose()
        {
            if (Handle != null)
                Api->freeEnvelope(Handle);
        }
    }

    /// <summary>A synthesizer voice (<c>PDSynth</c>).</summary>
    public readonly unsafe struct Synth : IDisposable
    {
        public readonly PDSynth* Handle;
        private static playdate_sound_synth* Api => Sound.Api->synth;

        public Synth(PDSynth* handle) { Handle = handle; }

        public static Synth Create() => new Synth(Api->newSynth());

        public static Synth Create(SoundWaveform waveform)
        {
            Synth synth = Create();
            synth.Waveform = waveform;
            return synth;
        }

        public static implicit operator SoundSource(Synth s) => new SoundSource((Native.SoundSource*)s.Handle);

        public Synth Copy() => new Synth(Api->copy(Handle));

        public SoundWaveform Waveform { set => Api->setWaveform(Handle, value); }

        /// <summary>Plays a sample instead of a waveform; the sustain range loops while the note is held.</summary>
        public void SetSample(AudioSample sample, uint sustainStart = 0, uint sustainEnd = 0) => Api->setSample(Handle, sample.Handle, sustainStart, sustainEnd);

        public bool SetWavetable(AudioSample sample, int log2Size, int columns, int rows) => Api->setWavetable(Handle, sample.Handle, log2Size, columns, rows) != 0;

        /// <summary>Uses native callbacks to render audio (they run on the audio thread).</summary>
        public void SetGenerator(bool stereo,
            delegate* unmanaged[Cdecl]<void*, int*, int*, int, uint, int, int> render,
            delegate* unmanaged[Cdecl]<void*, float, float, float, void> noteOn,
            delegate* unmanaged[Cdecl]<void*, int, void> release,
            delegate* unmanaged[Cdecl]<void*, int, float, int> setParameter,
            delegate* unmanaged[Cdecl]<void*, void> dealloc,
            delegate* unmanaged[Cdecl]<void*, void*> copyUserdata,
            void* userdata)
            => Api->setGenerator(Handle, stereo ? 1 : 0, render, noteOn, release, setParameter, dealloc, copyUserdata, userdata);

        public float AttackTime { set => Api->setAttackTime(Handle, value); }
        public float DecayTime { set => Api->setDecayTime(Handle, value); }
        public float SustainLevel { set => Api->setSustainLevel(Handle, value); }
        public float ReleaseTime { set => Api->setReleaseTime(Handle, value); }
        public float Transpose { set => Api->setTranspose(Handle, value); }

        /// <summary>The synth's amplitude envelope (owned by the synth; do not free).</summary>
        public Envelope Envelope => new Envelope(Api->getEnvelope(Handle));

        public void ClearEnvelope() => Api->clearEnvelope(Handle);

        public SynthSignalValue FrequencyModulator
        {
            get => new SynthSignalValue(Api->getFrequencyModulator(Handle));
            set => Api->setFrequencyModulator(Handle, value.Handle);
        }

        public SynthSignalValue AmplitudeModulator
        {
            get => new SynthSignalValue(Api->getAmplitudeModulator(Handle));
            set => Api->setAmplitudeModulator(Handle, value.Handle);
        }

        public int ParameterCount => Api->getParameterCount(Handle);
        public bool SetParameter(int parameter, float value) => Api->setParameter(Handle, parameter, value) != 0;
        public void SetParameterModulator(int parameter, SynthSignalValue modulator) => Api->setParameterModulator(Handle, parameter, modulator.Handle);
        public SynthSignalValue GetParameterModulator(int parameter) => new SynthSignalValue(Api->getParameterModulator(Handle, parameter));

        /// <summary>Plays a note at <paramref name="frequency"/> Hz; <paramref name="length"/> -1 holds until <see cref="NoteOff"/>.</summary>
        public void PlayNote(float frequency, float velocity = 1, float length = -1, uint when = 0) => Api->playNote(Handle, frequency, velocity, length, when);

        /// <summary>Plays a MIDI note (60 = C4).</summary>
        public void PlayMIDINote(float note, float velocity = 1, float length = -1, uint when = 0) => Api->playMIDINote(Handle, note, velocity, length, when);

        public void NoteOff(uint when = 0) => Api->noteOff(Handle, when);
        public void Stop() => Api->stop(Handle);
        public void SetVolume(float left, float right) => Api->setVolume(Handle, left, right);
        public void SetVolume(float volume) => Api->setVolume(Handle, volume, volume);

        public void GetVolume(out float left, out float right)
        {
            float l, r;
            Api->getVolume(Handle, &l, &r);
            left = l;
            right = r;
        }

        public bool IsPlaying => Api->isPlaying(Handle) != 0;

        public void Dispose()
        {
            if (Handle != null)
                Api->freeSynth(Handle);
        }
    }

    /// <summary>A bank of synth voices used to play a sequence track (<c>PDSynthInstrument</c>).</summary>
    public readonly unsafe struct Instrument : IDisposable
    {
        public readonly PDSynthInstrument* Handle;
        private static playdate_sound_instrument* Api => Sound.Api->instrument;

        public Instrument(PDSynthInstrument* handle) { Handle = handle; }

        public static Instrument Create() => new Instrument(Api->newInstrument());

        public static implicit operator SoundSource(Instrument i) => new SoundSource((Native.SoundSource*)i.Handle);

        /// <summary>Adds a voice for the MIDI note range [rangeStart, rangeEnd]. The instrument takes ownership of the synth.</summary>
        public bool AddVoice(Synth synth, float rangeStart = 0, float rangeEnd = 127, float transpose = 0)
            => Api->addVoice(Handle, synth.Handle, rangeStart, rangeEnd, transpose) != 0;

        public Synth PlayNote(float frequency, float velocity = 1, float length = -1, uint when = 0) => new Synth(Api->playNote(Handle, frequency, velocity, length, when));
        public Synth PlayMIDINote(float note, float velocity = 1, float length = -1, uint when = 0) => new Synth(Api->playMIDINote(Handle, note, velocity, length, when));
        public void NoteOff(float note, uint when = 0) => Api->noteOff(Handle, note, when);
        public void AllNotesOff(uint when = 0) => Api->allNotesOff(Handle, when);
        public float PitchBend { set => Api->setPitchBend(Handle, value); }
        public float PitchBendRange { set => Api->setPitchBendRange(Handle, value); }
        public float Transpose { set => Api->setTranspose(Handle, value); }
        public void SetVolume(float left, float right) => Api->setVolume(Handle, left, right);
        public void SetVolume(float volume) => Api->setVolume(Handle, volume, volume);

        public void GetVolume(out float left, out float right)
        {
            float l, r;
            Api->getVolume(Handle, &l, &r);
            left = l;
            right = r;
        }

        public int ActiveVoiceCount => Api->activeVoiceCount(Handle);

        public void Dispose()
        {
            if (Handle != null)
                Api->freeInstrument(Handle);
        }
    }

    /// <summary>A timeline of control values, e.g. a MIDI controller (<c>ControlSignal</c>).</summary>
    public readonly unsafe struct ControlSignal : IDisposable
    {
        public readonly Native.ControlSignal* Handle;
        private static playdate_control_signal* Api => Sound.Api->controlsignal;

        public ControlSignal(Native.ControlSignal* handle) { Handle = handle; }

        public static ControlSignal Create() => new ControlSignal(Api->newSignal());

        public static implicit operator SynthSignalValue(ControlSignal c) => new SynthSignalValue((PDSynthSignalValue*)c.Handle);

        public void ClearEvents() => Api->clearEvents(Handle);
        public void AddEvent(int step, float value, bool interpolate) => Api->addEvent(Handle, step, value, interpolate ? 1 : 0);
        public void RemoveEvent(int step) => Api->removeEvent(Handle, step);
        public int MIDIControllerNumber => Api->getMIDIControllerNumber(Handle);

        public void Dispose()
        {
            if (Handle != null)
                Api->freeSignal(Handle);
        }
    }

    /// <summary>The notes for one instrument in a sequence (<c>SequenceTrack</c>).</summary>
    public readonly unsafe struct SequenceTrack : IDisposable
    {
        public readonly Native.SequenceTrack* Handle;
        private static playdate_sound_track* Api => Sound.Api->track;

        public SequenceTrack(Native.SequenceTrack* handle) { Handle = handle; }

        public static SequenceTrack Create() => new SequenceTrack(Api->newTrack());

        public Instrument Instrument
        {
            get => new Instrument(Api->getInstrument(Handle));
            set => Api->setInstrument(Handle, value.Handle);
        }

        public void AddNoteEvent(uint step, uint length, float note, float velocity) => Api->addNoteEvent(Handle, step, length, note, velocity);
        public void RemoveNoteEvent(uint step, float note) => Api->removeNoteEvent(Handle, step, note);
        public void ClearNotes() => Api->clearNotes(Handle);

        public int ControlSignalCount => Api->getControlSignalCount(Handle);
        public ControlSignal GetControlSignal(int index) => new ControlSignal(Api->getControlSignal(Handle, index));
        public ControlSignal GetSignalForController(int controller, bool create) => new ControlSignal(Api->getSignalForController(Handle, controller, create ? 1 : 0));
        public void ClearControlEvents() => Api->clearControlEvents(Handle);

        /// <summary>Maximum number of simultaneously playing notes in the track.</summary>
        public int Polyphony => Api->getPolyphony(Handle);
        public int ActiveVoiceCount => Api->activeVoiceCount(Handle);
        public bool Muted { set => Api->setMuted(Handle, value ? 1 : 0); }

        /// <summary>Length in steps, including the full last note.</summary>
        public uint Length => Api->getLength(Handle);

        /// <summary>Index of the first note at or after <paramref name="step"/>.</summary>
        public int GetIndexForStep(uint step) => Api->getIndexForStep(Handle, step);

        /// <summary>Reads the note at <paramref name="index"/>; returns false past the end of the track.</summary>
        public bool GetNoteAtIndex(int index, out uint step, out uint length, out float note, out float velocity)
        {
            uint s, l;
            float n, v;
            int ok = Api->getNoteAtIndex(Handle, index, &s, &l, &n, &v);
            step = s;
            length = l;
            note = n;
            velocity = v;
            return ok != 0;
        }

        public void Dispose()
        {
            if (Handle != null)
                Api->freeTrack(Handle);
        }
    }

    /// <summary>A multi-track sequence, e.g. loaded from a MIDI file (<c>SoundSequence</c>).</summary>
    public readonly unsafe struct SoundSequence : IDisposable
    {
        public readonly Native.SoundSequence* Handle;
        private static playdate_sound_sequence* Api => Sound.Api->sequence;

        public SoundSequence(Native.SoundSequence* handle) { Handle = handle; }

        public static SoundSequence Create() => new SoundSequence(Api->newSequence());

        public bool LoadMIDIFile(string path) => Api->loadMIDIFile(Handle, Utf8.Arg(path)) != 0;

        public uint Time
        {
            get => Api->getTime(Handle);
            set => Api->setTime(Handle, value);
        }

        public void SetLoops(int loopStart, int loopEnd, int loops) => Api->setLoops(Handle, loopStart, loopEnd, loops);

        /// <summary>Tempo in steps per second.</summary>
        public float Tempo
        {
            get => Api->getTempo(Handle);
            set => Api->setTempo(Handle, value);
        }

        public int TrackCount => Api->getTrackCount(Handle);
        public SequenceTrack AddTrack() => new SequenceTrack(Api->addTrack(Handle));
        public SequenceTrack GetTrack(int index) => new SequenceTrack(Api->getTrackAtIndex(Handle, (uint)index));
        public void SetTrack(int index, SequenceTrack track) => Api->setTrackAtIndex(Handle, track.Handle, (uint)index);
        public void AllNotesOff() => Api->allNotesOff(Handle);
        public bool IsPlaying => Api->isPlaying(Handle) != 0;
        public uint Length => Api->getLength(Handle);

        private static readonly Dictionary<nint, Action> s_finished = new Dictionary<nint, Action>();

        public void Play(Action finished = null)
        {
            if (finished == null)
            {
                s_finished.Remove((nint)Handle);
                Api->play(Handle, null, null);
            }
            else
            {
                s_finished[(nint)Handle] = finished;
                Api->play(Handle, &FinishedTrampoline, null);
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void FinishedTrampoline(Native.SoundSequence* seq, void* userdata)
        {
            if (s_finished.TryGetValue((nint)seq, out Action action))
                action();
        }

        public void Stop() => Api->stop(Handle);

        public int GetCurrentStep(out int timeOffset)
        {
            int offset;
            int step = Api->getCurrentStep(Handle, &offset);
            timeOffset = offset;
            return step;
        }

        public int CurrentStep => Api->getCurrentStep(Handle, null);

        public void SetCurrentStep(int step, int timeOffset = 0, bool playNotes = false) => Api->setCurrentStep(Handle, step, timeOffset, playNotes ? 1 : 0);

        public void Dispose()
        {
            if (Handle == null)
                return;
            s_finished.Remove((nint)Handle);
            Api->freeSequence(Handle);
        }
    }

    /// <summary>A mixer channel with its own sources and effects (<c>SoundChannel</c>).</summary>
    public readonly unsafe struct SoundChannel : IDisposable
    {
        public readonly Native.SoundChannel* Handle;
        private static playdate_sound_channel* Api => Sound.Api->channel;

        public SoundChannel(Native.SoundChannel* handle) { Handle = handle; }

        public static SoundChannel Create() => new SoundChannel(Api->newChannel());

        public bool AddSource(SoundSource source) => Api->addSource(Handle, source.Handle) != 0;
        public bool RemoveSource(SoundSource source) => Api->removeSource(Handle, source.Handle) != 0;

        /// <summary>Adds a custom native source (runs on the audio thread).</summary>
        public SoundSource AddCallbackSource(delegate* unmanaged[Cdecl]<void*, short*, short*, int, int> callback, void* context, bool stereo)
            => new SoundSource(Api->addCallbackSource(Handle, callback, context, stereo ? 1 : 0));

        public bool AddEffect(SoundEffect effect) => Api->addEffect(Handle, effect.Handle) != 0;
        public bool RemoveEffect(SoundEffect effect) => Api->removeEffect(Handle, effect.Handle) != 0;

        public float Volume
        {
            get => Api->getVolume(Handle);
            set => Api->setVolume(Handle, value);
        }

        public SynthSignalValue VolumeModulator
        {
            get => new SynthSignalValue(Api->getVolumeModulator(Handle));
            set => Api->setVolumeModulator(Handle, value.Handle);
        }

        /// <summary>Pan: -1 (left) to 1 (right).</summary>
        public float Pan { set => Api->setPan(Handle, value); }

        public SynthSignalValue PanModulator
        {
            get => new SynthSignalValue(Api->getPanModulator(Handle));
            set => Api->setPanModulator(Handle, value.Handle);
        }

        public SynthSignalValue DryLevelSignal => new SynthSignalValue(Api->getDryLevelSignal(Handle));
        public SynthSignalValue WetLevelSignal => new SynthSignalValue(Api->getWetLevelSignal(Handle));

        /// <summary>The channel's output as a source, so it can be routed into another channel.</summary>
        public SoundSource OutputAsSource => new SoundSource(Api->getOutputAsSource(Handle));

        public void Dispose()
        {
            if (Handle != null)
                Api->freeChannel(Handle);
        }
    }

    /// <summary>An audio effect (<c>SoundEffect</c>): filters, bitcrusher, delay, overdrive, or custom.</summary>
    public readonly unsafe struct SoundEffect : IDisposable
    {
        public readonly Native.SoundEffect* Handle;
        private static playdate_sound_effect* Api => Sound.Api->effect;

        public SoundEffect(Native.SoundEffect* handle) { Handle = handle; }

        /// <summary>A custom effect processing Q8.24 samples on the audio thread.</summary>
        public static SoundEffect Create(delegate* unmanaged[Cdecl]<Native.SoundEffect*, int*, int*, int, int, int> process, void* userdata)
            => new SoundEffect(Api->newEffect(process, userdata));

        /// <summary>Wet/dry mix, 0..1.</summary>
        public float Mix { set => Api->setMix(Handle, value); }

        public SynthSignalValue MixModulator
        {
            get => new SynthSignalValue(Api->getMixModulator(Handle));
            set => Api->setMixModulator(Handle, value.Handle);
        }

        public void* Userdata
        {
            get => Api->getUserdata(Handle);
            set => Api->setUserdata(Handle, value);
        }

        // ---- Built-in effects ----

        public static SoundEffect TwoPoleFilter(TwoPoleFilterType type, float frequency, float resonance = 0)
        {
            var f = Api->twopolefilter->newFilter();
            Api->twopolefilter->setType(f, type);
            Api->twopolefilter->setFrequency(f, frequency);
            Api->twopolefilter->setResonance(f, resonance);
            return new SoundEffect((Native.SoundEffect*)f);
        }

        public static SoundEffect OnePoleFilter(float parameter)
        {
            var f = Api->onepolefilter->newFilter();
            Api->onepolefilter->setParameter(f, parameter);
            return new SoundEffect((Native.SoundEffect*)f);
        }

        public static SoundEffect BitCrusher(float depth, float downsampling)
        {
            var b = Api->bitcrusher->newBitCrusher();
            Api->bitcrusher->setDepth(b, depth);
            Api->bitcrusher->setDownsampling(b, downsampling);
            return new SoundEffect((Native.SoundEffect*)b);
        }

        public static SoundEffect RingModulator(float frequency)
        {
            var r = Api->ringmodulator->newRingmod();
            Api->ringmodulator->setFrequency(r, frequency);
            return new SoundEffect((Native.SoundEffect*)r);
        }

        public static SoundEffect Overdrive(float gain, float limit)
        {
            var o = Api->overdrive->newOverdrive();
            Api->overdrive->setGain(o, gain);
            Api->overdrive->setLimit(o, limit);
            return new SoundEffect((Native.SoundEffect*)o);
        }

        /// <summary>A delay line of <paramref name="lengthInSamples"/>; add taps with <see cref="AddDelayTap"/>.</summary>
        public static SoundEffect DelayLine(int lengthInSamples, bool stereo, float feedback)
        {
            var d = Api->delayline->newDelayLine(lengthInSamples, stereo ? 1 : 0);
            Api->delayline->setFeedback(d, feedback);
            return new SoundEffect((Native.SoundEffect*)d);
        }

        /// <summary>For delay-line effects: adds a tap (a sound source) at <paramref name="delayInSamples"/>.</summary>
        public SoundSource AddDelayTap(int delayInSamples) => new SoundSource((Native.SoundSource*)Api->delayline->addTap((DelayLine*)Handle, delayInSamples));

        public void Dispose()
        {
            if (Handle != null)
                Api->freeEffect(Handle);
        }
    }
}
