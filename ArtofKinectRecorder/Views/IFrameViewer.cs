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
        void UpdateMotionFrame(MotionSensorDevice device, MotionFrame frame);

        void Activate(MotionSensorDevice device);
        void Deactivate();
        void Clear();
    }
}
