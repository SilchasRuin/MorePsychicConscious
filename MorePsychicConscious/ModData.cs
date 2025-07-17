using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Mechanics;
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
        public static readonly FeatName SilentWhisper = ModManager.RegisterFeatName("SilentWhisper", "The Silent Whisper");
    }

    public static class ActionIds
    {
        public static readonly ActionId ForbiddenThought = ModManager.RegisterEnumMember<ActionId>("ForbiddenThought");
    }

    public static class QEffectIds
    {
        public static QEffectId ForbiddenThought { get; } = ModManager.RegisterEnumMember<QEffectId>("ForbiddenThought");
    }
}