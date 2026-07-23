using ARealmRepopulated.Infrastructure;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System.Threading;

namespace ARealmRepopulated.Core.Services.LookAt;

/// <summary>
/// Provides functionality to set the look-at target of client objects without having to set a hard-/ or soft-target.
/// </summary>
/// <remarks>
/// - The service hooks into CharacterLookAtController::UpdateLookAt and checks if the character is set to look at something. 
/// If not, it checks if the character is in the _currentlyLookingAt dictionary and if so, it sets the look-at target to the target stored in the dictionary.
/// - The service also hooks into CharacterLookAtController::ResetLookAt and prevents the game from resetting the look-at target if the character is in the _currentlyLookingAt dictionary.
/// 
/// I do that because it seems like setting the target on these entities have some interactions with the player object ... which i want to avoid at all cost.
/// </remarks>
public unsafe class LookAtService : IDisposable {

    private readonly IPluginLog _log;
    private readonly IObjectTable _objectTable;
    private readonly ArrpGameHooks _globalHooks;

    /// <summary>
    /// Client::Game::Character::LookAtContainer.UpdateLookAt    
    /// </summary>
    [Signature("40 55 57 41 54 48 8D 6C 24", DetourName = nameof(LookAtDetour))]
    private readonly Hook<LookAtDelegate> _lookAt = null!;
    private delegate void LookAtDelegate(LookAtContainer* ctrl);

    /// <summary>
    /// Client::Game::Character::LookAtContainer.UpdateLookAt
    /// -> Client::Game::Character::CharacterLookAtController.LookAtController.ResetLookAt
    /// </summary>
    [Signature("4C 8B DC 53 48 81 EC ?? ?? ?? ?? 45 0F 29 43", DetourName = nameof(ResetLookAtDetour))]
    private readonly Hook<ResetLookAtDelegate> _resetLookAt = null!;
    private delegate void ResetLookAtDelegate(CharacterLookAtController* ctrl, int param, float transition);

    /// <summary>
    /// Client::Game::Character::LookAtContainer.UpdateLookAt
    /// -> Client::Game::Character::CharacterLookAtController.LookAtController.SetupLookAt
    /// </summary>
    [Signature("E8 ?? ?? ?? ?? 8B D7 48 8D 8B")]
    private readonly delegate* unmanaged<CharacterLookAtController*, CharacterLookAtTargetParam*, int, CharacterLookAtUpdateParam*, void> _setupLookAt = null!;

    private readonly Dictionary<GameObjectId, GameObjectId> _currentlyLookingAt = [];
    private readonly Lock _actorAccessLock = new();

    public LookAtService(IGameInteropProvider provider, IObjectTable objectTable, IPluginLog log, ArrpGameHooks globalHooks) {
        provider.InitializeFromAttributes(this);

        _log = log;
        _objectTable = objectTable;
        _globalHooks = globalHooks;

        _lookAt?.Enable();
        _resetLookAt?.Enable();
        _globalHooks.OnCharacterDestroyed += _globalHooks_OnCharacterDestroyed;
    }

    private void _globalHooks_OnCharacterDestroyed(Character* chara) {
        using var _ = _actorAccessLock.EnterScope();
        _currentlyLookingAt.Remove(chara->GetGameObjectId());
    }

    public bool CanLookAtSomething(BattleChara* source) {
        return source->LookAt.Controller.ParamCount > 0;
    }

    public bool IsLookingAt(BattleChara* source, BattleChara* target) {
        return source->LookAt.Controller.Params[0].TargetParam.TargetId.Id == target->GetGameObjectId().Id;
    }

    public bool IsLookingAtSomething(BattleChara* source) {
        return source->LookAt.Controller.Params[0].TargetParam.TargetId != 0;
    }

    public void LookAt(BattleChara* source, BattleChara* target) {
        if (!CanLookAtSomething(source))
            return;

        _log.Debug($"{source->GetName()} set to look at {target->GetName()}");

        using var _ = _actorAccessLock.EnterScope();
        _currentlyLookingAt[source->GetGameObjectId()] = target->GetGameObjectId();
    }

    public void LookAtNothing(BattleChara* source) {
        _log.Debug($"{source->GetName()} set to look at nothing");

        using var _ = _actorAccessLock.EnterScope();
        _currentlyLookingAt.Remove(source->GetGameObjectId());
    }

    private void LookAtDetour(LookAtContainer* cntnr) {

        _lookAt?.Original(cntnr);

        if (cntnr->Controller.OwnerObject == null)
            return;

        if (IsLookingAtSomething(cntnr->Controller.OwnerObject))
            return;

        var sourceObjectId = cntnr->Controller.OwnerObject->GetGameObjectId();

        // this is a bit of a hack to get the look-at to work, but it seems that the game resets the params every frame in CharacterLookAtController::UpdateLookAt.
        // Why it does this? Who knows, maybe because client objects were never ment to have their look-at target set to follow a certain game object? 
        using var _ = _actorAccessLock.EnterScope();
        if (_currentlyLookingAt.TryGetValue(sourceObjectId, out var target)) {
            for (var i = 0; i < cntnr->Controller.ParamCount; i++) {
                var param = cntnr->Controller.Params[i].TargetParam;
                param.TargetId.Id = target.Id;
                param.Type = CharacterLookAtTargetParam.TargetInfoType.GameObjectId;
                _setupLookAt(&cntnr->Controller, &param, i, null);
            }
        }
    }

    private void ResetLookAtDetour(CharacterLookAtController* ctrl, int param, float transition) {
        using (var _ = _actorAccessLock.EnterScope()) {
            if (_currentlyLookingAt.ContainsKey(ctrl->OwnerObject->GetGameObjectId())) {
                return;
            }
        }

        _resetLookAt?.Original(ctrl, param, transition);
    }

    public void Dispose() {
        _lookAt?.Dispose();
        _resetLookAt?.Dispose();
        _globalHooks.OnCharacterDestroyed -= _globalHooks_OnCharacterDestroyed;
        GC.SuppressFinalize(this);
    }
}
