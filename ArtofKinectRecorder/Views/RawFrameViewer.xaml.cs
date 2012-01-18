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

namespace ArtofKinectRecorder.Views
{
    /// <summary>
    /// Interaction logic for RawFrameViewer.xaml
    /// </summary>
    public partial class RawFrameViewer : UserControl, IFrameViewer
    {
        #region Constructors

        public RawFrameViewer()
        {
            InitializeComponent();
        }
        
        #endregion

        #region IMotionFrameViewer Implementation

        public void UpdateMotionFrame(MotionSensorDevice device, MotionFrame frame)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke((Action)delegate
                {
                    UpdateMotionFrame(device, frame);
                });
                return;
            }
            
            depthImage.Source = frame.DepthFrame.AsDepthUserBitmapSource(frame.UserFrame);
            rgbImage.Source = frame.RGBFrame.AsRgbBitmapSource();
            skeletonImage.Source = frame.Skeletons.AsSkeletonBitmapSource(device, frame.DepthFrame.Width, frame.DepthFrame.Height);

        }

        public void Activate(MotionSensorDevice device)
        {

        }

        public void Deactivate()
        {

        }

        public void Clear()
        {

        }

        #endregion
    }
}
