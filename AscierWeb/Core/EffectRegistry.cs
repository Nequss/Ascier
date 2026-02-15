using AscierWeb.Core.Effects;

namespace AscierWeb.Core;

// rejestr efektów - mapuje nazwy na instancje efektów
// singleton - efekty są bezstanowe więc jedna instancja wystarczy
public static class EffectRegistry
{
    private static readonly Dictionary<string, IEffect> Effects = new(StringComparer.OrdinalIgnoreCase);

    static EffectRegistry()
    {
        Register(new ClassicEffect());
        Register(new ColorEffect());
        Register(new EdgeEffect());
        Register(new MatrixEffect());
        Register(new DitherEffect());
        Register(new BrailleEffect());
        Register(new BlockEffect());
        Register(new InvertEffect());
        Register(new ThresholdEffect());
        Register(new HalftoneEffect());
    }

    private static void Register(IEffect effect) => Effects[effect.Name] = effect;

    // pobiera efekt po nazwie, domyślnie classic
    public static IEffect Get(string name)
        => Effects.TryGetValue(name, out var effect) ? effect : Effects["classic"];

    // lista wszystkich dostępnych efektów
    public static IReadOnlyCollection<(string Name, string Description)> List()
        => Effects.Values.Select(e => (e.Name, e.Description)).ToList();
}
