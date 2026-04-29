using System.Collections.Frozen;

namespace PeriodicTable;

/// <summary>
/// Static table of all 118 known chemical elements (Z=1..118).
///
/// Atomic weights: IUPAC 2021 standard atomic weights for elements with stable
/// isotopes; mass number of the most stable known isotope (in days) for
/// synthetic elements (<see cref="Element.IsSynthetic"/> = true).
///
/// Category assignments follow the common reactivity-based scheme; placement of
/// borderline elements (At as metalloid, Cn as transition metal, Og as noble
/// gas, Mc/Lv/Nh/Fl as post-transition metal) follows the most widely used
/// IUPAC-aligned periodic-table layout.
///
/// Electron configurations are noble-gas-abbreviated ground-state configs using
/// Unicode superscripts so they render in one cell per digit on terminals with
/// no wide-char support.
/// </summary>
public static class Elements
{
    public static readonly FrozenDictionary<int, Element> ByAtomicNumber;
    public static readonly FrozenDictionary<string, Element> BySymbol;
    public static readonly IReadOnlyList<Element> All;

    static Elements()
    {
        var arr = Build();
        All = arr;
        ByAtomicNumber = arr.ToFrozenDictionary(e => e.AtomicNumber);
        BySymbol = arr.ToFrozenDictionary(e => e.Symbol, StringComparer.Ordinal);
    }

    private static Element[] Build()
    {
        // E = Element(Z, Symbol, Name, Group, Period, Block, Category, AtomicWeight, IsSynthetic, ElectronConfig)
        // Group is null only for lanthanides/actinides shown in f-block row.
        const Block S = Block.S, P = Block.P, D = Block.D, F = Block.F;
        const Category Alkali = Category.AlkaliMetal,
                       AlkEarth = Category.AlkalineEarthMetal,
                       Trans = Category.TransitionMetal,
                       PostT = Category.PostTransitionMetal,
                       Metalloid = Category.Metalloid,
                       NM = Category.ReactiveNonmetal,
                       Noble = Category.NobleGas,
                       Lan = Category.Lanthanide,
                       Act = Category.Actinide,
                       Unk = Category.Unknown;

        return
        [
            new(  1, "H",  "Hydrogen",       1, 1, S, NM,        1.008,   false, "1s¹"),
            new(  2, "He", "Helium",        18, 1, S, Noble,     4.0026,  false, "1s²"),
            new(  3, "Li", "Lithium",        1, 2, S, Alkali,    6.94,    false, "[He] 2s¹"),
            new(  4, "Be", "Beryllium",      2, 2, S, AlkEarth,  9.0122,  false, "[He] 2s²"),
            new(  5, "B",  "Boron",         13, 2, P, Metalloid, 10.81,   false, "[He] 2s² 2p¹"),
            new(  6, "C",  "Carbon",        14, 2, P, NM,        12.011,  false, "[He] 2s² 2p²"),
            new(  7, "N",  "Nitrogen",      15, 2, P, NM,        14.007,  false, "[He] 2s² 2p³"),
            new(  8, "O",  "Oxygen",        16, 2, P, NM,        15.999,  false, "[He] 2s² 2p⁴"),
            new(  9, "F",  "Fluorine",      17, 2, P, NM,        18.998,  false, "[He] 2s² 2p⁵"),
            new( 10, "Ne", "Neon",          18, 2, P, Noble,     20.180,  false, "[He] 2s² 2p⁶"),
            new( 11, "Na", "Sodium",         1, 3, S, Alkali,    22.990,  false, "[Ne] 3s¹"),
            new( 12, "Mg", "Magnesium",      2, 3, S, AlkEarth,  24.305,  false, "[Ne] 3s²"),
            new( 13, "Al", "Aluminium",     13, 3, P, PostT,     26.982,  false, "[Ne] 3s² 3p¹"),
            new( 14, "Si", "Silicon",       14, 3, P, Metalloid, 28.085,  false, "[Ne] 3s² 3p²"),
            new( 15, "P",  "Phosphorus",    15, 3, P, NM,        30.974,  false, "[Ne] 3s² 3p³"),
            new( 16, "S",  "Sulfur",        16, 3, P, NM,        32.06,   false, "[Ne] 3s² 3p⁴"),
            new( 17, "Cl", "Chlorine",      17, 3, P, NM,        35.45,   false, "[Ne] 3s² 3p⁵"),
            new( 18, "Ar", "Argon",         18, 3, P, Noble,     39.95,   false, "[Ne] 3s² 3p⁶"),
            new( 19, "K",  "Potassium",      1, 4, S, Alkali,    39.098,  false, "[Ar] 4s¹"),
            new( 20, "Ca", "Calcium",        2, 4, S, AlkEarth,  40.078,  false, "[Ar] 4s²"),
            new( 21, "Sc", "Scandium",       3, 4, D, Trans,     44.956,  false, "[Ar] 3d¹ 4s²"),
            new( 22, "Ti", "Titanium",       4, 4, D, Trans,     47.867,  false, "[Ar] 3d² 4s²"),
            new( 23, "V",  "Vanadium",       5, 4, D, Trans,     50.942,  false, "[Ar] 3d³ 4s²"),
            new( 24, "Cr", "Chromium",       6, 4, D, Trans,     51.996,  false, "[Ar] 3d⁵ 4s¹"),
            new( 25, "Mn", "Manganese",      7, 4, D, Trans,     54.938,  false, "[Ar] 3d⁵ 4s²"),
            new( 26, "Fe", "Iron",           8, 4, D, Trans,     55.845,  false, "[Ar] 3d⁶ 4s²"),
            new( 27, "Co", "Cobalt",         9, 4, D, Trans,     58.933,  false, "[Ar] 3d⁷ 4s²"),
            new( 28, "Ni", "Nickel",        10, 4, D, Trans,     58.693,  false, "[Ar] 3d⁸ 4s²"),
            new( 29, "Cu", "Copper",        11, 4, D, Trans,     63.546,  false, "[Ar] 3d¹⁰ 4s¹"),
            new( 30, "Zn", "Zinc",          12, 4, D, Trans,     65.38,   false, "[Ar] 3d¹⁰ 4s²"),
            new( 31, "Ga", "Gallium",       13, 4, P, PostT,     69.723,  false, "[Ar] 3d¹⁰ 4s² 4p¹"),
            new( 32, "Ge", "Germanium",     14, 4, P, Metalloid, 72.630,  false, "[Ar] 3d¹⁰ 4s² 4p²"),
            new( 33, "As", "Arsenic",       15, 4, P, Metalloid, 74.922,  false, "[Ar] 3d¹⁰ 4s² 4p³"),
            new( 34, "Se", "Selenium",      16, 4, P, NM,        78.971,  false, "[Ar] 3d¹⁰ 4s² 4p⁴"),
            new( 35, "Br", "Bromine",       17, 4, P, NM,        79.904,  false, "[Ar] 3d¹⁰ 4s² 4p⁵"),
            new( 36, "Kr", "Krypton",       18, 4, P, Noble,     83.798,  false, "[Ar] 3d¹⁰ 4s² 4p⁶"),
            new( 37, "Rb", "Rubidium",       1, 5, S, Alkali,    85.468,  false, "[Kr] 5s¹"),
            new( 38, "Sr", "Strontium",      2, 5, S, AlkEarth,  87.62,   false, "[Kr] 5s²"),
            new( 39, "Y",  "Yttrium",        3, 5, D, Trans,     88.906,  false, "[Kr] 4d¹ 5s²"),
            new( 40, "Zr", "Zirconium",      4, 5, D, Trans,     91.224,  false, "[Kr] 4d² 5s²"),
            new( 41, "Nb", "Niobium",        5, 5, D, Trans,     92.906,  false, "[Kr] 4d⁴ 5s¹"),
            new( 42, "Mo", "Molybdenum",     6, 5, D, Trans,     95.95,   false, "[Kr] 4d⁵ 5s¹"),
            new( 43, "Tc", "Technetium",     7, 5, D, Trans,     98,      true,  "[Kr] 4d⁵ 5s²"),
            new( 44, "Ru", "Ruthenium",      8, 5, D, Trans,     101.07,  false, "[Kr] 4d⁷ 5s¹"),
            new( 45, "Rh", "Rhodium",        9, 5, D, Trans,     102.91,  false, "[Kr] 4d⁸ 5s¹"),
            new( 46, "Pd", "Palladium",     10, 5, D, Trans,     106.42,  false, "[Kr] 4d¹⁰"),
            new( 47, "Ag", "Silver",        11, 5, D, Trans,     107.87,  false, "[Kr] 4d¹⁰ 5s¹"),
            new( 48, "Cd", "Cadmium",       12, 5, D, Trans,     112.41,  false, "[Kr] 4d¹⁰ 5s²"),
            new( 49, "In", "Indium",        13, 5, P, PostT,     114.82,  false, "[Kr] 4d¹⁰ 5s² 5p¹"),
            new( 50, "Sn", "Tin",           14, 5, P, PostT,     118.71,  false, "[Kr] 4d¹⁰ 5s² 5p²"),
            new( 51, "Sb", "Antimony",      15, 5, P, Metalloid, 121.76,  false, "[Kr] 4d¹⁰ 5s² 5p³"),
            new( 52, "Te", "Tellurium",     16, 5, P, Metalloid, 127.60,  false, "[Kr] 4d¹⁰ 5s² 5p⁴"),
            new( 53, "I",  "Iodine",        17, 5, P, NM,        126.90,  false, "[Kr] 4d¹⁰ 5s² 5p⁵"),
            new( 54, "Xe", "Xenon",         18, 5, P, Noble,     131.29,  false, "[Kr] 4d¹⁰ 5s² 5p⁶"),
            new( 55, "Cs", "Caesium",        1, 6, S, Alkali,    132.91,  false, "[Xe] 6s¹"),
            new( 56, "Ba", "Barium",         2, 6, S, AlkEarth,  137.33,  false, "[Xe] 6s²"),
            new( 57, "La", "Lanthanum",   null, 6, F, Lan,       138.91,  false, "[Xe] 5d¹ 6s²"),
            new( 58, "Ce", "Cerium",      null, 6, F, Lan,       140.12,  false, "[Xe] 4f¹ 5d¹ 6s²"),
            new( 59, "Pr", "Praseodymium",null, 6, F, Lan,       140.91,  false, "[Xe] 4f³ 6s²"),
            new( 60, "Nd", "Neodymium",   null, 6, F, Lan,       144.24,  false, "[Xe] 4f⁴ 6s²"),
            new( 61, "Pm", "Promethium",  null, 6, F, Lan,       145,     true,  "[Xe] 4f⁵ 6s²"),
            new( 62, "Sm", "Samarium",    null, 6, F, Lan,       150.36,  false, "[Xe] 4f⁶ 6s²"),
            new( 63, "Eu", "Europium",    null, 6, F, Lan,       151.96,  false, "[Xe] 4f⁷ 6s²"),
            new( 64, "Gd", "Gadolinium",  null, 6, F, Lan,       157.25,  false, "[Xe] 4f⁷ 5d¹ 6s²"),
            new( 65, "Tb", "Terbium",     null, 6, F, Lan,       158.93,  false, "[Xe] 4f⁹ 6s²"),
            new( 66, "Dy", "Dysprosium",  null, 6, F, Lan,       162.50,  false, "[Xe] 4f¹⁰ 6s²"),
            new( 67, "Ho", "Holmium",     null, 6, F, Lan,       164.93,  false, "[Xe] 4f¹¹ 6s²"),
            new( 68, "Er", "Erbium",      null, 6, F, Lan,       167.26,  false, "[Xe] 4f¹² 6s²"),
            new( 69, "Tm", "Thulium",     null, 6, F, Lan,       168.93,  false, "[Xe] 4f¹³ 6s²"),
            new( 70, "Yb", "Ytterbium",   null, 6, F, Lan,       173.05,  false, "[Xe] 4f¹⁴ 6s²"),
            new( 71, "Lu", "Lutetium",    null, 6, D, Lan,       174.97,  false, "[Xe] 4f¹⁴ 5d¹ 6s²"),
            new( 72, "Hf", "Hafnium",        4, 6, D, Trans,     178.49,  false, "[Xe] 4f¹⁴ 5d² 6s²"),
            new( 73, "Ta", "Tantalum",       5, 6, D, Trans,     180.95,  false, "[Xe] 4f¹⁴ 5d³ 6s²"),
            new( 74, "W",  "Tungsten",       6, 6, D, Trans,     183.84,  false, "[Xe] 4f¹⁴ 5d⁴ 6s²"),
            new( 75, "Re", "Rhenium",        7, 6, D, Trans,     186.21,  false, "[Xe] 4f¹⁴ 5d⁵ 6s²"),
            new( 76, "Os", "Osmium",         8, 6, D, Trans,     190.23,  false, "[Xe] 4f¹⁴ 5d⁶ 6s²"),
            new( 77, "Ir", "Iridium",        9, 6, D, Trans,     192.22,  false, "[Xe] 4f¹⁴ 5d⁷ 6s²"),
            new( 78, "Pt", "Platinum",      10, 6, D, Trans,     195.08,  false, "[Xe] 4f¹⁴ 5d⁹ 6s¹"),
            new( 79, "Au", "Gold",          11, 6, D, Trans,     196.97,  false, "[Xe] 4f¹⁴ 5d¹⁰ 6s¹"),
            new( 80, "Hg", "Mercury",       12, 6, D, Trans,     200.59,  false, "[Xe] 4f¹⁴ 5d¹⁰ 6s²"),
            new( 81, "Tl", "Thallium",      13, 6, P, PostT,     204.38,  false, "[Xe] 4f¹⁴ 5d¹⁰ 6s² 6p¹"),
            new( 82, "Pb", "Lead",          14, 6, P, PostT,     207.2,   false, "[Xe] 4f¹⁴ 5d¹⁰ 6s² 6p²"),
            new( 83, "Bi", "Bismuth",       15, 6, P, PostT,     208.98,  false, "[Xe] 4f¹⁴ 5d¹⁰ 6s² 6p³"),
            new( 84, "Po", "Polonium",      16, 6, P, PostT,     209,     true,  "[Xe] 4f¹⁴ 5d¹⁰ 6s² 6p⁴"),
            new( 85, "At", "Astatine",      17, 6, P, Metalloid, 210,     true,  "[Xe] 4f¹⁴ 5d¹⁰ 6s² 6p⁵"),
            new( 86, "Rn", "Radon",         18, 6, P, Noble,     222,     true,  "[Xe] 4f¹⁴ 5d¹⁰ 6s² 6p⁶"),
            new( 87, "Fr", "Francium",       1, 7, S, Alkali,    223,     true,  "[Rn] 7s¹"),
            new( 88, "Ra", "Radium",         2, 7, S, AlkEarth,  226,     true,  "[Rn] 7s²"),
            new( 89, "Ac", "Actinium",    null, 7, F, Act,       227,     true,  "[Rn] 6d¹ 7s²"),
            new( 90, "Th", "Thorium",     null, 7, F, Act,       232.04,  true,  "[Rn] 6d² 7s²"),
            new( 91, "Pa", "Protactinium",null, 7, F, Act,       231.04,  true,  "[Rn] 5f² 6d¹ 7s²"),
            new( 92, "U",  "Uranium",     null, 7, F, Act,       238.03,  true,  "[Rn] 5f³ 6d¹ 7s²"),
            new( 93, "Np", "Neptunium",   null, 7, F, Act,       237,     true,  "[Rn] 5f⁴ 6d¹ 7s²"),
            new( 94, "Pu", "Plutonium",   null, 7, F, Act,       244,     true,  "[Rn] 5f⁶ 7s²"),
            new( 95, "Am", "Americium",   null, 7, F, Act,       243,     true,  "[Rn] 5f⁷ 7s²"),
            new( 96, "Cm", "Curium",      null, 7, F, Act,       247,     true,  "[Rn] 5f⁷ 6d¹ 7s²"),
            new( 97, "Bk", "Berkelium",   null, 7, F, Act,       247,     true,  "[Rn] 5f⁹ 7s²"),
            new( 98, "Cf", "Californium", null, 7, F, Act,       251,     true,  "[Rn] 5f¹⁰ 7s²"),
            new( 99, "Es", "Einsteinium", null, 7, F, Act,       252,     true,  "[Rn] 5f¹¹ 7s²"),
            new(100, "Fm", "Fermium",     null, 7, F, Act,       257,     true,  "[Rn] 5f¹² 7s²"),
            new(101, "Md", "Mendelevium", null, 7, F, Act,       258,     true,  "[Rn] 5f¹³ 7s²"),
            new(102, "No", "Nobelium",    null, 7, F, Act,       259,     true,  "[Rn] 5f¹⁴ 7s²"),
            new(103, "Lr", "Lawrencium",  null, 7, D, Act,       266,     true,  "[Rn] 5f¹⁴ 7s² 7p¹"),
            new(104, "Rf", "Rutherfordium", 4, 7, D, Trans,      267,     true,  "[Rn] 5f¹⁴ 6d² 7s²"),
            new(105, "Db", "Dubnium",        5, 7, D, Trans,     268,     true,  "[Rn] 5f¹⁴ 6d³ 7s²"),
            new(106, "Sg", "Seaborgium",     6, 7, D, Trans,     269,     true,  "[Rn] 5f¹⁴ 6d⁴ 7s²"),
            new(107, "Bh", "Bohrium",        7, 7, D, Trans,     270,     true,  "[Rn] 5f¹⁴ 6d⁵ 7s²"),
            new(108, "Hs", "Hassium",        8, 7, D, Trans,     269,     true,  "[Rn] 5f¹⁴ 6d⁶ 7s²"),
            new(109, "Mt", "Meitnerium",     9, 7, D, Trans,     278,     true,  "[Rn] 5f¹⁴ 6d⁷ 7s²"),
            new(110, "Ds", "Darmstadtium", 10, 7, D, Trans,      281,     true,  "[Rn] 5f¹⁴ 6d⁸ 7s²"),
            new(111, "Rg", "Roentgenium", 11, 7, D, Trans,       282,     true,  "[Rn] 5f¹⁴ 6d⁹ 7s²"),
            new(112, "Cn", "Copernicium", 12, 7, D, Trans,       285,     true,  "[Rn] 5f¹⁴ 6d¹⁰ 7s²"),
            new(113, "Nh", "Nihonium",    13, 7, P, PostT,       286,     true,  "[Rn] 5f¹⁴ 6d¹⁰ 7s² 7p¹"),
            new(114, "Fl", "Flerovium",   14, 7, P, PostT,       289,     true,  "[Rn] 5f¹⁴ 6d¹⁰ 7s² 7p²"),
            new(115, "Mc", "Moscovium",   15, 7, P, PostT,       289,     true,  "[Rn] 5f¹⁴ 6d¹⁰ 7s² 7p³"),
            new(116, "Lv", "Livermorium", 16, 7, P, PostT,       293,     true,  "[Rn] 5f¹⁴ 6d¹⁰ 7s² 7p⁴"),
            new(117, "Ts", "Tennessine",  17, 7, P, Unk,         294,     true,  "[Rn] 5f¹⁴ 6d¹⁰ 7s² 7p⁵"),
            new(118, "Og", "Oganesson",   18, 7, P, Noble,       294,     true,  "[Rn] 5f¹⁴ 6d¹⁰ 7s² 7p⁶"),
        ];
    }
}
