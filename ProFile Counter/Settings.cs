using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.IO;

namespace ProFileCounter
{
    public static class Settings
    {
        readonly static string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProFile Counter");
        readonly static string settingsFile = Path.Combine(appData, "UserSettings.xml");

        static UserSettings userSettings = null;

        public static UserSettings UserSettings1 { get { return userSettings; } }

        static Settings()
        {
            if (!Directory.Exists(appData))
                Directory.CreateDirectory(appData);
        }

        public static void Load()
        {
            var reader = new XmlSerializer(typeof(UserSettings));
            FileStream fileStream = null;

            try
            {
                fileStream = new FileStream(settingsFile, FileMode.Open);
                userSettings = reader.Deserialize(fileStream) as UserSettings;
            }
            catch (Exception)
            {
                MessageBox.Show("Default settings are loaded.", "Defaults", MessageBoxButtons.OK, MessageBoxIcon.Information);
                userSettings = DefaultUserSettings;
            }
            finally
            {
                if (fileStream != null)
                    fileStream.Dispose();
            }
        }

        public static void Save()
        {
            var writer = new XmlSerializer(typeof(UserSettings));
            FileStream fileStream = null;

            try
            {
                fileStream = new FileStream(settingsFile, FileMode.Create);
                writer.Serialize(fileStream, userSettings);
            }
            catch (Exception) { }
            finally
            {
                if (fileStream != null)
                    fileStream.Dispose();
            }
        }

        static UserSettings DefaultUserSettings
        {
            get
            {
                var def = new UserSettings();
                def.PageStore = new List<PageSize>();
                def.PageStore.Add(new PageSize("ANSI-A [ 8.5 × 11 ]", 8.5f, 11f, .07f));
                def.PageStore.Add(new PageSize("Ledger [ 8.5 × 14 ]", 8.5f, 14f, .07f));
                def.PageStore.Add(new PageSize("ANSI-B [ 11 × 17 ]", 11f, 17f, .04f));
                def.PageStore.Add(new PageSize("ANSI-C [ 17 × 22 ]", 17f, 22f, .04f));
                def.PageStore.Add(new PageSize("ANSI-D [ 22 × 34 ]", 22f, 34f, .04f));
                def.PageStore.Add(new PageSize("ANSI-E [ 34 × 44 ]", 34f, 44f, .04f));
                def.PageStore.Add(new PageSize("ARCH-A [ 9 × 12 ]", 9f, 12f, .04f, false));
                def.PageStore.Add(new PageSize("ARCH-B [ 12 × 18 ]", 12f, 18f, .04f));
                def.PageStore.Add(new PageSize("ARCH-C [ 18 × 24 ]", 18f, 24f, .04f));
                def.PageStore.Add(new PageSize("ARCH-D [ 24 × 36 ]", 24f, 36f, .04f));
                def.PageStore.Add(new PageSize("ARCH-E1 [ 30 × 42 ]", 30f, 42f, .04f));
                def.PageStore.Add(new PageSize("ARCH-E [ 36 × 48 ]", 36f, 48f, .04f));
                def.WindowLocation = new Point(0, 0);
                def.WindowSize = new Size(640, 480);
                def.WindowState = FormWindowState.Normal;
                def.PDFColorThreshold = 0.25f;
                def.PerformColorAnalysis = true;
                def.CheckForDuplicateFiles = true;
                def.CheckForProgramUpdates = true;
                return def;
            }
        }

        public class UserSettings
        {
            public List<PageSize> PageStore { get; set; }
            public Point WindowLocation { get; set; }
            public Size WindowSize { get; set; }
            public FormWindowState WindowState { get; set; }
            public float PDFColorThreshold { get; set; }
            public bool PerformColorAnalysis { get; set; }
            public bool CheckForProgramUpdates { get; set; }
            public bool CheckForDuplicateFiles { get; set; }
        }
    }
}
