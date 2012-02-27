using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using InfoStrat.MotionFx;
using Ionic.Zip;

namespace ArtofKinect.Common
{
    public class PointCloudStreamRecorder : IDisposable
    {
        #region Fields

        string _descriptionFilename = "description.xaml";

        PointCloudStreamDescription _description;

        IMotionFrameSerializer _serializer;

        WorkQueue<MotionFrame> _frameQueue;

        string _filename;
        string _scratchDirectory;

        SoundRecording soundRecording;
        
        #endregion

        #region Properties

        bool _isRecording;
        public bool IsRecording
        {
            get
            {
                return _isRecording;
            }
        }

        #endregion

        #region Constructors

        public PointCloudStreamRecorder(IMotionFrameSerializer serializer)
        {
            this._serializer = serializer;

            SetupFrameQueue();

            soundRecording = new SoundRecording();
        }

        #endregion

        #region Public Methods

        public void StartRecording(string filename, string scratchDirectory)
        {
            if (_isRecording)
            {
                throw new InvalidOperationException("Recording in process. Call StopRecording() first.");
            }

            _isRecording = true;

            _description = new PointCloudStreamDescription()
            {
                FrameCount = 0
            };

            this._scratchDirectory = scratchDirectory;
            this._filename = filename;

            VerifyDirectories();

            soundRecording.Start(scratchDirectory);
        }

        public void AddFrame(MotionFrame frame)
        {
            if (_isRecording)
            {
                _frameQueue.AddWork(frame);
            }
        }

        public void StopRecording()
        {
            if (!_isRecording)
            {
                throw new InvalidOperationException("Cannot stop recording when recording is not in progress.");
            }
            _isRecording = false;
            soundRecording.Stop();
            SaveDescription();

            ZipAllFiles(_scratchDirectory, _filename);
        }

        #endregion

        #region Private Methods

        private void VerifyDirectories()
        {
            string finalDirectory = Path.GetDirectoryName(Path.GetFullPath(_filename));
            if (!Directory.Exists(finalDirectory))
            {
                Directory.CreateDirectory(finalDirectory);
            }

            if (!Directory.Exists(_scratchDirectory))
            {
                Directory.CreateDirectory(_scratchDirectory);
            }

            var files = Directory.EnumerateFiles(_scratchDirectory);

            files.ToList().ForEach(File.Delete);
        }

        private void SetupFrameQueue()
        {
            _frameQueue = new WorkQueue<MotionFrame>();
            _frameQueue.Callback = ProcessFrame;
            _frameQueue.MaxQueueLength = 5;
        }

        private void ShutdownFrameQueue()
        {
            if (_frameQueue == null)
            {
                _frameQueue.Dispose();
                _frameQueue = null;
            }
        }

        private void ZipAllFiles(string ScratchDirectory, string _filename)
        {
            using (var zip = new ZipFile(_filename))
            {
                var files = Directory.EnumerateFiles(ScratchDirectory);

                zip.AddFiles(files, "");

                zip.Save();
            }
        }

        private void SaveDescription()
        {
            string filename = Path.Combine(_scratchDirectory, _descriptionFilename);
            PointCloudStreamDescription.Save(filename, _description);
        }

        void ProcessFrame(MotionFrame frame)
        {
            int currentFrameId = _description.FrameCount;

            if (currentFrameId == 0)
            {
                _description.RecordingStartDateTimeUTC = frame.TimeUTC;
            }

            string filename = "frame" + currentFrameId.ToString("D8") + ".mfx";
            filename = Path.Combine(_scratchDirectory, filename);
           
            var bytes = _serializer.Serialize(frame);
            File.WriteAllBytes(filename, bytes);

            currentFrameId++;
            _description.FrameCount = currentFrameId;
            _description.RecordingStopDateTimeUTC = frame.TimeUTC;
            if (currentFrameId % 10 == 0)
            {
                SaveDescription();
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
            ShutdownFrameQueue();

            if (soundRecording != null)
            {
                soundRecording.Stop();
                soundRecording.Dispose();
                soundRecording = null;
            }
        }

        ~PointCloudStreamRecorder()
        {
            Dispose(false);
        }

        #endregion
    }
}
