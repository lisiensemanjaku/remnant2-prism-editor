using System.Collections.Generic;

namespace PrismEditor
{
    public record CommunityBuild(
        string Name,
        string Playstyle,
        string[] EnhancementRowNames, // exactly 5
        string BonusRowName,
        string Source,
        string Notes);

    /// <summary>
    /// Real prism configurations pulled from Remnant 2 community discussion (Steam forums)
    /// and the game's own wiki, re-expressed using this save's confirmed real row names.
    /// Every row name here is one already verified to exist in the game's item database
    /// (see PrismCatalog) -- nothing invented.
    /// </summary>
    public static class CommunityBuilds
    {
        public static readonly List<CommunityBuild> All = new()
        {
            new CommunityBuild(
                "Pure Ranged DPS",
                "Ranged",
                new[] { "FireRateReloadSpeed", "WeakspotDamageCriticalDamage", "RangedDamageIdealRange", "RangedCriticalAmmoReserves", "SwapSpeed" },
                "CriticalSituation",
                "Assembled from Remnant 2 Wiki fusion effects + Steam forum ranged-prism discussion (steamcommunity.com, discussion 4634861089657500550)",
                "Gunfighter + Meta + Sniper + Munitions covers fire rate, reload, weakspot/crit damage, ranged damage, range, ammo and ranged crit chance. Firearm Swap Speed fills the 5th slot without repeating a stat. Critical Situation needs Light armor to matter -- swap it if you don't run Light."),

            new CommunityBuild(
                "Status / Regen Ritualist",
                "Skill & Status",
                new[] { "HealthRegenSkillCooldown", "StatusDamage", "SkillDamage", "CastSpeed", "ReloadSpeed" },
                "InsultToInjury",
                "Steam Community discussion 4634861089657500550 (player-reported build)",
                "Revitalize (health regen + skill cooldown) backing a status-effect / mod-skill playstyle. Insult to Injury applies Exposed on any negative status, which synergizes directly with the Status Damage segment."),

            new CommunityBuild(
                "Pure Melee Tank",
                "Melee",
                new[] { "MeleeDamageMeleeSpeed", "ShieldEfficacyArmorFlat", "HealthPercentStaminaPercent", "ExplosiveDamageDamageReduction", "MovementSpeed" },
                "Immovable",
                "Steam Community discussion 4693405793412498132 (player-reported \"pure melee\" build)",
                "Warrior (melee damage+speed) + Protected (shield/armor) + Hulk (health+stamina) + Sapper (explosive dmg/dmg reduction) fills all 4 fusion slots; Movement Speed segment rounds it out. Immovable keeps you unstaggered while Bulwark is active."),

            new CommunityBuild(
                "Mobile Ranged DPS",
                "Ranged",
                new[] { "WeakspotDamageCriticalDamage", "FireRateReloadSpeed", "RangedDamageIdealRange", "RangedCriticalAmmoReserves", "MovementSpeed" },
                "SharpShooter",
                "Steam Community discussion 604159477116093828 (player-reported ranged prism)",
                "Same 4 ranged fusions as Pure Ranged DPS (Meta, Gunfighter, Sniper, Munitions), but trades the Critical Situation legendary (Light-armor only) for Sharpshooter (+60% Ranged Damage, no armor requirement) and uses a flat Movement Speed segment instead of Swap Speed -- this poster's variant prioritized staying mobile over Light-armor crit stacking."),

            new CommunityBuild(
                "Mod Mage",
                "Skill & Mod",
                new[] { "ModCriticalSkillCritical", "ModDamageModGeneration", "ModDurationSkillDuration", "CastSpeedUseSpeed", "SkillDamage" },
                "PowerTrip",
                "Assembled from Remnant 2 Wiki fusion table + Steam \"mod spam / mod mage\" discussion (steamcommunity.com, discussion 4634861089657500550)",
                "Wizard + Mage + Longevity + Flash fill all 4 fusion slots around mod/skill damage, generation, duration and cast speed; Skill Damage segment reinforces it. Power Trip doubles mod charges, the signature payoff for a mod-spam build. (This is the same fusion family already on your own Prism of Passion.)"),

            new CommunityBuild(
                "Jack-of-All-Trades Hybrid",
                "Ranged + Melee Hybrid",
                new[] { "RangedDamageMeleeDamage", "FireRateReloadSpeed", "MeleeCriticalEvadeSpeed", "WeakspotDamageCriticalDamage", "StaminaPercent" },
                "JackOfAllTrades",
                "Assembled from Remnant 2 Wiki fusion/legendary tables (no single overlap-free hybrid build was found on forums, so this combines Pirate -- the game's own ranged+melee hybrid fusion -- with non-overlapping ranged and melee fusions)",
                "Pirate covers both Ranged and Melee Damage without double-dipping; Gunfighter and Rogue add fire-rate/reload and melee-crit/evade on top without repeating a stat; Meta adds weakspot/crit damage. Jack of all Trades (+40% all damage) rewards exactly this kind of generalist build."),

            new CommunityBuild(
                "Combat Medic",
                "Support / Healer",
                new[] { "ReviveSpeedHealingEfficacy", "HealthPercentGreyHealthRate", "HealthRegenSkillCooldown", "ShieldEfficacyArmorFlat", "HealthFlat" },
                "FullHearted",
                "Assembled from Remnant 2 Wiki fusion/legendary tables -- for the Medic archetype's revive/heal/shield loop",
                "Selfless + Threshold + Revitalize + Protected stack revive speed, healing, health recovery and armor/shield without repeating a stat; flat Health rounds out the 5th slot. Full Hearted (+100% Relic Charges) means more heals to go around for the team."),

            new CommunityBuild(
                "Untouchable Evasion",
                "Dodge Tank",
                new[] { "MovementSpeedEvadeSpeed", "DamageReductionArmorPercent", "HealthPercentStaminaPercent", "EvadeDistance", "StaminaFlat" },
                "FleetFooted",
                "Assembled from Remnant 2 Wiki fusion/legendary tables",
                "Athletic + Tank + Hulk (only 3 of the 4 fusion slots used) cover movement/evade speed, damage reduction/armor, and health/stamina; Evade Distance and flat Stamina segments back up a dodge-heavy playstyle. Fleet Footed (+100 Dodge Weight Threshold) lets you dodge in heavier gear without losing the i-frame."),

            new CommunityBuild(
                "Omni-Crit Glass Cannon",
                "Crit-Everything",
                new[] { "WeakspotDamageCriticalDamage", "ModCriticalSkillCritical", "MeleeCriticalEvadeSpeed", "RangedCriticalAmmoReserves", "ExplosiveDamage" },
                "PowerFantasy",
                "Assembled from Remnant 2 Wiki fusion/legendary tables",
                "Meta + Wizard + Rogue + Munitions fill all 4 fusion slots, each adding a crit-chance stat for a different damage type (weakspot, mod, skill, melee, ranged) without any repeats; Explosive Damage segment plus Power Fantasy (+30% Explosive Crit Chance & Damage) rounds the crit theme out across the board."),

            new CommunityBuild(
                "Endless Skirmisher",
                "Stamina / Mobility",
                new[] { "MeleeSpeedStaminaFlat", "HealthPercentStaminaPercent", "MovementSpeedEvadeSpeed", "EvadeDistance", "ReviveSpeed" },
                "PeakConditioning",
                "Assembled from Remnant 2 Wiki fusion/legendary tables",
                "Pugilist + Hulk + Athletic (3 of 4 fusion slots) cover melee speed, stamina, health, movement and evade speed without repeats; Evade Distance and Revive Speed segments round it out. Peak Conditioning grants infinite stamina outright, making this build about never stopping."),
        };
    }
}
