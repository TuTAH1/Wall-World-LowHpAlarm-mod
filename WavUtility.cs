using UnityEngine;
using System.IO;

public static class WavUtility
{
    public static AudioClip ToAudioClip(byte[] wavData, string name)
    {
        // Convert byte array to AudioClip
        using (var reader = new BinaryReader(new MemoryStream(wavData)))
        {
            // RIFF header
            string riff = new string(reader.ReadChars(4));
            reader.ReadInt32();
            string wave = new string(reader.ReadChars(4));

            // fmt chunk
            string fmtChunkId = new string(reader.ReadChars(4));
            int fmtChunkSize = reader.ReadInt32();
            int audioFormat = reader.ReadInt16();
            int numChannels = reader.ReadInt16();
            int sampleRate = reader.ReadInt32();
            int byteRate = reader.ReadInt32();
            int blockAlign = reader.ReadInt16();
            int bitDepth = reader.ReadInt16();

            // Skip any non-data chunks (e.g. LIST metadata) until we find data
            string dataChunkId = new string(reader.ReadChars(4));
            int dataChunkSize = reader.ReadInt32();
            while (dataChunkId != "data" && reader.BaseStream.Position < reader.BaseStream.Length)
            {
                reader.ReadBytes(dataChunkSize); // skip this chunk
                if (reader.BaseStream.Position + 8 > reader.BaseStream.Length) break;
                dataChunkId = new string(reader.ReadChars(4));
                dataChunkSize = reader.ReadInt32();
            }

            if (dataChunkId != "data")
            {
                return null;
            }

            byte[] audioData = reader.ReadBytes(dataChunkSize);

            // Convert to Unity's format
            float[] samples = new float[audioData.Length / (bitDepth / 8)];
            int sampleIndex = 0;
            int bytesPerSample = bitDepth / 8;

            for (int i = 0; i < audioData.Length; i += bytesPerSample * numChannels)
            {
                for (int ch = 0; ch < numChannels; ch++)
                {
                    int byteIndex = i + (ch * bytesPerSample);
                    if (byteIndex + bytesPerSample > audioData.Length) break;

                    float sample = 0;
                    if (bitDepth == 16)
                    {
                        sample = System.BitConverter.ToInt16(audioData, byteIndex) / 32768f;
                    }
                    else if (bitDepth == 8)
                    {
                        sample = (audioData[byteIndex] - 128) / 128f;
                    }
                    samples[sampleIndex++] = sample;
                }
            }

            AudioClip clip = AudioClip.Create(name, samples.Length / numChannels, numChannels, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}