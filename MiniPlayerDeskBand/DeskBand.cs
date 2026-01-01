using System;
using System.Runtime.InteropServices;
using System.Windows;
using CSDeskBand;
using CSDeskBand.Wpf;

namespace MiniPlayerDeskBand
{
    [ComVisible(true)]
    [Guid("A8D15632-6804-4767-873B-9A7763695272")] // Unique GUID for the toolbar
    [CSDeskBandRegistration(Name = "MiniPlayer", ShowDeskBand = true)]
    public class DeskBand : CSDeskBandWpf
    {
        public DeskBand()
        {
            // Define initial size
            Options.MinHorizontalSize = new CSDeskBand.Size(200, 30);
            Options.MinVerticalSize = new CSDeskBand.Size(200, 30);
            Options.HorizontalSize = new CSDeskBand.Size(200, 30); // Default width
            
            // Set the visual content
            Content = new PlayerControl();
        }



    }
}
