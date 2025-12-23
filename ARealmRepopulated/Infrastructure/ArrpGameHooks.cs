using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace ARealmRepopulated.Infrastructure;

public unsafe class ArrpGameHooks : IDisposable {
    public delegate void CharacterEventDelegate(Character* chara);
    public event CharacterEventDelegate? OnCharacterDestroyed;

    /// <summary>
    /// Client::Game::Character::Character.Finalizer()
    /// </summary>
    [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 48 8D 05 ?? ?? ?? ?? 48 8B D9 48 89 01 48 8D 05 ?? ?? ?? ?? 48 89 81 ?? ?? ?? ?? 48 81 C1", DetourName = nameof(CharacterFinalizerDetour))]
    private readonly Hook<CharacterFinalizerDelegate> _characterFinalizerHook = null!;
    private delegate void CharacterFinalizerDelegate(Character* character);

    public ArrpGameHooks(IGameInteropProvider interopProvider) {
        interopProvider.InitializeFromAttributes(this);
        _characterFinalizerHook.Enable();
    }

    private void CharacterFinalizerDetour(Character* character) {
        if (character != null) {
            OnCharacterDestroyed?.Invoke(character);
        }

        _characterFinalizerHook.Original(character);
    }

    public void Dispose() {
        _characterFinalizerHook?.Dispose();
        GC.SuppressFinalize(this);
    }
}
