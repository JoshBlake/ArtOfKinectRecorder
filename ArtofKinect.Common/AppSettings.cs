using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Xaml;
using System.IO;

namespace ArtofKinect.Common
{
    public class AppSettings
    {
        #region Properties

        [DefaultValue(null)]
        public string ScratchDirectory { get; set; }

        [DefaultValue(null)]
        public string RecordingsDirectory { get; set; }
        
        #endregion

        #region Static Serialization

        public static void Save(string filename, AppSettings settings)
        {
            XamlServices.Save(filename, settings);
        }

        public static AppSettings Load(string filename)
        {
            if (!File.Exists(filename))
            {
                return new AppSettings();
            }
            var settings = XamlServices.Load(filename) as AppSettings;
            if (settings == null)
                settings = new AppSettings();

            return settings;
        }

        #endregion
    }
}
