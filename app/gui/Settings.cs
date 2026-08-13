using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace TIFPDFCounter
{
    /// <summary>
    /// Which library analyses TIFF files.
    /// </summary>
    public enum TiffEngine
    {
        LibTiff,
        MuPdf
    }

    public static class Settings
    {
        readonly static string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProFile Counter");

        /// <summary>
        /// The settings file as version 3.3 and earlier understood it.
        /// <para>
        /// Those versions treat an unrecognised element as a hard error, discard every
        /// setting, and then overwrite the file with defaults when the window closes --
        /// so a user who installs an older build after a newer one loses their page
        /// sizes permanently. This file is therefore frozen at the 3.3 schema: new
        /// settings are marked [XmlIgnore] and live only in <see cref="preferencesFile"/>.
        /// It is still written on every save so an older build sees current values for
        /// the settings it does understand.
        /// </para>
        /// </summary>
        readonly static string legacyFile = Path.Combine(appData, "UserSettings.xml");

        /// <summary>
        /// The settings file from 3.4 onwards, holding every setting. JSON rather than
        /// XmlSerializer because it ignores properties it does not recognise, so a file
        /// written by a future version stays readable here.
        /// </summary>
        readonly static string preferencesFile = Path.Combine(appData, "Preferences.json");

        public static UserSettings Current { get; private set; }

        static Settings()
        {
            Directory.CreateDirectory(appData);
        }

        public static void Load()
        {
            var fromPreferences = LoadPreferences();
            var fromLegacy = LoadLegacy();

            if (fromPreferences == null && fromLegacy == null)
            {
                Current = DefaultUserSettings;
                Save();
                return;
            }

            if (fromPreferences == null)
            {
                // First run of 3.4 or later: carry the older file's values forward.
                Current = fromLegacy;
                Save();
                return;
            }

            if (fromLegacy == null)
            {
                Current = fromPreferences;
                return;
            }

            // Both exist. Normally Preferences.json is the newer of the two, but a user
            // can run an older build in between, which writes only the legacy file. Take
            // whichever was written last so those edits are not silently discarded.
            if (File.GetLastWriteTimeUtc(legacyFile) > File.GetLastWriteTimeUtc(preferencesFile))
            {
                // Settings the older build cannot represent are not in its file; keep the
                // values we already hold rather than resetting them to defaults.
                fromLegacy.TiffEngine = fromPreferences.TiffEngine;
                Current = fromLegacy;
                Save();
            }
            else
            {
                Current = fromPreferences;
            }
        }

        static UserSettings LoadPreferences()
        {
            if (!File.Exists(preferencesFile))
                return null;

            try
            {
                var json = File.ReadAllText(preferencesFile);
                var loaded = JsonConvert.DeserializeObject<UserSettings>(json);
                return loaded ?? throw new Exception("Preferences file was empty.");
            }
            catch (Exception ex)
            {
                Quarantine(preferencesFile, ex);
                return null;
            }
        }

        static UserSettings LoadLegacy()
        {
            if (!File.Exists(legacyFile))
                return null;

            try
            {
                using (var stream = new FileStream(legacyFile, FileMode.Open, FileAccess.Read))
                {
                    var xml = new XmlSerializer(typeof(UserSettings));

                    // Log unrecognised content rather than failing on it. Treating it as an
                    // error is what made a newer settings file destroy an older build's
                    // configuration; a setting we do not know about is not a reason to throw
                    // away the ones we do.
                    xml.UnknownAttribute += (sender, args) =>
                        System.Diagnostics.Debug.Print("UnknownAttribute: " + args.Attr.Name);
                    xml.UnknownElement += (sender, args) =>
                        System.Diagnostics.Debug.Print("UnknownElement: " + args.Element.Name);

                    return xml.Deserialize(stream) as UserSettings;
                }
            }
            catch (Exception ex)
            {
                Quarantine(legacyFile, ex);
                return null;
            }
        }

        /// <summary>
        /// Moves a settings file we could not read aside instead of letting the next save
        /// overwrite it. Without this a transient problem -- a half-written file, a disk
        /// error -- silently costs the user every custom page size they had.
        /// </summary>
        static void Quarantine(string path, Exception ex)
        {
            System.Diagnostics.Debug.Print("Could not read " + path + ": " + ex.Message);
            try
            {
                var bad = path + ".bad";
                File.Delete(bad);
                File.Move(path, bad);
            }
            catch { /* nothing further to try; do not block startup over it */ }
        }

        public static void Save()
        {
            SavePreferences();
            SaveLegacy();
        }

        static void SavePreferences()
        {
            try
            {
                var json = JsonConvert.SerializeObject(Current, Formatting.Indented);
                File.WriteAllText(preferencesFile, json);
            }
            catch { }
        }

        /// <summary>
        /// Written on every save purely so that an older build still sees current values.
        /// Properties added after 3.3 are [XmlIgnore] and must stay that way -- adding one
        /// here would make those versions discard the whole file.
        /// </summary>
        static void SaveLegacy()
        {
            try
            {
                using (var stream = new FileStream(legacyFile, FileMode.Create))
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
                def.PageSizes = DefaultPageSizes;
                def.WindowLocation = new Point(0, 0);
                def.WindowSize = new Size(640, 480);
                def.WindowState = FormWindowState.Normal;
                def.TiffEngine = TiffEngine.LibTiff;
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
        public static List<PageSize> DefaultPageSizes
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
            public List<PageSize> PageSizes { get; set; }
            public Point WindowLocation { get; set; }
            public Size WindowSize { get; set; }
            public FormWindowState WindowState { get; set; }
            /// <summary>
            /// Which library analyses TIFF files. LibTIFF is markedly faster and uses a
            /// fraction of the memory; MuPDF is kept as an escape hatch for files where
            /// LibTIFF gives an unexpected answer.
            /// <para>
            /// [XmlIgnore] is required, not cosmetic: version 3.3 and earlier abort on any
            /// element they do not recognise and then overwrite the file with defaults.
            /// Every setting added after 3.3 must carry this attribute, so it appears in
            /// Preferences.json only.
            /// </para>
            /// </summary>
            [XmlIgnore]
            [JsonConverter(typeof(StringEnumConverter))]
            public TiffEngine TiffEngine { get; set; }

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
