using Dalamud.Logging;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Numerics;

namespace ActionTimeline
{
    public class Settings
    {
        public bool ShowTimeline = true;
        public bool TimelineLocked = false;
        public int OutOfCombatClearTime = 10;
        public bool ShowTimelineOnlyInDuty = false;
        public bool ShowTimelineOnlyInCombat = false;
        public int TimelineTime = 5; // seconds

        public Vector4 TimelineLockedBackgroundColor = new Vector4(0f, 0f, 0f, 0.5f);
        public Vector4 TimelineUnlockedBackgroundColor = new Vector4(0f, 0f, 0f, 0.75f);

        public int TimelineIconSize = 40;

        public int TimelineOffGCDIconSize = 30;
        public int TimelineOffGCDOffset = -5;

        public bool TimelineShowAutoAttacks = false;
        public int TimelineAutoAttackSize = 20;
        public int TimelineAutoAttackOffset = 10;

        public Vector4 CastInProgressColor = new Vector4(0.5f, 0.5f, 0.5f, 1f);
        public Vector4 CastFinishedColor = new Vector4(0.2f, 0.8f, 0.2f, 1f);
        public Vector4 CastCanceledColor = new Vector4(0.8f, 0.2f, 0.2f, 1f);

        public bool ShowGrid = true;
        public bool ShowGridCenterLine = false;
        public bool GridDivideBySeconds = true;
        public bool GridShowSecondsText = true;
        public bool GridSubdivideSeconds = true;
        public int GridSubdivisionCount = 2;
        public int GridLineWidth = 1;
        public int GridSubdivisionLineWidth = 1;
        public Vector4 GridLineColor = new Vector4(0.3f, 0.3f, 0.3f, 1f);
        public Vector4 GridSubdivisionLineColor = new Vector4(0.3f, 0.3f, 0.3f, 0.2f);

        public bool ShowGCDClipping = true;
        public float GCDClippingThreshold = 0.05f; // seconds
        public float GCDClippingCastsThreshold = 0.5f; // seconds
        public int GCDClippingMaxTime = 5; // seconds
        public Vector4 GCDClippingColor = new Vector4(1f, 0.2f, 0.2f, 0.5f);

        public bool ShowRotation = true;
        public bool RotationLocked = false;
        public bool ShowRotationOnlyInDuty = false;
        public bool ShowRotationOnlyInCombat = false;

        public int RotationGCDSpacing = 20;
        public int RotationOffGCDSpacing = 5;

        public bool RotationSeparatorEnabled = true;
        public int RotationSeparatorTime = 10; // seconds
        public int RotationSeparatorWidth = 3;
        public Vector4 RotationSeparatorColor = new Vector4(0.3f, 0.3f, 0.3f, 1f);

        public Vector4 RotationLockedBackgroundColor = new Vector4(0f, 0f, 0f, 0.5f);
        public Vector4 RotationUnlockedBackgroundColor = new Vector4(0f, 0f, 0f, 0.75f);

        public int RotationIconSize = 40;

        public int RotationOffGCDIconSize = 30;
        public int RotationOffGCDOffset = -5;

        #region load / save
        private static string JsonPath = Path.Combine(Plugin.PluginInterface.GetPluginConfigDirectory(), "Settings.json");
        public static Settings Load()
        {
            string path = JsonPath;
            Settings? settings = null;

            try
            {
                if (File.Exists(path))
                {
                    string jsonString = File.ReadAllText(path);
                    settings = JsonConvert.DeserializeObject<Settings>(jsonString);
                }
            }
            catch (Exception e)
            {
                PluginLog.Error("Error reading settings file: " + e.Message);
            }

            if (settings == null)
            {
                settings = new Settings();
                Save(settings);
            }

            return settings;
        }

        public static void Save(Settings settings)
        {
            try
            {
                JsonSerializerSettings serializerSettings = new JsonSerializerSettings
                {
                    TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                    TypeNameHandling = TypeNameHandling.Objects
                };
                string jsonString = JsonConvert.SerializeObject(settings, Formatting.Indented, serializerSettings);

                File.WriteAllText(JsonPath, jsonString);
            }
            catch (Exception e)
            {
                PluginLog.Error("Error saving settings file: " + e.Message);
            }
        }
        #endregion
    }
}
