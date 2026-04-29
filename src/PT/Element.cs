namespace PeriodicTable;

public enum Block { S, P, D, F }

public enum Category
{
    AlkaliMetal,
    AlkalineEarthMetal,
    TransitionMetal,
    PostTransitionMetal,
    Metalloid,
    ReactiveNonmetal,
    NobleGas,
    Lanthanide,
    Actinide,
    Unknown,
}

/// <summary>
/// One chemical element. <see cref="Group"/> is null for the lanthanide/actinide
/// series (Z=57–71 and 89–103) when laid out in the f-block row instead of the
/// main 18-column grid; the f-block row uses <see cref="FBlockIndex"/>.
/// <see cref="AtomicWeight"/> is the IUPAC 2021 standard atomic weight for stable
/// elements, and the mass number of the most stable known isotope for
/// synthetics (signalled by <see cref="IsSynthetic"/>).
/// </summary>
public sealed record Element(
    int AtomicNumber,
    string Symbol,
    string Name,
    int? Group,
    int Period,
    Block Block,
    Category Category,
    double AtomicWeight,
    bool IsSynthetic,
    string ElectronConfiguration)
{
    public int? FBlockIndex =>
        Category switch
        {
            Category.Lanthanide => AtomicNumber - 57,
            Category.Actinide => AtomicNumber - 89,
            _ => null,
        };
}
