using System.IO;
using System.Media;
using System.Text;

namespace LunaCopy.Services;

public static class SoundService
{
    private static readonly byte[] WavBytes = GenerateSoftPopWav();

    private static byte[] GenerateSoftPopWav()
    {
        const int sampleRate = 44100;
        const double soundDuration = 0.028;  // 28 ms tone
        const double silenceDuration = 0.120; // 120 ms silence tail for Bluetooth/DAC wake
        const double totalDuration = soundDuration + silenceDuration;
        const double frequency = 800.0;

        int soundSamples = (int)(sampleRate * soundDuration);
        int totalSamples = (int)(sampleRate * totalDuration);
        int dataChunkSize = totalSamples * sizeof(short);

        using var ms = new MemoryStream(44 + dataChunkSize);
        using var bw = new BinaryWriter(ms, Encoding.ASCII);

        // RIFF header
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataChunkSize);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));

        // fmt subchunk
        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);                 // Subchunk1Size (16 for PCM)
        bw.Write((short)1);           // AudioFormat (1 = PCM)
        bw.Write((short)1);           // NumChannels (1 = mono)
        bw.Write(sampleRate);         // SampleRate
        bw.Write(sampleRate * 2);     // ByteRate
        bw.Write((short)2);           // BlockAlign
        bw.Write((short)16);          // BitsPerSample

        // data subchunk
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(dataChunkSize);

        // Audio part
        for (int i = 0; i < soundSamples; i++)
        {
            double t = (double)i / sampleRate;
            double progress = (double)i / soundSamples;

            // Attack (first 8%) + smooth decay
            double attack = progress < 0.08 ? (progress / 0.08) : 1.0;
            double decay = Math.Exp(-4.8 * progress) * Math.Cos(progress * Math.PI * 0.5);
            double envelope = attack * decay;

            double instFreq = frequency * (1.05 - 0.12 * progress);
            double sample = Math.Sin(2.0 * Math.PI * instFreq * t) * envelope;

            short pcmValue = (short)(sample * 11500);
            bw.Write(pcmValue);
        }

        // Silence tail part
        for (int i = soundSamples; i < totalSamples; i++)
        {
            bw.Write((short)0);
        }

        bw.Flush();
        return ms.ToArray();
    }

    public static void PlayPop()
    {
        if (!ConfigService.Current.SoundEnabled) return;

        Task.Run(() =>
        {
            try
            {
                using var ms = new MemoryStream(WavBytes, writable: false);
                using var player = new SoundPlayer(ms);
                player.PlaySync();
            }
            catch (Exception ex)
            {
                Logger.Warn($"Sound playback error: {ex.Message}");
            }
        });
    }
}
