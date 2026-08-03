using System;
using System.IO;
using SunnyStop.Game;

public static class Render
{
    static void Wav(string path, float[] pcm)
    {
        using (var fs = new FileStream(path, FileMode.Create))
        using (var w = new BinaryWriter(fs))
        {
            int rate = SfxSynth.SampleRate, bytes = pcm.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes);
            w.Write(new[] { 'W', 'A', 'V', 'E' });
            w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
            foreach (float s in pcm)
                w.Write((short)(Math.Max(-1f, Math.Min(1f, s)) * 32000));
        }
    }

    static void Report(string name, float[] pcm)
    {
        float peak = 0f; double rms = 0;
        foreach (float s in pcm) { peak = Math.Max(peak, Math.Abs(s)); rms += s * (double)s; }
        rms = Math.Sqrt(rms / Math.Max(1, pcm.Length));
        bool clipped = peak > 0.999f;
        bool startsClean = Math.Abs(pcm[0]) < 1e-4f;
        bool endsClean = Math.Abs(pcm[pcm.Length - 1]) < 1e-4f;
        Console.WriteLine($"{name,-12} {pcm.Length / (double)SfxSynth.SampleRate,5:F2}s  peak {peak,5:F3}  rms {rms,5:F3}"
            + (clipped ? "  CLIPPED" : "") + (startsClean && endsClean ? "" : "  EDGE-CLICK"));
    }

    public static int Main(string[] args)
    {
        string dir = args.Length > 0 ? args[0] : ".";
        string[] names = { "dispatch", "dock", "depart", "refused", "rewind", "win", "postcard", "uitap" };
        float[][] clips = { SfxSynth.Dispatch(), SfxSynth.Dock(), SfxSynth.Depart(),
                            SfxSynth.Refused(), SfxSynth.Rewind(), SfxSynth.Win(),
                            SfxSynth.Postcard(), SfxSynth.UiTap() };
        for (int k = 0; k < names.Length; k++)
        {
            Report(names[k], clips[k]);
            Wav(Path.Combine(dir, names[k] + ".wav"), clips[k]);
        }

        // The signature sound: a full twelve-seat double-decker filling in one go.
        var chain = new System.Collections.Generic.List<float>();
        for (int i = 0; i < 12; i++)
        {
            float[] note = SfxSynth.Board(i);
            int step = (int)(SfxSynth.SampleRate * 0.135);
            for (int n = 0; n < note.Length; n++)
            {
                int at = i * step + n;
                while (chain.Count <= at) chain.Add(0f);
                chain[at] = (float)Math.Tanh(chain[at] + note[n]);
            }
        }
        float[] run = chain.ToArray();
        Report("board x12", run);
        Wav(Path.Combine(dir, "boarding-chain.wav"), run);
        return 0;
    }
}
