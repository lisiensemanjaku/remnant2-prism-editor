using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using lib.remnant2.analyzer;

namespace PrismEditor
{
    public record CatalogEntry(string RowName, string Id, string Name, string Description);

    /// <summary>
    /// Pulls the full, real catalog of prism fusions / segments / legendary bonuses
    /// straight out of the bundled Remnant 2 item database (lib.remnant2.analyzer),
    /// instead of hardcoding a list that could go stale after a game patch.
    /// </summary>
    public static class PrismCatalog
    {
        public static readonly List<CatalogEntry> Fusions;
        public static readonly List<CatalogEntry> Segments;
        public static readonly List<CatalogEntry> Bonuses;

        private static readonly Dictionary<string, Dictionary<string, string>> RawDb;

        static PrismCatalog()
        {
            RawDb = LoadItemDbRaw();

            Fusions = Extract(RawDb, "PrismFusion_");
            Segments = Extract(RawDb, "PrismSegment_");
            Bonuses = Extract(RawDb, "PrismBonus_");
        }

        private static List<CatalogEntry> Extract(Dictionary<string, Dictionary<string, string>> dict, string prefix)
        {
            var result = new List<CatalogEntry>();
            foreach (var kv in dict)
            {
                if (!kv.Key.StartsWith(prefix)) continue;
                var rowName = kv.Key.Substring(prefix.Length);
                // Fall back to a HUMANIZED row name, not the raw camelCase one, when the DB has no "Name" entry.
                var name = kv.Value.TryGetValue("Name", out var n) && !string.IsNullOrWhiteSpace(n) ? n : Humanize(rowName);
                var desc = kv.Value.TryGetValue("Description", out var d) ? d : "";
                result.Add(new CatalogEntry(rowName, kv.Key, name, desc));
            }
            return result.OrderBy(e => e.Name).ToList();
        }

        private static Dictionary<string, Dictionary<string, string>> LoadItemDbRaw()
        {
            var t = typeof(ItemDb);
            var lookupField = t.GetField("LookupById", BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)!;
            var lazy = lookupField.GetValue(null)!;
            var valueProp = lazy.GetType().GetProperty("Value")!;
            return (Dictionary<string, Dictionary<string, string>>)valueProp.GetValue(lazy)!;
        }

        /// <summary>Turns "FireRateReloadSpeed" into "Fire Rate Reload Speed".</summary>
        public static string Humanize(string rowName) =>
            Regex.Replace(rowName, "(?<=[a-z0-9])(?=[A-Z])", " ");

        /// <summary>
        /// The real max value (e.g. "+10%") a Fragment grants at full prism investment,
        /// read straight from the game's own data (RelicFragment_*.CustomMaxValue/Unit) --
        /// the same number a Segment slot (which references one Fragment) or one half of a
        /// Fusion slot (which references two) actually applies in-game.
        /// </summary>
        private static string? FragmentValueText(string? fragmentId)
        {
            if (string.IsNullOrEmpty(fragmentId)) return null;
            if (!RawDb.TryGetValue(fragmentId, out var f)) return null;
            if (!f.TryGetValue("CustomMaxValue", out var val)) return null;
            var unit = f.TryGetValue("Unit", out var u) ? u : "";
            // Some fragments (e.g. Capacitor's charge-time reduction) have a negative max value --
            // don't double up the sign into "+-10%".
            var sign = val.StartsWith("-") ? "" : "+";
            return sign + val + unit;
        }

        /// <summary>The real max value a Segment slot grants, e.g. "Shield Amount +10%".</summary>
        public static string DescribeSegment(CatalogEntry s)
        {
            var name = s.Name;
            if (RawDb.TryGetValue(s.Id, out var row) && row.TryGetValue("Fragment", out var fragId))
            {
                var val = FragmentValueText(fragId);
                if (val != null) return $"{name} {val}";
            }
            return name;
        }

        /// <summary>The real max values a Fusion slot grants, e.g. "Ranged Fire Rate +10%, Reload Speed +10%".</summary>
        public static string DescribeFusion(string rowName)
        {
            var id = "PrismFusion_" + rowName;
            if (!RawDb.TryGetValue(id, out var row)) return Humanize(rowName);

            string Half(string fragKey)
            {
                var fragId = row.TryGetValue(fragKey, out var f) ? f : null;
                var val = FragmentValueText(fragId);
                // The Fragment's own id is always present and reliable; prefer its Name field,
                // and fall back to humanizing the id itself (never a fragile name-matching guess).
                string label = "?";
                if (fragId != null)
                {
                    if (RawDb.TryGetValue(fragId, out var fragRow) && fragRow.TryGetValue("Name", out var fn) && !string.IsNullOrWhiteSpace(fn))
                        label = fn;
                    else
                        label = Humanize(fragId.Replace("RelicFragment_", ""));
                }
                return val != null ? $"{label} {val}" : label;
            }

            return $"{Half("Fragment1")}, {Half("Fragment2")}";
        }

        /// <summary>
        /// The two real Segment row names each Fusion actually grants. A plain substring match
        /// against the fusion's own row name gets most of these right (e.g. "WeakspotDamageCriticalDamage"
        /// contains both "WeakspotDamage" and "CriticalDamage" verbatim), but several fusions use a
        /// shortened or differently-worded half that ISN'T a literal substring of their row name
        /// (e.g. Gunfighter's row is "FireRateReloadSpeed" but the real segment is "RangedFireRate",
        /// confirmed directly from this game's own save data). Those are hardcoded here instead of guessed.
        /// "Protected" (ShieldEfficacyArmorFlat) has no confirmed matching Segment for its first half as
        /// of this game version -- it's left to the generic fallback rather than guessed at.
        /// </summary>
        private static readonly Dictionary<string, (string, string)> FusionComponents = new()
        {
            ["FireRateReloadSpeed"] = ("RangedFireRate", "ReloadSpeed"),              // Gunfighter -- confirmed from save data
            ["CastSpeedUseSpeed"] = ("CastSpeed", "ConsumableSpeed"),                 // Flash
            ["HealingEfficacyUseSpeed"] = ("HealingEfficacy", "ConsumableSpeed"),     // Cleric
            ["MeleeCriticalEvadeSpeed"] = ("MeleeCriticalChance", "EvadeSpeed"),      // Rogue
            ["MeleeDamageMeleeSpeed"] = ("MeleeDamage", "MeleeAttackSpeed"),          // Warrior
            ["MeleeSpeedStaminaFlat"] = ("MeleeAttackSpeed", "StaminaFlat"),          // Pugilist
            ["ModCriticalSkillCritical"] = ("ModCriticalChance", "SkillCriticalChance"), // Wizard
            ["RangedCriticalAmmoReserves"] = ("RangedCriticalChance", "AmmoReserves"), // Munitions
        };

        /// <summary>
        /// Returns the 1 (Segment) or 2 (Fusion) real underlying stat row names an enhancement
        /// slot choice grants. Used both for the "already granted" overlap check and for display.
        /// </summary>
        public static List<string> GetUnderlyingStats(string enhancementRowName)
        {
            var segmentRowNames = Segments.Select(s => s.RowName).ToHashSet();
            if (segmentRowNames.Contains(enhancementRowName))
                return new List<string> { enhancementRowName };

            if (FusionComponents.TryGetValue(enhancementRowName, out var known))
                return new List<string> { known.Item1, known.Item2 };

            var matches = segmentRowNames.Where(s => enhancementRowName.Contains(s, StringComparison.Ordinal)).ToList();
            return matches.Count >= 2 ? matches.Take(2).ToList() : new List<string> { enhancementRowName };
        }

        public static string DescribeBonus(CatalogEntry b) =>
            string.IsNullOrWhiteSpace(b.Description) ? b.Name : b.Description;

        /// <summary>The real color the game itself assigns a Segment's icon ("Red"/"Blue"/"Yellow"/...),
        /// straight from PrismSegment_*.Color -- not a guess.</summary>
        public static string? GetSegmentColorName(string segmentRowName) =>
            RawDb.TryGetValue("PrismSegment_" + segmentRowName, out var row) && row.TryGetValue("Color", out var c) ? c : null;

        /// <summary>The real colors the game assigns a Fusion's two halves, straight from
        /// PrismFusion_*.Color1/Color2.</summary>
        public static (string? c1, string? c2) GetFusionColorNames(string fusionRowName)
        {
            if (!RawDb.TryGetValue("PrismFusion_" + fusionRowName, out var row)) return (null, null);
            row.TryGetValue("Color1", out var c1);
            row.TryGetValue("Color2", out var c2);
            return (c1, c2);
        }
    }
}
