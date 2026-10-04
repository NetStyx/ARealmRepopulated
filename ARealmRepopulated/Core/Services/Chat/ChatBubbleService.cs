using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.System.String;

namespace ARealmRepopulated.Core.Services.Chat;

/// <summary>
/// Service to manage the chat bubble functionality which allows the display of text above an actors head.
/// </summary>
/// <remarks>
/// see /doc/research/chat-bubbles.md
/// </remarks>
public unsafe class ChatBubbleService {
    public void Talk(Character* character, string text, float duration) {
        using var newText = *Utf8String.FromString(text);
        character->YellBalloon.OpenBalloon(newText, duration, true, 0, false, false, true, 0);
    }
}
