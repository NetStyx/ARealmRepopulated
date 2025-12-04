using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using InteropGenerator.Runtime;
using System.Timers;

namespace ARealmRepopulated.Core.Services.Chat;

public unsafe class ChatBubbleService : IDisposable {
    private readonly IPluginLog _log;
    private readonly IClientState _state;

    /// <summary>
    /// Client::UI::Agent::AgentScreenLog.OpenBalloon()
    /// </summary>
    [Signature("E8 ?? ?? ?? ?? F6 86 ?? ?? ?? ?? ?? C7 46", DetourName = nameof(OpenBubbleDetour))]
    private Hook<OpenBubbleDelegate> _openBalloonHook = null!;
    private delegate void OpenBubbleDelegate(AgentScreenLog* screenLog, Character* character, CStringPointer text, bool unk, int attachmentPoint);

    private List<ChatBubbleEntry> _npcTexts = [];
    private Timer _cleanupTimer;

    public ChatBubbleService(IGameInteropProvider provider, IClientState state, IPluginLog log) {
        provider.InitializeFromAttributes(this);

        _log = log;
        _state = state;
        _cleanupTimer = new Timer(TimeSpan.FromSeconds(5));
        _cleanupTimer.Elapsed += _cleanupTimer_Elapsed;
        _cleanupTimer.Enabled = true;

        _openBalloonHook?.Enable();
    }

    private void _cleanupTimer_Elapsed(object? sender, ElapsedEventArgs e) {
        lock (_npcTexts) {
            var overdueNpcTexts = _npcTexts.Where(c => c.Timeout < DateTime.Now).ToList();
            if (overdueNpcTexts.Count > 0) {
                overdueNpcTexts.ForEach(a => _npcTexts.Remove(a));
            }
        }
    }

    private void OpenBubbleDetour(AgentScreenLog* screenLog, Character* character, CStringPointer text, bool unk, int attachmentPoint) {
        if (_npcTexts.FirstOrDefault(x => x.Character == (nint)character) is var entry && entry != null) {
            using var newText = *Utf8String.FromString(entry.Text);
            _openBalloonHook?.Original(screenLog, character, newText.StringPtr, unk, attachmentPoint);
            return;
        }
        _openBalloonHook?.Original(screenLog, character, text, unk, attachmentPoint);
    }

    public void Talk(Character* character, string text, float duration) {
        var characterAddress = (nint)character;
        lock (_npcTexts) {
            var existingEntry = _npcTexts.FirstOrDefault(c => c.Character == characterAddress);
            if (existingEntry == null) {
                _npcTexts.Add(existingEntry = new ChatBubbleEntry());
            }

            existingEntry.Character = characterAddress;
            existingEntry.Timeout = DateTime.Now.AddSeconds(duration + 0.1);
            existingEntry.Text = text;
        }

        character->Balloon.PlayTimer = duration;
        character->Balloon.Type = FFXIVClientStructs.FFXIV.Client.Game.BalloonType.Timer;
        character->Balloon.State = FFXIVClientStructs.FFXIV.Client.Game.BalloonState.Waiting;
    }

    public void Dispose() {
        _cleanupTimer?.Dispose();
        _openBalloonHook?.Dispose();
    }
}

public class ChatBubbleEntry {
    public nint Character { get; set; } = 0;
    public string Text { get; set; } = "";
    public DateTime Timeout { get; set; } = DateTime.MinValue;
}


/*
 * 

/// <summary>
/// Definition for the minitalk call
/// </summary>
/// <remarks>
/// miniTalkController = <AgentScreenLog + 0x3e8>
/// miniTalkDisplayer = <miniTalkController + 0x10>
/// miniTalkDisplayTypeTable = <miniTalkDisplayer + 0xa4>
/// miniTalkDisplayTypeTableEntrySize = 0x10
/// 
/// Client::UI::Misc::RaptureLogModule.ShowMiniTalkPlayer()
///     MaybeDisplayChatBubble(miniTalkController,&miniTalkArgs)
///         ForceDisplayChatBubble(miniTalkDisplayer, 0 , miniTalkArgs, miniTalkDisplayTypeTable + chatDisplayKind* miniTalkDisplayTypeTableEntrySize)
/// </remarks>

[Signature("48 89 74 24 ?? 57 48 83 EC ?? 48 8B 71 ?? 48 8B FA 48 85 F6 0F 84 ?? ?? ?? ?? 48 8B 42")]
private delegate* unmanaged<IntPtr, MiniTalkArgs, void> _showMiniTalkBubbles = null!;

[Signature("40 55 53 56 57 41 54 41 55 41 56 41 57 48 8D AC 24 ?? ?? ?? ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 85 ?? ?? ?? ?? FF 41")]
private delegate* unmanaged<IntPtr, IntPtr, MiniTalkArgs, IntPtr, void> _forceShowMiniTalkBubbles = null!;

private const int _offsetMtController = 0x3e8;
private const int _offsetMtDisplayer = 0x10;
private const int _offsetMtDisplayTypes = 0xa4;
private const int _sizeMtDisplayType = 0x10;

var _miniTalkController = IntPtr.Add(new IntPtr(agentScreenLog), _offsetMtController);
if (_miniTalkController == IntPtr.Zero)
    return;

var _miniTalkDisplayer = Marshal.ReadIntPtr(_miniTalkController, _offsetMtDisplayer);
if (_miniTalkDisplayer == IntPtr.Zero)
    return;

var _miniTalkDisplayTypeTable = IntPtr.Add(_miniTalkDisplayer, _offsetMtDisplayTypes);
if (_miniTalkDisplayTypeTable == IntPtr.Zero)
    return;

var newString = Utf8String.CreateEmpty();
newString->SetString(text);

var miniTalkArgs = new MiniTalkArgs
{
    MiniTalkKind = 1,
    Unk2 = 0,
    StyleOrOpt = uint.MaxValue,
    Message = newString->StringPtr,
    BattleChar = (BattleChara*)gameObject
};



[StructLayout(LayoutKind.Explicit, Size = 0x20)]
public unsafe struct MiniTalkArgs
{
    /// <summary>
    /// 1..18 / Say, Yell, Shout ... and so on 
    /// </summary>
    [FieldOffset(0x0)]
    public byte MiniTalkKind;

    [FieldOffset(0x8)]
    public BattleChara* BattleChar;

    [FieldOffset(0x10)]
    public CStringPointer Message;

    [FieldOffset(0x18)]
    public byte Unk2;

    [FieldOffset(0x1c)]
    public uint StyleOrOpt;
}
*/
