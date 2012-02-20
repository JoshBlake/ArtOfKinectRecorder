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
using InfoStrat.MotionFx;
using InfoStrat.MotionFx.Wpf;
using InfoStrat.MotionFx.Devices;
using InfoStrat.MotionFx.ImageProcessing;

namespace ArtofKinectRecorder.Views
{
    /// <summary>
    /// Interaction logic for RawFrameViewer.xaml
    /// </summary>
    public partial class RawFrameViewer : UserControl, IFrameViewer
    {
        #region Fields

        ImageProcessorContext imageContext;
        SensorImageProcessor sensorImage;
        #endregion
        
        #region Constructors

        public RawFrameViewer()
        {
            InitializeComponent();
        }
        
        #endregion

        #region IMotionFrameViewer Implementation

        public void UpdateMotionFrame(DeviceConfiguration config, MotionFrame frame)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke((Action)delegate
                {
                    UpdateMotionFrame(config, frame);
                });
                return;
            }

            sensorImage.ProcessDepthFrame(frame.DepthFrame);
            depthImage.Source = sensorImage.DepthImageSource;
            rgbImage.Source = frame.RGBFrame.AsRgbBitmapSource();
            skeletonImage.Source = frame.Skeletons.AsSkeletonBitmapSource(frame.DepthFrame.Width, frame.DepthFrame.Height);
        }

        public void Activate(DeviceConfiguration config)
        {
            imageContext = new ImageProcessorContext();
            sensorImage = new SensorImageProcessor(imageContext);
        }

        public void Deactivate()
        {
            if (imageContext != null)
            {
                imageContext.Dispose();
                imageContext = null;
            }
            if (sensorImage != null)
            {
                sensorImage.Dispose();
                sensorImage = null;
            }
            depthImage.Source = null;
            rgbImage.Source = null;
        }

        public void Clear()
        {
            depthImage.Source = null;
            rgbImage.Source = null;
        }

        #endregion
    }
}
