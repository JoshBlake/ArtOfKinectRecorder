using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Xaml;
using System.IO;

namespace ArtofKinect.Common
{
    public class PointCloudStreamDescription
    {
        #region Properties

        [DefaultValue(0)]
        public long RecordingStartDateTimestampUTC
        {
            get
            {
                return RecordingStartDateTimeUTC.Ticks;
            }
            set
            {
                RecordingStartDateTimeUTC = new DateTime(value);
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DateTime RecordingStartDateTimeUTC { get; set; }

        [DefaultValue(0)]
        public long RecordingStopDateTimestampUTC
        {
            get
            {
                return RecordingStopDateTimeUTC.Ticks;
            }
            set
            {
                RecordingStopDateTimeUTC = new DateTime(value);
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DateTime RecordingStopDateTimeUTC { get; set; }

        [DefaultValue(0)]
        public int FrameCount { get; set; }

        #endregion

        #region Static Serialization

        public static void Save(string filename, PointCloudStreamDescription desc)
        {
            XamlServices.Save(filename, desc);
        }

        public static PointCloudStreamDescription Load(string filename)
        {
            if (!File.Exists(filename))
            {
                return new PointCloudStreamDescription();
            }
            var desc = XamlServices.Load(filename) as PointCloudStreamDescription;
            if (desc == null)
                desc = new PointCloudStreamDescription();

            return desc;
        }

        #endregion
    }
}
