// WavWriter: RIFF/WAVE output for the offline renders and the -audio-dump of the built player (14.3.1, 11.9).
// PCM16 (format 1, 44-byte header) with deterministic TPDF dither of +-1 LSB, or IEEE float32 (format 3, with
// cbSize and a fact chunk, 58-byte header; overs above 1.0 stay visible). Little-endian (BinaryWriter).
// WavStreamWriter streams block by block (a 10-minute render never has to sit in memory) and patches the
// sizes on Dispose. Main/test thread only (file I/O). Pure C#, no UnityEngine.
using System;
using System.IO;

namespace Megastructure.Audio
{
    public enum WavFormat { Pcm16 = 1, Float32 = 3 }

    public static class WavWriter
    {
        /// <summary>One-shot write of an interleaved buffer (sampleCount = frames x channels).</summary>
        public static void Write(string path, float[] interleaved, int sampleCount, int channels, int sampleRate, WavFormat format, int ditherSeed = 1)
        {
            using (var w = new WavStreamWriter(path, channels, sampleRate, format, ditherSeed))
                w.WriteSamples(interleaved, 0, sampleCount);
        }

        public static void WritePcm16(string path, float[] interleaved, int sampleCount, int channels, int sampleRate)
        {
            Write(path, interleaved, sampleCount, channels, sampleRate, WavFormat.Pcm16);
        }

        public static void WriteFloat32(string path, float[] interleaved, int sampleCount, int channels, int sampleRate)
        {
            Write(path, interleaved, sampleCount, channels, sampleRate, WavFormat.Float32);
        }

        /// <summary>Reads a PCM16 or float32 WAV written by this class (tests, the dump comparison).</summary>
        public static float[] Read(string path, out int channels, out int sampleRate)
        {
            channels = 0; sampleRate = 0;
            byte[] raw = File.ReadAllBytes(path);
            if (raw.Length < 12 || raw[0] != 'R' || raw[1] != 'I' || raw[2] != 'F' || raw[3] != 'F') throw new InvalidDataException("not RIFF: " + path);
            int pos = 12, tag = 0, bits = 0;
            float[] data = null;
            while (pos + 8 <= raw.Length)
            {
                int size = BitConverter.ToInt32(raw, pos + 4);
                int body = pos + 8;
                if (raw[pos] == 'f' && raw[pos + 1] == 'm' && raw[pos + 2] == 't' && raw[pos + 3] == ' ')
                {
                    tag = BitConverter.ToInt16(raw, body);
                    channels = BitConverter.ToInt16(raw, body + 2);
                    sampleRate = BitConverter.ToInt32(raw, body + 4);
                    bits = BitConverter.ToInt16(raw, body + 14);
                }
                else if (raw[pos] == 'd' && raw[pos + 1] == 'a' && raw[pos + 2] == 't' && raw[pos + 3] == 'a')
                {
                    int avail = Math.Min(size, raw.Length - body);
                    if (tag == 1 && bits == 16)
                    {
                        data = new float[avail / 2];
                        for (int i = 0; i < data.Length; i++) data[i] = BitConverter.ToInt16(raw, body + 2 * i) / 32768f;
                    }
                    else if (tag == 3 && bits == 32)
                    {
                        data = new float[avail / 4];
                        Buffer.BlockCopy(raw, body, data, 0, data.Length * 4);
                    }
                    else throw new InvalidDataException("unsupported WAV format " + tag + "/" + bits);
                }
                pos = body + size + (size & 1);
            }
            if (data == null) throw new InvalidDataException("no data chunk: " + path);
            return data;
        }
    }

    /// <summary>Streaming WAV writer; also an IAudioBlockSink so the offline renderer can write straight to disk.</summary>
    public sealed class WavStreamWriter : IDisposable, IAudioBlockSink
    {
        readonly FileStream m_fs;
        readonly BinaryWriter m_w;
        readonly WavFormat m_format;
        readonly int m_channels, m_rate;
        readonly byte[] m_bytes = new byte[1 << 16];
        long m_samples;
        XorShift32 m_dither;
        bool m_closed;

        public WavStreamWriter(string path, int channels, int sampleRate, WavFormat format, int ditherSeed = 1)
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            m_fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16);
            m_w = new BinaryWriter(m_fs);
            m_format = format;
            m_channels = Math.Max(1, channels);
            m_rate = sampleRate;
            m_dither = XorShift32.Stream(ditherSeed, 0xD17Bu);
            WriteHeader(0);
        }

        public long SamplesWritten { get { return m_samples; } }

        void WriteHeader(long dataSamples)
        {
            int bytesPer = m_format == WavFormat.Pcm16 ? 2 : 4;
            long dataBytes = dataSamples * bytesPer;
            if (dataBytes > int.MaxValue - 64) dataBytes = int.MaxValue - 64;   // RIFF size limit (about 3.7 h stereo PCM16)
            m_w.Write(0x46464952);                                          // "RIFF"
            if (m_format == WavFormat.Pcm16) m_w.Write((int)(36 + dataBytes));
            else m_w.Write((int)(4 + 26 + 12 + 8 + dataBytes));
            m_w.Write(0x45564157);                                          // "WAVE"
            m_w.Write(0x20746D66);                                          // "fmt "
            m_w.Write(m_format == WavFormat.Pcm16 ? 16 : 18);
            m_w.Write((short)m_format);
            m_w.Write((short)m_channels);
            m_w.Write(m_rate);
            m_w.Write(m_rate * m_channels * bytesPer);
            m_w.Write((short)(m_channels * bytesPer));
            m_w.Write((short)(bytesPer * 8));
            if (m_format == WavFormat.Float32)
            {
                m_w.Write((short)0);                                        // cbSize
                m_w.Write(0x74636166); m_w.Write(4);                        // "fact"
                m_w.Write((int)(dataSamples / m_channels));
            }
            m_w.Write(0x61746164);                                          // "data"
            m_w.Write((int)dataBytes);
        }

        public void WriteSamples(float[] x, int offset, int count)
        {
            if (m_closed) throw new ObjectDisposedException("WavStreamWriter");
            int bytesPer = m_format == WavFormat.Pcm16 ? 2 : 4;
            int per = m_bytes.Length / bytesPer;
            int i = 0;
            while (i < count)
            {
                int k = Math.Min(per, count - i);
                if (m_format == WavFormat.Pcm16)
                {
                    for (int j = 0; j < k; j++)
                    {
                        float v = x[offset + i + j];
                        if (float.IsNaN(v)) v = 0f;
                        // TPDF dither of +-1 LSB (two uniform values in [-0.5, 0.5) LSB)
                        float d = 0.5f * (m_dither.NextBipolar() + m_dither.NextBipolar());
                        double q = Math.Round(Math.Max(-1.0, Math.Min(1.0, v)) * 32767.0 + d);
                        if (q > 32767) q = 32767; else if (q < -32768) q = -32768;
                        short s = (short)q;
                        m_bytes[2 * j] = (byte)(s & 0xFF);
                        m_bytes[2 * j + 1] = (byte)((s >> 8) & 0xFF);
                    }
                    m_w.Write(m_bytes, 0, 2 * k);
                }
                else
                {
                    Buffer.BlockCopy(x, (offset + i) * 4, m_bytes, 0, 4 * k);
                    if (!BitConverter.IsLittleEndian)
                        for (int j = 0; j < k; j++) Array.Reverse(m_bytes, 4 * j, 4);
                    m_w.Write(m_bytes, 0, 4 * k);
                }
                i += k;
            }
            m_samples += count;
        }

        // IAudioBlockSink
        public void Begin(int sampleRate, int channels) { }
        public void Write(float[] interleaved, int frames, in AudioBlockInfo info) { WriteSamples(interleaved, 0, frames * m_channels); }
        public void End() { }

        public void Dispose()
        {
            if (m_closed) return;
            m_closed = true;
            m_w.Flush();
            m_fs.Seek(0, SeekOrigin.Begin);
            WriteHeader(m_samples);
            m_w.Flush();
            m_w.Dispose();
            m_fs.Dispose();
        }
    }
}
