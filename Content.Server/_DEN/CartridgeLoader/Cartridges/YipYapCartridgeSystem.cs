using Content.Server.CartridgeLoader;
using Content.Shared.CartridgeLoader;
using Content.Shared.CartridgeLoader.Cartridges;


namespace Content.Server._Den.CartridgeLoader.Cartridges;


public sealed class YipYapCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem? _cartridgeLoaderSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<YipYapCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<YipYapCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
    }

    private void OnUiReady(EntityUid uid, YipYapCartridgeComponent component, CartridgeUiReadyEvent args)
    {
        UpdateUiState(uid, args.Loader, component);
    }

    private void OnUiMessage(EntityUid uid, YipYapCartridgeComponent component, CartridgeMessageEvent args)
    {
        if (args is not NotekeeperUiMessageEvent message)
            return;

        if (message.Action == NotekeeperUiAction.Add)
        {
            component.Notes.Add(message.Note);
        }
        else
        {
            component.Notes.Remove(message.Note);
        }

        UpdateUiState(uid, GetEntity(args.LoaderUid), component);
    }


    private void UpdateUiState(EntityUid uid, EntityUid loaderUid, YipYapCartridgeComponent? component)
    {
        if (!Resolve(uid, ref component))
            return;

        var state = new NotekeeperUiState(component.Notes);
        _cartridgeLoaderSystem?.UpdateCartridgeUiState(loaderUid, state);
    }
}
