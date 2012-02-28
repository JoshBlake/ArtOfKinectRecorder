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
        
        bool isRecording = false;

        DepthCodeMotionFrameSerializer serializer;

        MotionFrame lastFrame;

        PointCloudPlayerSource playerSource;
        PointCloudStreamRecorder pointRecorder;

        string _settingsFilename = "settings.xaml";

        string _currentFilename = "PointCloud0001.zip";
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

            UpdateButtonStates();

            InitConfiguration();
            //InitSensor();
            cbxKinect.IsChecked = false;
            UpdateButtonStates();

            InitSerializerAndPlayerSouce();
            pointCloudFrameViewer.Activate(currentConfiguration);

            lastFPSCheck = DateTime.Now;

            Application.Current.Exit += (s, e) =>
            {
                Cleanup();
            };
        }

        private void Cleanup()
        {
            if (pointCloudFrameViewer != null &&
                pointCloudFrameViewer.pointCloudImage != null)
            {
                pointCloudFrameViewer.pointCloudImage.Dispose();
            }

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
        }

        private void InitConfiguration()
        {
            var config = new DeviceConfiguration();
            config.DepthBufferFormat = DepthBufferFormats.Format320X240X16;
            config.VideoBufferFormat = ImageBufferFormats.Format1280X960X32;

            //config.DepthBufferFormat = DepthBufferFormats.Format640X480X16;
            //config.VideoBufferFormat = ImageBufferFormats.Format1280X960X32;
            currentConfiguration = config;
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
            playerSource.PlaybackEnded += new EventHandler(playerSource_PlaybackEnded);

            pointRecorder = new PointCloudStreamRecorder(serializer);

        }

        void playerSource_PlaybackEnded(object sender, EventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke((Action)delegate
                {
                    playerSource_PlaybackEnded(sender, e);
                });
                return;
            }
            if (isPlaying)
            {
                txtFileFPS.Text = "";
                isPlaying = false;
                UpdateButtonStates();
            }
            playerSource.Unload();
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
        }

        private void InitSensor()
        {
            sensorDevice = new KinectSdkDevice();
            sensorDevice.CompositeFrameAvailable += new CompositeFrameAvailableEventHandler(device_CompositeFrameAvailable);

            sensorDevice.Initialize(currentConfiguration);
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
            if (isRecording)
            {
                pointRecorder.AddFrame(e.MotionFrame);
            }

            if (!isPlaying)
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

        private void StartRecording()
        {
            playerSource.Unload();

            string filename = System.IO.Path.Combine(Settings.RecordingsDirectory, _currentFilename);
            pointRecorder.StartRecording(filename, Settings.ScratchDirectory);

            playerSource.Unload();
            isRecording = true;
            isPlaying = false;

            UpdateButtonStates();
        }

        private void StopRecording()
        {
            isRecording = false;
            UpdateButtonStates();

            pointRecorder.StopRecording();
        }

        private void StartPlayback()
        {
            StopSensor();

            if (playerSource.Status == PointCloudPlayerStatus.NotLoaded)
            {
                string filename = System.IO.Path.Combine(Settings.RecordingsDirectory, _currentFilename);
                playerSource.Load(filename);
            }
            txtFileFPS.Text = playerSource.FileFPS.ToString("F2");
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
            if (isRecording)
            {
                StopRecording();
                isPlaying = false;
            }
            else
            {
                isPlaying = !isPlaying;
                if (isPlaying)
                {
                    StartPlayback();
                }
                else
                {
                    StopPlayback();
                }
            }
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            if (isPlaying || isRecording)
            {
                btnPlayPause.IsEnabled = true;
                gridPlay.Visibility = System.Windows.Visibility.Collapsed;
                gridPause.Visibility = System.Windows.Visibility.Visible;
                cbxKinect.IsEnabled = false;
                btnRecord.IsEnabled = false;
            }
            else
            {
                gridPlay.Visibility = System.Windows.Visibility.Visible;
                gridPause.Visibility = System.Windows.Visibility.Collapsed;
                cbxKinect.IsEnabled = true;
                
                bool isKinectChecked = cbxKinect.IsChecked.Value;
                if (isKinectChecked)
                {
                    btnRecord.IsEnabled = true;
                    btnPlayPause.IsEnabled = false;
                }
                else
                {
                    btnRecord.IsEnabled = false;
                    btnPlayPause.IsEnabled = true;
                }
            }
            if (isRecording)
            {
                btnNext.IsEnabled = false;
                btnPrevious.IsEnabled = false;
                btnFastForward.IsEnabled = false;
                btnRewind.IsEnabled = false;
            }
            else
            {
                btnNext.IsEnabled = true;
                btnPrevious.IsEnabled = true;
                btnFastForward.IsEnabled = true;
                btnRewind.IsEnabled = true;
            }

            
        }

        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            StopSensor();
            this.Close();
        }

        private void btnPrevious_Click(object sender, RoutedEventArgs e)
        {
            if (playerSource.Status != PointCloudPlayerStatus.NotLoaded)
            {
                playerSource.Seek(0);
            }
        }

        private void btnRecord_Click(object sender, RoutedEventArgs e)
        {
            if (!isRecording)
            {
                StartRecording();
            }
        }

        private void cbxKinect_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = cbxKinect.IsChecked.Value;
            if (isChecked)
            {
                playerSource.Unload();
                InitSensor();
            }
            else
            {
                StopSensor();
            }
            UpdateButtonStates();
        }

        #endregion
    }
}