using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;
using Lumina.Excel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using static FFXIVClientStructs.FFXIV.Client.Game.Character.ActionEffectHandler;
using LuminaAction = Lumina.Excel.Sheets.Action;
using LuminaItem = Lumina.Excel.Sheets.Item;

namespace ActionTimeline.Helpers
{
    public enum TimelineItemType
    {
        Action = 0,
        CastStart = 1,
        CastCancel = 2,
        OffGCD = 3,
        AutoAttack = 4,
        Item = 5
    }

    public class TimelineItem
    {
        public uint ActionID { get; }
        public uint IconID { get; }
        public TimelineItemType Type { get; }
        public double Time { get; }

        public float GCDDuration { get; }
        public float CastTime { get; }

        public GCDClipData? GCDClipData = null;

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
        public bool IsClipped { get; }
        public double StartTime { get; }
        public double? EndTime { get; }
        public bool IsFakeEndTime { get; }

        public bool ShouldDraw => IsClipped && !Utils.UnderThreshold(StartTime, EndTime.HasValue ? EndTime.Value : ImGui.GetTime());

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

        private unsafe TimelineManager()
        {
            _actionSheet = Plugin.DataManager.GetExcelSheet<LuminaAction>();
            _itemSheet = Plugin.DataManager.GetExcelSheet<LuminaItem>();

            try
            {
                _onActionUsedHook = Plugin.GameInteropProvider.HookFromAddress<ActionEffectHandler.Delegates.Receive>(
                    ActionEffectHandler.MemberFunctionPointers.Receive,
                    OnActionUsed
                );
                _onActionUsedHook?.Enable();

                _onActorControlHook = Plugin.GameInteropProvider.HookFromSignature<OnActorControlDelegate>(
                    "E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64",
                    OnActorControl
                );
                _onActorControlHook?.Enable();

                _onCastHook = Plugin.GameInteropProvider.HookFromSignature<OnCastDelegate>(
                    "40 56 41 56 48 81 EC ?? ?? ?? ?? 48 8B F2",
                    OnCast
                );
                _onCastHook?.Enable();
            }
            catch (Exception e)
            {
                Plugin.Logger.Error("Error initiating hooks: " + e.Message);
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

        private Hook<ActionEffectHandler.Delegates.Receive>? _onActionUsedHook;

        private delegate void OnActorControlDelegate(uint entityId, uint id, uint unk1, uint type, uint unk2, uint unk3, uint unk4, uint unk5, UInt64 targetId, byte unk6);
        private Hook<OnActorControlDelegate>? _onActorControlHook;

        private delegate void OnCastDelegate(uint sourceId, IntPtr sourceCharacter);
        private Hook<OnCastDelegate>? _onCastHook;

        private ExcelSheet<LuminaAction>? _actionSheet;
        private ExcelSheet<LuminaItem>? _itemSheet;

        private Dictionary<uint, uint> _specialCasesMap = new()
        {
            // MNK
            [16475] = 53, // anatman

            // SAM
            [16484] = 7477, // kaeshi higanbana
            [16485] = 7477, // kaeshi goken
            [16486] = 7477, // keashi setsugekka
            [25782] = 7477, // kaeshi namikiri

            // RDM
            [25858] = 7504 // resolution
        };
        private Dictionary<uint, float> _hardcodedCasesMap = new()
        {
            // NIN
            [2259] = 0.5f, // ten
            [2261] = 0.5f, // chi
            [2263] = 0.5f, // jin
            [18805] = 0.5f, // ten
            [18806] = 0.5f, // chi
            [18807] = 0.5f, // jin

            [2265] = 1.5f, // fuma shuriken
            [18873] = 1.5f, // fuma shuriken
            [18874] = 1.5f, // fuma shuriken
            [18875] = 1.5f, // fuma shuriken
            [2266] = 1.5f, // katon
            [18876] = 1.5f, // katon
            [2267] = 1.5f, // raiton
            [18877] = 1.5f, // raiton
            [2268] = 1.5f, // hyoton
            [18878] = 1.5f, // hyoton
            [2269] = 1.5f, // huton
            [18879] = 1.5f, // huton
            [2270] = 1.5f, // doton
            [10892] = 1.5f, // doton
            [18880] = 1.5f, // doton
            [2271] = 1.5f, // suiton
            [18881] = 1.5f, // suiton
            [2272] = 1.5f, // rabbit medium

            [16491] = 1.5f, // goka mekkyaku
            [16492] = 1.5f, // hyosho ranryu
        };

        private static int kMaxItemCount = 50;
        private List<TimelineItem> _items = new List<TimelineItem>(kMaxItemCount);
        public IReadOnlyCollection<TimelineItem> Items => _items.AsReadOnly();

        private Settings Settings => Plugin.Settings;

        private double _outOfCombatStartTime = -1;
        private bool _hadSwiftcast = false;


        private unsafe void Update(IFramework framework)
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
                if (item.GCDClipData.HasValue && item.GCDClipData.Value.EndTime.HasValue && !item.GCDClipData.Value.IsFakeEndTime) { continue; }

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
                    if (!isFakeEnd && gcdClipEnd.HasValue && Utils.UnderThreshold(gcdClipStart, gcdClipEnd.Value))
                    {
                        item.GCDClipData = new GCDClipData(false, 0, 0, false);
                    }
                    // clipped :(
                    else
                    {
                        item.GCDClipData = new GCDClipData(true, gcdClipStart, gcdClipEnd, isFakeEnd);
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
            IPlayerCharacter? player = Plugin.ClientState.LocalPlayer;
            if (player != null)
            {
                _hadSwiftcast = player.StatusList.Any(s => s.StatusId == 167);
            }
        }

        private (double?, bool) FindGCDClipEndTime(TimelineItem item, int index)
        {
            if (index >= _items.Count - 1) { return (null, false); }

            TimelineItem? prevItem = null;

            for (int i = index + 1; i < _items.Count; i++)
            {
                TimelineItem nextItem = _items[i];
                if (nextItem.Type == TimelineItemType.Action)
                {
                    double time = prevItem != null && prevItem.Type == TimelineItemType.CastStart ? prevItem.Time : nextItem.Time;
                    return (time, false);
                }
                else if (nextItem.Type == TimelineItemType.CastStart && i == _items.Count - 1)
                {
                    return (nextItem.Time, true);
                }

                prevItem = nextItem;
            }

            return (null, false);
        }

        private unsafe float GetGCDTime(uint actionId)
        {
            ActionManager* actionManager = ActionManager.Instance();
            uint adjustedId = actionManager->GetAdjustedActionId(actionId);
            return actionManager->GetRecastTime(ActionType.Action, adjustedId);
        }

        private unsafe float GetCastTime(uint actionId)
        {
            ActionManager* actionManager = ActionManager.Instance();
            uint adjustedId = actionManager->GetAdjustedActionId(actionId);
            return (float)ActionManager.GetAdjustedCastTime(ActionType.Action, adjustedId) / 1000f;
        }

        private void Add(uint id, TimelineItemType type)
        {
            if (type == TimelineItemType.Item) 
            {
                AddItem(id);
                return;
            }

            LuminaAction? action = _actionSheet?.GetRowOrDefault(id);
            if (action == null || !action.HasValue) { return; }

            // only cache the last kMaxItemCount items
            if (_items.Count >= kMaxItemCount)
            {
                _items.RemoveAt(0);
            }

            double now = ImGui.GetTime();
            float gcdDuration = 0;
            float castTime = 0;

            // handle sprint and auto attack icons
            int iconId = id == 3 ? 104 : (id == 1 ? 101 : action.Value.Icon);

            // handle weird cases
            uint _id = id;
            if (_specialCasesMap.TryGetValue(id, out uint replacedId))
            {
                type = TimelineItemType.Action;
                _id = replacedId;
            }

            // calculate gcd and cast time
            if (type == TimelineItemType.CastStart)
            {
                gcdDuration = GetGCDTime(_id);
                castTime = GetCastTime(_id);
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
                    gcdDuration = GetGCDTime(_id);
                    castTime = _hadSwiftcast ? 0 : GetCastTime(_id);
                }
            }

            // handle more weird cases
            if (_hardcodedCasesMap.TryGetValue(id, out float gcd))
            {
                type = TimelineItemType.Action;
                gcdDuration = gcd;
            }

            TimelineItem item = new TimelineItem(id, (uint)iconId, type, now, gcdDuration, castTime);
            _items.Add(item);
        }

        private void AddItem(uint id)
        {
            LuminaItem? item = _itemSheet?.GetRowOrDefault(id) ?? _itemSheet?.GetRowOrDefault(id - 1000000);
            if (item == null || !item.HasValue) { return; }

            // only cache the last kMaxItemCount items
            if (_items.Count >= kMaxItemCount)
            {
                _items.RemoveAt(0);
            }

            TimelineItem i = new TimelineItem(
                id, 
                item.Value.Icon, 
                TimelineItemType.Item,
                ImGui.GetTime(), 
                0,
                0
            );
            _items.Add(i);
        }

        private TimelineItemType? TypeForID(uint id)
        {
            LuminaAction? action = _actionSheet?.GetRowOrDefault(id);
            if (action != null)
            {
                return TypeForAction(action.Value);
            }

            LuminaItem? item = _itemSheet?.GetRowOrDefault(id) ?? _itemSheet?.GetRowOrDefault(id - 1000000);
            if (item != null)
            {
                return TimelineItemType.Item;
            }

            return TimelineItemType.Action;
        }

        private TimelineItemType TypeForAction(LuminaAction action)
        {
            // off gcd or sprint
            if (action.ActionCategory.RowId is 4 || action.RowId == 3)
            {
                return TimelineItemType.OffGCD;
            }

            if (action.ActionCategory.RowId is 1)
            {
                return TimelineItemType.AutoAttack;
            }

            return TimelineItemType.Action;
        }

        private unsafe void OnActionUsed(uint actorId, Character* casterPtr, Vector3* targetPos, Header* header, TargetEffects* effects, GameObjectId* targetEntityIds)
        {
            _onActionUsedHook?.Original(actorId, casterPtr, targetPos, header, effects, targetEntityIds);

            IPlayerCharacter ? player = Plugin.ClientState.LocalPlayer;
            if (player == null || actorId != player.GameObjectId) { return; }

            uint actionId = header->ActionId;
            TimelineItemType? type = TypeForID(actionId);
            if (!type.HasValue) { return; }

            Add(actionId, type.Value);
        }

        private void OnActorControl(uint entityId, uint type, uint buffID, uint direct, uint actionId, uint sourceId, uint arg4, uint arg5, ulong targetId, byte a10)
        {
            _onActorControlHook?.Original(entityId, type, buffID, direct, actionId, sourceId, arg4, arg5, targetId, a10);

            if (type != 15) { return; }

            IPlayerCharacter? player = Plugin.ClientState.LocalPlayer;
            if (player == null || entityId != player.GameObjectId) { return; }

            Add(actionId, TimelineItemType.CastCancel);
        }

        private void OnCast(uint sourceId, IntPtr ptr)
        {
            _onCastHook?.Original(sourceId, ptr);

            IPlayerCharacter? player = Plugin.ClientState.LocalPlayer;
            if (player == null || sourceId != player.GameObjectId) { return; }

            int value = Marshal.ReadInt16(ptr);
            uint actionId = value < 0 ? (uint)(value + 65536) : (uint)value;

            Add(actionId, TimelineItemType.CastStart);
        }
    }
}
