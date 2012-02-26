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
        Queue<MotionFrame> _bufferedFrames;

        Thread _playbackThread;

        IMotionFrameSerializer _serializer;

        bool _isRunning;

        int _numFramesToBuffer = 60;
        int _nextFrameToLoadIndex = 0;

        DateTime _playbackStartUTC;

        WorkQueue<string> _loadingQueue;

        SoundPlayer _soundPlayer;

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
            _bufferedFrames = new Queue<MotionFrame>();

            _loadingQueue = new WorkQueue<string>();
            _loadingQueue.Callback = LoadFrameWorker;

            Unload();

            CreatePlaybackThread();
            _soundPlayer = new SoundPlayer();

        }

        #endregion

        #region Public Methods

        public void Load(string directory, string frameMask, string audioFilename)
        {
            Reset();

            if (!Directory.Exists("Recording/"))
            {
                Directory.CreateDirectory("Recording/");
            }

            _soundPlayer.Load(audioFilename);
            
            IEnumerable<string> files = Directory.EnumerateFiles("Recording/", "frame*.mfx");

            _filesToLoad = new List<string>(files.OrderBy(f => f));

            if (_filesToLoad.Count == 0)
            {
                Unload();
                return;
            }
            MotionFrameHeader header = _serializer.LoadHeader(_filesToLoad.FirstOrDefault());

            MinTimeUTC = header.TimeUTC;
            CurrentTimeUTC = header.TimeUTC;

            header = _serializer.LoadHeader(_filesToLoad.LastOrDefault());

            MaxTimeUTC = header.TimeUTC;

            Seek(0);
            Status = PointCloudPlayerStatus.Stopped;
        }

        private void Unload()
        {
            Status = PointCloudPlayerStatus.NotLoaded;
            Reset();
        }

        public void Play()
        {
            if (Status != PointCloudPlayerStatus.Playing && CurrentFrameIndex == 0)
            {
                _playbackStartUTC = DateTime.Now;
            }
            Status = PointCloudPlayerStatus.Playing;
            _soundPlayer.Play();
        }

        public void Stop()
        {
            Status = PointCloudPlayerStatus.Stopped;
            _soundPlayer.Stop();
        }

        public void Seek(int frameNumber)
        {
            if (frameNumber < 0 || frameNumber > MaxFrameIndex)
            {
                if (frameNumber == 0)
                    return;
                throw new ArgumentOutOfRangeException("frameNumber");
            }

            if (Status == PointCloudPlayerStatus.Playing)
            {
                //TODO recalculate _playbackStartUTC
            }

            //TODO: intelligently reset buffer
            _bufferedFrames.Clear(); 
            _nextFrameToLoadIndex = frameNumber;
            CurrentFrameIndex = frameNumber;

            MotionFrameHeader header = _serializer.LoadHeader(_filesToLoad[frameNumber]);

            var timeOffset = header.TimeUTC - MinTimeUTC;
            _soundPlayer.Seek(timeOffset);
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

            _bufferedFrames.Clear();
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
            var frame = _serializer.Load(filename);
            Trace.WriteLine("Buffering frame " + filename);

            lock (_bufferedFrames)
            {
                _bufferedFrames.Enqueue(frame);
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
            }

            var currentTime = DateTime.Now;
            var timeOffset = currentTime - _playbackStartUTC;
            _soundPlayer.Seek(timeOffset);
            var targetPlayerTime = MinTimeUTC + timeOffset;

            MotionFrame playFrame = null;

            lock (_bufferedFrames)
            {
                while (_bufferedFrames.Count > 0)
                {
                    var frame = _bufferedFrames.Peek();

                    if (frame.TimeUTC <= targetPlayerTime)
                    {
                        playFrame = _bufferedFrames.Dequeue();
                        CurrentFrameIndex++;
                    }
                    else
                    {
                        break;
                    }
                }
                if (_bufferedFrames.Count == 0)
                {
                    Trace.WriteLine("empty buffer");
                }
            }

            if (playFrame != null)
            {
                CurrentTimeUTC = playFrame.TimeUTC;

                var offsetMS = (playFrame.TimeUTC - targetPlayerTime).TotalMilliseconds;

                Trace.WriteLine("Playing frame " + CurrentFrameIndex + " id: " + playFrame.Id + " time offset: " + offsetMS.ToString("F4"));

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
