using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Kinect;
using System.Threading;
using System.IO;

namespace ArtofKinect.Common
{
    public class SoundRecording : IDisposable
    {
        #region Fields

        private KinectAudioSource kinectSource;
        AudioStreamEnergy energyStream;

        private const double ANGLE_CHANGE_SMOOTHING_FACTOR = 0.35;
        private bool isRunning;

        Thread audioCaptureThread;

        double angle;

        Stream kinectStream;

        byte[] buffer = new byte[4096];

        int recordingLength = 0;

        #endregion

        #region Events

        #region AudioRecorded



        #endregion

        #endregion

        #region Constructors

        public SoundRecording()
        {
            kinectSource = GetAudioSource();
            if (kinectSource == null)
                throw new InvalidOperationException("Kinect device not available");
        }

        #endregion

        #region Public Methods

        public void Start()
        {
            isRunning = true;
            kinectSource.BeamAngleMode = BeamAngleMode.Adaptive;
            kinectSource.AutomaticGainControlEnabled = false;
            recordingLength = 0;

            kinectStream = kinectSource.Start();
            energyStream = new AudioStreamEnergy(kinectStream);
            audioCaptureThread = new Thread(CaptureAudio);
            audioCaptureThread.Priority = ThreadPriority.Highest;
            audioCaptureThread.Name = "Kinect audio capture";
            audioCaptureThread.Start();
        }

        public void Stop()
        {
            isRunning = false;
        }

        #endregion

        #region Private Methods

        KinectAudioSource GetAudioSource()
        {
            //Find 1st Kinect that has a .Status==KinectStatus.Connected
            KinectSensor sensor = (from sensorToCheck in KinectSensor.KinectSensors where sensorToCheck.Status == KinectStatus.Connected select sensorToCheck).FirstOrDefault();
            if (sensor == null)
            {
                return null;
            }

            return sensor.AudioSource;
        }

        private void CaptureAudio()
        {
            string outputFileName = "Recording/kinectaudio.wav";

            using (var fileStream = new FileStream(outputFileName, FileMode.Create))
            {
                using (var sampleStream = new StreamWriter(new FileStream("Recording/kinectaudiosamples.log", FileMode.Create)))
                {
                    WavWriter.WriteWavHeader(fileStream);

                    //Simply copy the data from the stream down to the file
                    int count;
                    while (isRunning && ((count = kinectStream.Read(buffer, 0, buffer.Length)) > 0))
                    {
                        fileStream.Write(buffer, 0, count);
                        recordingLength += count;

                        double confidence = kinectSource.SoundSourceAngleConfidence;
                        double a = ANGLE_CHANGE_SMOOTHING_FACTOR * confidence;
                        angle = (1 - a) * angle + a * kinectSource.SoundSourceAngle;

                        sampleStream.WriteLine(recordingLength + "\t" +
                                               angle + "\t" +
                                               confidence + "\t" +
                                               kinectSource.BeamAngle);
                    }

                    //Complete the wav file by updating the header
                    WavWriter.UpdateDataLength(fileStream, recordingLength);

                }
            }
        }


        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            isRunning = false;
            
            if (audioCaptureThread != null &&
                audioCaptureThread.IsAlive)
            {
                audioCaptureThread.Join(200);

                if (audioCaptureThread.IsAlive)
                {
                    audioCaptureThread.Abort();
                }
                audioCaptureThread = null;
            }

            if (kinectStream != null)
            {
                kinectStream.Dispose();
                kinectStream = null;
            }
            if (kinectSource != null)
            {
                kinectSource.Stop();
                kinectSource.Dispose();
                kinectSource = null;
            }
            if (energyStream != null)
            {
                energyStream.Dispose();
                energyStream = null;
            }
        }

        ~SoundRecording()
        {
            Dispose(false);
        }

        #endregion
    }
}
