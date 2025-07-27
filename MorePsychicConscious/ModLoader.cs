using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Display;
using Dawnsbury.Display.Text;
using Dawnsbury.Modding;
using HarmonyLib;

namespace MorePsychicConscious;

public class ModLoader
{
    [DawnsburyDaysModMainMethod]
    public static void LoadMod()
    {
        var harmony = new Harmony("tumblefix");
        harmony.PatchAll();
        ModData.MActionIds.PrepareToAid = ModManager.TryParse("PrepareToAid", out ActionId prepareAid) ? prepareAid : ModManager.RegisterEnumMember<ActionId>("PrepareToAid");
        ModData.MActionIds.AidReaction = ModManager.TryParse("AidReaction", out ActionId aidReaction) ? aidReaction : ModManager.RegisterEnumMember<ActionId>("AidReaction");
        ModManager.RegisterInlineTooltip("aid", "Aid {icon:Reaction}\n\n" +
                                                   "You try to help your ally with a task. To use this reaction, you must first prepare to help, usually by using an action during your turn." +
                                                   $"\n\nWhen you use your Aid reaction, attempt a skill check or attack roll of a type decided by the ability. The DC is {ConsciousMind.AidDC()}." +
                                                   S.FourDegreesOfSuccess("You grant your ally a +2 circumstance bonus to the triggering check. If you're a master with the check you attempted, the bonus is +3, and if you're legendary, it's +4.",
                                                       "You grant your ally a +1 circumstance bonus to the triggering check.",
                                                       null,
                                                       "Your ally takes a –1 circumstance penalty to the triggering check."));
        PsychicSpells.RegisterSpells();
        foreach (Feat feat in ConsciousMind.GetFeats())
        {
            ModManager.AddFeat(feat);
        }
        ModManager.RegisterActionOnEachCreature(cr =>
        {
            if (cr.Level >= 6 && cr.HasFeat(ModData.MFeatNames.SilentWhisper))
            {
                cr.AddQEffect(ConsciousMind.ShatterMindToggle());
            }
            if (Possibilities.Create(cr).Filter(ap => ap.CombatAction is { PsychicAmpInformation.Amped: true, SpellId: SpellId.Guidance }).CreateActions(false).Count != 0)
            {
                cr.AddQEffect(ConsciousMind.SetupReactionForGuidance(cr));
            }
        });
        foreach (Feat feat in ConsciousMind.GetSubFeats())
        {
            ModManager.AddFeat(feat);
            AllFeats.All.Find(ft => ft.Name == Trait.Psychic.HumanizeTitleCase2() + " Dedication")?.Subfeats
                ?.Add(feat);
        }
        foreach (Feat feat in ConsciousMind.ParallelFeats())
        {
            ModManager.AddFeat(feat);
            AllFeats.GetFeatByFeatName(FeatName.ParallelBreakthrough).Subfeats?.Add(feat);
        }
        if (!ModManager.TryParse("PrepareToAid", out ActionId _))
        {
            ModManager.RegisterBooleanSettingsOption("MoreBasicActions.AidDCIs15", "Psychic Enhanced: Reduce Aid DC",
                "The DC to Aid is normally 20. If enabled, the DC is reduced to 15 instead.",
                false);
        }
    }
}