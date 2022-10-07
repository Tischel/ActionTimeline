using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Lumina.Excel;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public float GCDDuration { get; }
        public float CastTime { get; }

        public GCDClipData? gcdClipData = null;

        public TimelineItem(uint actionID, uint iconID, TimelineItemType type, double time)
        {
            ActionID = actionID;
            IconID = iconID;
            Type = type;
            Time = time;
            GCDDuration = 0;
            CastTime = 0;
        }

        public TimelineItem(uint actionID, uint iconID, TimelineItemType type, double time, float gcdDuration, float castTime) : this(actionID, iconID, type, time)
        {
            GCDDuration = gcdDuration;
            CastTime = castTime;
        }
    }

    public struct GCDClipData
    {
        public bool IsClipped{ get; }
        public double StartTime { get; }
        public double? EndTime { get; }
        public bool IsFakeEndTime { get; }

        public GCDClipData(bool isClipped, double startTime, double? endTime, bool isFakeEndTime)
        {
            IsClipped = isClipped;
            StartTime = startTime;
            EndTime = endTime;
            IsFakeEndTime = isFakeEndTime;
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

            Plugin.Framework.Update += Update;
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

            Plugin.Framework.Update -= Update;

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

        private Settings Settings => Plugin.Settings;

        private double _outOfCombatStartTime = -1;
        private bool _hadSwiftcast = false;


        private unsafe void Update(Framework framework)
        {
            double now = ImGui.GetTime();

            CheckOutOfCombat(now);
            CheckSwiftcast();

            // gcd clipping logic
            for (int i = 0; i < _items.Count; i++)
            {
                TimelineItem item = _items[i];
                if (item.Type != TimelineItemType.Action) { continue; }
                if (item.GCDDuration == 0) { continue; } // does this ever happen???
                if (item.gcdClipData.HasValue && item.gcdClipData.Value.EndTime.HasValue && !item.gcdClipData.Value.IsFakeEndTime) { continue; }

                double gcdClipStart = item.Time + Math.Max(0, item.GCDDuration - item.CastTime);

                // cast threshold
                if (item.CastTime > 0)
                {
                    gcdClipStart += Settings.GCDClippingCastsThreshold;
                }

                // check if clipped
                if (now >= gcdClipStart)
                {
                    var (gcdClipEnd, isFakeEnd) = FindGCDClipEndTime(item, i);

                    // make sure threshold doesn't break the math
                    if (gcdClipEnd.HasValue && gcdClipStart > gcdClipEnd.Value)
                    {
                        gcdClipStart = gcdClipEnd.Value;
                    }

                    // check max time
                    if (!gcdClipEnd.HasValue && now - gcdClipStart > Settings.GCDClippingMaxTime)
                    {
                        gcdClipEnd = now;
                        isFakeEnd = false;
                    }

                    // not clipped?
                    if (gcdClipEnd.HasValue && Math.Abs(gcdClipEnd.Value - gcdClipStart) < Settings.GCDClippingThreshold)
                    {
                        item.gcdClipData = new GCDClipData(false, 0, 0, false);
                    }
                    // clipped :(
                    else
                    {
                        //if (gcdClipEnd.HasValue)
                        //{
                        //    PluginLog.Log("id        : " + i);
                        //    PluginLog.Log("time      : " + item.Time);
                        //    PluginLog.Log("gcd       : " + item.GCDDuration);
                        //    PluginLog.Log("cast      : " + item.CastTime);
                        //    PluginLog.Log("clip start: " + gcdClipStart);
                        //    PluginLog.Log("clip end  : " + gcdClipEnd.Value);
                        //    PluginLog.Log("--------------------------------------------------");
                        //}

                        item.gcdClipData = new GCDClipData(true, gcdClipStart, gcdClipEnd, false);
                    }
                }
            }
        }

        private void CheckOutOfCombat(double now)
        {
            if (!Plugin.Condition[ConditionFlag.InCombat] && _outOfCombatStartTime != -2)
            {
                if (_outOfCombatStartTime == -1)
                {
                    _outOfCombatStartTime = now;
                }
                else if (now - _outOfCombatStartTime >= Settings.OutOfCombatClearTime)
                {
                    _items.Clear();
                    _outOfCombatStartTime = -2;
                }
            }
            else
            {
                _outOfCombatStartTime = -1;
            }
        }

        private void CheckSwiftcast()
        {
            PlayerCharacter? player = Plugin.ClientState.LocalPlayer;
            if (player != null)
            {
                _hadSwiftcast = player.StatusList.Any(s => s.StatusId == 167);
            }
        }

        private (double?, bool) FindGCDClipEndTime(TimelineItem item, int index)
        {
            if (index >= _items.Count - 1) { return (null, false); }

            for (int i = index + 1; i < _items.Count; i++)
            {
                TimelineItem nextItem = _items[i];
                if (nextItem.Type == TimelineItemType.Action)
                {
                    return (nextItem.Time, false);
                }
                else if (nextItem.Type == TimelineItemType.CastStart && i == _items.Count - 1)
                {
                    return (nextItem.Time, true);
                }
            }

            return (null, false);
        }

        private unsafe float GetGCDTime(uint actionId)
        {
            ActionManager* actionManager = ActionManager.Instance();
            uint adjustedId = actionManager->GetAdjustedActionId(actionId);
            return actionManager->GetRecastTime(ActionType.Spell, adjustedId);
        }

        private unsafe float GetCastTime(uint actionId)
        {
            ActionManager* actionManager = ActionManager.Instance();
            uint adjustedId = actionManager->GetAdjustedActionId(actionId);
            return (float)ActionManager.GetAdjustedCastTime(ActionType.Spell, adjustedId) / 1000f;
        }

        private void AddItem(uint actionId, TimelineItemType type)
        {
            LuminaAction? action = _sheet?.GetRow(actionId);
            if (action == null) { return; }

            // only cache the last kMaxItemCount items
            if (_items.Count >= kMaxItemCount)
            {
                _items.RemoveAt(0);
            }

            double now = ImGui.GetTime();
            float gcdDuration = 0;
            float castTime = 0;

            // handle sprint and auto attack icons
            int iconId = actionId == 3 ? 104 : (actionId == 1 ? 101 : action.Icon);

            // calculate gcd and cast time
            if (type == TimelineItemType.CastStart)
            {
                gcdDuration = GetGCDTime(actionId);
                castTime = GetCastTime(actionId);
            }
            else if (type == TimelineItemType.Action)
            {
                TimelineItem? lastItem = _items.LastOrDefault();
                if (lastItem != null && lastItem.Type == TimelineItemType.CastStart)
                {
                    gcdDuration = lastItem.GCDDuration;
                    castTime = lastItem.CastTime;
                }
                else
                {
                    gcdDuration = GetGCDTime(actionId);
                    castTime = _hadSwiftcast ? 0 : GetCastTime(actionId);
                }
            }

            TimelineItem item = new TimelineItem(actionId, (uint)iconId, type, now, gcdDuration, castTime);
            _items.Add(item);
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

            PluginLog.Log(GetGCDTime((uint)actionId).ToString());
            PluginLog.Log(GetCastTime((uint)actionId).ToString());
        }
    }
}
