using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Surface;
using Microsoft.Surface.Presentation;
using Microsoft.Surface.Presentation.Controls;
using Microsoft.Surface.Presentation.Input;
using InfoStrat.MotionFx.Devices;
using InfoStrat.MotionFx;
using InfoStrat.MotionFx.PointCloud.DepthCoding;
using InfoStrat.MotionFx.Wpf;
using System.Threading.Tasks;
using System.IO;
using ArtofKinect.Common;
using System.Windows.Threading;
using System.Threading;
using ArtofKinectRecorder.Views;
using System.Diagnostics;

namespace ArtofKinectRecorder
{
    /// <summary>
    /// Interaction logic for SurfaceWindow1.xaml
    /// </summary>
    public partial class ArtofKinectRecorderWindow : SurfaceWindow
    {
        #region Fields

        bool isPlaying = false;

        KinectSdkDevice sensorDevice;
        
        DeviceConfiguration currentConfiguration;

        double fps;
        int fpsCount;
        DateTime lastFPSCheck;
        
        bool isRecordingOn = false;
        bool isShowingSavedFrame = false;

        DepthCodeMotionFrameSerializer serializer;

        MotionFrame lastFrame;

        PointCloudPlayerSource playerSource;
        PointCloudStreamRecorder pointRecorder;

        string _settingsFilename = "settings.xaml";

        #endregion

        #region Properties

        AppSettings _settings;
        AppSettings Settings
        {
            get
            {
                if (_settings == null)
                {
                    _settings = AppSettings.Load(_settingsFilename);
                }
                return _settings;
            }
            set
            {
                _settings = value;
                AppSettings.Save(_settingsFilename, _settings);
            }
        }

        #endregion

        #region Constructors

        public ArtofKinectRecorderWindow()
        {
            InitializeComponent();

            VerifySettings();

            UpdatePlayPauseIconVisibility();

            UpdateRecordingButtonVisibility();

            InitSensor();
            InitSerializerAndPlayerSouce();
            pointCloudFrameViewer.Activate(currentConfiguration);

            lastFPSCheck = DateTime.Now;

            Application.Current.Exit += (s, e) =>
            {
                
                pointCloudFrameViewer.pointCloudImage.Dispose();
                //pointCloudFrameViewer2.Deactivate();
                //pointCloudFrameViewer2.pointCloudImage.Dispose();
                if (playerSource != null)
                {
                    playerSource.Dispose();
                    playerSource = null;
                }
                if (pointRecorder != null)
                {
                    pointRecorder.Dispose();
                    pointRecorder = null;
                }

                if (sensorDevice != null)
                {
                    sensorDevice.Dispose();
                    sensorDevice = null;
                }
            };
        }

        private void VerifySettings()
        {
            if (!Directory.Exists(Settings.RecordingsDirectory))
            {
                Directory.CreateDirectory(Settings.RecordingsDirectory);
            }

            if (!Directory.Exists(Settings.ScratchDirectory))
            {
                Directory.CreateDirectory(Settings.ScratchDirectory);
            }
        }

        #endregion

        #region Private Methods

        private void UpdateFrameViewer(MotionFrame frame)
        {
            if (pointCloudFrameViewer != null)
            {
                currentConfiguration = new DeviceConfiguration()
                    {
                        DepthBufferFormat = new BufferFormat(frame.DepthFrame.Width, frame.DepthFrame.Height, frame.DepthFrame.PixelFormat),
                        VideoBufferFormat = new BufferFormat(frame.RGBFrame.Width, frame.RGBFrame.Height, frame.RGBFrame.PixelFormat),
                    };
                pointCloudFrameViewer.UpdateMotionFrame(currentConfiguration, frame);                
            }
        }

        private void InitSerializerAndPlayerSouce()
        {
            serializer = new DepthCodeMotionFrameSerializer();
            serializer.JpegCompression = 60;
            serializer.UsersOnly = false;
            serializer.DownSize640to320 = false;
            serializer.IncludeUserFrame = true;
            serializer.CompressionQuality = InfoStrat.MotionFx.PointCloud.CompressionQuality.High;

            playerSource = new PointCloudPlayerSource(serializer);
            playerSource.MotionFrameAvailable += new EventHandler<MotionFrameAvailableEventArgs>(playerSource_MotionFrameAvailable);
            playerSource.StatusChanged += new EventHandler(playerSource_StatusChanged);

            pointRecorder = new PointCloudStreamRecorder(serializer);

            playerSource.Load("Recording/", "frame*.mfx", "Recording/kinectaudio.wav");
        }

        void playerSource_StatusChanged(object sender, EventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke((Action)delegate
                {
                    playerSource_StatusChanged(sender, e);
                });
                return;
            }
            if (playerSource.Status != PointCloudPlayerStatus.Playing && isPlaying)
            {
                isShowingSavedFrame = false;

                if (sensorDevice == null)
                {
                    InitSensor();
                }
                isPlaying = false;
                UpdatePlayPauseIconVisibility();
            }
        }

        private void InitSensor()
        {
            var config = new DeviceConfiguration();
            config.DepthBufferFormat = DepthBufferFormats.Format640X480X16;
            config.VideoBufferFormat = ImageBufferFormats.Format1280X960X32;
            currentConfiguration = config;

            sensorDevice = new KinectSdkDevice();
            sensorDevice.CompositeFrameAvailable += new CompositeFrameAvailableEventHandler(device_CompositeFrameAvailable);

            sensorDevice.Initialize(config);
            sensorDevice.SetTiltAngle(0);
        }

        private void StopSensor()
        {
            if (sensorDevice != null)
            {
                sensorDevice.CompositeFrameAvailable -= new CompositeFrameAvailableEventHandler(device_CompositeFrameAvailable);
                sensorDevice.Shutdown();
                sensorDevice = null;
            }
        }

        void playerSource_MotionFrameAvailable(object sender, MotionFrameAvailableEventArgs e)
        {
            DisplayFrame(e.MotionFrame);
        }

        void device_CompositeFrameAvailable(object sender, CompositeFrameAvailableEventArgs e)
        {
            if (isRecordingOn)
            {
                pointRecorder.AddFrame(e.MotionFrame);
            }

            if (!isShowingSavedFrame)
            {
                DisplayFrame(e.MotionFrame);
            }
        }

        private void DisplayFrame(MotionFrame frame)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke((Action)delegate
                {
                    DisplayFrame(frame);
                });
                return;
            }
            lastFrame = frame;

            UpdateFrameViewer(frame);

            UpdateCompressionSizes(frame);

            CheckFPS();
            txtFPS.Text = fps.ToString("F1");
        }

        private void UpdateCompressionSizes(MotionFrame frame)
        {
            txtFrameId.Text = "0";

        }

        private void CheckFPS()
        {
            var now = DateTime.Now;
            fpsCount++;
            var span = (now - lastFPSCheck);
            if (span.TotalSeconds > 1)
            {
                fps = fpsCount / span.TotalSeconds;
                fpsCount = 0;
                lastFPSCheck = now;
            }
        }

        private void LoadFrame(string filename)
        {
            if (!File.Exists(filename))
            {
                return;
            }
            isShowingSavedFrame = true;
            var frame = serializer.Load(filename);
            
            DisplayFrame(frame);
        }
        
        private void StartRecording()
        {
            string filename = "PointCloud0001.zip";
            filename = System.IO.Path.Combine(Settings.RecordingsDirectory, filename);
            pointRecorder.StartRecording(filename, Settings.ScratchDirectory);
           
            StopPlayback();
            isRecordingOn = true;
            isPlaying = false;
            
            UpdateRecordingButtonVisibility();
        }

        private void StopRecording()
        {
            isRecordingOn = false;
            UpdateRecordingButtonVisibility();

            pointRecorder.StopRecording();

            //playerSource.Load("Recording/", "frame*.mfx", "Recording/kinectaudio.wav");
        }

        private void StartPlayback()
        {
            StopSensor();

            isShowingSavedFrame = true;

            if (playerSource.Status == PointCloudPlayerStatus.NotLoaded)
            {
                playerSource.Load("Recording/", "frame*.mfx", "Recording/kinectaudio.wav");
            }
            playerSource.Play();
        }
        
        private void StopPlayback()
        {
            playerSource.Stop();
        }

        #endregion

        #region Interaction
        
        private void btnPlayPause_Click(object sender, RoutedEventArgs e)
        {
            isPlaying = !isPlaying;
            if (isRecordingOn)
            {
                StopRecording();
            }
            else if (isPlaying)
            {                
                StartPlayback();
            }
            else
            {
                StopPlayback();
            }
            UpdatePlayPauseIconVisibility();
        }

        private void UpdatePlayPauseIconVisibility()
        {
            if (isPlaying)
            {
                gridPlay.Visibility = System.Windows.Visibility.Collapsed;
                gridPause.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                gridPlay.Visibility = System.Windows.Visibility.Visible;
                gridPause.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void UpdateRecordingButtonVisibility()
        {
            if (isRecordingOn)
            {
                gridPause.Visibility = System.Windows.Visibility.Visible;
                gridPlay.Visibility = System.Windows.Visibility.Collapsed;

                btnRecord.IsEnabled = false;
                btnNext.IsEnabled = false;
                btnPrevious.IsEnabled = false;
                btnFastForward.IsEnabled = false;
                btnRewind.IsEnabled = false;
            }
            else
            {
                btnRecord.IsEnabled = true;
                btnNext.IsEnabled = true;
                btnPrevious.IsEnabled = true;
                btnFastForward.IsEnabled = true;
                btnRewind.IsEnabled = true;
                UpdatePlayPauseIconVisibility();
            }
        }


        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            StopSensor();
            this.Close();
        }

        #endregion

        private void btnPrevious_Click(object sender, RoutedEventArgs e)
        {
            playerSource.Seek(0);
        }

        private void btnRecord_Click(object sender, RoutedEventArgs e)
        {
            if (!isRecordingOn)
            {
                StartRecording();
            }
        }
    }
}