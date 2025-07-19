using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Modding;

namespace MorePsychicConscious;

public class ModData
{
    public static class MSpellIds
    {
        public static SpellId Message { get; set; } 
        public static SpellId ForbiddenThought { get; set; }
        public static SpellId ShatterMind { get; set; }
        public static SpellId AltDaze { get; set; }
        public static SpellId Ignition { get; set; }
        public static SpellId Frostbite { get; set; }
        public static SpellId ThermalStasis { get; set; }
        public static SpellId EntropicWheel { get; set; }
    }
    public static class MFeatNames
    {
        public static readonly FeatName SilentWhisper = ModManager.RegisterFeatName("SilentWhisper", "The Silent Whisper");
        public static readonly FeatName OscillatingWave = ModManager.RegisterFeatName("OscillatingWave", "The Oscillating Wave");
    }

    public static class MActionIds
    {
        public static readonly ActionId ForbiddenThought = ModManager.RegisterEnumMember<ActionId>("ForbiddenThought");
    }

    public static class MQEffectIds
    {
        public static QEffectId ForbiddenThought { get; } = ModManager.RegisterEnumMember<QEffectId>("ForbiddenThought");
        public static QEffectId ShatterMind60 { get; } = ModManager.RegisterEnumMember<QEffectId>("ShatterMind60");
        public static QEffectId EntropicWheel { get; } = ModManager.RegisterEnumMember<QEffectId>("EntropicWheel");
        public static QEffectId EntropicBlock { get; } = ModManager.RegisterEnumMember<QEffectId>("EntropicBlock");
        public static QEffectId OscillatingWaveFire { get; } = ModManager.RegisterEnumMember<QEffectId>("OscillatingWaveFire");
        public static QEffectId OscillatingWaveCold { get; } = ModManager.RegisterEnumMember<QEffectId>("OscillatingWaveCold");
        public static QEffectId OscillatingWaveToggle { get; } = ModManager.RegisterEnumMember<QEffectId>("OscillatingWaveToggle");
    }
    public static class MIllustrations
    {
        public static readonly Illustration FireColdWheel = new ModdedIllustration("CMAssets/FireAndIce.png");
        public static readonly Illustration FireWheel = new ModdedIllustration("CMAssets/FireRing.png");
        public static readonly Illustration ColdWheel = new ModdedIllustration("CMAssets/ColdRing.png");
        public static readonly Illustration ColdFireWheel = new ModdedIllustration("CMAssets/IceAndFire.png");
    }
}