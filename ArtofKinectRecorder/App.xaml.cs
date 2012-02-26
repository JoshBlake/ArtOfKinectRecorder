using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using Microsoft.Surface.Presentation.Palettes;
using Microsoft.Surface.Presentation;

namespace ArtofKinectRecorder
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            base.OnStartup(e);

            SurfacePalette myPalette = new LightSurfacePalette();
            SurfaceColors.SetDefaultApplicationPalette(myPalette);
        }

    }
}