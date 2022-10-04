using ActionTimeline.Helpers;
using ActionTimeline.Windows;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using System;
using System.Reflection;
using SigScanner = Dalamud.Game.SigScanner;

namespace ActionTimeline
{
    public class Plugin : IDalamudPlugin
    {
        public static ClientState ClientState { get; private set; } = null!;
        public static CommandManager CommandManager { get; private set; } = null!;
        public static DalamudPluginInterface PluginInterface { get; private set; } = null!;
        public static Condition Condition { get; private set; } = null!;
        public static DataManager DataManager { get; private set; } = null!;
        public static Framework Framework { get; private set; } = null!;
        public static GameGui GameGui { get; private set; } = null!;
        public static SigScanner SigScanner { get; private set; } = null!;
        public static UiBuilder UiBuilder { get; private set; } = null!;
        public static KeyState KeyState { get; private set; } = null!;

        public static string AssemblyLocation { get; private set; } = "";
        public string Name => "ActionTimeline";

        public static string Version { get; private set; } = "";

        public static Settings Settings { get; private set; } = null!;

        private static WindowSystem _windowSystem = null!;
        private static SettingsWindow _settingsWindow = null!;
        private static TimelineSettingsWindow _timelineSettingsWindow = null!;
        private static RotationSettingsWindow _rotationSettingsWindow = null!;
        private static TimelineWindow _timelineWindow = null!;
        private static RotationWindow _rotationWindow = null!;


        public Plugin(
            ClientState clientState,
            CommandManager commandManager,
            DalamudPluginInterface pluginInterface,
            Condition condition,
            DataManager dataManager,
            Framework framework,
            GameGui gameGui,
            SigScanner sigScanner,
            KeyState keyState
        )
        {
            ClientState = clientState;
            CommandManager = commandManager;
            PluginInterface = pluginInterface;
            Condition = condition;
            DataManager = dataManager;
            Framework = framework;
            GameGui = gameGui;
            SigScanner = sigScanner;
            UiBuilder = PluginInterface.UiBuilder;
            KeyState = keyState;

            if (pluginInterface.AssemblyLocation.DirectoryName != null)
            {
                AssemblyLocation = pluginInterface.AssemblyLocation.DirectoryName + "\\";
            }
            else
            {
                AssemblyLocation = Assembly.GetExecutingAssembly().Location;
            }

            Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.1.0.0";

            UiBuilder.Draw += Draw;
            UiBuilder.OpenConfigUi += OpenConfigUi;

            CommandManager.AddHandler(
                "/actiontimeline",
                new CommandInfo(PluginCommand)
                {
                    HelpMessage = "Opens the ActionTimeline configuration window.",

                    ShowInHelp = true
                }
            );

            CommandManager.AddHandler(
                "/att",
                new CommandInfo(PluginCommand)
                {
                    HelpMessage = "Opens the ActionTimeline Timeline Settings window.",

                    ShowInHelp = true
                }
            );

            CommandManager.AddHandler(
                "/atr",
                new CommandInfo(PluginCommand)
                {
                    HelpMessage = "Opens the ActionTimeline Rotation Settings window.",

                    ShowInHelp = true
                }
            );

            TexturesCache.Initialize();
            TimelineManager.Initialize();

            Settings = Settings.Load();

            CreateWindows();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        private void PluginCommand(string command, string arguments)
        {
            if (command == "/att")
            {
                _timelineSettingsWindow.IsOpen = !_timelineSettingsWindow.IsOpen;
            }
            else if (command == "/atr")
            {
                _rotationSettingsWindow.IsOpen = !_rotationSettingsWindow.IsOpen;
            }
            else
            {
                _settingsWindow.IsOpen = !_settingsWindow.IsOpen;
            }
        }

        private void CreateWindows()
        {
            _settingsWindow = new SettingsWindow("ActionTimeline v"+ Version);
            _timelineSettingsWindow = new TimelineSettingsWindow("Timeline Settings");
            _rotationSettingsWindow = new RotationSettingsWindow("Rotation Settings");
            _timelineWindow = new TimelineWindow("Timeline");
            _rotationWindow = new RotationWindow("Rotation");

            _windowSystem = new WindowSystem("ActionTimeline_Windows");
            _windowSystem.AddWindow(_settingsWindow);
            _windowSystem.AddWindow(_timelineSettingsWindow);
            _windowSystem.AddWindow(_rotationSettingsWindow);
            _windowSystem.AddWindow(_timelineWindow);
            _windowSystem.AddWindow(_rotationWindow);
        }

        private void Draw()
        {
            if (Settings == null || ClientState.LocalPlayer == null) return;

            UpdateTimeline();
            UpdateRotation();

            _windowSystem?.Draw();
        }

        public static void ShowTimelineSettingsWindow()
        {
            _timelineSettingsWindow.IsOpen = true;
        }

        public static void ShowRotationSettingsWindow()
        {
            _rotationSettingsWindow.IsOpen = true;
        }

        private void UpdateTimeline()
        {
            bool show = Settings.ShowTimeline;
            if (show)
            {
                if (Settings.ShowTimelineOnlyInCombat && !Condition[ConditionFlag.InCombat])
                {
                    show = false;
                }

                if (Settings.ShowTimelineOnlyInDuty && !Condition[ConditionFlag.BoundByDuty])
                {
                    show = false;
                }
            }

            _timelineWindow.IsOpen = show;
        }

        private void UpdateRotation()
        {
            bool show = Settings.ShowRotation;
            if (show)
            {
                if (Settings.ShowRotationOnlyInCombat && !Condition[ConditionFlag.InCombat])
                {
                    show = false;
                }

                if (Settings.ShowRotationOnlyInDuty && !Condition[ConditionFlag.BoundByDuty])
                {
                    show = false;
                }
            }

            _rotationWindow.IsOpen = show;
        }

        private void OpenConfigUi()
        {
            _settingsWindow.IsOpen = true;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            Settings.Save(Settings);

            TexturesCache.Instance?.Dispose();
            TimelineManager.Instance?.Dispose();

            _windowSystem.RemoveAllWindows();

            CommandManager.RemoveHandler("/actiontimeline");
            CommandManager.RemoveHandler("/att");
            CommandManager.RemoveHandler("/atr");

            UiBuilder.Draw -= Draw;
            UiBuilder.OpenConfigUi -= OpenConfigUi;
            UiBuilder.RebuildFonts();
        }
    }
}
