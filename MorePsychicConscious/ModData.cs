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
        // public static SpellId DistortionLens { get; set; }
        public static SpellId GhostlyShift { get; set; }
        public static SpellId TesseractTunnel { get; set; }
        public static SpellId ThoughtfulGift { get; set; }
    }
    public static class MFeatNames
    {
        public static readonly FeatName SilentWhisper = ModManager.RegisterFeatName("SilentWhisper", "The Silent Whisper");
        public static readonly FeatName OscillatingWave = ModManager.RegisterFeatName("OscillatingWave", "The Oscillating Wave");
        public static readonly FeatName UnboundStep = ModManager.RegisterFeatName("UnboundStep", "The Unbound Step");
    }

    public static class MActionIds
    {
        public static readonly ActionId ForbiddenThought = ModManager.RegisterEnumMember<ActionId>("ForbiddenThought");
        public static readonly ActionId GhostThrough = ModManager.RegisterEnumMember<ActionId>("GhostThrough");
        public static ActionId PrepareToAid;
        public static ActionId AidReaction;
    }

    public static class MQEffectIds
    {
        public static QEffectId ForbiddenThought { get; } = ModManager.RegisterEnumMember<QEffectId>("PE_ForbiddenThought");
        public static QEffectId ShatterMind60 { get; } = ModManager.RegisterEnumMember<QEffectId>("PE_ShatterMind60");
        public static QEffectId EntropicWheel { get; } = ModManager.RegisterEnumMember<QEffectId>("PE_EntropicWheel");
        public static QEffectId EntropicBlock { get; } = ModManager.RegisterEnumMember<QEffectId>("PE_EntropicBlock");
        public static QEffectId OscillatingWaveFire { get; } = ModManager.RegisterEnumMember<QEffectId>("PE_OscillatingWaveFire");
        public static QEffectId OscillatingWaveCold { get; } = ModManager.RegisterEnumMember<QEffectId>("PE_OscillatingWaveCold");
        public static QEffectId OscillatingWaveToggle { get; } = ModManager.RegisterEnumMember<QEffectId>("PE_OscillatingWaveToggle");
        public static QEffectId Phase { get; } = ModManager.RegisterEnumMember<QEffectId>("PE_Phase");
        public static QEffectId GhostThrough { get; } = ModManager.RegisterEnumMember<QEffectId>("PE_GhostThrough");
        public static QEffectId Ghosted { get; } = ModManager.RegisterEnumMember<QEffectId>("PE_Ghosted");
        public static QEffectId Ghost { get; } = ModManager.RegisterEnumMember<QEffectId>("PE_Ghost");
        public static QEffectId Tunnel { get; } = ModManager.RegisterEnumMember<QEffectId>("PE_Tunnel");
        public static QEffectId Complete { get; } = ModManager.RegisterEnumMember<QEffectId>("PE_Complete");
        public static QEffectId LibraryAid { get; } = ModManager.RegisterEnumMember<QEffectId>("PE_LibraryAid");
        public static QEffectId OmniBonus { get; } = ModManager.RegisterEnumMember<QEffectId>("PE_OmniBonus");
        
        
    }
    public static class MIllustrations
    {
        public static readonly Illustration FireColdWheel = new ModdedIllustration("CMAssets/FireAndIce.png");
        public static readonly Illustration FireWheel = new ModdedIllustration("CMAssets/FireRing.png");
        public static readonly Illustration ColdWheel = new ModdedIllustration("CMAssets/ColdRing.png");
        public static readonly Illustration ColdFireWheel = new ModdedIllustration("CMAssets/IceAndFire.png");
        public static readonly Illustration Message = new ModdedIllustration("CMAssets/Message.png");
    }
}