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
    public partial class PointCloudFrameViewer : UserControl, IFrameViewer
    {
        #region Constructors

        public PointCloudFrameViewer()
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

            if (!pointCloudImage.IsInitialized)
            {
                Activate(config);
            }
            pointCloudImage.SetMotionFrame(frame);
        }

        public void Activate(DeviceConfiguration config)
        {
            pointCloudImage.Init(config);            
            if (!pointCloudImage.IsRenderingActive)
            {
                pointCloudImage.StartRendering();
            }
            pointCloudImage.Visibility = System.Windows.Visibility.Visible;
        }

        public void Deactivate()
        {
            if (pointCloudImage.IsRenderingActive)
            {
                pointCloudImage.StopRendering();
            }
            pointCloudImage.Visibility = System.Windows.Visibility.Collapsed;
        }

        public void Clear()
        {
            Deactivate();
        }

        #endregion
    }
}
