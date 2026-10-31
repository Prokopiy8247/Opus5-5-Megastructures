using System;
using System.IO;

namespace Megastructure.Audio
{
    /// <summary>
    /// Streaming writer for 32-bit IEEE float WAV files (WAVE_FORMAT_IEEE_FLOAT = 3, fmt chunk of 18 bytes with cbSize 0,
    /// a fact chunk with the frame count, then data). The RIFF, fact and data sizes are patched on Dispose, so a render
    /// of any length streams to disk without a large buffer. Not used on the audio thread.
    /// </summary>
    public sealed class WavStreamWriter : IDisposable
    {
        readonly FileStream _fs;
        readonly BinaryWriter _w;
        readonly int _channels;
        readonly byte[] _bytes = new byte[64 * 1024];
        long _frames;
        long _riffSizePos, _factPos, _dataSizePos;
        bool _closed;

        public WavStreamWriter(string path, int channels, int sampleRate)
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            _channels = channels;
            _fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16);
            _w = new BinaryWriter(_fs);
            _w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            _riffSizePos = _fs.Position;
            _w.Write(0);
            _w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
            _w.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            _w.Write(18);
            _w.Write((short)3);                          // IEEE float
            _w.Write((short)channels);
            _w.Write(sampleRate);
            _w.Write(sampleRate * channels * 4);         // byte rate
            _w.Write((short)(channels * 4));             // block align
            _w.Write((short)32);                         // bits per sample
            _w.Write((short)0);                          // cbSize
            _w.Write(new[] { (byte)'f', (byte)'a', (byte)'c', (byte)'t' });
            _w.Write(4);
            _factPos = _fs.Position;
            _w.Write(0);
            _w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            _dataSizePos = _fs.Position;
            _w.Write(0);
        }

        public long FramesWritten => _frames;

        /// <summary>Append <paramref name="frames"/> interleaved frames from <paramref name="interleaved"/>.</summary>
        public void Write(float[] interleaved, int frames)
        {
            int samples = frames * _channels;
            int offset = 0;
            while (offset < samples)
            {
                int n = Math.Min(samples - offset, _bytes.Length / 4);
                Buffer.BlockCopy(interleaved, offset * 4, _bytes, 0, n * 4);
                _w.Write(_bytes, 0, n * 4);
                offset += n;
            }
            _frames += frames;
        }

        public void Dispose()
        {
            if (_closed) return;
            _closed = true;
            long dataBytes = _frames * _channels * 4;
            _w.Flush();
            long end = _fs.Position;
            _fs.Position = _riffSizePos; _w.Write((int)Math.Min(end - 8, int.MaxValue));
            _fs.Position = _factPos; _w.Write((int)Math.Min(_frames, int.MaxValue));
            _fs.Position = _dataSizePos; _w.Write((int)Math.Min(dataBytes, int.MaxValue));
            _w.Flush();
            _w.Dispose();
            _fs.Dispose();
        }
    }

    /// <summary>One-call WAV export of an interleaved float buffer.</summary>
    public static class WavWriter
    {
        public static void WriteFloat32(string path, float[] interleaved, int channels, int sampleRate, int frames = -1)
        {
            if (frames < 0) frames = interleaved.Length / channels;
            using (var w = new WavStreamWriter(path, channels, sampleRate)) w.Write(interleaved, frames);
        }
    }
}
