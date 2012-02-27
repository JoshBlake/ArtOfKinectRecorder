using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace ArtofKinect.Common
{
    class SoundPlayer : IDisposable
    {
        #region Fields

        WaveOutEvent playbackDevice;
        WaveStream wavStream;
        
        #endregion

        #region Constructors

        public SoundPlayer()
        {
            //InitSoundThread();
            playbackDevice = new WaveOutEvent();
            
        }

        #endregion

        #region Public Methods

        public void Load(string filename)
        {
            if (!File.Exists(filename))
                return;
            
            var inputStream = CreateInputStream(filename);
            playbackDevice.Init(new SampleToWaveProvider(inputStream));
        }

        public void LoadWavStream(Stream stream)
        {
            var inputStream = CreateInputStream(stream);
            playbackDevice.Init(new SampleToWaveProvider(inputStream));
        }

        private ISampleProvider CreateInputStream(Stream stream)
        {
            wavStream = OpenWavStream(stream);
            return CreateSampleStream(wavStream);
        }

        private ISampleProvider CreateInputStream(string fileName)
        {
            if (fileName.EndsWith(".wav"))
            {
                wavStream = OpenWavFile(fileName);
            }
            else if (fileName.EndsWith(".mp3"))
            {
                wavStream = new Mp3FileReader(fileName);
            }
            else
            {
                throw new InvalidOperationException("Unsupported extension");
            }
            return CreateSampleStream(wavStream);
        }

        private static ISampleProvider CreateSampleStream(WaveStream fileStream)
        {
            var inputStream = new SampleChannel(fileStream);
            var sampleStream = new NotifyingSampleProvider(inputStream);
            return sampleStream;
        }

        private static WaveStream OpenWavFile(string fileName)
        {
            WaveStream readerStream = new WaveFileReader(fileName);
            if (readerStream.WaveFormat.Encoding != WaveFormatEncoding.Pcm)
            {
                readerStream = WaveFormatConversionStream.CreatePcmStream(readerStream);
                readerStream = new BlockAlignReductionStream(readerStream);
            }
            return readerStream;
        }

        private static WaveStream OpenWavStream(Stream stream)
        {
            WaveStream readerStream = new WaveFileReader(stream);
            if (readerStream.WaveFormat.Encoding != WaveFormatEncoding.Pcm)
            {
                readerStream = WaveFormatConversionStream.CreatePcmStream(readerStream);
                readerStream = new BlockAlignReductionStream(readerStream);
            }
            return readerStream;
        }

        public void Play()
        {
            if (playbackDevice != null && wavStream != null && playbackDevice.PlaybackState != PlaybackState.Playing)
            {
                playbackDevice.Play();
            }
        }

        public void Stop()
        {
            playbackDevice.Stop();
        }

        public void Seek(TimeSpan offset)
        {
            if (wavStream == null)
                return;
            wavStream.CurrentTime = offset;
        }

        private void SoundWorker()
        {

        }

        #endregion

        #region Private Methods

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (wavStream != null)
            {
                wavStream.Dispose();
                wavStream = null;
            }
            if (playbackDevice != null)
            {
                playbackDevice.Stop();
                playbackDevice.Dispose();
                playbackDevice = null;
            }
        }

        ~SoundPlayer()
        {
            Dispose(false);
        }

        #endregion

    }
}
