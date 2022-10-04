using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using Dalamud.Logging;
using ImGuiNET;
using Lumina.Excel;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LuminaAction = Lumina.Excel.GeneratedSheets.Action;

namespace ActionTimeline.Helpers
{
    public enum TimelineItemType
    {
        Action = 0,
        CastStart = 1,
        CastCancel = 2,
        OffGCD = 3,
        AutoAttack = 4
    }

    public class TimelineItem
    {
        public uint ActionID { get; }
        public uint IconID { get; }
        public TimelineItemType Type { get; }
        public double Time { get; }

        public TimelineItem(uint actionID, uint iconID, TimelineItemType type, double time)
        {
            ActionID = actionID;
            IconID = iconID;
            Type = type;
            Time = time;
        }
    }

    public class TimelineManager
    {
        #region singleton
        public static void Initialize() { Instance = new TimelineManager(); }

        public static TimelineManager Instance { get; private set; } = null!;

        public TimelineManager()
        {
            _sheet = Plugin.DataManager.GetExcelSheet<LuminaAction>();

            try
            {
                _onActionUsedHook = Hook<OnActionUsedDelegate>.FromAddress(Plugin.SigScanner.ScanText("4C 89 44 24 ?? 55 56 41 54 41 55 41 56"), OnActionUsed);
                _onActionUsedHook?.Enable();

                _onActorControlHook = Hook<OnActorControlDelegate>.FromAddress(Plugin.SigScanner.ScanText("E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64"), OnActorControl);
                _onActorControlHook?.Enable();

                _onCastHook = Hook<OnCastDelegate>.FromAddress(Plugin.SigScanner.ScanText("40 55 56 48 81 EC ?? ?? ?? ?? 48 8B EA"), OnCast);
                _onCastHook?.Enable();
            }
            catch (Exception e)
            {
                PluginLog.Error("Error initiating hooks: " + e.Message);
            }
        }

        ~TimelineManager()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            _items.Clear();

            _onActionUsedHook?.Disable();
            _onActionUsedHook?.Dispose();

            _onActorControlHook?.Disable();
            _onActorControlHook?.Dispose();

            _onCastHook?.Disable();
            _onCastHook?.Dispose();
        }
        #endregion

        private delegate void OnActionUsedDelegate(uint sourceId, IntPtr sourceCharacter, IntPtr pos, IntPtr effectHeader, IntPtr effectArray, IntPtr effectTrail);
        private Hook<OnActionUsedDelegate>? _onActionUsedHook;

        private delegate void OnActorControlDelegate(uint entityId, uint id, uint unk1, uint type, uint unk2, uint unk3, uint unk4, uint unk5, UInt64 targetId, byte unk6);
        private Hook<OnActorControlDelegate>? _onActorControlHook;

        private delegate void OnCastDelegate(uint sourceId, IntPtr sourceCharacter);
        private Hook<OnCastDelegate>? _onCastHook;

        private ExcelSheet<LuminaAction>? _sheet;

        private static int kMaxItemCount = 50;
        private List<TimelineItem> _items = new List<TimelineItem>(kMaxItemCount);
        public IReadOnlyCollection<TimelineItem> Items => _items.AsReadOnly();

        private void AddItem(TimelineItem item)
        {
            if (_items.Count >= kMaxItemCount)
            {
                _items.RemoveAt(0);
            }

            _items.Add(item);
        }

        private void AddItem(uint actionId, TimelineItemType type)
        {
            LuminaAction? action = _sheet?.GetRow(actionId);
            if (action == null) { return; }

            AddItem(new TimelineItem(actionId, action.Icon, type, ImGui.GetTime()));
        }

        private TimelineItemType? TypeForActionID(uint actionId)
        {
            LuminaAction? action = _sheet?.GetRow(actionId);
            if (action == null) { return null; }

            // off gcd or sprint
            if (action.ActionCategory.Row is 4 || actionId == 3)
            {
                return TimelineItemType.OffGCD;
            }

            if (action.ActionCategory.Row is 1)
            {
                return TimelineItemType.AutoAttack;
            }

            return TimelineItemType.Action;
        }

        private void OnActionUsed(uint sourceId, IntPtr sourceCharacter, IntPtr pos, IntPtr effectHeader,
            IntPtr effectArray, IntPtr effectTrail)
        {
            _onActionUsedHook?.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTrail);

            PlayerCharacter? player = Plugin.ClientState.LocalPlayer;
            if (player == null || sourceId != player.ObjectId) { return; }

            int actionId = Marshal.ReadInt32(effectHeader, 0x8);
            TimelineItemType? type = TypeForActionID((uint)actionId);
            if (!type.HasValue) { return; }

            AddItem((uint)actionId, type.Value);
        }

        private void OnActorControl(uint entityId, uint type, uint buffID, uint direct, uint actionId, uint sourceId, uint arg4, uint arg5, ulong targetId, byte a10)
        {
            _onActorControlHook?.Original(entityId, type, buffID, direct, actionId, sourceId, arg4, arg5, targetId, a10);

            if (type != 15) { return; }

            PlayerCharacter? player = Plugin.ClientState.LocalPlayer;
            if (player == null || entityId != player.ObjectId) { return; }

            AddItem(actionId, TimelineItemType.CastCancel);
        }

        private void OnCast(uint sourceId, IntPtr ptr)
        {
            _onCastHook?.Original(sourceId, ptr);

            PlayerCharacter? player = Plugin.ClientState.LocalPlayer;
            if (player == null || sourceId != player.ObjectId) { return; }

            short actionId = Marshal.ReadInt16(ptr);
            AddItem((uint)actionId, TimelineItemType.CastStart);
        }
    }
}
