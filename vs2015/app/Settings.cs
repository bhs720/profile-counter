using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.IO;

namespace TIFPDFCounter
{
    public static class Settings
    {
        readonly static string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProFile Counter");
        readonly static string settingsFilename = "UserSettings.xml";
        readonly static string settingsFile = Path.Combine(appData, settingsFilename);
        
        public static UserSettings Current { get; private set; }

        static Settings()
        {
            Directory.CreateDirectory(appData);
        }

        public static void Load()
        {
            try
            {
                using (var stream = new FileStream(settingsFile, FileMode.Open))
                {
                    var xml = new XmlSerializer(typeof(UserSettings));
                    Current = xml.Deserialize(stream) as UserSettings;
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Default settings are loaded.", "Defaults", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Current = DefaultUserSettings;
            }
        }

        public static void Save()
        {
            try
            {
                using (var stream = new FileStream(settingsFile, FileMode.Create))
                {
                    var xml = new XmlSerializer(typeof(UserSettings));
                    xml.Serialize(stream, Current);
                }
            }
            catch { }
        }

        /// <summary>
        /// Hard-coded default user settings
        /// </summary>
        public static UserSettings DefaultUserSettings
        {
            get
            {
                var def = new UserSettings();
                def.AppWebsiteUrl = "https://bhs720.github.io/profile-counter";
                def.AppUpdateJsonUrl = "https://bhs720.github.io/profile-counter/latest_version.json";
                def.PageStore = DefaultPageStore;
                def.WindowLocation = new Point(0, 0);
                def.WindowSize = new Size(640, 480);
                def.WindowState = FormWindowState.Normal;
                def.ColorThreshold = 0.25m;
                def.PerformColorAnalysis = true;
                def.CheckForDuplicateFiles = true;
                def.CheckForProgramUpdates = true;
                def.CheckImagePixels = true;
                return def;
            }
        }

        /// <summary>
        /// Hard-coded standard page sizes
        /// </summary>
        public static List<PageSize> DefaultPageStore
        {
            get
            {
                return new List<PageSize>
                {
                    new PageSize("ANSI-A [ 8.5 × 11 ]", 8, 9, 10, 12),
                    new PageSize("Legal [ 8.5 × 14 ]", 8, 9, 13, 15),
                    new PageSize("ANSI-B [ 11 × 17 ]", 10.5m, 11.5m, 16.5m, 17.5m),
                    new PageSize("ANSI-C [ 17 × 22 ]", 16, 18, 21, 23),
                    new PageSize("ANSI-D [ 22 × 34 ]", 21, 23, 33, 35),
                    new PageSize("ANSI-E [ 34 × 44 ]", 33, 35, 43, 45),
                    new PageSize("ARCH-B [ 12 × 18 ]", 11.5m, 12.5m, 17.5m, 18.5m),
                    new PageSize("ARCH-C [ 18 × 24 ]", 17, 19, 23, 25),
                    new PageSize("ARCH-D [ 24 × 36 ]", 23, 25, 35, 37),
                    new PageSize("ARCH-E1 [ 30 × 42 ]", 29, 31, 41, 43),
                    new PageSize("ARCH-E [ 36 × 48 ]", 35, 37, 47, 49),
                    new PageSize("Large Format", 13, 99999, 13, 99999),
                    new PageSize("Small Format", 0, 99999, 0, 99999)
                };
            }
        }

        public class UserSettings
        {
            public string AppWebsiteUrl { get; set; }
            public string AppUpdateJsonUrl { get; set; }
            public List<PageSize> PageStore { get; set; }
            public Point WindowLocation { get; set; }
            public Size WindowSize { get; set; }
            public FormWindowState WindowState { get; set; }
            /// <summary>
            /// A number between 0 and 1 which represents how far away from gray a color can be before it is considered color. 
            /// 0.02 is very strict. 0.25 allows for some variation (like in JPEG artifacts).
            /// </summary>
            public decimal ColorThreshold { get; set; }
            /// <summary>
            /// Do we want to check if the page is in color (true), or get the page size only (false)
            /// </summary>
            public bool PerformColorAnalysis { get; set; }
            /// <summary>
            /// Check for program updates on startup
            /// </summary>
            public bool CheckForProgramUpdates { get; set; }
            /// <summary>
            /// Compare the MD5 sum of files to see if the file bytes are exactly the same
            /// </summary>
            public bool CheckForDuplicateFiles { get; set; }
            /// <summary>
            /// Should we check pixels exhaustively (true), or look at the image colorspace only (false)
            /// </summary>
            public bool CheckImagePixels { get; set; }
        }
    }
}
