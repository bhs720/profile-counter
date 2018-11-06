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
                def.PageStore = DefaultPageStore;
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

        public static List<PageSize> DefaultPageStore
        {
            get
            {
                var store = new List<PageSize>();
                store.Add(new PageSize("ANSI-A [ 8.5 × 11 ]", 8f, 9f, 10f, 12f));
                store.Add(new PageSize("Legal [ 8.5 × 14 ]", 8f, 9f, 13f, 15f));
                store.Add(new PageSize("ANSI-B [ 11 × 17 ]", 10.5f, 11.5f, 16.5f, 17.5f));
                store.Add(new PageSize("ANSI-C [ 17 × 22 ]", 16f, 18f, 21f, 23f));
                store.Add(new PageSize("ANSI-D [ 22 × 34 ]", 21f, 23f, 33f, 35f));
                store.Add(new PageSize("ANSI-E [ 34 × 44 ]", 33f, 35f, 43f, 45f));
                store.Add(new PageSize("ARCH-A [ 9 × 12 ]", 8.5f, 9.5f, 11.5f, 12.5f, false));
                store.Add(new PageSize("ARCH-B [ 12 × 18 ]", 11.5f, 12.5f, 17.5f, 18.5f));
                store.Add(new PageSize("ARCH-C [ 18 × 24 ]", 17f, 19f, 23f, 25f));
                store.Add(new PageSize("ARCH-D [ 24 × 36 ]", 23f, 25f, 35f, 37f));
                store.Add(new PageSize("ARCH-E1 [ 30 × 42 ]", 29f, 31f, 41f, 43f));
                store.Add(new PageSize("ARCH-E [ 36 × 48 ]", 35f, 37f, 47f, 49f));
                return store;
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
            public void LoadDefaultPageSizes()
            {
                this.PageStore = DefaultPageStore;
            }
        }
    }
}
