using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Modding;

namespace MorePsychicConscious;

public class ModLoader
{
    [DawnsburyDaysModMainMethod]
    public static void LoadMod()
    {
        LoadOrder.WhenFeatsBecomeLoaded += () =>
        {
            foreach (Feat feat in SilentWhisper.GetFeats())
            {
                ModManager.AddFeat(feat);
            }
        };
        PsychicSpells.RegisterSpells();
    }
}