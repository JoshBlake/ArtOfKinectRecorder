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
        WaveStream fileStream;
        
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

        private ISampleProvider CreateInputStream(string fileName)
        {
            if (fileName.EndsWith(".wav"))
            {
                fileStream = OpenWavStream(fileName);
            }
            else if (fileName.EndsWith(".mp3"))
            {
                fileStream = new Mp3FileReader(fileName);
            }
            else
            {
                throw new InvalidOperationException("Unsupported extension");
            }
            var inputStream = new SampleChannel(fileStream);
            var sampleStream = new NotifyingSampleProvider(inputStream);
            return sampleStream;
        }

        private static WaveStream OpenWavStream(string fileName)
        {
            WaveStream readerStream = new WaveFileReader(fileName);
            if (readerStream.WaveFormat.Encoding != WaveFormatEncoding.Pcm)
            {
                readerStream = WaveFormatConversionStream.CreatePcmStream(readerStream);
                readerStream = new BlockAlignReductionStream(readerStream);
            }
            return readerStream;
        }

        public void Play()
        {
            if (playbackDevice != null && fileStream != null && playbackDevice.PlaybackState != PlaybackState.Playing)
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
            if (fileStream == null)
                return;
            fileStream.CurrentTime = offset;
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
            if (fileStream != null)
            {
                fileStream.Dispose();
                fileStream = null;
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
