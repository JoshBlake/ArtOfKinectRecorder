using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace ArtofKinect.Common
{
    class AudioStreamEnergy : Stream
    {
        private readonly Stream baseStream;
        private int index = 0;

        private const int WavImageWidth = 500;
        private const int WavImageHeight = 100;
        private readonly double[] energy = new double[WavImageWidth];
        private readonly object syncRoot = new object();
        const int samplesPerPixel = 10;
        int sampleCount = 0;
        double avgSample = 0;

        public AudioStreamEnergy(Stream stream)
        {
            baseStream = stream;
        }

        public override bool CanRead
        {
            get { return baseStream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return baseStream.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return baseStream.CanWrite; }
        }

        public override void Flush()
        {
            baseStream.Flush();
        }

        public override long Length
        {
            get { return baseStream.Length; }
        }

        public override long Position
        {
            get { return baseStream.Position; }
            set { baseStream.Position = value; }
        }

        public void GetEnergy(double[] energyBuffer)
        {
            lock (syncRoot)
            {
                int energyIndex = index;
                for (int i = 0; i < energy.Length; i++)
                {
                    energyBuffer[i] = energy[energyIndex];
                    energyIndex++;
                    if (energyIndex >= energy.Length)
                        energyIndex = 0;

                }
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int retVal = baseStream.Read(buffer, offset, count);
            double a = 0.3;
            lock (syncRoot)
            {
                for (int i = 0; i < retVal; i += 2)
                {

                    short sample = BitConverter.ToInt16(buffer, i + offset);
                    avgSample += sample * sample;
                    sampleCount++;

                    if (sampleCount == samplesPerPixel)
                    {
                        avgSample /= samplesPerPixel;

                        energy[index] = .2 + (avgSample * 11) / (int.MaxValue / 2); //2^30 = (2^15)^2
                        energy[index] = energy[index] > 10 ? 10 : energy[index];

                        if (index > 0)
                            energy[index] = energy[index] * a + (1 - a) * energy[index - 1];

                        index++;
                        if (index >= energy.Length)
                            index = 0;
                        avgSample = 0;
                        sampleCount = 0;
                    }

                }
            }

            return retVal;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return baseStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            baseStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            baseStream.Write(buffer, offset, count);
        }
    }
}
