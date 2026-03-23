namespace Codapter.Core.Services;

/// <summary>
/// Predefined agent nicknames for collaboration.
/// Port of packages/core/src/collab-nicknames.ts
/// </summary>
public static class CollabNicknames
{
    public static readonly string[] AgentNicknames =
    {
        "Robie", "Zara", "Pixel", "Nova", "Atlas", "Byte", "Echo", "Luna", "Milo", "Iris",
        "Kite", "Rex", "Piper", "Juno", "Orion", "Tango", "Clover", "Sable", "Quest", "Nori",
        "Moxie", "Scout", "Vega", "Basil", "Coda", "Flint", "Astra", "Dune", "Rumi", "Fable",
        "Pico", "Zeke", "Lyra", "Onyx", "Pebble", "Sol", "Juniper", "Marlo", "Tess", "Knox",
        "Aero", "Bram", "Cleo", "Dax", "Elio", "Fern", "Gizmo", "Halo", "Indie", "Jett",
        "Koda", "Lark", "Mira", "Nimble", "Olive", "Pax", "Quill", "Rory", "Sirius", "Tula",
        "Uma", "Vale", "Wren", "Xeno", "Yara", "Zephyr", "Amber", "Blip", "Comet", "Drift",
        "Ember", "Fig", "Glint", "Harbor", "Ingot", "Jasper", "Kepler", "Lotus", "Merit", "Nimbus",
        "Opal", "Prism", "Quartz", "Ripple", "Saffron", "Thistle", "Ulric", "Velvet", "Willow", "Xylo",
        "Yonder", "Zinnia", "Argon", "Bixby", "Cirrus", "Delta", "Edison", "Fjord", "Galen", "Hera",
        "Iskra", "Jovie", "Kismet", "Lumen", "Mistral", "Nyx"
    };

    private static int _counter;

    public static string GetNextNickname(HashSet<string> usedNames)
    {
        foreach (var name in AgentNicknames)
        {
            if (!usedNames.Contains(name))
            {
                usedNames.Add(name);
                return name;
            }
        }

        // Fallback: numbered names
        var fallback = $"Agent-{Interlocked.Increment(ref _counter)}";
        usedNames.Add(fallback);
        return fallback;
    }
}
