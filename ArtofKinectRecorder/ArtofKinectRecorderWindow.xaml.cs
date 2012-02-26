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
        
        int lastSizeDepth = 0;
        int lastSizeAll = 0;
        int savedFrameId = 0;

        bool isRecordingOn = false;
        bool isShowingSavedFrame = false;

        DepthCodeMotionFrameSerializer serializer;

        WorkQueue<MotionFrame> frameQueue;

        MotionFrame lastFrame;
        SoundRecording soundRecording;

        PointCloudPlayerSource playerSource;

        #endregion

        #region Properties
       
        #endregion

        #region Constructors

        public ArtofKinectRecorderWindow()
        {
            InitializeComponent();
            
            UpdatePlayPauseIconVisibility();

            frameQueue = new WorkQueue<MotionFrame>();
            frameQueue.Callback = ProcessFrame;
            frameQueue.MaxQueueLength = 5;

            InitSensor();
            InitSerializer();
            InitSoundCapture();
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

                if (soundRecording != null)
                {
                    soundRecording.Stop();
                    soundRecording.Dispose();
                    soundRecording = null;
                }
                if (sensorDevice != null)
                {
                    sensorDevice.Dispose();
                    sensorDevice = null;
                }
                if (frameQueue != null)
                {
                    frameQueue.Dispose();
                    frameQueue = null;
                }
            };
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

        private void InitSoundCapture()
        {
            return;
            soundRecording = new SoundRecording();
        }
        
        private void InitSerializer()
        {
            serializer = new DepthCodeMotionFrameSerializer();
            serializer.JpegCompression = 60;
            serializer.UsersOnly = false;
            serializer.DownSize640to320 = false;
            serializer.IncludeUserFrame = true;
            serializer.CompressionQuality = InfoStrat.MotionFx.PointCloud.CompressionQuality.High;

            playerSource = new PointCloudPlayerSource(serializer);
            playerSource.MotionFrameAvailable += new EventHandler<MotionFrameAvailableEventArgs>(playerSource_MotionFrameAvailable);

            playerSource.Load("Recording/", "frame*.mfx", "Recording/kinectaudio.wav");
        }

        private void InitSensor()
        {
            var config = new DeviceConfiguration();
            config.DepthBufferFormat = DepthBufferFormats.Format640X480X16;
            config.VideoBufferFormat = ImageBufferFormats.Format1280X960X32;
            currentConfiguration = config;
            return;
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
            savedFrameId = e.MotionFrame.Id;
            DisplayFrame(e.MotionFrame);
        }

        void device_CompositeFrameAvailable(object sender, CompositeFrameAvailableEventArgs e)
        {
            frameQueue.AddWork(e.MotionFrame);
            //if (pointCloudFrameViewer2 != null)
            //{
            //    pointCloudFrameViewer2.UpdateMotionFrame(sensorDevice, e.MotionFrame);
            //}
            if (isShowingSavedFrame)
            {
                return;
            }
            else
            {
                DisplayFrame(e.MotionFrame);
            }
        }

        void ProcessFrame(MotionFrame frame)
        {
            if (isRecordingOn)
            {
                SaveFrame(frame, "Recording/frame" + frame.Id.ToString("D8") + ".mfx");
                savedFrameId = frame.Id;
            }
            else
            {
                lastSizeDepth = frame.DepthFrame.Data.Length;
                int originalRGBSize = frame.RGBFrame.Data.Length;
                lastSizeAll = lastSizeDepth + originalRGBSize;
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

            bool newIsRecordingOn = false;// cbxRecord.IsChecked.Value;
            if (!isRecordingOn && newIsRecordingOn)
            {
                StartRecording();
                isRecordingOn = newIsRecordingOn;
            }
            else if (isRecordingOn && !newIsRecordingOn)
            {
                StopRecording();
                isRecordingOn = newIsRecordingOn;
            }

            UpdateCompressionSizes(frame);

            CheckFPS();
            txtFPS.Text = fps.ToString("F1");
        }

        private void UpdateCompressionSizes(MotionFrame frame)
        {
            int originalDepthSize = frame.DepthFrame.Data.Length;
            
            int originalRGBSize = frame.RGBFrame.Data.Length;

            double ratio = originalDepthSize / (double)lastSizeDepth;
            //txtSizeDepth.Text = "Depth: " + lastSizeDepth.ToString() + " bytes  Ratio: " + ratio.ToString("F1") + " : 1";

            int totalSize = originalDepthSize + originalRGBSize;

            ratio = totalSize / (double)lastSizeAll;
            //txtSizeAll.Text = "All: " + lastSizeAll.ToString() + " bytes  Ratio: " + ratio.ToString("F1") + " : 1";
            txtFrameId.Text = savedFrameId.ToString();

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

        private void SaveFrame(MotionFrame frame, string filename)
        {
            var bytes = serializer.Serialize(frame);
            lastSizeDepth = serializer.DepthUserFrameSize;
            lastSizeAll = bytes.Length;
            File.WriteAllBytes(filename, bytes);
        }
        
        private void LoadFrame(string filename)
        {
            if (!File.Exists(filename))
            {
                return;
            }
            isShowingSavedFrame = true;
            var frame = serializer.Load(filename);
            
            savedFrameId = frame.Id;
            DisplayFrame(frame);
        }
        
        private void StartRecording()
        {
            soundRecording.Start();
        }

        private void StopRecording()
        {
            soundRecording.Stop();
            Thread.Sleep(100);
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
        
        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            isShowingSavedFrame = false;

            playerSource.Stop();

            if (sensorDevice == null)
            {
                InitSensor();
            }
        }

        private void btnPlayPause_Click(object sender, RoutedEventArgs e)
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
    }
}