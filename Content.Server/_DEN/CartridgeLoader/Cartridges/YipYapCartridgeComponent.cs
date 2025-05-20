namespace Content.Server._Den.CartridgeLoader.Cartridges;

[RegisterComponent]
public sealed partial class YipYapCartridgeComponent : Component
{
    /// <summary>
    /// The list of notes that got written down
    /// </summary>
    [DataField("notes")]
    public List<string> Notes = new();
}
