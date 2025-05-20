﻿using Content.Client.UserInterface.Fragments;
using Content.Shared.CartridgeLoader;
using Content.Shared.CartridgeLoader.Cartridges;
using Robust.Client.UserInterface;

namespace Content.Client.CartridgeLoader.Cartridges;

public sealed partial class YipYapUi : UIFragment
{
    private YipYapUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new YipYapUiFragment();
        _fragment.OnNoteRemoved += note => SendNotekeeperMessage(NotekeeperUiAction.Remove, note, userInterface);
        _fragment.OnNoteAdded += note => SendNotekeeperMessage(NotekeeperUiAction.Add, note, userInterface);
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not NotekeeperUiState notekeepeerState)
            return;

        _fragment?.UpdateState(notekeepeerState.Notes);
    }

    private void SendNotekeeperMessage(NotekeeperUiAction action, string note, BoundUserInterface userInterface)
    {
        var notekeeperMessage = new NotekeeperUiMessageEvent(action, note.ToUpper());
        var message = new CartridgeUiMessage(notekeeperMessage);
        userInterface.SendMessage(message);
    }
}
