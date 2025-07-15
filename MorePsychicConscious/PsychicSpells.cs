using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Modding;

namespace MorePsychicConscious;

public class PsychicSpells
{
    public static void RegisterSpells()
    {
        ModData.SpellIds.Message = ModManager.RegisterNewSpell("Message", 1, (_, _, _, _, _) =>
        {
            CombatAction message = Spells.CreateModern(IllustrationName.Divination, "Message", 
                    [Trait.Auditory, Trait.Cantrip, Trait.Concentrate, Trait.Illusion, Trait.Linguistic, Trait.Mental, Trait.Level1PsychicCantrip], 
                    "", "", Target.RangedFriend(24).WithAdditionalConditionOnTargetCreature((self, target) => target == self ? Usability.NotUsableOnThisCreature("You cannot message yourself.") : Usability.Usable),1, null)
                .WithActionCost(1);
            if (message.SpellInformation?.PsychicAmpInformation != null)
            {
                
            }
            return message;

        });
    }
}