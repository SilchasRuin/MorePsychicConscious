using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Display;
using Dawnsbury.Modding;

namespace MorePsychicConscious;

public class ModLoader
{
    [DawnsburyDaysModMainMethod]
    public static void LoadMod()
    {
        PsychicSpells.RegisterSpells();
        LoadOrder.WhenFeatsBecomeLoaded += () =>
        {
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
            });
            foreach (Feat feat in ConsciousMind.GetSubFeats())
            {
                ModManager.AddFeat(feat);
                AllFeats.All.Find(ft => ft.Name == Trait.Psychic.HumanizeTitleCase2() + " Dedication")?.Subfeats?.Add(feat);
            }
            foreach (Feat feat in ConsciousMind.ParallelFeats())
            {
                ModManager.AddFeat(feat);
                AllFeats.GetFeatByFeatName(FeatName.ParallelBreakthrough).Subfeats?.Add(feat);
            }
        };
    }
}