using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using InfoStrat.MotionFx;
using InfoStrat.MotionFx.Devices;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using Ionic.Zip;

namespace ArtofKinect.Common
{
    public class MotionFrameAvailableEventArgs : EventArgs
    {
        public MotionFrame MotionFrame { get; private set; }

        public MotionFrameAvailableEventArgs(MotionFrame motionFrame)
        {
            this.MotionFrame = motionFrame;
        }
    }

    public enum PointCloudPlayerStatus
    {
        NotLoaded,
        Stopped,
        Playing
    }

    public class PointCloudPlayerSource : IDisposable
    {
        #region Fields

        List<string> _filesToLoad;
        List<MotionFrame> _bufferedFrames;

        Thread _playbackThread;

        IMotionFrameSerializer _serializer;

        bool _isRunning;

        int _numFramesToBuffer = 60;
        int _nextFrameToLoadIndex = 0;

        DateTime _playbackStartUTC;

        WorkQueue<string> _loadingQueue;

        SoundPlayer _soundPlayer;

        ZipFile _zipFile;
        object _zipFileLock = new object();

        string descriptionFilename = "description.xaml";

        string audioFilename = "kinectaudio.wav";
        MemoryStream _audioStream;

        #endregion

        #region Properties

        #region Status

        private PointCloudPlayerStatus _status;
        public PointCloudPlayerStatus Status
        {
            get
            {
                return _status;
            }
            set
            {
                if (_status == value)
                    return;
                _status = value;
                OnStatusChanged();
            }
        }

        #endregion

        #region CurrentFrameIndex

        public int CurrentFrameIndex { get; private set; }

        #endregion

        #region MaxFrameIndex

        public int MaxFrameIndex { get { return _filesToLoad.Count - 1; } }

        #endregion

        #region CurrentTimeUTC

        public DateTime CurrentTimeUTC { get; private set; }

        #endregion

        #region MinTimeUTC

        public DateTime MinTimeUTC { get; private set; }

        #endregion

        #region MaxTimeUTC

        public DateTime MaxTimeUTC { get; private set; }

        #endregion

        #region FileFPS

        public double FileFPS { get; private set; }

        #endregion

        #endregion

        #region Events

        #region StatusChanged

        public event EventHandler StatusChanged;

        protected void OnStatusChanged()
        {
            if (StatusChanged == null)
                return;

            StatusChanged(this, EventArgs.Empty);
        }

        #endregion

        #region PlaybackEnded

        public event EventHandler PlaybackEnded;

        protected void OnPlaybackEnded()
        {
            if (PlaybackEnded == null)
                return;
            PlaybackEnded(this, EventArgs.Empty);
        }

        #endregion

        #region MotionFrameAvaiable

        public event EventHandler<MotionFrameAvailableEventArgs> MotionFrameAvailable;

        protected void OnMotionFrameAvailable(MotionFrame frame)
        {
            if (MotionFrameAvailable == null)
                return;

            MotionFrameAvailable(this, new MotionFrameAvailableEventArgs(frame));
        }

        #endregion

        #endregion

        #region Constructors

        public PointCloudPlayerSource(IMotionFrameSerializer serializer)
        {
            _serializer = serializer;
            _filesToLoad = new List<string>();
            _bufferedFrames = new List<MotionFrame>();

            _loadingQueue = new WorkQueue<string>();
            _loadingQueue.Callback = LoadFrameWorker;

            Unload();

            CreatePlaybackThread();
            _soundPlayer = new SoundPlayer();

        }

        #endregion

        #region Public Methods

        public void Load(string filename)
        {
            Reset();

            if (!File.Exists(filename))
            {
                Unload();
                return;
            }

            lock (_zipFileLock)
            {
                if (_zipFile != null)
                {
                    _zipFile.Dispose();
                    _zipFile = null;
                }

                _zipFile = new ZipFile(filename);

                if (!_zipFile.EntryFileNames.Contains(descriptionFilename))
                {
                    Unload();
                    return;
                }

                var frameFiles = _zipFile.EntryFileNames.Where(s => s.Substring(s.Length - 3, 3) == "mfx");

                _filesToLoad = frameFiles.OrderBy(f => f).ToList();

                if (_filesToLoad.Count == 0)
                {
                    Unload();
                    return;
                }

                if (_zipFile.EntryFileNames.Contains(audioFilename))
                {
                    if (_audioStream != null)
                    {
                        _audioStream.Dispose();
                        _audioStream = null;
                    }
                    _audioStream = new MemoryStream();
                    _zipFile[audioFilename].Extract(_audioStream);
                    _audioStream.Position = 0;
                    _soundPlayer.LoadWavStream(_audioStream);
                }

                using (var settingsStream = new MemoryStream())
                {
                    _zipFile[descriptionFilename].Extract(settingsStream);
                    settingsStream.Position = 0;
                    var description = PointCloudStreamDescription.Load(settingsStream);

                    MinTimeUTC = description.RecordingStartDateTimeUTC;
                    CurrentTimeUTC = MinTimeUTC;
                    MaxTimeUTC = description.RecordingStopDateTimeUTC;

                    int count = description.FrameCount;
                    var span = MaxTimeUTC - MinTimeUTC;
                    if (count > 0 && span.TotalSeconds > 0)
                    {
                        FileFPS = count / span.TotalSeconds;
                    }
                }
            }

            Status = PointCloudPlayerStatus.Stopped;
            Seek(0);
        }

        public void Unload()
        {
            Status = PointCloudPlayerStatus.NotLoaded;
            Reset();
        }

        public void Play()
        {
            if (Status != PointCloudPlayerStatus.Playing)
            {
                var playOffset = CurrentTimeUTC - MinTimeUTC;
                _playbackStartUTC = DateTime.Now - playOffset;
                _soundPlayer.Seek(playOffset);
            }
            Status = PointCloudPlayerStatus.Playing;
            _soundPlayer.Play();
        }

        public void Stop()
        {
            if (Status == PointCloudPlayerStatus.Playing)
            {
                Status = PointCloudPlayerStatus.Stopped;
            }
            _soundPlayer.Stop();
        }

        public void Seek(int frameNumber)
        {
            if (Status == PointCloudPlayerStatus.NotLoaded)
            {
                throw new InvalidOperationException("Player not loaded");
            }
            if (frameNumber < 0 || frameNumber > MaxFrameIndex)
            {
                throw new ArgumentOutOfRangeException("frameNumber");
            }

            //TODO: intelligently reset buffer
            lock (_bufferedFrames)
            {
                _bufferedFrames.Clear();
            }
            _nextFrameToLoadIndex = frameNumber;
            CurrentFrameIndex = frameNumber;

            using (var stream = new MemoryStream())
            {
                lock (_zipFileLock)
                {
                    _zipFile[_filesToLoad[frameNumber]].Extract(stream);
                }
                stream.Position = 0;
                MotionFrameHeader header = _serializer.DeserializeHeader(stream);

                var timeOffset = header.TimeUTC - MinTimeUTC;

                if (Status == PointCloudPlayerStatus.Playing)
                {
                    _playbackStartUTC = DateTime.Now - timeOffset;
                }

                _soundPlayer.Seek(timeOffset);

            }

        }

        #endregion

        #region Private Methods

        private void Reset()
        {
            MinTimeUTC = DateTime.MinValue;
            CurrentTimeUTC = DateTime.MinValue;
            MaxTimeUTC = DateTime.MinValue;

            _playbackStartUTC = DateTime.MinValue;
            _nextFrameToLoadIndex = 0;
            CurrentFrameIndex = 0;

            _filesToLoad.Clear();

            _loadingQueue.ClearQueue();

            lock (_bufferedFrames)
            {
                _bufferedFrames.Clear();
            }

            if (_audioStream != null)
            {
                _audioStream.Dispose();
                _audioStream = null;
            }
            lock (_zipFileLock)
            {
                if (_zipFile != null)
                {
                    _zipFile.Dispose();
                    _zipFile = null;
                }
            }
        }

        private void CreatePlaybackThread()
        {
            if (_playbackThread != null && _playbackThread.IsAlive)
                return;

            _isRunning = true;
            _playbackThread = new Thread(PlaybackWorker);
            //_playbackThread.Priority = ThreadPriority.Highest;
            _playbackThread.Name = "PointCloudPlayerSource play back thread";
            _playbackThread.Start();
        }

        private void PlaybackWorker()
        {

            while (_isRunning)
            {
                BufferFrames();

                PlayFrames();

                if (Status != PointCloudPlayerStatus.Playing)
                {
                    Thread.Sleep(5);
                }
            }
        }

        private void BufferFrames()
        {
            if (Status == PointCloudPlayerStatus.NotLoaded ||
                _filesToLoad.Count == 0)
            {
                return;
            }

            int numFramesToLoadNow = _numFramesToBuffer - (_nextFrameToLoadIndex - CurrentFrameIndex);

            for (int i = 0; i < numFramesToLoadNow; i++)
            {
                if (_nextFrameToLoadIndex > MaxFrameIndex)
                    break;

                string filename = _filesToLoad[_nextFrameToLoadIndex];
                _nextFrameToLoadIndex++;

                _loadingQueue.AddWork(filename);
            }
        }

        private void LoadFrameWorker(string filename)
        {

            MotionFrame frame = null;
            using (var stream = new MemoryStream())
            {
                lock (_zipFileLock)
                {
                    if (_zipFile != null)
                    {
                        _zipFile[filename].Extract(stream);
                    }
                    else
                    {
                        return;
                    }
                }
                stream.Position = 0;
                frame = _serializer.Deserialize(stream);
            }
            if (frame == null)
            {
                throw new InvalidOperationException("MotionFrame " + filename + " not deserialized correctly");
            }
            Debug.WriteLine("Buffering frame " + filename);


            lock (_bufferedFrames)
            {
                _bufferedFrames.Add(frame);
            }
        }

        private void PlayFrames()
        {
            if (Status != PointCloudPlayerStatus.Playing)
            {
                return;
            }

            if (CurrentFrameIndex >= MaxFrameIndex)
            {
                Status = PointCloudPlayerStatus.Stopped;
                Seek(0);
                _soundPlayer.Stop();
                OnPlaybackEnded();
            }

            var timeOffset = DateTime.Now - _playbackStartUTC;

            var targetPlayerTime = MinTimeUTC + timeOffset;

            MotionFrame playFrame = null;

            int lastFrameIndex = CurrentFrameIndex;
            lock (_bufferedFrames)
            {
                while (_bufferedFrames.Count > 0)
                {
                    var frame = _bufferedFrames.OrderBy(f => f.Id).First();

                    if (frame.TimeUTC <= targetPlayerTime)
                    {
                        playFrame = frame;
                        _bufferedFrames.Remove(frame);
                        CurrentFrameIndex++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (CurrentFrameIndex - lastFrameIndex > 1)
            {
                Trace.WriteLine("Skipped a frame from " + lastFrameIndex + " to " + CurrentFrameIndex);
            }

            if (playFrame != null)
            {
                CurrentTimeUTC = playFrame.TimeUTC;

                var offsetMS = (playFrame.TimeUTC - targetPlayerTime).TotalMilliseconds;

                Debug.WriteLine("Playing frame " + CurrentFrameIndex + " id: " + playFrame.Id + " time offset: " + offsetMS.ToString("F4"));

                //_soundPlayer.Stop();
                if (Math.Abs(offsetMS) > 100)
                {
                    var span = CurrentTimeUTC - MinTimeUTC;
                    if (CurrentFrameIndex % 30 == 0)
                        Trace.WriteLine("Reseeking audio to " + span.ToString());
                    _soundPlayer.Seek(span);
                }
                //_soundPlayer.Play();
                OnMotionFrameAvailable(playFrame);
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
            _isRunning = false;

            if (_soundPlayer != null)
            {
                _soundPlayer.Dispose();
                _soundPlayer = null;
            }

            if (_playbackThread != null &&
                _playbackThread.IsAlive)
            {
                _playbackThread.Join(200);

                if (_playbackThread.IsAlive)
                {
                    _playbackThread.Abort();
                }
                _playbackThread = null;
            }

            if (_loadingQueue != null)
            {
                _loadingQueue.Dispose();
                _loadingQueue = null;
            }
        }

        ~PointCloudPlayerSource()
        {
            Dispose(false);
        }

        #endregion
    }
}
