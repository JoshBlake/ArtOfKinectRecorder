using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InfoStrat.MotionFx;
using InfoStrat.MotionFx.Devices;

namespace ArtofKinectRecorder.Views
{
    public interface IFrameViewer
    {
        void UpdateMotionFrame(DeviceConfiguration config, MotionFrame frame);

        void Activate(DeviceConfiguration config);
        void Deactivate();
        void Clear();
    }
}
