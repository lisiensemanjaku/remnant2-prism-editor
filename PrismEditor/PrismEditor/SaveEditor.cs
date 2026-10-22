using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using lib.remnant2.saves.Model;
using lib.remnant2.saves.Model.Parts;
using lib.remnant2.saves.Model.Properties;

namespace PrismEditor
{
    public class SlotViewModel
    {
        public string RowName { get; set; } = "";   // e.g. "FireRateReloadSpeed"
        public int Level { get; set; }               // 10 for a maxed enhancement slot, 1 for a maxed bonus slot
    }

    public class PrismViewModel
    {
        public string DisplayName { get; set; } = "";
        public string ItemBpPath { get; set; } = "";
        public int RawLevel { get; set; }
        public bool HasBeenFed { get; set; }
        public int CurrentSeed { get; set; }
        public bool IsBuilt { get; set; } // true if CurrentSegments/Level already exist (vs. an empty/un-leveled prism)

        // 5 enhancement slots (fusion or segment) + 1 legendary bonus slot, in that order.
        public SlotViewModel[] Slots { get; set; } = new SlotViewModel[6];

        internal PropertyBag InventoryEntry = null!;
        internal UObject InstanceDataObject = null!;
    }

    /// <summary>
    /// Opens a Remnant 2 profile.sav, finds the player's prism items, lets you read and
    /// rewrite their fusion/segment/legendary slot configuration, and writes the result
    /// back out. All prism data lives in profile.sav (inside a nested per-character
    /// sub-archive) -- nothing here touches save_0.sav / save_1.sav.
    /// </summary>
    public class SaveEditor
    {
        private SaveFile _save = null!;
        private string _path = null!;
        private string _originalBackupPath = null!;
        private ArrayStructProperty _itemsArray = null!;

        public List<PrismViewModel> Prisms { get; private set; } = new();

        /// <summary>True once this file has an app-made pristine copy of how it looked the
        /// very first time it was opened -- i.e. RevertToOriginal() has something to restore.</summary>
        public bool HasOriginalBackup => _originalBackupPath != null && System.IO.File.Exists(_originalBackupPath);

        public void Open(string profilePath)
        {
            _path = profilePath;
            _originalBackupPath = profilePath + ".original";

            // Snapshot the file exactly as it was the first time this app ever opened it --
            // never overwritten again, so the user never has to remember to back it up themselves.
            if (!System.IO.File.Exists(_originalBackupPath))
                System.IO.File.Copy(profilePath, _originalBackupPath);

            _save = SaveFile.Read(profilePath);

            var savedCharacter = _save.SaveData.Objects.First(o =>
                o.Name == "SavedCharacter" && o.Properties.Properties.Any(kv => kv.Key == "CharacterData"));
            var charDataStruct = (StructProperty)savedCharacter.Properties.Properties.First(kv => kv.Key == "CharacterData").Value.Value!;
            var nestedSave = (SaveData)charDataStruct.Value!;
            var playerObj = nestedSave.Objects.First(o =>
                o.ObjectPath == "Character_Master_Player_C" || o.Name == "Character_Master_Player_C");
            var invComp = playerObj.Components.First(c => c.Properties.Properties.Any(kv => kv.Key == "Items"));
            _itemsArray = (ArrayStructProperty)invComp.Properties.Properties.First(kv => kv.Key == "Items").Value.Value!;

            Prisms = BuildPrismList();
        }

        private List<PrismViewModel> BuildPrismList()
        {
            var prismIds = PrismCatalog_PrismItemIds();
            var result = new List<PrismViewModel>();

            foreach (var it in _itemsArray.Items)
            {
                if (it is not PropertyBag bag) continue;
                var bpKv = bag.Properties.FirstOrDefault(kv => kv.Key == "ItemBP");
                var path = (bpKv.Value?.Value as ObjectProperty)?.Object?.ObjectPath ?? "";
                if (!prismIds.Any(id => path.Contains(id, StringComparison.OrdinalIgnoreCase))) continue;

                var instKv = bag.Properties.FirstOrDefault(kv => kv.Key == "InstanceData");
                var instObj = (instKv.Value?.Value as ObjectProperty)?.Object;
                if (instObj == null) continue;

                var vm = new PrismViewModel
                {
                    ItemBpPath = path,
                    DisplayName = PrismDisplayName(path),
                    InventoryEntry = bag,
                    InstanceDataObject = instObj,
                };

                var props = instObj.Properties.Properties;
                var segKv = props.FirstOrDefault(kv => kv.Key == "CurrentSegments");
                var levelKv = props.FirstOrDefault(kv => kv.Key == "Level");
                var seedKv = props.FirstOrDefault(kv => kv.Key == "CurrentSeed");
                var fedKv = props.FirstOrDefault(kv => kv.Key == "HasBeenFed");

                vm.CurrentSeed = seedKv.Value?.Value is int si ? si : 0;
                vm.HasBeenFed = ReadBool(fedKv.Value);
                vm.IsBuilt = segKv.Value != null && levelKv.Value != null;

                if (vm.IsBuilt)
                {
                    vm.RawLevel = ReadByteOrInt(levelKv.Value);
                    var segArray = (ArrayStructProperty)segKv.Value.Value!;
                    for (int i = 0; i < Math.Min(6, segArray.Items.Count); i++)
                    {
                        var sbag = (PropertyBag)segArray.Items[i]!;
                        var rn = ReadFName(sbag, "RowName");
                        var lvl = ReadIntField(sbag, "Level");
                        vm.Slots[i] = new SlotViewModel { RowName = rn, Level = lvl };
                    }
                }
                for (int i = 0; i < 6; i++)
                    vm.Slots[i] ??= new SlotViewModel { RowName = "", Level = 0 };

                result.Add(vm);
            }
            return result;
        }

/// <summary>
        /// Returns the underlying sub-stat(s) a given enhancement row name grants: one for a
        /// standalone Segment, two for a Fusion (its two component Segment row names). Backed by
        /// PrismCatalog's verified fusion-component table (not a raw substring guess).
        /// </summary>
        public static List<string> GetUnderlyingStats(string enhancementRowName) =>
            PrismCatalog.GetUnderlyingStats(enhancementRowName);

        /// <summary>
        /// Checks the 5 chosen enhancement slots for a stat that would end up granted twice
        /// (once by a Fusion, once by a standalone Segment, or by two Fusions sharing a half).
        /// The real game doesn't allow this. Returns the conflicting stat names, empty if none.
        /// </summary>
        public static List<string> FindOverlapConflicts(IEnumerable<string> enhancementRowNames)
        {
            // Track which SLOT POSITION first claimed each stat -- comparing row names would
            // miss the case of picking the exact same fusion twice in two different slots
            // (two different positions, identical name), which is still a duplicate stat.
            var seen = new Dictionary<string, int>(); // stat -> which slot index first claimed it
            var conflicts = new List<string>();
            int index = 0;
            foreach (var row in enhancementRowNames)
            {
                foreach (var stat in GetUnderlyingStats(row))
                {
                    if (seen.TryGetValue(stat, out var ownerIndex) && ownerIndex != index)
                        conflicts.Add(stat);
                    else
                        seen[stat] = index;
                }
                index++;
            }
            return conflicts.Distinct().ToList();
        }

        public const int MaxFusions = 4;

        /// <summary>
        /// The game caps a prism at 4 Fusions among its 5 enhancement slots (at least 1 slot
        /// must be a standalone Segment). Returns true and the count if that cap is exceeded.
        /// </summary>
        public static bool TooManyFusions(IEnumerable<string> enhancementRowNames, out int fusionCount)
        {
            fusionCount = enhancementRowNames.Count(row => PrismCatalog.Fusions.Any(f => f.RowName == row));
            return fusionCount > MaxFusions;
        }

        /// <summary>
        /// Rewrites a prism's configuration: 5 enhancement slots (fusion or segment row
        /// names) + 1 legendary bonus row name. Works whether the prism was already built
        /// (has CurrentSegments/Level) or empty/un-leveled (only has CurrentSeed etc.).
        ///
        /// By default this refuses a configuration where a stat would be granted twice
        /// (the real game doesn't allow that). Set <paramref name="allowOverlap"/> to true
        /// to override that check -- useful for experimenting with stacking the same stat
        /// twice, which the game may or may not actually double-apply.
        /// </summary>
        public void ApplyConfiguration(PrismViewModel vm, string[] enhancementRowNames, string bonusRowName, int rawLevel = 99, bool allowOverlap = false)
        {
            if (enhancementRowNames.Length != 5) throw new ArgumentException("Need exactly 5 enhancement row names.");

            if (!allowOverlap)
            {
                var conflicts = FindOverlapConflicts(enhancementRowNames);
                if (conflicts.Count > 0)
                    throw new InvalidOperationException(
                        $"These slots grant the same stat more than once: {string.Join(", ", conflicts.Select(PrismCatalog.Humanize))}. " +
                        "Pick different slots, or pass allowOverlap: true to override.");

                if (TooManyFusions(enhancementRowNames, out var fusionCount))
                    throw new InvalidOperationException(
                        $"{fusionCount} of the 5 slots are Fusions, but the game caps a prism at {MaxFusions} -- " +
                        "at least 1 slot must be a standalone Segment. Pick a Segment for one slot, or pass allowOverlap: true to override.");
            }

            var allRows = enhancementRowNames.Concat(new[] { bonusRowName }).ToArray();
            var levels = new[] { 10, 10, 10, 10, 10, 1 };

            var props = vm.InstanceDataObject.Properties.Properties;

            Property segProp, feedProp, levelProp;
            if (vm.IsBuilt)
            {
                segProp = props.First(kv => kv.Key == "CurrentSegments").Value;
                feedProp = props.First(kv => kv.Key == "CurrentFeedData").Value;
                levelProp = props.First(kv => kv.Key == "Level").Value;
            }
            else
            {
                // Build fresh property instances by cloning the shape from any already-built prism in this save.
                var template = Prisms.FirstOrDefault(p => p.IsBuilt) ??
                    throw new InvalidOperationException("No already-built prism in this save to use as a structural template.");
                segProp = (Property)DeepClone(template.InstanceDataObject.Properties.Properties.First(kv => kv.Key == "CurrentSegments").Value)!;
                feedProp = (Property)DeepClone(template.InstanceDataObject.Properties.Properties.First(kv => kv.Key == "CurrentFeedData").Value)!;
                levelProp = (Property)DeepClone(template.InstanceDataObject.Properties.Properties.First(kv => kv.Key == "Level").Value)!;
            }

            // Fill the 6 slots.
            var segArray = (ArrayStructProperty)segProp.Value!;
            while (segArray.Items.Count < 6)
                segArray.Items.Add(DeepClone(segArray.Items[0]));
            for (int i = 0; i < 6; i++)
            {
                var sbag = (PropertyBag)segArray.Items[i]!;
                WriteFName(sbag, "RowName", allRows[i]);
                WriteIntField(sbag, "Level", levels[i]);
            }

            // Rebuild feed history: one entry per distinct underlying sub-stat granted by
            // the chosen fusions/segments (cosmetic "materials fed" bookkeeping only).
            var feedRows = ResolveFeedRows(allRows.Take(5));
            var feedArray = (ArrayStructProperty)feedProp.Value!;
            var feedTemplate = feedArray.Items.Count > 0 ? feedArray.Items[0] : null;
            if (feedTemplate == null)
            {
                var anyBuilt = Prisms.First(p => p.IsBuilt);
                var anyFeed = (ArrayStructProperty)anyBuilt.InstanceDataObject.Properties.Properties
                    .First(kv => kv.Key == "CurrentFeedData").Value.Value!;
                feedTemplate = anyFeed.Items[0];
            }
            var newFeedItems = feedRows.Select(row =>
            {
                var clone = (PropertyBag)DeepClone(feedTemplate)!;
                WriteFName(clone, "RowName", row);
                WriteIntField(clone, "FedLevel", 32);
                return (object?)clone;
            }).ToList();
            feedArray.Items.Clear();
            foreach (var it in newFeedItems) feedArray.Items.Add(it);

            WriteByteOrInt(levelProp, rawLevel);

            if (!vm.IsBuilt)
            {
                // A prism can be in one of two "un-leveled" raw shapes: freshly picked up
                // (only CurrentSeed present), or fed once already (also has PendingRoll /
                // PendingExperience / HasBeenFed). Handle both: keep CurrentSeed if present,
                // clone a fresh HasBeenFed field from the template if this prism never had one.
                props.RemoveAll(kv => kv.Key is "PendingRoll" or "PendingExperience");

                var template = Prisms.First(p => p.IsBuilt);
                var seedKv = props.FirstOrDefault(kv => kv.Key == "CurrentSeed");
                if (seedKv.Value == null)
                    seedKv = new KeyValuePair<string, Property>("CurrentSeed",
                        (Property)DeepClone(template.InstanceDataObject.Properties.Properties.First(kv => kv.Key == "CurrentSeed").Value)!);

                var fedKv = props.FirstOrDefault(kv => kv.Key == "HasBeenFed");
                if (fedKv.Value == null)
                    fedKv = new KeyValuePair<string, Property>("HasBeenFed",
                        (Property)DeepClone(template.InstanceDataObject.Properties.Properties.First(kv => kv.Key == "HasBeenFed").Value)!);
                WriteBool(fedKv.Value, true);

                props.Clear();
                props.Add(new KeyValuePair<string, Property>("CurrentSegments", segProp));
                props.Add(seedKv);
                props.Add(new KeyValuePair<string, Property>("CurrentFeedData", feedProp));
                props.Add(fedKv);
                props.Add(new KeyValuePair<string, Property>("Level", levelProp));
                RebuildLookup(vm.InstanceDataObject.Properties);

                vm.IsBuilt = true;
            }

            vm.RawLevel = rawLevel;
            vm.HasBeenFed = true;
            for (int i = 0; i < 6; i++)
                vm.Slots[i] = new SlotViewModel { RowName = allRows[i], Level = levels[i] };
        }

        private static List<string> ResolveFeedRows(IEnumerable<string> enhancementRows)
        {
            // Cosmetic "materials fed" history: one entry per distinct underlying stat
            // across all 5 chosen slots (deduped -- feeding the same stat via two slots,
            // when allowOverlap is on, still only needs one history row).
            return enhancementRows.SelectMany(GetUnderlyingStats).Distinct().ToList();
        }

        public void Save(bool backup = true)
        {
            if (backup)
            {
                var bakPath = _path + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                System.IO.File.Copy(_path, bakPath, overwrite: false);
            }
            SaveFile.Write(_path, _save);
        }

        public void SaveAs(string newPath)
        {
            SaveFile.Write(newPath, _save);
        }

        /// <summary>
        /// Overwrites the real file with the pristine copy saved the first time this app ever
        /// opened it, discarding every change made since -- including ones already Applied and
        /// saved. Re-opens the file afterward so Prisms reflects the restored state.
        /// </summary>
        public void RevertToOriginal()
        {
            if (!HasOriginalBackup)
                throw new InvalidOperationException("No original backup exists for this file yet.");
            System.IO.File.Copy(_originalBackupPath, _path, overwrite: true);
            Open(_path);
        }

        private static readonly List<string> _prismItemIdsCache = new();
        private static List<string> PrismCatalog_PrismItemIds()
        {
            if (_prismItemIdsCache.Count > 0) return _prismItemIdsCache;
            // "PrismOf<Name>" items are the 7 prism stones themselves.
            _prismItemIdsCache.AddRange(new[] { "PrismOfHatred", "PrismOfLethargy", "PrismOfPassion", "PrismOfPride",
                "PrismOfJealousy", "PrismOfVoracity", "PrismOfGreed" });
            return _prismItemIdsCache;
        }

        private static string PrismDisplayName(string itemBpPath)
        {
            var file = itemBpPath.Split('/', '.').LastOrDefault(s => s.EndsWith("_C") || !s.Contains(':')) ?? itemBpPath;
            var baseName = itemBpPath.Split('/').Last().Split('.').First();
            return System.Text.RegularExpressions.Regex.Replace(baseName, "(?<=[a-z])(?=[A-Z])", " ");
        }

        // ---------- low level field helpers ----------

        private static bool ReadBool(Property? p)
        {
            if (p?.Value is byte b) return b != 0;
            if (p?.Value is bool bo) return bo;
            return false;
        }

        private static void WriteBool(Property p, bool value)
        {
            if (p.Value is byte) p.Value = (byte)(value ? 1 : 0);
            else p.Value = value;
        }

        private static int ReadByteOrInt(Property p)
        {
            if (p.Value is ByteProperty bp) return bp.EnumByte ?? 0;
            if (p.Value is byte b) return b;
            if (p.Value is int i) return i;
            return 0;
        }

        private static void WriteByteOrInt(Property p, int value)
        {
            if (p.Value is ByteProperty bp) bp.EnumByte = (byte)value;
            else if (p.Value is byte) p.Value = (byte)value;
            else p.Value = value;
        }

        private static string ReadFName(PropertyBag bag, string key)
        {
            var prop = bag.Properties.First(kv => kv.Key == key).Value;
            var fname = (FName)prop.Value!;
            return FNameToString(fname);
        }

        private static void WriteFName(PropertyBag bag, string key, string value)
        {
            var prop = bag.Properties.First(kv => kv.Key == key).Value;
            var fname = (FName)prop.Value!;
            foreach (var f in typeof(FName).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (f.FieldType == typeof(string)) { f.SetValue(fname, value); return; }
            }
            throw new Exception("FName has no string field");
        }

        private static string FNameToString(FName fname)
        {
            foreach (var f in typeof(FName).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (f.FieldType == typeof(string)) return (string)(f.GetValue(fname) ?? "");
            }
            return fname.ToString() ?? "";
        }

        private static int ReadIntField(PropertyBag bag, string key)
        {
            var prop = bag.Properties.First(kv => kv.Key == key).Value;
            return prop.Value is int i ? i : 0;
        }

        private static void WriteIntField(PropertyBag bag, string key, int value)
        {
            var prop = bag.Properties.First(kv => kv.Key == key).Value;
            prop.Value = value;
        }

        private static void RebuildLookup(PropertyBag bag)
        {
            var lookupField = typeof(PropertyBag).GetField("Lookup", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (lookupField?.GetValue(bag) is IDictionary dict)
            {
                dict.Clear();
                foreach (var kv in bag.Properties) dict[kv.Key] = kv.Value;
            }
        }

        private static object? DeepClone(object? src)
        {
            if (src == null) return null;
            var t = src.GetType();
            if (t.IsPrimitive || t == typeof(string) || t.IsEnum || src is decimal) return src;
            if (t.Namespace != null && t.Namespace.StartsWith("System") && !t.IsGenericType) return src;

            if (src is IList list && t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                var newList = (IList)Activator.CreateInstance(t)!;
                foreach (var item in list) newList.Add(DeepClone(item));
                return newList;
            }

            object clone;
            try { clone = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(t); }
            catch { clone = Activator.CreateInstance(t, nonPublic: true)!; }

            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var val = f.GetValue(src);
                if (val == null) continue;
                var ft = val.GetType();
                object? cloned;
                if (ft.IsPrimitive || ft == typeof(string) || ft.IsEnum)
                    cloned = val;
                else if (ft.Namespace != null && ft.Namespace.StartsWith("lib.remnant2.saves"))
                    cloned = DeepClone(val);
                else if (val is IList && ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>))
                    cloned = DeepClone(val);
                else
                    cloned = val;
                f.SetValue(clone, cloned);
            }
            return clone;
        }
    }
}
