using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using static MorePsychicConscious.ConsciousMindFeat;

namespace MorePsychicConscious;

public abstract class SilentWhisper :  ModData
{
    internal static IEnumerable<Feat> GetFeats()
    {
        Feat silentWhisper = CreateConsciousMindFeat2(FeatNames.SilentWhisper, "Every mind murmurs constantly, and the static from those nearby reverberates in your head like wind through leaves. What better focus for your magic then, than the very thoughts of those around you? Your versatile telepathic abilities let you soothe and communicate with your allies or control your enemies.",
            [SpellId.Command, SpellId.TouchOfIdiocy, SpellId.Heroism, SpellId.Confusion, SpellId.SynapticPulse], SpellIds.Message, SpellId.Daze, SpellIds.ForbiddenThought, SpellIds.ShatterMind);
        yield return silentWhisper;
    }
} 