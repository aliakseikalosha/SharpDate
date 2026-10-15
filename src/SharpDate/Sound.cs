// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Playdate.Native;

namespace Playdate
{
    /// <summary>
    /// The sound engine (C: <c>pd->sound</c>, Lua: <c>playdate.sound</c>): channels, custom sources, the
    /// microphone and headphones. The players, synths and effects are separate types.
    /// </summary>
    /// <remarks>
    /// Sound objects (<see cref="FilePlayer"/>, <see cref="Synth"/>, <see cref="SoundEffect"/> ...) are small
    /// structs around native pointers: copying one copies the handle. Dispose what you create. Volumes are
    /// 0-1, times are in seconds unless a parameter says samples, and the audio clock runs at 44,100 samples
    /// per second. Everything plays on <see cref="DefaultChannel"/> unless added to another channel. Finish
    /// and loop callbacks run on the main thread; callbacks that take native function pointers (custom
    /// sources, effects, signals, the microphone) run on the audio thread and must not allocate.
    /// </remarks>
    public static unsafe class Sound
    {
        internal static playdate_sound* Api => PD.Sound;

        /// <summary>Current audio time in samples (44.1 kHz).</summary>
        public static uint CurrentTime => Api->getCurrentTime();

        /// <summary>The channel sources play on unless added to another one.</summary>
        public static SoundChannel DefaultChannel => new SoundChannel(Api->getDefaultChannel());

        /// <summary>Adds a channel created with <see cref="SoundChannel.Create"/> to the mix.</summary>
        /// <returns>False if it was already added.</returns>
        /// <param name="channel">A channel from <see cref="SoundChannel.Create"/>.</param>
        public static bool AddChannel(SoundChannel channel) => Api->addChannel(channel.Handle) != 0;
        /// <summary>Removes a channel from the mix (the default channel can't be removed).</summary>
        /// <param name="channel">A channel added with <see cref="AddChannel"/>.</param>
        public static bool RemoveChannel(SoundChannel channel) => Api->removeChannel(channel.Handle) != 0;

        /// <summary>
        /// Adds a custom audio source to the default channel. The callback fills <c>len</c> 16-bit samples
        /// into the left (and right, for stereo) buffer and returns 1, or returns 0 when silent; it runs on
        /// the audio thread. Free the returned source with <c>PD.System->realloc(source.Handle, 0)</c> after
        /// removing it.
        /// </summary>
        /// <param name="callback">
        /// Static <c>[UnmanagedCallersOnly]</c> function (context, left, right, length) that writes
        /// <c>length</c> 16-bit samples (-32768 to 32767) and returns 1, or 0 for silence. Runs on the audio
        /// thread.
        /// </param>
        /// <param name="context">Passed to the callback unchanged (may be null).</param>
        /// <param name="stereo">True to fill both buffers, false for mono (only <c>left</c> is used).</param>
        public static SoundSource AddSource(delegate* unmanaged[Cdecl]<void*, short*, short*, int, int> callback, void* context, bool stereo)
            => new SoundSource(Api->addSource(callback, context, stereo ? 1 : 0));

        /// <summary>Removes a source from whichever channel it is on.</summary>
        /// <param name="source">Any source currently playing on a channel.</param>
        public static bool RemoveSource(SoundSource source) => Api->removeSource(source.Handle) != 0;

        /// <summary>
        /// Starts recording: the callback receives mono 16-bit samples on the audio thread and returns 1 to
        /// continue or 0 to stop. Ask for permission with <see cref="RequestMicAccess"/> first.
        /// </summary>
        /// <returns>
        /// The source used (1 = internal, 2 = headset), or 0 on error (including denied access).
        /// </returns>
        /// <param name="callback">
        /// Static <c>[UnmanagedCallersOnly]</c> function (context, samples, length) receiving mono 16-bit
        /// samples; returns 1 to continue, 0 to stop. null stops recording.
        /// </param>
        /// <param name="context">Passed to the callback unchanged (may be null).</param>
        /// <param name="source">Which microphone to use.</param>
        public static int SetMicCallback(delegate* unmanaged[Cdecl]<void*, short*, int, int> callback, void* context, MicSource source = MicSource.Autodetect)
            => Api->setMicCallback(callback, context, source);

        /// <summary>
        /// Asks the player for permission to use the microphone; required before
        /// <see cref="SetMicCallback"/>. The callback receives the answer.
        /// </summary>
        /// <param name="purpose">Shown in the permission dialog.</param>
        /// <param name="callback">
        /// Receives true if the player allowed access. Runs on the main thread.
        /// </param>
        public static AccessReply RequestMicAccess(string purpose, Action<bool> callback)
            => Api->requestMicAccess(Utf8.Arg(purpose), &Network.AccessCallback, Network.AccessUserdata(callback));

        /// <summary>Whether headphones (and a headset microphone) are plugged in.</summary>
        /// <param name="headphone">Receives true if headphones are plugged in.</param>
        /// <param name="headsetMic">Receives true if they have a microphone.</param>
        public static void GetHeadphoneState(out bool headphone, out bool headsetMic)
        {
            int h, m;
            Api->getHeadphoneState(&h, &m, null);
            headphone = h != 0;
            headsetMic = m != 0;
        }

        /// <summary>
        /// Forces audio to the headphones and/or the speaker regardless of what is plugged in.
        /// </summary>
        /// <param name="headphone">Play through the headphone jack.</param>
        /// <param name="speaker">Play through the speaker.</param>
        public static void SetOutputsActive(bool headphone, bool speaker) => Api->setOutputsActive(headphone ? 1 : 0, speaker ? 1 : 0);

        /// <summary>The last sound error (e.g. from <see cref="Synth.SetWavetable"/>).</summary>
        public static string GetError() => Utf8.ToString(Api->getError());

        /// <summary>
        /// Converts a MIDI note number (60 = middle C, 69 = A 440 Hz) to a frequency in Hz.
        /// </summary>
        /// <param name="note">
        /// MIDI note number: 0-127, 60 = middle C, 69 = A 440 Hz; fractions allowed.
        /// </param>
        /// <returns>Frequency in Hz.</returns>
        public static float NoteToFrequency(float note) => 440f * MathF.Pow(2f, (note - 69f) / 12f);
        /// <summary>Converts a frequency in Hz to a (fractional) MIDI note number.</summary>
        /// <param name="frequency">Frequency in Hz, above 0.</param>
        /// <returns>MIDI note number (fractional).</returns>
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

    /// <summary>
    /// Anything that produces audio (C: SoundSource): <see cref="FilePlayer"/>, <see cref="SamplePlayer"/>,
    /// <see cref="Synth"/>, <see cref="Instrument"/> and delay-line taps all convert to it implicitly, so
    /// they can be added to a <see cref="SoundChannel"/>.
    /// </summary>
    public readonly unsafe struct SoundSource
    {
        /// <summary>The native pointer.</summary>
        public readonly Native.SoundSource* Handle;

        /// <summary>Wraps a native source pointer.</summary>
        /// <param name="handle">A native source pointer; this struct doesn't take ownership.</param>
        public SoundSource(Native.SoundSource* handle) { Handle = handle; }

        /// <summary>Sets the left and right volume, 0-1.</summary>
        /// <param name="left">Left channel volume, 0-1.</param>
        /// <param name="right">Right channel volume, 0-1.</param>
        public void SetVolume(float left, float right) => Sound.Api->source->setVolume(Handle, left, right);

        /// <summary>Gets the left and right volume.</summary>
        /// <param name="left">Receives the left volume, 0-1.</param>
        /// <param name="right">Receives the right volume, 0-1.</param>
        public void GetVolume(out float left, out float right)
        {
            float l, r;
            Sound.Api->source->getVolume(Handle, &l, &r);
            left = l;
            right = r;
        }

        /// <summary>True while the source is playing.</summary>
        public bool IsPlaying => Sound.Api->source->isPlaying(Handle) != 0;

        /// <summary>
        /// Runs <paramref name="callback"/> on the main thread when the source finishes playing (null removes
        /// it).
        /// </summary>
        /// <param name="callback">Runs on the main thread when playback ends; null removes it.</param>
        public void SetFinishCallback(Action callback)
        {
            Sound.SetCallback(Sound.s_finishCallbacks, Handle, callback);
            Sound.Api->source->setFinishCallback(Handle, Sound.FinishProc(callback), null);
        }
    }

    /// <summary>
    /// Streams a sound file from storage instead of loading it into memory: best for music (C: FilePlayer,
    /// Lua: playdate.sound.fileplayer). pdc compiles .wav/.aif to .pda; .mp3 is played as is.
    /// </summary>
    public readonly unsafe struct FilePlayer : IDisposable
    {
        /// <summary>The native pointer.</summary>
        public readonly Native.FilePlayer* Handle;
        private static playdate_sound_fileplayer* Api => Sound.Api->fileplayer;

        /// <summary>Wraps a native player pointer.</summary>
        /// <param name="handle">A native player pointer; this struct doesn't take ownership.</param>
        public FilePlayer(Native.FilePlayer* handle) { Handle = handle; }

        /// <summary>Creates a player without a file; use <see cref="LoadIntoPlayer"/>.</summary>
        public static FilePlayer Create() => new FilePlayer(Api->newPlayer());

        /// <summary>
        /// Creates a player for a file in the pdx, e.g. "music/theme" (no extension). Stops the game with an
        /// error if the file doesn't exist.
        /// </summary>
        /// <param name="path">Sound file in the pdx without extension, e.g. "music/theme".</param>
        public static FilePlayer Load(string path)
        {
            FilePlayer player = Create();
            if (!player.LoadIntoPlayer(path))
                Sys.Error(string.Concat("Couldn't open audio file ", path));
            return player;
        }

        /// <summary>Switches the player to another file.</summary>
        /// <returns>False if the file doesn't exist.</returns>
        /// <param name="path">Sound file in the pdx without extension.</param>
        public bool LoadIntoPlayer(string path) => Api->loadIntoPlayer(Handle, Utf8.Arg(path)) != 0;

        /// <summary>Lets the player be used as a <see cref="SoundSource"/>.</summary>
        public static implicit operator SoundSource(FilePlayer p) => new SoundSource((Native.SoundSource*)p.Handle);

        /// <summary>
        /// Sets how many seconds of audio are buffered (more avoids underruns, costs memory).
        /// </summary>
        /// <param name="seconds">
        /// Seconds of audio to buffer, above 0 (more = fewer underruns, more memory).
        /// </param>
        public void SetBufferLength(float seconds) => Api->setBufferLength(Handle, seconds);

        /// <summary>
        /// Starts playing. <paramref name="repeat"/> is the number of times to play: 1 = once, 0 = loop until
        /// <see cref="Stop"/>.
        /// </summary>
        /// <returns>False if the buffer couldn't be allocated.</returns>
        /// <param name="repeat">Times to play: 1 = once, n = n times, 0 = forever.</param>
        public bool Play(int repeat = 1) => Api->play(Handle, repeat) != 0;
        /// <summary>True while playing.</summary>
        public bool IsPlaying => Api->isPlaying(Handle) != 0;
        /// <summary>Pauses; <see cref="Play"/> resumes.</summary>
        public void Pause() => Api->pause(Handle);
        /// <summary>Stops playback.</summary>
        public void Stop() => Api->stop(Handle);
        /// <summary>Sets the left and right volume, 0-1.</summary>
        /// <param name="left">Left channel volume, 0-1.</param>
        /// <param name="right">Right channel volume, 0-1.</param>
        public void SetVolume(float left, float right) => Api->setVolume(Handle, left, right);
        /// <summary>Sets both channels' volume, 0-1.</summary>
        /// <param name="volume">Volume for both channels, 0-1.</param>
        public void SetVolume(float volume) => Api->setVolume(Handle, volume, volume);

        /// <summary>Gets the left and right volume.</summary>
        /// <param name="left">Receives the left volume, 0-1.</param>
        /// <param name="right">Receives the right volume, 0-1.</param>
        public void GetVolume(out float left, out float right)
        {
            float l, r;
            Api->getVolume(Handle, &l, &r);
            left = l;
            right = r;
        }

        /// <summary>Length of the file in seconds.</summary>
        public float Length => Api->getLength(Handle);

        /// <summary>The playback position in seconds, 0 to <see cref="Length"/>.</summary>
        public float Offset
        {
            get => Api->getOffset(Handle);
            set => Api->setOffset(Handle, value);
        }

        /// <summary>
        /// Playback speed, above 0: 1 normal, 0.5 an octave down, 2 an octave up. File players can't play
        /// backwards.
        /// </summary>
        public float Rate
        {
            get => Api->getRate(Handle);
            set => Api->setRate(Handle, value);
        }

        /// <summary>Loops between <paramref name="start"/> and <paramref name="end"/> seconds.</summary>
        /// <param name="start">Loop start in seconds, 0 to the file length.</param>
        /// <param name="end">
        /// Loop end in seconds, after <paramref name="start"/>; 0 = end of the file.
        /// </param>
        public void SetLoopRange(float start, float end) => Api->setLoopRange(Handle, start, end);
        /// <summary>True if the buffer ran out of data (the file couldn't be read fast enough).</summary>
        public bool DidUnderrun => Api->didUnderrun(Handle) != 0;
        /// <summary>
        /// Stops (and calls the finish callback) on an underrun instead of resuming after a stutter.
        /// </summary>
        /// <param name="flag">
        /// True = stop on underrun, false = pause and resume when data arrives (default).
        /// </param>
        public void SetStopOnUnderrun(bool flag) => Api->setStopOnUnderrun(Handle, flag ? 1 : 0);

        /// <summary>
        /// Fades to the given volume over <paramref name="lengthInSamples"/> samples (44100 = one second).
        /// </summary>
        /// <param name="left">Final left volume, 0-1.</param>
        /// <param name="right">Final right volume, 0-1.</param>
        /// <param name="lengthInSamples">Fade duration in samples, 44100 per second.</param>
        public void FadeVolume(float left, float right, int lengthInSamples) => Api->fadeVolume(Handle, left, right, lengthInSamples, null, null);

        /// <summary>
        /// Runs <paramref name="callback"/> on the main thread when playback finishes (null removes it).
        /// </summary>
        /// <param name="callback">Runs on the main thread when playback ends; null removes it.</param>
        public void SetFinishCallback(Action callback)
        {
            Sound.SetCallback(Sound.s_finishCallbacks, Handle, callback);
            Api->setFinishCallback(Handle, Sound.FinishProc(callback), null);
        }

        /// <summary>Runs <paramref name="callback"/> each time playback loops.</summary>
        /// <param name="callback">Runs on the main thread each time playback loops; null removes it.</param>
        public void SetLoopCallback(Action callback)
        {
            Sound.SetCallback(Sound.s_loopCallbacks, Handle, callback);
            Api->setLoopCallback(Handle, Sound.LoopProc(callback), null);
        }

        /// <summary>A signal added to the playback rate (e.g. an <see cref="LFO"/> for vibrato).</summary>
        public SynthSignalValue RateModulator
        {
            get => new SynthSignalValue(Api->getRateModulator(Handle));
            set => Api->setRateModulator(Handle, value.Handle);
        }

        /// <summary>Stops and frees the player.</summary>
        public void Dispose()
        {
            if (Handle == null)
                return;
            Sound.s_finishCallbacks.Remove((nint)Handle);
            Sound.s_loopCallbacks.Remove((nint)Handle);
            Api->freePlayer(Handle);
        }
    }

    /// <summary>
    /// A sound loaded into memory, for short sounds played often (C: AudioSample, Lua:
    /// playdate.sound.sample). Play it with a <see cref="SamplePlayer"/> or use it in a <see cref="Synth"/>.
    /// </summary>
    public readonly unsafe struct AudioSample : IDisposable
    {
        /// <summary>The native pointer.</summary>
        public readonly Native.AudioSample* Handle;
        private static playdate_sound_sample* Api => Sound.Api->sample;

        /// <summary>Wraps a native sample pointer.</summary>
        /// <param name="handle">A native sample pointer; this struct doesn't take ownership.</param>
        public AudioSample(Native.AudioSample* handle) { Handle = handle; }

        /// <summary>True if this handle doesn't refer to a sample.</summary>
        public bool IsNull => Handle == null;

        /// <summary>
        /// Loads a sound from the pdx, e.g. "sfx/jump" for Source/sfx/jump.wav (compiled to .pda). Stops the
        /// game with an error if it doesn't exist.
        /// </summary>
        /// <param name="path">Sound file in the pdx without extension, e.g. "sfx/jump".</param>
        public static AudioSample Load(string path)
        {
            var sample = new AudioSample(Api->load(Utf8.Arg(path)));
            if (sample.IsNull)
                Sys.Error(string.Concat("Couldn't load audio sample ", path));
            return sample;
        }

        /// <summary>
        /// Creates an empty sample with room for <paramref name="byteCount"/> bytes, to load files into with
        /// <see cref="LoadInto"/>.
        /// </summary>
        /// <param name="byteCount">Buffer size in bytes, large enough for the files loaded into it.</param>
        public static AudioSample Create(int byteCount) => new AudioSample(Api->newSampleBuffer(byteCount));

        /// <summary>
        /// Creates a sample that plays <paramref name="data"/> directly (not copied: keep it valid). If
        /// <paramref name="shouldFreeData"/> is true the data is freed with the sample (allocate it with
        /// <c>NativeMemory</c> or <c>PD.System->realloc</c>).
        /// </summary>
        /// <param name="data">The audio data, in <paramref name="format"/>; little-endian for 16-bit.</param>
        /// <param name="format">Sample format of <paramref name="data"/>.</param>
        /// <param name="sampleRate">Samples per second, e.g. 44100 or 22050.</param>
        /// <param name="byteCount">Size of <paramref name="data"/> in bytes.</param>
        /// <param name="shouldFreeData">True to free <paramref name="data"/> with the sample.</param>
        public static AudioSample FromData(byte* data, SoundFormat format, uint sampleRate, int byteCount, bool shouldFreeData)
            => new AudioSample(Api->newSampleFromData(data, format, sampleRate, byteCount, shouldFreeData ? 1 : 0));

        /// <summary>Loads a sound file into this sample.</summary>
        /// <param name="path">Sound file in the pdx without extension; must fit the buffer.</param>
        public bool LoadInto(string path) => Api->loadIntoSample(Handle, Utf8.Arg(path)) != 0;

        /// <summary>
        /// Gives access to the raw audio data, its format, sample rate and length in bytes.
        /// </summary>
        /// <param name="data">Receives a pointer to the audio data (owned by the sample).</param>
        /// <param name="format">Receives the sample format.</param>
        /// <param name="sampleRate">Receives samples per second.</param>
        /// <param name="byteLength">Receives the data size in bytes.</param>
        public void GetData(out byte* data, out SoundFormat format, out uint sampleRate, out uint byteLength)
        {
            byte* d;
            SoundFormat f = 0;          // 1-byte enum on the device: only the low byte is written
            uint sr, len;
            Api->getData(Handle, &d, &f, &sr, &len);
            data = d;
            format = f;
            sampleRate = sr;
            byteLength = len;
        }

        /// <summary>Length in seconds.</summary>
        public float Length => Api->getLength(Handle);

        /// <summary>
        /// Decompresses ADPCM data to 16-bit PCM (4x the memory). Needed to play the sample backwards or use
        /// it in a synth.
        /// </summary>
        /// <returns>False if there isn't enough memory.</returns>
        public bool Decompress() => Api->decompress(Handle) != 0;

        /// <summary>Frees the sample (and its data, if created with shouldFreeData).</summary>
        public void Dispose()
        {
            if (Handle != null)
                Api->freeSample(Handle);
        }
    }

    /// <summary>
    /// Plays an <see cref="AudioSample"/> (C: SamplePlayer, Lua: playdate.sound.sampleplayer). Several
    /// players can share one sample.
    /// </summary>
    public readonly unsafe struct SamplePlayer : IDisposable
    {
        /// <summary>The native pointer.</summary>
        public readonly Native.SamplePlayer* Handle;
        private static playdate_sound_sampleplayer* Api => Sound.Api->sampleplayer;

        /// <summary>Wraps a native player pointer.</summary>
        /// <param name="handle">A native player pointer; this struct doesn't take ownership.</param>
        public SamplePlayer(Native.SamplePlayer* handle) { Handle = handle; }

        /// <summary>Creates a player; set <see cref="Sample"/> before playing.</summary>
        public static SamplePlayer Create() => new SamplePlayer(Api->newPlayer());

        /// <summary>Creates a player for <paramref name="sample"/>.</summary>
        /// <param name="sample">The sample to play; it must outlive the player's use of it.</param>
        public static SamplePlayer Create(AudioSample sample)
        {
            SamplePlayer player = Create();
            player.Sample = sample;
            return player;
        }

        /// <summary>Lets the player be used as a <see cref="SoundSource"/>.</summary>
        public static implicit operator SoundSource(SamplePlayer p) => new SoundSource((Native.SoundSource*)p.Handle);

        /// <summary>The sample to play (not owned by the player).</summary>
        public AudioSample Sample
        {
            set => Api->setSample(Handle, value.Handle);
        }

        /// <summary>
        /// Starts playing. <paramref name="repeat"/>: 1 = once, n = n times, 0 = loop until
        /// <see cref="Stop"/>, -1 = ping-pong (forward, backward, ...). <paramref name="rate"/>: 1 normal
        /// speed, 2 an octave up, negative plays backwards (PCM only).
        /// </summary>
        /// <param name="repeat">1 = once, n = n times, 0 = forever, -1 = ping-pong.</param>
        /// <param name="rate">
        /// Speed: 1 = normal, 2 = octave up, 0.5 = octave down, negative = backwards (PCM only).
        /// </param>
        public bool Play(int repeat = 1, float rate = 1) => Api->play(Handle, repeat, rate) != 0;
        /// <summary>True while playing.</summary>
        public bool IsPlaying => Api->isPlaying(Handle) != 0;
        /// <summary>Stops playback.</summary>
        public void Stop() => Api->stop(Handle);
        /// <summary>Sets the left and right volume, 0-1.</summary>
        /// <param name="left">Left channel volume, 0-1.</param>
        /// <param name="right">Right channel volume, 0-1.</param>
        public void SetVolume(float left, float right) => Api->setVolume(Handle, left, right);
        /// <summary>Sets both channels' volume, 0-1.</summary>
        /// <param name="volume">Volume for both channels, 0-1.</param>
        public void SetVolume(float volume) => Api->setVolume(Handle, volume, volume);

        /// <summary>Gets the left and right volume.</summary>
        /// <param name="left">Receives the left volume, 0-1.</param>
        /// <param name="right">Receives the right volume, 0-1.</param>
        public void GetVolume(out float left, out float right)
        {
            float l, r;
            Api->getVolume(Handle, &l, &r);
            left = l;
            right = r;
        }

        /// <summary>Length of the sample in seconds.</summary>
        public float Length => Api->getLength(Handle);

        /// <summary>Playback position in seconds, 0 to <see cref="Length"/>.</summary>
        public float Offset
        {
            get => Api->getOffset(Handle);
            set => Api->setOffset(Handle, value);
        }

        /// <summary>
        /// Playback speed: 1 normal, 2 an octave up, 0.5 an octave down; negative plays backwards (PCM
        /// samples only).
        /// </summary>
        public float Rate
        {
            get => Api->getRate(Handle);
            set => Api->setRate(Handle, value);
        }

        /// <summary>
        /// Limits playback to sample frames <paramref name="start"/>..<paramref name="end"/> (used for
        /// ping-pong looping).
        /// </summary>
        /// <param name="start">First sample frame, 0 or more.</param>
        /// <param name="end">Last sample frame, after <paramref name="start"/>.</param>
        public void SetPlayRange(int start, int end) => Api->setPlayRange(Handle, start, end);
        /// <summary>Pauses or resumes playback.</summary>
        /// <param name="paused">True to pause, false to resume.</param>
        public void SetPaused(bool paused) => Api->setPaused(Handle, paused ? 1 : 0);

        /// <summary>
        /// Runs <paramref name="callback"/> on the main thread when playback finishes (null removes it).
        /// </summary>
        /// <param name="callback">Runs on the main thread when playback ends; null removes it.</param>
        public void SetFinishCallback(Action callback)
        {
            Sound.SetCallback(Sound.s_finishCallbacks, Handle, callback);
            Api->setFinishCallback(Handle, Sound.FinishProc(callback), null);
        }

        /// <summary>Runs <paramref name="callback"/> each time playback loops.</summary>
        /// <param name="callback">Runs on the main thread each time playback loops; null removes it.</param>
        public void SetLoopCallback(Action callback)
        {
            Sound.SetCallback(Sound.s_loopCallbacks, Handle, callback);
            Api->setLoopCallback(Handle, Sound.LoopProc(callback), null);
        }

        /// <summary>A signal added to the playback rate.</summary>
        public SynthSignalValue RateModulator
        {
            get => new SynthSignalValue(Api->getRateModulator(Handle));
            set => Api->setRateModulator(Handle, value.Handle);
        }

        /// <summary>Stops and frees the player (not the sample).</summary>
        public void Dispose()
        {
            if (Handle == null)
                return;
            Sound.s_finishCallbacks.Remove((nint)Handle);
            Sound.s_loopCallbacks.Remove((nint)Handle);
            Api->freePlayer(Handle);
        }
    }

    /// <summary>
    /// A value that can modulate a parameter (C: PDSynthSignalValue). <see cref="LFO"/>,
    /// <see cref="Envelope"/>, <see cref="ControlSignal"/> and <see cref="SynthSignal"/> convert to it
    /// implicitly; a default value (null handle) removes a modulator.
    /// </summary>
    public readonly unsafe struct SynthSignalValue
    {
        /// <summary>The native pointer.</summary>
        public readonly PDSynthSignalValue* Handle;

        /// <summary>Wraps a native signal value pointer.</summary>
        /// <param name="handle">A native signal value pointer (null = no signal).</param>
        public SynthSignalValue(PDSynthSignalValue* handle) { Handle = handle; }

        /// <summary>True if this is "no signal".</summary>
        public bool IsNull => Handle == null;
    }

    /// <summary>
    /// A signal computed by your own native callbacks, or one that follows another signal with a scale and
    /// offset (C: PDSynthSignal).
    /// </summary>
    public readonly unsafe struct SynthSignal : IDisposable
    {
        /// <summary>The native pointer.</summary>
        public readonly PDSynthSignal* Handle;
        private static playdate_sound_signal* Api => Sound.Api->signal;

        /// <summary>Wraps a native signal pointer.</summary>
        /// <param name="handle">A native signal pointer; this struct doesn't take ownership.</param>
        public SynthSignal(PDSynthSignal* handle) { Handle = handle; }

        /// <summary>
        /// Creates a custom signal. <paramref name="step"/> (required) returns the value at the end of the
        /// current audio frame; the others react to notes and to freeing. All run on the audio thread.
        /// </summary>
        /// <param name="step">
        /// Required: (userdata, ref samplesUntilFrameEnd, ref interframeValue) returns the value at the end
        /// of the audio frame.
        /// </param>
        /// <param name="noteOn">
        /// Optional (null): (userdata, MIDI note, velocity 0-1, length in seconds or -1).
        /// </param>
        /// <param name="noteOff">
        /// Optional: (userdata, stopped: 0 on release / 1 when the note stops, sample offset in the frame).
        /// </param>
        /// <param name="dealloc">Optional: called when the signal is freed; free your userdata here.</param>
        /// <param name="userdata">Passed to every callback.</param>
        public static SynthSignal Create(
            delegate* unmanaged[Cdecl]<void*, int*, float*, float> step,
            delegate* unmanaged[Cdecl]<void*, float, float, float, void> noteOn,
            delegate* unmanaged[Cdecl]<void*, int, int, void> noteOff,
            delegate* unmanaged[Cdecl]<void*, void> dealloc,
            void* userdata)
            => new SynthSignal(Api->newSignal(step, noteOn, noteOff, dealloc, userdata));

        /// <summary>Creates a signal that follows another signal value, with its own scale and offset.</summary>
        /// <param name="value">The signal to follow.</param>
        public static SynthSignal ForValue(SynthSignalValue value) => new SynthSignal(Api->newSignalForValue(value.Handle));

        /// <summary>Lets the signal be used as a modulator.</summary>
        public static implicit operator SynthSignalValue(SynthSignal s) => new SynthSignalValue((PDSynthSignalValue*)s.Handle);

        /// <summary>The current output value.</summary>
        public float Value => Api->getValue(Handle);
        /// <summary>Multiplies the output by <paramref name="scale"/> (applied before the offset).</summary>
        /// <param name="scale">Multiplier, any value (1 = unchanged).</param>
        public void SetValueScale(float scale) => Api->setValueScale(Handle, scale);
        /// <summary>Adds <paramref name="offset"/> to the output.</summary>
        /// <param name="offset">Added after scaling (0 = unchanged).</param>
        public void SetValueOffset(float offset) => Api->setValueOffset(Handle, offset);

        /// <summary>Frees the signal.</summary>
        public void Dispose()
        {
            if (Handle != null)
                Api->freeSignal(Handle);
        }
    }

    /// <summary>
    /// A low-frequency oscillator to modulate pitch, volume, filters ... (C: PDSynthLFO, Lua:
    /// playdate.sound.lfo). The output is <see cref="Center"/> plus the wave scaled by <see cref="Depth"/>.
    /// </summary>
    public readonly unsafe struct LFO : IDisposable
    {
        /// <summary>The native pointer.</summary>
        public readonly PDSynthLFO* Handle;
        private static playdate_sound_lfo* Api => Sound.Api->lfo;

        /// <summary>Wraps a native LFO pointer.</summary>
        /// <param name="handle">A native LFO pointer; this struct doesn't take ownership.</param>
        public LFO(PDSynthLFO* handle) { Handle = handle; }

        /// <summary>Creates an LFO with the given wave shape.</summary>
        /// <param name="type">Wave shape.</param>
        public static LFO Create(LFOType type) => new LFO(Api->newLFO(type));

        /// <summary>Lets the LFO be used as a modulator.</summary>
        public static implicit operator SynthSignalValue(LFO lfo) => new SynthSignalValue((PDSynthSignalValue*)lfo.Handle);

        /// <summary>The wave shape.</summary>
        public LFOType Type { set => Api->setType(Handle, value); }
        /// <summary>Cycles per second, above 0 (for an arpeggiator: steps per second).</summary>
        public float Rate { set => Api->setRate(Handle, value); }
        /// <summary>Current phase, 0-1.</summary>
        public float Phase { set => Api->setPhase(Handle, value); }
        /// <summary>Phase the LFO starts (and retriggers) at, 0-1.</summary>
        public float StartPhase { set => Api->setStartPhase(Handle, value); }
        /// <summary>
        /// The value the wave oscillates around, in the units of whatever it modulates (e.g. octaves for a
        /// synth's frequency, 0-1 for volume).
        /// </summary>
        public float Center { set => Api->setCenter(Handle, value); }
        /// <summary>
        /// The wave's amplitude: the output swings from Center - Depth to Center + Depth.
        /// </summary>
        public float Depth { set => Api->setDepth(Handle, value); }
        /// <summary>Resets the phase whenever a synth using it starts a note.</summary>
        public bool Retrigger { set => Api->setRetrigger(Handle, value ? 1 : 0); }
        /// <summary>Keeps the LFO running even while nothing uses it.</summary>
        public bool Global { set => Api->setGlobal(Handle, value ? 1 : 0); }
        /// <summary>
        /// Seed for <see cref="LFOType.SampleAndHold"/> (0-65535), for repeatable random sequences.
        /// </summary>
        public ushort RandomSeed { set => Api->setRandomSeed(Handle, value); }
        /// <summary>The current output value.</summary>
        public float Value => Api->getValue(Handle);

        /// <summary>
        /// Holds the LFO at its center for <paramref name="holdoff"/> seconds, then ramps to full depth over
        /// <paramref name="rampTime"/> seconds.
        /// </summary>
        /// <param name="holdoff">Seconds at the center value before the LFO starts, 0 or more.</param>
        /// <param name="rampTime">Seconds to ramp up to full depth, 0 or more.</param>
        public void SetDelay(float holdoff, float rampTime) => Api->setDelay(Handle, holdoff, rampTime);

        /// <summary>
        /// Makes this an arpeggiator stepping through the given half-step offsets, e.g. { 0, 4, 7, 12 } for a
        /// major chord.
        /// </summary>
        /// <param name="steps">
        /// Offsets in half steps from the played note, e.g. 0, 4, 7, 12; at least one.
        /// </param>
        public void SetArpeggiation(ReadOnlySpan<float> steps)
        {
            fixed (float* p = steps)
                Api->setArpeggiation(Handle, steps.Length, p);
        }

        /// <summary>Computes the values with a native function (called on the audio thread).</summary>
        /// <param name="interpolate">Interpolate between successive values.</param>
        /// <param name="function">
        /// Static <c>[UnmanagedCallersOnly]</c> function (lfo, userdata) returning the next value; audio
        /// thread.
        /// </param>
        /// <param name="userdata">Passed to <paramref name="function"/>.</param>
        public void SetFunction(delegate* unmanaged[Cdecl]<PDSynthLFO*, void*, float> function, void* userdata, bool interpolate)
            => Api->setFunction(Handle, function, userdata, interpolate ? 1 : 0);

        /// <summary>Frees the LFO.</summary>
        public void Dispose()
        {
            if (Handle != null)
                Api->freeLFO(Handle);
        }
    }

    /// <summary>
    /// An attack/decay/sustain/release envelope (C: PDSynthEnvelope, Lua: playdate.sound.envelope). Use it as
    /// a modulator, or configure a synth's own envelope through <see cref="Synth.Envelope"/>.
    /// </summary>
    public readonly unsafe struct Envelope : IDisposable
    {
        /// <summary>The native pointer.</summary>
        public readonly PDSynthEnvelope* Handle;
        private static playdate_sound_envelope* Api => Sound.Api->envelope;

        /// <summary>Wraps a native envelope pointer.</summary>
        /// <param name="handle">A native envelope pointer; this struct doesn't take ownership.</param>
        public Envelope(PDSynthEnvelope* handle) { Handle = handle; }

        /// <summary>
        /// Creates an envelope. Times are in seconds; <paramref name="sustain"/> is a level, 0-1.
        /// </summary>
        /// <param name="attack">Seconds from 0 to full level, 0 or more.</param>
        /// <param name="decay">Seconds from full to the sustain level, 0 or more.</param>
        /// <param name="sustain">Level held while the note is on, 0-1.</param>
        /// <param name="release">Seconds to fall to 0 after the note ends, 0 or more.</param>
        public static Envelope Create(float attack, float decay, float sustain, float release) => new Envelope(Api->newEnvelope(attack, decay, sustain, release));

        /// <summary>Lets the envelope be used as a modulator.</summary>
        public static implicit operator SynthSignalValue(Envelope e) => new SynthSignalValue((PDSynthSignalValue*)e.Handle);

        /// <summary>Seconds to rise from 0 to full level, 0 or more.</summary>
        public float Attack { set => Api->setAttack(Handle, value); }
        /// <summary>Seconds to fall from full to the sustain level, 0 or more.</summary>
        public float Decay { set => Api->setDecay(Handle, value); }
        /// <summary>Level held while the note is on, 0-1.</summary>
        public float Sustain { set => Api->setSustain(Handle, value); }
        /// <summary>Seconds to fall to 0 after the note ends, 0 or more.</summary>
        public float Release { set => Api->setRelease(Handle, value); }
        /// <summary>
        /// When a note starts before the previous one is released, stay at the sustain level instead of
        /// restarting the attack.
        /// </summary>
        public bool Legato { set => Api->setLegato(Handle, value ? 1 : 0); }
        /// <summary>Always restart from 0 when a note starts.</summary>
        public bool Retrigger { set => Api->setRetrigger(Handle, value ? 1 : 0); }
        /// <summary>Shape of the ramps, 0-1: 0 linear, 1 exponential.</summary>
        public float Curvature { set => Api->setCurvature(Handle, value); }
        /// <summary>
        /// How much note velocity scales the output, 0-1: 1 (default) proportional, 0 not at all.
        /// </summary>
        public float VelocitySensitivity { set => Api->setVelocitySensitivity(Handle, value); }
        /// <summary>The current output value.</summary>
        public float Value => Api->getValue(Handle);

        /// <summary>
        /// Speeds the envelope up for higher notes: notes below <paramref name="startNote"/> use the normal
        /// rates, notes above <paramref name="endNote"/> are scaled by <paramref name="scaling"/>, with
        /// interpolation between.
        /// </summary>
        /// <param name="scaling">
        /// Rate factor for notes at and above <paramref name="endNote"/> (1 = no change, 2 = twice as fast).
        /// </param>
        /// <param name="startNote">MIDI note below which rates are unchanged.</param>
        /// <param name="endNote">MIDI note from which <paramref name="scaling"/> applies fully.</param>
        public void SetRateScaling(float scaling, float startNote, float endNote) => Api->setRateScaling(Handle, scaling, startNote, endNote);

        /// <summary>Frees the envelope.</summary>
        public void Dispose()
        {
            if (Handle != null)
                Api->freeEnvelope(Handle);
        }
    }

    /// <summary>
    /// A synthesizer voice playing a waveform, a sample, a wavetable or a custom generator (C: PDSynth, Lua:
    /// playdate.sound.synth). Monophonic: use an <see cref="Instrument"/> for chords.
    /// </summary>
    public readonly unsafe struct Synth : IDisposable
    {
        /// <summary>The native pointer.</summary>
        public readonly PDSynth* Handle;
        private static playdate_sound_synth* Api => Sound.Api->synth;

        /// <summary>Wraps a native synth pointer.</summary>
        /// <param name="handle">A native synth pointer; this struct doesn't take ownership.</param>
        public Synth(PDSynth* handle) { Handle = handle; }

        /// <summary>Creates a synth (square wave).</summary>
        public static Synth Create() => new Synth(Api->newSynth());

        /// <summary>Creates a synth with the given waveform.</summary>
        /// <param name="waveform">The initial waveform.</param>
        public static Synth Create(SoundWaveform waveform)
        {
            Synth synth = Create();
            synth.Waveform = waveform;
            return synth;
        }

        /// <summary>Lets the synth be used as a <see cref="SoundSource"/>.</summary>
        public static implicit operator SoundSource(Synth s) => new SoundSource((Native.SoundSource*)s.Handle);

        /// <summary>
        /// Returns a copy with the same settings (handy for adding voices to an instrument). Dispose it, or
        /// give it to an instrument.
        /// </summary>
        public Synth Copy() => new Synth(Api->copy(Handle));

        /// <summary>The waveform.</summary>
        public SoundWaveform Waveform { set => Api->setWaveform(Handle, value); }

        /// <summary>
        /// Plays an uncompressed sample instead of a waveform. The sustain range (in sample frames) loops
        /// while the note is held; with sustainEnd 0 and sustainStart above 0 it loops to the end.
        /// </summary>
        /// <param name="sample">
        /// Uncompressed PCM sample (call <see cref="AudioSample.Decompress"/> on ADPCM); must outlive its
        /// use.
        /// </param>
        /// <param name="sustainStart">First frame of the sustain loop (0 = no loop).</param>
        /// <param name="sustainEnd">
        /// Last frame of the sustain loop (0 = end of sample when sustainStart is above 0).
        /// </param>
        public void SetSample(AudioSample sample, uint sustainStart = 0, uint sustainEnd = 0) => Api->setSample(Handle, sample.Handle, sustainStart, sustainEnd);

        /// <summary>
        /// Plays a wavetable: a 16-bit mono sample divided into columns x rows cells of 2^log2Size samples.
        /// Move through it with parameters 1-4 (see <see cref="SetParameter"/>).
        /// </summary>
        /// <returns>False on error (see <see cref="Sound.GetError"/>).</returns>
        /// <param name="sample">16-bit mono uncompressed sample holding all cells.</param>
        /// <param name="log2Size">Base-2 log of the samples per cell, e.g. 8 for 256-sample cells.</param>
        /// <param name="columns">Cells across, 1 or more.</param>
        /// <param name="rows">Cells down, 1 or more (1 = a 1-D table).</param>
        public bool SetWavetable(AudioSample sample, int log2Size, int columns, int rows) => Api->setWavetable(Handle, sample.Handle, log2Size, columns, rows) != 0;

        /// <summary>
        /// Generates audio with native callbacks (on the audio thread). <paramref name="render"/> (required)
        /// fills Q8.24 sample buffers; the others handle notes, parameters, freeing and copying.
        /// </summary>
        /// <param name="stereo">Render two channels.</param>
        /// <param name="render">
        /// Required: (userdata, left, right, sampleCount, rate, deltaRate) fills Q8.24 buffers and returns 1
        /// if it produced sound.
        /// </param>
        /// <param name="noteOn">
        /// (userdata, MIDI note, velocity 0-1, length in seconds or -1); may be null.
        /// </param>
        /// <param name="release">
        /// (userdata, sample offset where the note ends in this cycle); may be null.
        /// </param>
        /// <param name="setParameter">
        /// (userdata, 1-based parameter, value) returns 1 if handled; may be null.
        /// </param>
        /// <param name="dealloc">(userdata) frees the userdata; may be null.</param>
        /// <param name="copyUserdata">(userdata) returns a copy for <see cref="Copy"/>; may be null.</param>
        /// <param name="userdata">Passed to all callbacks.</param>
        public void SetGenerator(bool stereo,
            delegate* unmanaged[Cdecl]<void*, int*, int*, int, uint, int, int> render,
            delegate* unmanaged[Cdecl]<void*, float, float, float, void> noteOn,
            delegate* unmanaged[Cdecl]<void*, int, void> release,
            delegate* unmanaged[Cdecl]<void*, int, float, int> setParameter,
            delegate* unmanaged[Cdecl]<void*, void> dealloc,
            delegate* unmanaged[Cdecl]<void*, void*> copyUserdata,
            void* userdata)
            => Api->setGenerator(Handle, stereo ? 1 : 0, render, noteOn, release, setParameter, dealloc, copyUserdata, userdata);

        /// <summary>Envelope attack time in seconds, 0 or more.</summary>
        public float AttackTime { set => Api->setAttackTime(Handle, value); }
        /// <summary>Envelope decay time in seconds, 0 or more.</summary>
        public float DecayTime { set => Api->setDecayTime(Handle, value); }
        /// <summary>Envelope sustain level, 0-1.</summary>
        public float SustainLevel { set => Api->setSustainLevel(Handle, value); }
        /// <summary>Envelope release time in seconds, 0 or more.</summary>
        public float ReleaseTime { set => Api->setReleaseTime(Handle, value); }
        /// <summary>Transposition in half steps, positive or negative (12 = one octave up).</summary>
        public float Transpose { set => Api->setTranspose(Handle, value); }

        /// <summary>The synth's amplitude envelope (owned by the synth; do not free).</summary>
        public Envelope Envelope => new Envelope(Api->getEnvelope(Handle));

        /// <summary>Resets the envelope.</summary>
        public void ClearEnvelope() => Api->clearEnvelope(Handle);

        /// <summary>Pitch modulation: a signal value of 1 is an octave up, -1 an octave down.</summary>
        public SynthSignalValue FrequencyModulator
        {
            get => new SynthSignalValue(Api->getFrequencyModulator(Handle));
            set => Api->setFrequencyModulator(Handle, value.Handle);
        }

        /// <summary>Volume modulation.</summary>
        public SynthSignalValue AmplitudeModulator
        {
            get => new SynthSignalValue(Api->getAmplitudeModulator(Handle));
            set => Api->setAmplitudeModulator(Handle, value.Handle);
        }

        /// <summary>
        /// How many parameters the waveform has (square: 1, pulse width; PO synths: 2; wavetables: up to 4).
        /// </summary>
        public int ParameterCount => Api->getParameterCount(Handle);
        /// <summary>Sets the 1-based <paramref name="parameter"/>.</summary>
        /// <returns>False if there's no such parameter.</returns>
        /// <param name="parameter">1-based index, 1 to <see cref="ParameterCount"/>.</param>
        /// <param name="value">Usually 0-1 (wavetable parameters 1 and 3 take cell positions).</param>
        public bool SetParameter(int parameter, float value) => Api->setParameter(Handle, parameter, value) != 0;
        /// <summary>Modulates a 1-based parameter with a signal.</summary>
        /// <param name="parameter">1-based index, 1 to <see cref="ParameterCount"/>.</param>
        /// <param name="modulator">The modulating signal; <c>default</c> removes it.</param>
        public void SetParameterModulator(int parameter, SynthSignalValue modulator) => Api->setParameterModulator(Handle, parameter, modulator.Handle);
        /// <summary>The modulator of a 1-based parameter.</summary>
        /// <param name="parameter">1-based index.</param>
        public SynthSignalValue GetParameterModulator(int parameter) => new SynthSignalValue(Api->getParameterModulator(Handle, parameter));

        /// <summary>Plays a note at <paramref name="frequency"/> Hz.</summary>
        /// <param name="velocity">Loudness, 0-1.</param>
        /// <param name="length">Seconds; -1 holds the note until <see cref="NoteOff"/>.</param>
        /// <param name="when">
        /// Sound-clock time to start (<see cref="Sound.CurrentTime"/> based), 0 = now.
        /// </param>
        /// <param name="frequency">Pitch in Hz, above 0.</param>
        public void PlayNote(float frequency, float velocity = 1, float length = -1, uint when = 0) => Api->playNote(Handle, frequency, velocity, length, when);

        /// <summary>Plays a MIDI note (60 = middle C; fractions allowed).</summary>
        /// <param name="velocity">Loudness, 0-1.</param>
        /// <param name="length">Seconds; -1 holds the note until <see cref="NoteOff"/>.</param>
        /// <param name="when">Sound-clock time to start, 0 = now.</param>
        /// <param name="note">MIDI note number, 0-127 (60 = middle C); fractions allowed.</param>
        public void PlayMIDINote(float note, float velocity = 1, float length = -1, uint when = 0) => Api->playMIDINote(Handle, note, velocity, length, when);

        /// <summary>Releases the note (now, or at sound-clock time <paramref name="when"/>).</summary>
        /// <param name="when">
        /// Sound-clock time (samples, see <see cref="Sound.CurrentTime"/>); 0 = now.
        /// </param>
        public void NoteOff(uint when = 0) => Api->noteOff(Handle, when);
        /// <summary>Stops the synth immediately.</summary>
        public void Stop() => Api->stop(Handle);
        /// <summary>Sets the left and right volume, 0-1.</summary>
        /// <param name="left">Left channel volume, 0-1.</param>
        /// <param name="right">Right channel volume (stereo synths), 0-1.</param>
        public void SetVolume(float left, float right) => Api->setVolume(Handle, left, right);
        /// <summary>Sets both channels' volume, 0-1.</summary>
        /// <param name="volume">Volume, 0-1.</param>
        public void SetVolume(float volume) => Api->setVolume(Handle, volume, volume);

        /// <summary>Gets the left and right volume.</summary>
        /// <param name="left">Receives the left volume, 0-1.</param>
        /// <param name="right">Receives the right volume, 0-1.</param>
        public void GetVolume(out float left, out float right)
        {
            float l, r;
            Api->getVolume(Handle, &l, &r);
            left = l;
            right = r;
        }

        /// <summary>True while a note is playing.</summary>
        public bool IsPlaying => Api->isPlaying(Handle) != 0;

        /// <summary>Frees the synth. Don't dispose synths owned by an instrument.</summary>
        public void Dispose()
        {
            if (Handle != null)
                Api->freeSynth(Handle);
        }
    }

    /// <summary>
    /// A set of synth voices for polyphony (C: PDSynthInstrument, Lua: playdate.sound.instrument). Each note
    /// goes to a free voice. Used by sequence tracks and for chords.
    /// </summary>
    public readonly unsafe struct Instrument : IDisposable
    {
        /// <summary>The native pointer.</summary>
        public readonly PDSynthInstrument* Handle;
        private static playdate_sound_instrument* Api => Sound.Api->instrument;

        /// <summary>Wraps a native instrument pointer.</summary>
        /// <param name="handle">A native instrument pointer; this struct doesn't take ownership.</param>
        public Instrument(PDSynthInstrument* handle) { Handle = handle; }

        /// <summary>Creates an instrument without voices.</summary>
        public static Instrument Create() => new Instrument(Api->newInstrument());

        /// <summary>Lets the instrument be used as a <see cref="SoundSource"/>.</summary>
        public static implicit operator SoundSource(Instrument i) => new SoundSource((Native.SoundSource*)i.Handle);

        /// <summary>Adds a voice for the MIDI note range [rangeStart, rangeEnd]. The instrument takes ownership of the synth.</summary>
        /// <param name="synth">
        /// The voice; the instrument takes ownership. Fails if it is already in another instrument or
        /// channel.
        /// </param>
        /// <param name="rangeStart">Lowest MIDI note the voice plays, 0-127.</param>
        /// <param name="rangeEnd">Highest MIDI note (included), 0-127.</param>
        /// <param name="transpose">Half steps added to notes played by this voice.</param>
        /// <returns>False if the synth is already used elsewhere.</returns>
        public bool AddVoice(Synth synth, float rangeStart = 0, float rangeEnd = 127, float transpose = 0)
            => Api->addVoice(Handle, synth.Handle, rangeStart, rangeEnd, transpose) != 0;

        /// <summary>Plays a note on a free voice.</summary>
        /// <returns>The voice that plays it.</returns>
        /// <param name="frequency">Pitch in Hz, above 0.</param>
        /// <param name="velocity">Loudness, 0-1.</param>
        /// <param name="length">Seconds, or -1 to hold until <see cref="NoteOff"/>.</param>
        /// <param name="when">Sound-clock time in samples; 0 = now.</param>
        public Synth PlayNote(float frequency, float velocity = 1, float length = -1, uint when = 0) => new Synth(Api->playNote(Handle, frequency, velocity, length, when));
        /// <summary>Plays a MIDI note on a free voice.</summary>
        /// <returns>The voice that plays it.</returns>
        /// <param name="note">MIDI note number, 0-127 (fractions allowed).</param>
        /// <param name="velocity">Loudness, 0-1.</param>
        /// <param name="length">Seconds, or -1 to hold until <see cref="NoteOff"/>.</param>
        /// <param name="when">Sound-clock time in samples; 0 = now.</param>
        public Synth PlayMIDINote(float note, float velocity = 1, float length = -1, uint when = 0) => new Synth(Api->playMIDINote(Handle, note, velocity, length, when));
        /// <summary>Releases the voice playing <paramref name="note"/>.</summary>
        /// <param name="note">The MIDI note to release.</param>
        /// <param name="when">Sound-clock time in samples; 0 = now.</param>
        public void NoteOff(float note, uint when = 0) => Api->noteOff(Handle, note, when);
        /// <summary>Releases all voices.</summary>
        /// <param name="when">Sound-clock time in samples; 0 = now.</param>
        public void AllNotesOff(uint when = 0) => Api->allNotesOff(Handle, when);
        /// <summary>Pitch bend as a fraction of <see cref="PitchBendRange"/>, -1 to 1.</summary>
        public float PitchBend { set => Api->setPitchBend(Handle, value); }
        /// <summary>Pitch bend range in half steps, 0 or more (default 12 = one octave).</summary>
        public float PitchBendRange { set => Api->setPitchBendRange(Handle, value); }
        /// <summary>Transposition of all voices in half steps, positive or negative.</summary>
        public float Transpose { set => Api->setTranspose(Handle, value); }
        /// <summary>Sets the left and right volume, 0-1.</summary>
        /// <param name="left">Left channel volume, 0-1.</param>
        /// <param name="right">Right channel volume, 0-1.</param>
        public void SetVolume(float left, float right) => Api->setVolume(Handle, left, right);
        /// <summary>Sets both channels' volume, 0-1.</summary>
        /// <param name="volume">Volume for both channels, 0-1.</param>
        public void SetVolume(float volume) => Api->setVolume(Handle, volume, volume);

        /// <summary>Gets the left and right volume.</summary>
        /// <param name="left">Receives the left volume, 0-1.</param>
        /// <param name="right">Receives the right volume, 0-1.</param>
        public void GetVolume(out float left, out float right)
        {
            float l, r;
            Api->getVolume(Handle, &l, &r);
            left = l;
            right = r;
        }

        /// <summary>Number of voices playing.</summary>
        public int ActiveVoiceCount => Api->activeVoiceCount(Handle);

        /// <summary>Frees the instrument and its voices.</summary>
        public void Dispose()
        {
            if (Handle != null)
                Api->freeInstrument(Handle);
        }
    }

    /// <summary>
    /// Values that change at given steps of a sequence, e.g. a MIDI controller (C: ControlSignal, Lua:
    /// playdate.sound.controlsignal). Usable as a modulator.
    /// </summary>
    public readonly unsafe struct ControlSignal : IDisposable
    {
        /// <summary>The native pointer.</summary>
        public readonly Native.ControlSignal* Handle;
        private static playdate_control_signal* Api => Sound.Api->controlsignal;

        /// <summary>Wraps a native control signal pointer.</summary>
        /// <param name="handle">A native control signal pointer; this struct doesn't take ownership.</param>
        public ControlSignal(Native.ControlSignal* handle) { Handle = handle; }

        /// <summary>Creates an empty control signal.</summary>
        public static ControlSignal Create() => new ControlSignal(Api->newSignal());

        /// <summary>Lets the signal be used as a modulator.</summary>
        public static implicit operator SynthSignalValue(ControlSignal c) => new SynthSignalValue((PDSynthSignalValue*)c.Handle);

        /// <summary>Removes all events.</summary>
        public void ClearEvents() => Api->clearEvents(Handle);
        /// <summary>
        /// Sets the value at <paramref name="step"/>; with <paramref name="interpolate"/> the value ramps
        /// from the previous event.
        /// </summary>
        /// <param name="step">Sequence step, 0 or more.</param>
        /// <param name="value">The value from this step on (any range; 0-1 for most uses).</param>
        /// <param name="interpolate">Ramp from the previous event's value instead of jumping.</param>
        public void AddEvent(int step, float value, bool interpolate) => Api->addEvent(Handle, step, value, interpolate ? 1 : 0);
        /// <summary>Removes the event at <paramref name="step"/>.</summary>
        /// <param name="step">The step of the event to remove.</param>
        public void RemoveEvent(int step) => Api->removeEvent(Handle, step);
        /// <summary>The MIDI controller number, for signals loaded from a MIDI file.</summary>
        public int MIDIControllerNumber => Api->getMIDIControllerNumber(Handle);

        /// <summary>Frees the signal.</summary>
        public void Dispose()
        {
            if (Handle != null)
                Api->freeSignal(Handle);
        }
    }

    /// <summary>
    /// One track of a <see cref="SoundSequence"/>: notes and control signals played on an
    /// <see cref="Instrument"/> (C: SequenceTrack, Lua: playdate.sound.track). Times are in steps.
    /// </summary>
    public readonly unsafe struct SequenceTrack : IDisposable
    {
        /// <summary>The native pointer.</summary>
        public readonly Native.SequenceTrack* Handle;
        private static playdate_sound_track* Api => Sound.Api->track;

        /// <summary>Wraps a native track pointer.</summary>
        /// <param name="handle">A native track pointer; this struct doesn't take ownership.</param>
        public SequenceTrack(Native.SequenceTrack* handle) { Handle = handle; }

        /// <summary>Creates an empty track.</summary>
        public static SequenceTrack Create() => new SequenceTrack(Api->newTrack());

        /// <summary>The instrument that plays the track's notes.</summary>
        public Instrument Instrument
        {
            get => new Instrument(Api->getInstrument(Handle));
            set => Api->setInstrument(Handle, value.Handle);
        }

        /// <summary>
        /// Adds a note starting at <paramref name="step"/> lasting <paramref name="length"/> steps.
        /// </summary>
        /// <param name="note">MIDI note number.</param>
        /// <param name="velocity">Loudness, 0-1.</param>
        /// <param name="step">Start step, 0 or more.</param>
        /// <param name="length">Duration in steps, 1 or more.</param>
        public void AddNoteEvent(uint step, uint length, float note, float velocity) => Api->addNoteEvent(Handle, step, length, note, velocity);
        /// <summary>Removes the note <paramref name="note"/> at <paramref name="step"/>.</summary>
        /// <param name="step">Start step of the note.</param>
        /// <param name="note">Its MIDI note number.</param>
        public void RemoveNoteEvent(uint step, float note) => Api->removeNoteEvent(Handle, step, note);
        /// <summary>Removes all notes.</summary>
        public void ClearNotes() => Api->clearNotes(Handle);

        /// <summary>Number of control signals.</summary>
        public int ControlSignalCount => Api->getControlSignalCount(Handle);
        /// <summary>The control signal at <paramref name="index"/>.</summary>
        /// <param name="index">0 to <see cref="ControlSignalCount"/> - 1.</param>
        public ControlSignal GetControlSignal(int index) => new ControlSignal(Api->getControlSignal(Handle, index));
        /// <summary>
        /// The control signal for MIDI controller <paramref name="controller"/>, created if
        /// <paramref name="create"/> is true.
        /// </summary>
        /// <param name="controller">MIDI controller number, 0-127 (e.g. 7 = volume, 10 = pan).</param>
        /// <param name="create">Create the signal if the track doesn't have one yet.</param>
        public ControlSignal GetSignalForController(int controller, bool create) => new ControlSignal(Api->getSignalForController(Handle, controller, create ? 1 : 0));
        /// <summary>Removes all control signals.</summary>
        public void ClearControlEvents() => Api->clearControlEvents(Handle);

        /// <summary>Maximum number of simultaneously playing notes in the track.</summary>
        public int Polyphony => Api->getPolyphony(Handle);
        /// <summary>Voices playing in the track's instrument.</summary>
        public int ActiveVoiceCount => Api->activeVoiceCount(Handle);
        /// <summary>Mutes the track.</summary>
        public bool Muted { set => Api->setMuted(Handle, value ? 1 : 0); }

        /// <summary>Length in steps, including the full last note.</summary>
        public uint Length => Api->getLength(Handle);

        /// <summary>Index of the first note at or after <paramref name="step"/>.</summary>
        /// <param name="step">A step, 0 or more.</param>
        public int GetIndexForStep(uint step) => Api->getIndexForStep(Handle, step);

        /// <summary>Reads the note at <paramref name="index"/>; returns false past the end of the track.</summary>
        /// <param name="index">Note index, 0 or more (from <see cref="GetIndexForStep"/>).</param>
        /// <param name="step">Receives the start step.</param>
        /// <param name="length">Receives the length in steps.</param>
        /// <param name="note">Receives the MIDI note.</param>
        /// <param name="velocity">Receives the velocity, 0-1.</param>
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

        /// <summary>Frees the track.</summary>
        public void Dispose()
        {
            if (Handle != null)
                Api->freeTrack(Handle);
        }
    }

    /// <summary>
    /// A multi-track song, usually loaded from a MIDI file (C: SoundSequence, Lua: playdate.sound.sequence).
    /// Assign an <see cref="Instrument"/> to every track before playing.
    /// </summary>
    public readonly unsafe struct SoundSequence : IDisposable
    {
        /// <summary>The native pointer.</summary>
        public readonly Native.SoundSequence* Handle;
        private static playdate_sound_sequence* Api => Sound.Api->sequence;

        /// <summary>Wraps a native sequence pointer.</summary>
        /// <param name="handle">A native sequence pointer; this struct doesn't take ownership.</param>
        public SoundSequence(Native.SoundSequence* handle) { Handle = handle; }

        /// <summary>Creates an empty sequence.</summary>
        public static SoundSequence Create() => new SoundSequence(Api->newSequence());

        /// <summary>Loads a MIDI file from the pdx into an empty sequence.</summary>
        /// <returns>False on failure.</returns>
        /// <param name="path">Path of a .mid file in the pdx, with extension.</param>
        public bool LoadMIDIFile(string path) => Api->loadMIDIFile(Handle, Utf8.Arg(path)) != 0;

        /// <summary>Playback position in samples since the start (44100 per second), 0 or more.</summary>
        public uint Time
        {
            get => Api->getTime(Handle);
            set => Api->setTime(Handle, value);
        }

        /// <summary>
        /// Loops steps <paramref name="loopStart"/>..<paramref name="loopEnd"/> <paramref name="loops"/>
        /// times (0 = forever).
        /// </summary>
        /// <param name="loopStart">First step of the loop, 0 or more.</param>
        /// <param name="loopEnd">Step where the loop ends, after <paramref name="loopStart"/>.</param>
        /// <param name="loops">Times to repeat; 0 = forever.</param>
        public void SetLoops(int loopStart, int loopEnd, int loops) => Api->setLoops(Handle, loopStart, loopEnd, loops);

        /// <summary>Tempo in steps per second, above 0. A MIDI file sets it from its own tempo.</summary>
        public float Tempo
        {
            get => Api->getTempo(Handle);
            set => Api->setTempo(Handle, value);
        }

        /// <summary>Number of tracks.</summary>
        public int TrackCount => Api->getTrackCount(Handle);
        /// <summary>Adds a new empty track.</summary>
        public SequenceTrack AddTrack() => new SequenceTrack(Api->addTrack(Handle));
        /// <summary>The track at <paramref name="index"/>.</summary>
        /// <param name="index">0 to <see cref="TrackCount"/> - 1.</param>
        public SequenceTrack GetTrack(int index) => new SequenceTrack(Api->getTrackAtIndex(Handle, (uint)index));
        /// <summary>Replaces the track at <paramref name="index"/>.</summary>
        /// <param name="index">Track position, 0 or more.</param>
        /// <param name="track">The track to put there.</param>
        public void SetTrack(int index, SequenceTrack track) => Api->setTrackAtIndex(Handle, track.Handle, (uint)index);
        /// <summary>Releases all playing notes.</summary>
        public void AllNotesOff() => Api->allNotesOff(Handle);
        /// <summary>True while playing.</summary>
        public bool IsPlaying => Api->isPlaying(Handle) != 0;
        /// <summary>Length of the longest track in steps.</summary>
        public uint Length => Api->getLength(Handle);

        private static readonly Dictionary<nint, Action> s_finished = new Dictionary<nint, Action>();

        /// <summary>
        /// Starts playing; <paramref name="finished"/> runs on the main thread when the sequence ends or is
        /// stopped.
        /// </summary>
        /// <param name="finished">
        /// Runs on the main thread when the sequence ends or is stopped; may be null.
        /// </param>
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

        /// <summary>Stops playing.</summary>
        public void Stop() => Api->stop(Handle);

        /// <summary>The current step, and the sample offset within it.</summary>
        /// <param name="timeOffset">Receives the sample offset within the current step.</param>
        public int GetCurrentStep(out int timeOffset)
        {
            int offset;
            int step = Api->getCurrentStep(Handle, &offset);
            timeOffset = offset;
            return step;
        }

        /// <summary>The current step.</summary>
        public int CurrentStep => Api->getCurrentStep(Handle, null);

        /// <summary>
        /// Jumps to <paramref name="step"/>; with <paramref name="playNotes"/> the notes at that step are
        /// played.
        /// </summary>
        /// <param name="step">Target step, 0 to <see cref="Length"/>.</param>
        /// <param name="timeOffset">Sample offset within the step, 0 or more.</param>
        /// <param name="playNotes">Play the notes that start at <paramref name="step"/>.</param>
        public void SetCurrentStep(int step, int timeOffset = 0, bool playNotes = false) => Api->setCurrentStep(Handle, step, timeOffset, playNotes ? 1 : 0);

        /// <summary>Frees the sequence and its tracks.</summary>
        public void Dispose()
        {
            if (Handle == null)
                return;
            s_finished.Remove((nint)Handle);
            Api->freeSequence(Handle);
        }
    }

    /// <summary>
    /// A mixer channel with its own sources, effects, volume and pan (C: SoundChannel, Lua:
    /// playdate.sound.channel). Create one, add it with <see cref="Sound.AddChannel"/>, then add sources and
    /// effects to it.
    /// </summary>
    public readonly unsafe struct SoundChannel : IDisposable
    {
        /// <summary>The native pointer.</summary>
        public readonly Native.SoundChannel* Handle;
        private static playdate_sound_channel* Api => Sound.Api->channel;

        /// <summary>Wraps a native channel pointer.</summary>
        /// <param name="handle">A native channel pointer; this struct doesn't take ownership.</param>
        public SoundChannel(Native.SoundChannel* handle) { Handle = handle; }

        /// <summary>Creates a channel (add it with <see cref="Sound.AddChannel"/>).</summary>
        public static SoundChannel Create() => new SoundChannel(Api->newChannel());

        /// <summary>Moves a source onto this channel.</summary>
        /// <param name="source">A player, synth, instrument or tap; it leaves its previous channel.</param>
        public bool AddSource(SoundSource source) => Api->addSource(Handle, source.Handle) != 0;
        /// <summary>Removes a source from this channel.</summary>
        /// <param name="source">A source on this channel.</param>
        public bool RemoveSource(SoundSource source) => Api->removeSource(Handle, source.Handle) != 0;

        /// <summary>
        /// Adds a custom source rendered by a native callback on the audio thread (see
        /// <see cref="Sound.AddSource"/>). Free it with <c>PD.System->realloc(source.Handle, 0)</c> after
        /// removing it.
        /// </summary>
        /// <param name="callback">
        /// Static <c>[UnmanagedCallersOnly]</c> function (context, left, right, length) writing 16-bit
        /// samples; returns 1, or 0 for silence. Audio thread.
        /// </param>
        /// <param name="context">Passed to the callback unchanged.</param>
        /// <param name="stereo">True to fill both buffers.</param>
        public SoundSource AddCallbackSource(delegate* unmanaged[Cdecl]<void*, short*, short*, int, int> callback, void* context, bool stereo)
            => new SoundSource(Api->addCallbackSource(Handle, callback, context, stereo ? 1 : 0));

        /// <summary>Adds an effect to the channel.</summary>
        /// <returns>False if the effect is already in use.</returns>
        /// <param name="effect">An effect not used by another channel.</param>
        public bool AddEffect(SoundEffect effect) => Api->addEffect(Handle, effect.Handle) != 0;
        /// <summary>Removes an effect.</summary>
        /// <param name="effect">An effect on this channel.</param>
        public bool RemoveEffect(SoundEffect effect) => Api->removeEffect(Handle, effect.Handle) != 0;

        /// <summary>Channel volume, 0-1.</summary>
        public float Volume
        {
            get => Api->getVolume(Handle);
            set => Api->setVolume(Handle, value);
        }

        /// <summary>A signal modulating the volume.</summary>
        public SynthSignalValue VolumeModulator
        {
            get => new SynthSignalValue(Api->getVolumeModulator(Handle));
            set => Api->setVolumeModulator(Handle, value.Handle);
        }

        /// <summary>Pan: -1 (left) to 1 (right).</summary>
        public float Pan { set => Api->setPan(Handle, value); }

        /// <summary>A signal modulating the pan.</summary>
        public SynthSignalValue PanModulator
        {
            get => new SynthSignalValue(Api->getPanModulator(Handle));
            set => Api->setPanModulator(Handle, value.Handle);
        }

        /// <summary>A signal following the channel's level before effects (e.g. to drive visuals).</summary>
        public SynthSignalValue DryLevelSignal => new SynthSignalValue(Api->getDryLevelSignal(Handle));
        /// <summary>A signal following the channel's level after effects.</summary>
        public SynthSignalValue WetLevelSignal => new SynthSignalValue(Api->getWetLevelSignal(Handle));

        /// <summary>The channel's output as a source, so it can be routed into another channel.</summary>
        public SoundSource OutputAsSource => new SoundSource(Api->getOutputAsSource(Handle));

        /// <summary>Frees the channel (remove it from the engine first).</summary>
        public void Dispose()
        {
            if (Handle != null)
                Api->freeChannel(Handle);
        }
    }

    /// <summary>
    /// An effect applied to a <see cref="SoundChannel"/>: filters, bitcrusher, ring modulator, overdrive,
    /// delay line, or a custom native effect (C: SoundEffect, Lua: playdate.sound.effect). Add with
    /// <see cref="SoundChannel.AddEffect"/>; dispose after removing it.
    /// </summary>
    public readonly unsafe struct SoundEffect : IDisposable
    {
        /// <summary>The native pointer.</summary>
        public readonly Native.SoundEffect* Handle;
        private readonly EffectKind _kind;
        private static playdate_sound_effect* Api => Sound.Api->effect;

        // Built-in effects are freed with their own free function.
        private enum EffectKind : byte { Custom, TwoPoleFilter, OnePoleFilter, BitCrusher, RingModulator, Overdrive, DelayLine }

        /// <summary>
        /// Wraps a native effect pointer (freed as a custom effect by <see cref="Dispose"/>).
        /// </summary>
        /// <param name="handle">A native effect pointer; this struct doesn't take ownership.</param>
        public SoundEffect(Native.SoundEffect* handle) : this(handle, EffectKind.Custom) { }

        private SoundEffect(Native.SoundEffect* handle, EffectKind kind)
        {
            Handle = handle;
            _kind = kind;
        }

        /// <summary>
        /// Creates a custom effect. <paramref name="process"/> runs on the audio thread with the left and
        /// right buffers (Q8.24 fixed point), the sample count and whether the buffers hold sound; it returns
        /// 1 if it changed the samples.
        /// </summary>
        /// <param name="process">
        /// Static <c>[UnmanagedCallersOnly]</c> function (effect, left, right, sampleCount, buffersActive)
        /// processing Q8.24 samples in place; returns 1 if it changed them. Audio thread.
        /// </param>
        /// <param name="userdata">Stored as <see cref="Userdata"/>.</param>
        public static SoundEffect Create(delegate* unmanaged[Cdecl]<Native.SoundEffect*, int*, int*, int, int, int> process, void* userdata)
            => new SoundEffect(Api->newEffect(process, userdata));

        /// <summary>
        /// Wet/dry mix: 1 replaces the input with the effect's output, 0 leaves the effect out.
        /// </summary>
        public float Mix { set => Api->setMix(Handle, value); }

        /// <summary>A signal modulating the mix.</summary>
        public SynthSignalValue MixModulator
        {
            get => new SynthSignalValue(Api->getMixModulator(Handle));
            set => Api->setMixModulator(Handle, value.Handle);
        }

        /// <summary>A free pointer for custom effects.</summary>
        public void* Userdata
        {
            get => Api->getUserdata(Handle);
            set => Api->setUserdata(Handle, value);
        }

        // ---- Built-in effects ----

        /// <summary>Creates a two-pole (resonant) filter.</summary>
        /// <param name="frequency">Cutoff or center frequency in Hz.</param>
        /// <param name="resonance">Resonance, 0-1.</param>
        /// <param name="type">Filter shape.</param>
        public static SoundEffect TwoPoleFilter(TwoPoleFilterType type, float frequency, float resonance = 0)
        {
            var f = Api->twopolefilter->newFilter();
            Api->twopolefilter->setType(f, type);
            Api->twopolefilter->setFrequency(f, frequency);
            Api->twopolefilter->setResonance(f, resonance);
            return new SoundEffect((Native.SoundEffect*)f, EffectKind.TwoPoleFilter);
        }

        /// <summary>
        /// Creates a one-pole filter: <paramref name="parameter"/> from -1 to 0 is low-pass, 0 to 1
        /// high-pass.
        /// </summary>
        /// <param name="parameter">-1 to 0 = low-pass (lower = darker), 0 to 1 = high-pass.</param>
        public static SoundEffect OnePoleFilter(float parameter)
        {
            var f = Api->onepolefilter->newFilter();
            Api->onepolefilter->setParameter(f, parameter);
            return new SoundEffect((Native.SoundEffect*)f, EffectKind.OnePoleFilter);
        }

        /// <summary>Creates a bitcrusher.</summary>
        /// <param name="depth">Amplitude quantizing, 0 (none) to 1 (1-bit).</param>
        /// <param name="downsampling">Sample-rate reduction, 0 (none) to 1 (silence).</param>
        public static SoundEffect BitCrusher(float depth, float downsampling)
        {
            var b = Api->bitcrusher->newBitCrusher();
            Api->bitcrusher->setDepth(b, depth);
            Api->bitcrusher->setDownsampling(b, downsampling);
            return new SoundEffect((Native.SoundEffect*)b, EffectKind.BitCrusher);
        }

        /// <summary>Creates a ring modulator with a modulation frequency in Hz.</summary>
        /// <param name="frequency">Modulation frequency in Hz.</param>
        public static SoundEffect RingModulator(float frequency)
        {
            var r = Api->ringmodulator->newRingmod();
            Api->ringmodulator->setFrequency(r, frequency);
            return new SoundEffect((Native.SoundEffect*)r, EffectKind.RingModulator);
        }

        /// <summary>
        /// Creates an overdrive: the input is amplified by <paramref name="gain"/> and clipped at
        /// <paramref name="limit"/>.
        /// </summary>
        /// <param name="gain">Amplification before clipping, 1 or more (higher = more distortion).</param>
        /// <param name="limit">Level where the signal clips, 0-1.</param>
        public static SoundEffect Overdrive(float gain, float limit)
        {
            var o = Api->overdrive->newOverdrive();
            Api->overdrive->setGain(o, gain);
            Api->overdrive->setLimit(o, limit);
            return new SoundEffect((Native.SoundEffect*)o, EffectKind.Overdrive);
        }

        /// <summary>
        /// Creates a delay line <paramref name="lengthInSamples"/> long (44100 = one second). Its output is
        /// the full delay; add more outputs with <see cref="AddDelayTap"/>.
        /// </summary>
        /// <param name="feedback">How much of the output is fed back, 0-1.</param>
        /// <param name="lengthInSamples">
        /// Delay length in samples (44100 = one second); taps can't be further than this.
        /// </param>
        /// <param name="stereo">Keep left and right separate.</param>
        public static SoundEffect DelayLine(int lengthInSamples, bool stereo, float feedback)
        {
            var d = Api->delayline->newDelayLine(lengthInSamples, stereo ? 1 : 0);
            Api->delayline->setFeedback(d, feedback);
            return new SoundEffect((Native.SoundEffect*)d, EffectKind.DelayLine);
        }

        /// <summary>
        /// For a delay line: adds a tap that plays the delayed signal at <paramref name="delayInSamples"/>
        /// (at most the line's length). The tap is a <see cref="SoundSource"/> and can go on any channel.
        /// </summary>
        /// <param name="delayInSamples">Tap position in samples, up to the delay line's length.</param>
        public SoundSource AddDelayTap(int delayInSamples) => new SoundSource((Native.SoundSource*)Api->delayline->addTap((DelayLine*)Handle, delayInSamples));

        /// <summary>Frees the effect with the free function matching its type.</summary>
        public void Dispose()
        {
            if (Handle == null)
                return;
            switch (_kind)
            {
                case EffectKind.TwoPoleFilter: Api->twopolefilter->freeFilter((TwoPoleFilter*)Handle); break;
                case EffectKind.OnePoleFilter: Api->onepolefilter->freeFilter((OnePoleFilter*)Handle); break;
                case EffectKind.BitCrusher: Api->bitcrusher->freeBitCrusher((BitCrusher*)Handle); break;
                case EffectKind.RingModulator: Api->ringmodulator->freeRingmod((RingModulator*)Handle); break;
                case EffectKind.Overdrive: Api->overdrive->freeOverdrive((Overdrive*)Handle); break;
                case EffectKind.DelayLine: Api->delayline->freeDelayLine((DelayLine*)Handle); break;
                default: Api->freeEffect(Handle); break;
            }
        }
    }
}
