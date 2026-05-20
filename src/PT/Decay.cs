namespace PeriodicTable;

public enum DecayMode
{
    Alpha,              // α — emits ⁴₂He, A→A-4, Z→Z-2
    BetaMinus,          // β⁻ — emits e⁻ + ν̄, A unchanged, Z→Z+1
    BetaPlus,           // β⁺ — emits e⁺ + ν, A unchanged, Z→Z-1
    ElectronCapture,    // EC — absorbs e⁻ + ν, A unchanged, Z→Z-1
    SpontaneousFission, // SF
    IsomericTransition, // IT — gamma, no A or Z change
}

/// <summary>
/// One nuclide. <see cref="Z"/> = atomic number (proton count); <see cref="A"/>
/// = mass number (protons + neutrons). Symbol is resolved at display time
/// via <see cref="Elements.ByAtomicNumber"/>.
/// </summary>
public readonly record struct Isotope(int Z, int A)
{
    public string Symbol => Elements.ByAtomicNumber[Z].Symbol;
    public int Neutrons => A - Z;
    public override string ToString() => $"{Symbol}-{A}";
}

public sealed record DecayStep(
    Isotope Parent,
    Isotope Daughter,
    DecayMode Mode,
    string HalfLife);

/// <summary>
/// A linear (non-branching) sequence of decay steps from <see cref="Start"/>
/// to a final stable (or quasi-stable) <see cref="End"/> nuclide. Branches in
/// the real chains (e.g. Pb-214 → Bi-214 → Po-214 → Pb-210 vs the rare β
/// branch Bi-214 → Tl-210) are simplified to the dominant path.
/// </summary>
public sealed record DecayChain(
    string Name,
    Isotope Start,
    IReadOnlyList<DecayStep> Steps,
    Isotope End);

public static class DecayChains
{
    public static readonly DecayChain Uranium;
    public static readonly DecayChain Actinium;
    public static readonly DecayChain Thorium;
    public static readonly DecayChain Neptunium;

    public static readonly IReadOnlyList<DecayChain> All;

    /// <summary>
    /// Canonical first-decay step for elements that <see cref="ForElement"/>
    /// maps into a chain but whose isotope doesn't appear as a parent in the
    /// chain's linear <c>Steps</c> list. The actinide source nuclides Am-241
    /// and Pu-238 are the practical examples — Am-241 α-decays into the head
    /// of the Neptunium series (Np-237), and Pu-238 α-decays into U-234 mid-
    /// way through the Uranium series. The decay-chain panel falls back to
    /// these when its Parent.Z = selected.Z lookup misses, so the math legend
    /// (and its compact text equivalent) can still show a real decay equation
    /// instead of the "(stable)" terminus placeholder.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, DecayStep> CanonicalDecay;

    static DecayChains()
    {
        Uranium = BuildUranium();
        Actinium = BuildActinium();
        Thorium = BuildThorium();
        Neptunium = BuildNeptunium();
        All = [Uranium, Actinium, Thorium, Neptunium];

        CanonicalDecay = new Dictionary<string, DecayStep>(StringComparer.Ordinal)
        {
            // Pu-238 (α, 87.7 yr) → U-234 — joins the Uranium series mid-
            // stream. The 4n+2 family includes Pu-238 (238 mod 4 = 2), but
            // the canonical "Uranium series" is named after its U-238 head,
            // so Pu-238 lives outside the curated linear chain.
            ["Pu"] = new DecayStep(new Isotope(94, 238), new Isotope(92, 234), DecayMode.Alpha, "87.7 yr"),
            // Am-241 (α, 432.2 yr) → Np-237 — the canonical actinide-source
            // step that feeds the Neptunium series at its head (the chain
            // proper starts at Np-237 since Np is the heaviest naturally-
            // occurring element with a curated long-lived isotope here).
            ["Am"] = new DecayStep(new Isotope(95, 241), new Isotope(93, 237), DecayMode.Alpha, "432 yr"),
        };
    }

    /// <summary>
    /// Finds the canonical decay chain for the given element, or null if no
    /// chain is curated. Maps each radioactive element to the classical series
    /// that contains its most-cited isotope. Stable elements always return null.
    /// </summary>
    public static DecayChain? ForElement(Element e)
    {
        if (!e.IsSynthetic && e.AtomicNumber < 84) return null; // Po (84) onwards is radioactive
        return e.Symbol switch
        {
            // U-238 series
            "U"  => Uranium,
            "Th" => Thorium,           // Th-232 head
            "Pa" => Actinium,          // Pa-231
            "Ac" => Actinium,          // Ac-227
            "Ra" => Uranium,           // Ra-226 (most cited)
            "Rn" => Uranium,           // Rn-222
            "Po" => Uranium,           // Po-218 / Po-210
            "Bi" => Uranium,           // Bi-214 / Bi-210
            "Pb" => Uranium,           // Pb-214 / Pb-210 / Pb-206 sink
            "Tl" => Thorium,           // Tl-208
            "At" => Neptunium,         // At-217
            "Fr" => Neptunium,         // Fr-221
            "Np" => Neptunium,         // Np-237 head
            "Pu" => Uranium,           // Pu-238 → U-234 effectively joins U-238 series
            "Am" => Neptunium,         // Am-241 → Np-237 head
            _ => null,
        };
    }

    private static DecayChain BuildUranium()
    {
        Isotope U238 = new(92, 238), Th234 = new(90, 234), Pa234 = new(91, 234),
                U234 = new(92, 234), Th230 = new(90, 230), Ra226 = new(88, 226),
                Rn222 = new(86, 222), Po218 = new(84, 218), Pb214 = new(82, 214),
                Bi214 = new(83, 214), Po214 = new(84, 214), Pb210 = new(82, 210),
                Bi210 = new(83, 210), Po210 = new(84, 210), Pb206 = new(82, 206);
        return new DecayChain(
            "Uranium series (4n+2)", U238,
            [
                new(U238,  Th234, DecayMode.Alpha,     "4.47 Gyr"),
                new(Th234, Pa234, DecayMode.BetaMinus, "24.1 d"),
                new(Pa234, U234,  DecayMode.BetaMinus, "1.17 min"),
                new(U234,  Th230, DecayMode.Alpha,     "246 kyr"),
                new(Th230, Ra226, DecayMode.Alpha,     "75.4 kyr"),
                new(Ra226, Rn222, DecayMode.Alpha,     "1600 yr"),
                new(Rn222, Po218, DecayMode.Alpha,     "3.82 d"),
                new(Po218, Pb214, DecayMode.Alpha,     "3.10 min"),
                new(Pb214, Bi214, DecayMode.BetaMinus, "26.8 min"),
                new(Bi214, Po214, DecayMode.BetaMinus, "19.9 min"),
                new(Po214, Pb210, DecayMode.Alpha,     "164 μs"),
                new(Pb210, Bi210, DecayMode.BetaMinus, "22.2 yr"),
                new(Bi210, Po210, DecayMode.BetaMinus, "5.01 d"),
                new(Po210, Pb206, DecayMode.Alpha,     "138 d"),
            ],
            Pb206);
    }

    private static DecayChain BuildThorium()
    {
        Isotope Th232 = new(90, 232), Ra228 = new(88, 228), Ac228 = new(89, 228),
                Th228 = new(90, 228), Ra224 = new(88, 224), Rn220 = new(86, 220),
                Po216 = new(84, 216), Pb212 = new(82, 212), Bi212 = new(83, 212),
                Tl208 = new(81, 208), Pb208 = new(82, 208);
        return new DecayChain(
            "Thorium series (4n)", Th232,
            [
                new(Th232, Ra228, DecayMode.Alpha,     "14.05 Gyr"),
                new(Ra228, Ac228, DecayMode.BetaMinus, "5.75 yr"),
                new(Ac228, Th228, DecayMode.BetaMinus, "6.15 h"),
                new(Th228, Ra224, DecayMode.Alpha,     "1.91 yr"),
                new(Ra224, Rn220, DecayMode.Alpha,     "3.66 d"),
                new(Rn220, Po216, DecayMode.Alpha,     "55.6 s"),
                new(Po216, Pb212, DecayMode.Alpha,     "145 ms"),
                new(Pb212, Bi212, DecayMode.BetaMinus, "10.64 h"),
                new(Bi212, Tl208, DecayMode.Alpha,     "60.6 min"),
                new(Tl208, Pb208, DecayMode.BetaMinus, "3.05 min"),
            ],
            Pb208);
    }

    private static DecayChain BuildActinium()
    {
        Isotope U235 = new(92, 235), Th231 = new(90, 231), Pa231 = new(91, 231),
                Ac227 = new(89, 227), Th227 = new(90, 227), Ra223 = new(88, 223),
                Rn219 = new(86, 219), Po215 = new(84, 215), Pb211 = new(82, 211),
                Bi211 = new(83, 211), Tl207 = new(81, 207), Pb207 = new(82, 207);
        return new DecayChain(
            "Actinium series (4n+3)", U235,
            [
                new(U235,  Th231, DecayMode.Alpha,     "704 Myr"),
                new(Th231, Pa231, DecayMode.BetaMinus, "25.5 h"),
                new(Pa231, Ac227, DecayMode.Alpha,     "32.8 kyr"),
                new(Ac227, Th227, DecayMode.BetaMinus, "21.8 yr"),
                new(Th227, Ra223, DecayMode.Alpha,     "18.7 d"),
                new(Ra223, Rn219, DecayMode.Alpha,     "11.4 d"),
                new(Rn219, Po215, DecayMode.Alpha,     "3.96 s"),
                new(Po215, Pb211, DecayMode.Alpha,     "1.78 ms"),
                new(Pb211, Bi211, DecayMode.BetaMinus, "36.1 min"),
                new(Bi211, Tl207, DecayMode.Alpha,     "2.14 min"),
                new(Tl207, Pb207, DecayMode.BetaMinus, "4.77 min"),
            ],
            Pb207);
    }

    private static DecayChain BuildNeptunium()
    {
        Isotope Np237 = new(93, 237), Pa233 = new(91, 233), U233  = new(92, 233),
                Th229 = new(90, 229), Ra225 = new(88, 225), Ac225 = new(89, 225),
                Fr221 = new(87, 221), At217 = new(85, 217), Bi213 = new(83, 213),
                Po213 = new(84, 213), Pb209 = new(82, 209), Bi209 = new(83, 209);
        return new DecayChain(
            "Neptunium series (4n+1)", Np237,
            [
                new(Np237, Pa233, DecayMode.Alpha,     "2.14 Myr"),
                new(Pa233, U233,  DecayMode.BetaMinus, "27.0 d"),
                new(U233,  Th229, DecayMode.Alpha,     "159 kyr"),
                new(Th229, Ra225, DecayMode.Alpha,     "7920 yr"),
                new(Ra225, Ac225, DecayMode.BetaMinus, "14.9 d"),
                new(Ac225, Fr221, DecayMode.Alpha,     "9.92 d"),
                new(Fr221, At217, DecayMode.Alpha,     "4.8 min"),
                new(At217, Bi213, DecayMode.Alpha,     "32.6 ms"),
                new(Bi213, Po213, DecayMode.BetaMinus, "45.6 min"),
                new(Po213, Pb209, DecayMode.Alpha,     "4.2 μs"),
                new(Pb209, Bi209, DecayMode.BetaMinus, "3.23 h"),
            ],
            Bi209);
    }

    /// <summary>Compact symbol for the decay mode (α / β⁻ / β⁺ / EC / SF / IT).</summary>
    public static string Symbol(this DecayMode m) => m switch
    {
        DecayMode.Alpha              => "α",
        DecayMode.BetaMinus          => "β\u207B",
        DecayMode.BetaPlus           => "β\u207A",
        DecayMode.ElectronCapture    => "EC",
        DecayMode.SpontaneousFission => "SF",
        DecayMode.IsomericTransition => "IT",
        _ => "?",
    };
}
