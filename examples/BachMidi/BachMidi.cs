// Port of the Playdate SDK's C_API/Examples/bach.mid.

using System.Runtime.CompilerServices;

namespace BachMidi;

public sealed class BachMidiGame : Game
{
    private SoundSequence _sequence;
    private int _lastStep = -1;

    [ModuleInitializer]
    internal static void Register() => PD.Run(() => new BachMidiGame());

    protected override void Initialize()
    {
        _sequence = SoundSequence.Create();
        _sequence.LoadMIDIFile("bach.mid"); // from http://www.jsbach.net/midi/midi_goldbergvariations.html

        // usually you'd assign a different instrument to each track, but in this file
        // the tracks are separate parts played on the same instrument

        Instrument instrument = Instrument.Create();
        instrument.SetVolume(0.2f, 0.2f);
        Sound.DefaultChannel.AddSource(instrument);

        int trackCount = _sequence.TrackCount;

        Synth synth = Synth.Create();
        AudioSample piano = AudioSample.Load("piano");
        synth.SetSample(piano, 0, 0);

        for (int i = 0; i < trackCount; ++i)
        {
            SequenceTrack track = _sequence.GetTrack(i);
            track.Instrument = instrument;

            for (int p = track.Polyphony; p > 0; --p)
                instrument.AddVoice(synth.Copy(), 0, 127, 0);
        }

        _sequence.Play();
    }

    public override bool Update()
    {
        int step = _sequence.CurrentStep;

        if (step > _lastStep)
        {
            // draw the previous frame one pixel to the left
            Graphics.DrawBitmap(Graphics.GetDisplayBufferBitmap(), -1, 0);
            // and clear the end column
            Graphics.FillRect(399, 0, 1, 240, SolidColor.White);

            int trackCount = _sequence.TrackCount;

            for (int i = 0; i < trackCount; ++i)
            {
                SequenceTrack track = _sequence.GetTrack(i);
                int idx = track.GetIndexForStep((uint)(_lastStep + 1));

                while (track.GetNoteAtIndex(idx++, out uint s, out _, out float note, out _) && s <= step)
                    Graphics.FillRect(399, 240 - 3 * ((int)note - 20), 1, 3, SolidColor.Black);
            }
        }

        _lastStep = step;

        Sys.DrawFPS(0, 0);
        return true;
    }
}
