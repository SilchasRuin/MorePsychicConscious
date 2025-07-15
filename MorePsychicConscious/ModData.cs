using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Modding;

namespace MorePsychicConscious;

public class ModData
{
    public static class SpellIds
    {
        public static SpellId Message { get; set; } 
        public static SpellId ForbiddenThought { get; set; }
        public static SpellId ShatterMind { get; set; }
    }

    public static class FeatNames
    {
        public static FeatName SilentWhisper = ModManager.RegisterFeatName("SilentWhisper", "The Silent Whisper");
    }
}