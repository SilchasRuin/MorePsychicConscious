using System.Security.Cryptography.X509Certificates;
using Dawnsbury;
using Dawnsbury.Audio;
using Dawnsbury.Auxiliary;
using Dawnsbury.Campaign.Encounters.Elements_of_a_Crime;
using Dawnsbury.Core;
using Dawnsbury.Core.Animations;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Coroutines.Options;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Creatures.Parts;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Damage;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Targeting.TargetingRequirements;
using Dawnsbury.Core.Mechanics.Targeting.Targets;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Roller;
using Dawnsbury.Core.Tiles;
using Dawnsbury.Display;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Display.Text;
using Dawnsbury.Modding;
using Microsoft.Xna.Framework;

namespace MorePsychicConscious;

public abstract class PsychicSpells : ModData
{
    public static void RegisterSpells()
    {
        MSpellIds.Message = ModManager.RegisterNewSpell("Message", 0,
            (_, caster, spellLevel, inCombat, spellInformation) =>
            {
                PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
                bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
                CombatAction message = Spells.CreateModern(MIllustrations.Message, "Message",
                        [
                            Trait.Auditory, Trait.Cantrip, Trait.Concentrate, Trait.Illusion, Trait.Linguistic,
                            Trait.Mental, Trait.Level1PsychicCantrip, Trait.Arcane, Trait.Occult, Trait.Divine,
                            Trait.VerbalOnly
                        ],
                        "You send a message of hope to one of your allies.",
                        "One ally in range gains a +1 circumstance bonus to their next will saving throw before the start of your next turn." +
                        (psychicAmpInformation != null
                            ? "\n{Blue}You don't need a straight line of effect or line of sight to cast message as long as you know the target's space and there is an unblocked path of 120 feet or less that can reach them.{/Blue}" +
                              (inCombat ? "" : " {Blue}It also gains the following amp.\n\n{b}Amp{b} The target of the message can immediately spend its reaction to Step or Stride.{/Blue}" +
                               "\n{Blue}{b}Amp Heightened (4th){/b} The target of the message can choose to Shove, Strike, or Trip with its reaction instead.{/Blue}")
                            : ""),
                        Target.RangedFriend(spellLevel >= 3 ? 100 : 24)
                            .WithAdditionalConditionOnTargetCreature((self, target) =>
                                target == self
                                    ? Usability.NotUsableOnThisCreature("You cannot message yourself.")
                                    : Usability.Usable), spellLevel, null)
                    .WithActionCost(1)
                    .WithHeightenedAtSpecificLevel(spellLevel, 3, inCombat, "The spell's range increases to 500 feet.")
                    .WithEffectOnChosenTargets(async (_, targets) =>
                    {
                        Creature ally = targets.ChosenCreature!;
                        ally.AddQEffect(new QEffect(ExpirationCondition.ExpiresAtStartOfYourTurn)
                        {
                            BeforeYourSavingThrow = (effect, action, self) =>
                            {
                                if (action.SavingThrow is { Defense: Defense.Will })
                                {
                                    self.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                                    {
                                        BonusToDefenses = (_, _, defense) =>
                                            defense == Defense.Will
                                                ? new Bonus(1, BonusType.Circumstance, "Message")
                                                : null,
                                    });
                                    effect.ExpiresAt = ExpirationCondition.Immediately;
                                }

                                return Task.CompletedTask;
                            },
                            Illustration = MIllustrations.Message,
                            Name = "Message",
                            Description = "You gain a +1 circumstance bonus to your next will save.",
                        });
                        if (amped)
                        {
                            List<string> optionNames = ["Cancel"];
                            if (!ally.HasEffect(QEffectId.Restrained) && !ally.HasEffect(QEffectId.Immobilized) &&
                                !ally.HasEffect(QEffectId.Paralyzed) && !ally.HasEffect(QEffectId.Grabbed) &&
                                ally.Speed > 0)
                                optionNames.Add("Step or Stride");
                            //create action
                            CombatAction? shove = Possibilities.Create(ally).Filter(ap =>
                                {
                                    if (ap.CombatAction.ActionId != ActionId.Shove)
                                        return false;
                                    ap.CombatAction.ActionCost = 0;
                                    ap.RecalculateUsability();
                                    return true;
                                }).CreateActions(true)
                                .FirstOrDefault(pw => pw.Action.ActionId == ActionId.Shove) as CombatAction;
                            shove?.Traits.Add(Trait.ReactiveAttack);
                            if (shove != null && spellLevel >= 4 && shove.CanBeginToUse(ally))
                                optionNames.Add("Shove");
                            CombatAction? trip = Possibilities.Create(ally).Filter(ap =>
                                {
                                    if (ap.CombatAction.ActionId != ActionId.Trip)
                                        return false;
                                    ap.CombatAction.ActionCost = 0;
                                    ap.RecalculateUsability();
                                    return true;
                                }).CreateActions(true)
                                .FirstOrDefault(pw => pw.Action.ActionId == ActionId.Trip) as CombatAction;
                            trip?.Traits.Add(Trait.ReactiveAttack);
                            if (trip != null && spellLevel >= 4 && trip.CanBeginToUse(ally))
                                optionNames.Add("Trip");
                            CombatAction strike = ally.CreateStrike(ally.PrimaryWeaponIncludingRanged!)
                                .WithActionCost(0);
                            strike.Traits.Add(Trait.ReactiveAttack);
                            if (strike.CanBeginToUse(ally).CanBeUsed)
                                optionNames.Add("Strike");
                            ChoiceButtonOption chosenOption = await ally.AskForChoiceAmongButtons(
                                IllustrationName.Reaction,
                                spellLevel >= 4
                                    ? "Choose to Step/Stride, Shove, Strike, Trip, or Cancel. Canceling will refund the focus point."
                                    : "Choose to Step/Stride or Cancel. Canceling will refund the focus point.",
                                optionNames.ToArray());
                            switch (optionNames[chosenOption.Index])
                            {
                                case "Cancel":
                                    ++caster!.Spellcasting!.FocusPoints;
                                    break;
                                case "Shove":
                                    if (!await ally.Battle.GameLoop.FullCast(shove!))
                                        ++caster!.Spellcasting!.FocusPoints;
                                    else ally.Actions.UseUpReaction();
                                    break;
                                case "Strike":
                                    if (!await ally.Battle.GameLoop.FullCast(strike))
                                        ++caster!.Spellcasting!.FocusPoints;
                                    else ally.Actions.UseUpReaction();
                                    break;
                                case "Trip":
                                    if (!await ally.Battle.GameLoop.FullCast(trip!))
                                        ++caster!.Spellcasting!.FocusPoints;
                                    else ally.Actions.UseUpReaction();
                                    break;
                                case "Step or Stride":
                                    if (!await ally.StrideAsync("Choose where to stride or step.", true,
                                            allowCancel: true))
                                        ++caster!.Spellcasting!.FocusPoints;
                                    else ally.Actions.UseUpReaction();
                                    break;
                            }
                        }
                    });
                message.PsychicAmpInformation = spellInformation.PsychicAmpInformation;
                if (spellInformation.PsychicAmpInformation != null)
                {
                    message.Traits.Add(Trait.Psi);
                    message.Target = new CreatureTarget(RangeKind.Ranged, [
                            new FriendCreatureTargetingRequirement(),
                            new MaximumRangeCreatureTargetingRequirement(spellLevel >= 3 ? 100 : 24)
                        ],
                        (_, _, _) => int.MinValue);
                    if (amped)
                    {
                        message.Target = new CreatureTarget(RangeKind.Ranged, [
                                new FriendCreatureTargetingRequirement(),
                                new MaximumRangeCreatureTargetingRequirement(spellLevel >= 3 ? 100 : 24)
                            ],
                            (_, _, _) => int.MinValue).WithAdditionalConditionOnTargetCreature((_, ally) =>
                            !ally.Actions.IsReactionUsedUp
                                ? Usability.Usable
                                : Usability.NotUsableOnThisCreature("Ally cannot use a reaction."));
                        message.Description += spellLevel >= 4 ? "\n\n{Blue}The target of the message can immediately spend its reaction to Step, Stride, Strike, Shove, or Trip.{/Blue}" : "\n\n{Blue}The target of the message can immediately spend its reaction to Step or Stride.{/Blue}";
                    }
                }

                return message;
            });
        MSpellIds.ForbiddenThought = ModManager.RegisterNewSpell("Forbidden Thought", 0,
            (_, _, spellLevel, inCombat, spellInformation) =>
            {
                PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
                bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
                CombatAction forbiddenThought = Spells.CreateModern(IllustrationName.Confusion, "Forbidden Thought",
                        [Trait.Cantrip, Trait.Enchantment, Trait.Mental, Trait.Level1PsychicCantrip, Trait.Psychic],
                        "You place a psychic lock in a foe's mind that prevents it from a specific action.",
                        $"Choose “Strike,” “Stride,” “Cast a Spell,” or a specific action you know the creature to have (such as “Breath Weapon” against a dragon). If the creature attempts that action on its next turn, it must surmount your lock to do so, causing it to take {S.HeightenedVariable(1 + spellLevel, 0) + "d6"} mental damage (with a basic Will save).{(amped ? " {Blue}If the target fails its save, it's also stunned 1.{/Blue}" : "")} The target is then temporarily immune for 1 minute." +
                        (inCombat ? "" : "\n\n{Blue}{b}Amp{/b} If the target fails its save, it's also stunned 1.{/Blue} "),
                        Target.Ranged(6), spellLevel, null)
                    .WithActionCost(2).WithHeighteningOfDamageEveryLevel(spellLevel, 1, inCombat, "1d6")
                    .WithActionId(MActionIds.ForbiddenThought)
                    .WithEffectOnChosenTargets(async (spell, self, targets) =>
                    {
                        List<string> options = ["Cancel", "Strike", "Stride", "Cast a Spell", "Specific"];
                        List<string> specific = [];
                        Creature enemy = targets.ChosenCreature!;
                        foreach (ICombatAction combatAction in targets.ChosenCreature!.Possibilities.CreateActions(true)
                                     .Where(action =>
                                         action is CombatAction && !action.Action.Traits.Contains(Trait.Basic)))
                        {
                            CombatAction action = (CombatAction)combatAction;
                            specific.Add(action.Name);
                        }

                        specific.Add("Cancel");
                        ChoiceButtonOption chosenOption = await self.AskForChoiceAmongButtons(
                            IllustrationName.QuestionMark, "Choose an option for forbidden thought", options.ToArray());
                        switch (options[chosenOption.Index])
                        {
                            case "Cancel":
                                if (amped) ++self.Spellcasting!.FocusPoints;
                                spell.RevertRequested = true;
                                break;
                            case "Specific":
                                ChoiceButtonOption specificOption =
                                    await self.AskForChoiceAmongButtons(IllustrationName.QuestionMark,
                                        "Choose a Specific Action", specific.ToArray());
                                if (specific[specificOption.Index] != "Cancel")
                                    enemy.AddQEffect(ForbiddenThought(spellLevel, specific[specificOption.Index], self,
                                        spell, amped));
                                else
                                {
                                    if (amped) ++self.Spellcasting!.FocusPoints;
                                    spell.RevertRequested = true;
                                }

                                break;
                            case "Strike":
                                enemy.AddQEffect(ForbiddenThought(spellLevel, Trait.Strike, self, spell, amped));
                                break;
                            case "Stride":
                                enemy.AddQEffect(ForbiddenThought(spellLevel, ActionId.Stride, self, spell, amped));
                                break;
                            case "Cast a Spell":
                                enemy.AddQEffect(ForbiddenThought(spellLevel, self, spell, amped));
                                break;
                        }
                    });
                forbiddenThought.PsychicAmpInformation = spellInformation.PsychicAmpInformation;
                if (spellInformation.PsychicAmpInformation != null)
                {
                    forbiddenThought.Traits.Add(Trait.Psi);
                }
                return forbiddenThought;
            });
        MSpellIds.ShatterMind = ModManager.RegisterNewSpell("Shatter Mind", 0,
            (_, owner, spellLevel, inCombat, spellInformation) =>
            {
                PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
                bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
                CombatAction shatterMind = Spells.CreateModern(IllustrationName.BrainDrain, "Shatter Mind",
                        [Trait.Cantrip, Trait.Evocation, Trait.Mental, Trait.Psychic, Trait.Level1PsychicCantrip],
                        "You telepathically assail the minds of your foes.",
                        $"Deal {S.HeightenedVariable(spellLevel, 0) + (amped ? "d10" : "d4")} mental damage to all enemies in the area, with a basic Will save.{(amped ? " {Blue}Creatures that fail are stupefied 1 until the start of your next turn (or stupefied 2 on a critical failure).{/Blue}": "")}" +
                        (inCombat? "" : "\n\n{Blue}{b}Amp{/b} You increase the area of the spell to your choice of a 30-foot cone or 60-foot cone (use the toggle in other maneuvers), and the damage dice for the spell change to d10s. Creatures that fail are stupefied 1 until the start of your next turn (or stupefied 2 on a critical failure).{/Blue}"),
                        Target.Cone(amped ? owner != null && owner.HasEffect(MQEffectIds.ShatterMind60) ? 12 : 6 : 3),
                        spellLevel, SpellSavingThrow.Basic(Defense.Will))
                    .WithActionCost(2)
                    .WithHeighteningOfDamageEveryLevel(spellLevel, 3, inCombat, amped ? "1d10" : "1d4");
                shatterMind.WithEffectOnEachTarget(async (spell, caster, target, result) =>
                {
                    await CommonSpellEffects.DealBasicDamage(spell, caster, target, result,
                        amped ? spellLevel + "d10" : spellLevel + "d4", DamageKind.Mental);
                    if (result <= CheckResult.Failure && amped)
                        target.AddQEffect(QEffect.Stupefied(result == CheckResult.CriticalFailure ? 2 : 1)
                            .WithExpirationAtStartOfSourcesTurn(caster, 1));
                });
                shatterMind.PsychicAmpInformation = spellInformation.PsychicAmpInformation;
                if (spellInformation.PsychicAmpInformation != null)
                {
                    shatterMind.Traits.Add(Trait.Psi);
                }
                return shatterMind;
            });
        if (!ModManager.TryParse("Remaster", out Trait _))
        {
            ModManager.ReplaceExistingSpell(SpellId.Daze, 0, (owner, level, inCombat, spellInformation) =>
            {
                PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
                bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
                CombatAction daze = Spells.CreateModern(IllustrationName.Daze, "Daze",
                        [
                            Trait.Cantrip, Trait.Enchantment, Trait.Mental, Trait.Nonlethal, Trait.Arcane, Trait.Divine,
                            Trait.Occult, Trait.Level1PsychicCantrip
                        ],
                        "You cloud the target's mind and daze it with a mental jolt.",
                        $"Deal {(amped ? S.HeightenedVariable(1 + 2*((level - 1)/2), 0) + "d10" : (level >= 3 ? S.HeightenedVariable((level - 1) / 2, 0) + "d6+" : "") + S.SpellcastingModifier(owner, spellInformation))} mental damage.\n\nIf the target critically fails the save, it is also stunned 1."
                        + (psychicAmpInformation != null && !inCombat
                            ? $"\n\n{{Blue}}{{b}}Amp{{/b}} The spell's damage changes to 1d10. If the target fails its Will save, until the end of its next turn, it gains weakness {1+(level - 1)/2} to mental damage and takes a –1 status penalty to Will saves. On a critical failure, the weakness is {3+(level - 1)/2} (in addition to the target being stunned 1). The weakness applies before daze deals damage.{{/Blue}}"
                            : ""),
                        Target.Ranged(12), level, SpellSavingThrow.Basic(Defense.Will))
                    .WithSoundEffect(SfxName.Mental)
                    .WithHeighteningNumerical(level, 1, inCombat, 2,
                        amped ? "The damage increases by 2d10" : "The damage increases by 1d6.")
                    .WithGoodness((target, _, _) =>
                        target.OwnerAction.SpellcastingSource!.SpellcastingAbilityModifier + 1)
                    .WithEffectOnEachTarget(async (power, self, target, result) =>
                    {
                        int num = power.SpellcastingSource!.SpellcastingAbilityModifier;
                        DiceFormula diceFormula1 =
                            DiceFormula.FromText(num.ToString(), "Spellcasting ability modifier");
                        if (amped) diceFormula1 = DiceFormula.FromText("1d10", "Amp");
                        if (power.SpellLevel >= 3)
                        {
                            ComplexDiceFormula complexDiceFormula = new();
                            List<DiceFormula> list = complexDiceFormula.List;
                            num = (level - 1) / 2;
                            DiceFormula diceFormula2 = DiceFormula.FromText(num + "d6", "Daze");
                            int num2 = 2*((level - 1)/2);
                            if (amped) diceFormula2 = DiceFormula.FromText(num2 + "d10", "Amped Daze");
                            list.Add(diceFormula2);
                            complexDiceFormula.List.Add(diceFormula1);
                            diceFormula1 = complexDiceFormula;
                        }

                        if (result <= CheckResult.Failure && amped)
                        {
                            target.AddQEffect(QEffect
                                .DamageWeakness(DamageKind.Mental,
                                    (result == CheckResult.CriticalFailure ? 2 : 0) + (level + 1) / 2)
                                .WithExpirationAtEndOfOwnerTurn());
                        }

                        await CommonSpellEffects.DealBasicDamage(power, self, target, result, diceFormula1,
                            DamageKind.Mental);
                        if (result != CheckResult.CriticalFailure)
                            return;
                        target.AddQEffect(QEffect.Stunned(1));
                    });
                if (amped) daze.Description += $" {{Blue}}If the target fails its Will save, until the end of its next turn, it gains weakness {1+(level - 1)/2} to mental damage and takes a –1 status penalty to Will saves. On a critical failure, the weakness is {3+(level - 1)/2}  (in addition to the target being stunned 1). The weakness applies before daze deals damage.{{/Blue}}";
                daze.PsychicAmpInformation = spellInformation.PsychicAmpInformation;
                if (spellInformation.PsychicAmpInformation == null) return daze;
                daze.Traits.Add(Trait.Psi);
                daze.Target = Target.Ranged(24).WithOverriddenFullTargetLine("{b}Range{/b} {Blue}120 feet{/Blue}");
                daze.Description += inCombat
                    ? ""
                    : "\n{Blue}{b}Amp Heightened (+2){/b} The spell's damage increases by 2d10, and the weakness on a failure or critical failure increases by 1.{/Blue}"; 
                return daze;
            });
        }
        else
        {
            MSpellIds.AltDaze = ModManager.RegisterNewSpell("PsiDaze", 0, (_, _, level, inCombat, spellInformation) =>
            {
                PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
                bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
                CombatAction daze = Spells.CreateModern(IllustrationName.Daze, "Daze{b}{/b}",
                        [Trait.Cantrip, Trait.Enchantment, Trait.Mental, Trait.Nonlethal, Trait.Level1PsychicCantrip],
                        "You cloud the target's mind and daze it with a mental jolt.",
                        $"Deal {(amped ? S.HeightenedVariable(1 + 2*((level - 1)/2), 0) + "d10" : S.HeightenedVariable((level + 1) / 2, 0) + "d6")} mental damage.\n\nIf the target critically fails the save, it is also stunned 1."
                        + (psychicAmpInformation != null && !inCombat
                            ? $"\n\n{{Blue}}{{b}}Amp{{/b}} The spell's damage changes to 1d10. If the target fails its Will save, until the end of its next turn, it gains weakness {1+(level - 1)/2} to mental damage and takes a –1 status penalty to Will saves. On a critical failure, the weakness is {3+(level - 1)/2} (in addition to the target being stunned 1). The weakness applies before daze deals damage.{{/Blue}}"
                            : ""),
                        Target.Ranged(12), level, SpellSavingThrow.Basic(Defense.Will))
                    .WithSoundEffect(SfxName.Mental)
                    .WithHeighteningNumerical(level, 1, inCombat, 2,
                        amped ? "The damage increases by 2d10" : "The damage increases by 1d6.")
                    .WithGoodness((target, _, _) =>
                        target.OwnerAction.SpellcastingSource!.SpellcastingAbilityModifier + 1)
                    .WithEffectOnEachTarget(async (power, self, target, result) =>
                    {
                        DiceFormula diceFormula1 =
                            DiceFormula.FromText("1d6", "Daze");
                        if (amped) diceFormula1 = DiceFormula.FromText("1d10", "Amp");
                        if (power.SpellLevel >= 3)
                        {
                            ComplexDiceFormula complexDiceFormula = new();
                            List<DiceFormula> list = complexDiceFormula.List;
                            var num = (level - 1) / 2;
                            DiceFormula diceFormula2 = DiceFormula.FromText(num + "d6", "Daze");
                            var num2 = 2*((level - 1)/2);
                            if (amped) diceFormula2 = DiceFormula.FromText(num2 + "d10", "Amped Daze");
                            list.Add(diceFormula2);
                            complexDiceFormula.List.Add(diceFormula1);
                            diceFormula1 = complexDiceFormula;
                        }

                        if (result <= CheckResult.Failure && amped)
                        {
                            target.AddQEffect(QEffect
                                .DamageWeakness(DamageKind.Mental,
                                    (result == CheckResult.CriticalFailure ? 2 : 0) + (level + 1) / 2)
                                .WithExpirationAtEndOfOwnerTurn());
                        }

                        await CommonSpellEffects.DealBasicDamage(power, self, target, result, diceFormula1,
                            DamageKind.Mental);
                        if (result != CheckResult.CriticalFailure)
                            return;
                        target.AddQEffect(QEffect.Stunned(1));
                    });
                
                daze.PsychicAmpInformation = spellInformation.PsychicAmpInformation;
                if (spellInformation.PsychicAmpInformation == null) return daze;
                daze.Traits.Add(Trait.Psi);
                if (amped) daze.Description += $" {{Blue}}If the target fails its Will save, until the end of its next turn, it gains weakness {1+(level - 1)/2} to mental damage and takes a –1 status penalty to Will saves. On a critical failure, the weakness is {3+(level - 1)/2}  (in addition to the target being stunned 1). The weakness applies before daze deals damage.{{/Blue}}";
                daze.Target = Target.Ranged(24).WithOverriddenFullTargetLine("{b}Range{/b} {Blue}120 feet{/Blue}");
                daze.Description += inCombat
                    ? ""
                    : "\n{Blue}{b}Amp Heightened (+2){/b} The spell's damage increases by 2d10, and the weakness on a failure or critical failure increases by 1.{/Blue}"; 
                return daze;
            });
        }

        ModManager.ReplaceExistingSpell(SpellId.ProduceFlame, 0, (owner, level, inCombat, spellInformation) =>
        {
            PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
            bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
            CombatAction produceFlame = Spells.CreateModern(IllustrationName.ProduceFlame, "Produce Flame",
                    [
                        Trait.Attack, Trait.Cantrip, Trait.Evocation, Trait.Fire, Trait.Arcane, Trait.Primal,
                        Trait.VersatileMelee, Trait.Level1PsychicCantrip
                    ],
                    "A small ball of flame appears in the palm of your hand, and you lash out with it.",
                    $"Make a spell attack roll. The flame deals {(amped ? "{Blue}" : "")}{S.HeightenedVariable(level, 1)}{(amped ? "{/Blue}" : "")}{(amped ? "{Blue}d10{/Blue}" : "d4+")}{(!amped ? S.SpellcastingModifier(owner, spellInformation) : "")} fire damage.{S.FourDegreesOfSuccessReverse(null, null, "Full damage.", $"Double damage, and {S.HeightenedVariable(level, 1)}d4 persistent fire damage.")}\n\n{{b}}Special: Versatile Melee.{{/b}} If you're adjacent to the target, this becomes a melee spell attack and benefits from flanking."
                    + (psychicAmpInformation != null
                        ? " {Blue}When using produce flame as a melee attack, increase the damage dice of the initial damage (but not the persistent damage) from d4s to d6s.{/Blue}" +
                          (amped ? $" {{Blue}}You deal {level} splash damage and this damage does not harm you. When using amped produce flame as a melee attack, increase the damage dice of the initial damage from d10s to d12s.{{/Blue}}" : "") +
                          (inCombat ? "" : "\n{Blue}Your produce flame also gains the following amp.{/Blue}" +
                           $"\n\n{{Blue}}{{b}}Amp{{/b}} You project pure heat that causes a target to combust. The initial damage changes to {S.HeightenedVariable(level, 1)}d10 fire damage (not adding your ability modifier) plus {level} fire splash damage. When using amped produce flame as a melee attack, increase the damage dice of the initial damage from d10s to d12s. You are not harmed by splash damage from amped produce flame.{{/Blue}}")
                        : ""),
                    Target.Ranged(6), level, null)
                .WithSpellAttackRoll().WithSoundEffect(SfxName.FireRay)
                .WithHeighteningNumerical(level, 1, inCombat, 1,
                    "Increase the damage by 1d4 and the persistent damage on a critical hit by 1d4.")
                .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                {
                    int num2 = spell.SpellLevel;
                    string str8 = num2.ToString();
                    num2 = spell.SpellcastingSource!.SpellcastingAbilityModifier;
                    string str9 = num2.ToString();
                    string diceExpression = $"{str8}d4+{str9}";
                    if (target.IsAdjacentTo(caster) && psychicAmpInformation != null && !amped)
                        diceExpression = $"{str8}d6+{str9}";
                    if (target.IsAdjacentTo(caster) && psychicAmpInformation != null && amped)
                        diceExpression = $"{str8}d12";
                    if (!target.IsAdjacentTo(caster) && psychicAmpInformation != null && amped)
                        diceExpression = $"{str8}d10";
                    await CommonSpellEffects.DealAttackRollDamage(spell, caster, target, result, diceExpression,
                        DamageKind.Fire);
                    if (amped && result >= CheckResult.Failure)
                    {
                        foreach (Creature enemy in target.Battle.AllCreatures.Where(v =>
                                     v != caster && (v.IsAdjacentTo(target) || v == target)))
                        {
                            await CommonSpellEffects.DealDirectSplashDamage(spell,
                                DiceFormula.FromText(level.ToString(), "Amped Produce Flame"), enemy, DamageKind.Fire);
                        }
                    }

                    if (result != CheckResult.CriticalSuccess)
                        return;
                    target.AddQEffect(QEffect.PersistentDamage(spell.SpellLevel + "d4", DamageKind.Fire));
                });
            produceFlame.PsychicAmpInformation = spellInformation.PsychicAmpInformation;
            if (spellInformation.PsychicAmpInformation == null) return produceFlame;
            produceFlame.Traits.Add(Trait.Psi);
            produceFlame.Description += inCombat
                ? ""
                : "\n{Blue}{b}Amp Heightened (+1){/b} Instead of using produce flame's normal heightened entry, the initial damage increases by 1d10 (1d12 for melee) and the splash damage increases by 1. The persistent fire damage on a critical hit increases by 1d4.{/Blue}";
            return produceFlame;
        });
        ModManager.ReplaceExistingSpell(SpellId.RayOfFrost, 0, (owner, level, inCombat, spellInformation) =>
        {
            PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
            bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
            CombatAction rayOfFrost = Spells.CreateModern(IllustrationName.RayOfFrost, "Ray of Frost",
                    [Trait.Attack, Trait.Cantrip, Trait.Cold, Trait.Evocation, Trait.Arcane, Trait.Primal],
                    "You blast an icy ray.", "Make a spell attack roll." + 
                                             S.FourDegreesOfSuccessReverse(null, null,
                                                                             $"{(amped ? "{Blue}" : "")}{S.HeightenedVariable(level, 1)}{(amped ? "{/Blue}" : "")}{(amped ? "{Blue}d10{/Blue}" : "d4+")}{(!amped ? S.SpellcastingModifier(owner, spellInformation) : "")} cold damage.",
                                                                             "Double damage, and the target takes a –10-foot status penalty to its Speeds for 1 round.") +
                                             (amped ? "\n\n{Blue}You gain temporary Hit Points equal to half the damage the target takes (after applying resistances and the like). You lose any remaining temporary Hit Points at the end of the encounter.{/Blue}" : "")
                                                                         + (psychicAmpInformation != null && !inCombat
                                                                             ? "\n\n{Blue} Your Ray of Frost's range increases to 180 feet. Your ray of frost also gains the following amp.{/Blue}" +
                                                                               $"\n\n{{Blue}}{{b}}Amp{{/b}} The ray deals {S.HeightenedVariable(level, 1)}d10 cold damage. You gain temporary Hit Points equal to half the damage the target takes (after applying resistances and the like). You lose any remaining temporary Hit Points at the end of the encounter.{{/Blue}}"
                                                                             : ""),
                    Target.Ranged(psychicAmpInformation != null ? 36 : 24), level, null).WithSpellAttackRoll()
                .WithSoundEffect(SfxName.RayOfFrost)
                .WithGoodness((tg, _, _) =>
                    level * 2.5f + tg.OwnerAction.SpellcastingSource!.SpellcastingAbilityModifier)
                .WithProjectileCone(IllustrationName.RayOfFrost, 15, ProjectileKind.Ray)
                .WithEffectOnEachTarget(async (action, caster, target, checkResult) =>
                {
                    int spellcastingAbilityModifier = action.SpellcastingSource!.SpellcastingAbilityModifier;
                    if (amped && checkResult >= CheckResult.Success)
                        target.AddQEffect(new QEffect
                        {
                            AfterYouTakeAmountOfDamageOfKind = (effect, combatAction, amount, _) =>
                            {
                                if (combatAction!.SpellId == action.SpellId)
                                {
                                    caster.GainTemporaryHP(amount / 2);
                                    effect.ExpiresAt = ExpirationCondition.Ephemeral;
                                }

                                return Task.CompletedTask;
                            }
                        });
                    await CommonSpellEffects.DealAttackRollDamage(action, caster, target, checkResult,
                        DiceFormula.FromText(
                            $"{level.ToString()}{(amped ? "d10" : "d4+")}{(!amped ? spellcastingAbilityModifier.ToString() : "")}",
                            "Ray of Frost"), DamageKind.Cold);
                    if (checkResult != CheckResult.CriticalSuccess)
                        return;
                    target.AddQEffect(new QEffect("Speed reduced",
                        "You have a -10-foot status penalty to your Speeds for 1 round.",
                        ExpirationCondition.CountsDownAtStartOfSourcesTurn, caster,
                        (Illustration)IllustrationName.RayOfFrost)
                    {
                        BonusToAllSpeeds =
                            (Func<QEffect, Bonus>)(_ => new Bonus(-2, BonusType.Circumstance, "ray of frost"))
                    });
                }).WithHeighteningNumerical(level, 1, inCombat, 1, "The damage increases by 1d4.");
            rayOfFrost.PsychicAmpInformation = spellInformation.PsychicAmpInformation;
            if (spellInformation.PsychicAmpInformation == null) return rayOfFrost;
            rayOfFrost.Traits.Add(Trait.Psi);
            rayOfFrost.Description += inCombat
                ? ""
                : "\n{Blue}{b}Amp Heightened (+1){/b} The initial damage increases by 1d10 instead of 1d4.{/Blue}";
            rayOfFrost.Target.OverriddenFullTargetLine = "{b}Range{/b} {Blue}180 feet{/Blue}";
            return rayOfFrost;
        });

        MSpellIds.ThermalStasis =
            ModManager.RegisterNewSpell("ThermalStasis", 0, (_, _, spellLevel, inCombat, spellInformation) =>
            {
                PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
                bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
                CombatAction thermalStasis = Spells.CreateModern(IllustrationName.ResistEnergy, "Thermal Stasis",
                        [Trait.Cantrip, Trait.Abjuration, Trait.Psychic, Trait.Level1PsychicCantrip, Trait.VerbalOnly],
                        "The same abilities that let you raise or lower thermal energy also let you keep it at a safe medium.",
                        $"The target gains resistance {2 * spellLevel} against fire damage and resistance {2 * spellLevel} against cold damage" +
                        (amped ? "{Blue}for 1 minute.{/Blue}" : " until the start of your next turn.") +
                        (inCombat ? "" : "\n\n{Blue}{b}Amp{/b} The spell's duration becomes 1 minute.{/Blue}"),
                        Target.RangedFriend(12), spellLevel, null)
                    .WithActionCost(1)
                    .WithHeighteningNumerical(spellLevel, 1, inCombat, 1, "Each resistance increases by 2.")
                    .WithEffectOnEachTarget((_, creature, target, _) =>
                    {
                        QEffect resist = QEffect.DamageResistance(DamageKind.Fire, spellLevel * 2)
                            .WithExpirationAtStartOfSourcesTurn(creature, amped ? 10 : 1);
                        resist.Illustration = MIllustrations.ColdFireWheel;
                        resist.Name = "Thermal Stasis";
                        resist.Description =
                            $"You have resistance {2 * spellLevel} against fire damage and resistance {2 * spellLevel} against cold damage";
                        target.AddQEffect(resist);
                        target.AddQEffect(QEffect.DamageResistance(DamageKind.Cold, spellLevel * 2)
                            .WithExpirationAtStartOfSourcesTurn(creature, amped ? 10 : 1));
                        return Task.CompletedTask;
                    });
                thermalStasis.PsychicAmpInformation = spellInformation.PsychicAmpInformation;
                if (spellInformation.PsychicAmpInformation == null) return thermalStasis;
                thermalStasis.Traits.Add(Trait.Psi);
                return thermalStasis;
            });
        MSpellIds.EntropicWheel = ModManager.RegisterNewSpell("EntropicWheel", 0,
            (_, _, spellLevel, inCombat, spellInformation) =>
            {
                PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
                bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
                CombatAction entropicWheel = Spells.CreateModern(MIllustrations.FireColdWheel, "Entropic Wheel",
                        [Trait.Cantrip, Trait.Cold, Trait.Evocation, Trait.Fire, Trait.Psychic, Trait.VerbalOnly, Trait.DoNotShowInCombatLog],
                        "You stockpile thermal energy in a magical wheel-like construct that lets you burn opponents with cold or freeze their bodies with heat.",
                        $"Cast as a reaction after dealing cold or fire damage.\nWhen you Cast the Spell, the wheel has {(amped ? "{Blue}2 motes{/Blue}" : "1 mote")} of thermal energy, and when you use a cold or fire effect or deal cold or fire damage, the wheel spins, siphoning off a bit of energy and gaining {(amped ? "{Blue}two more motes{/Blue}" : "another mote")}. The wheel can't gain motes more than once on a given turn, and the maximum number of motes is equal to {(inCombat ? $"{{b}}{S.HeightenedVariable(spellLevel, 3)}{{/b}}" : "entropic wheel's level")}." +
                        $"\n\nWhen you Cast a Spell that deals fire damage, the target also takes cold damage equal to the number of motes in the entropic wheel. When you Cast a Spell that deals cold damage, the target also takes fire damage equal to the number of motes in the entropic wheel. This applies only to the initial damage of the spell, not to any persistent damage or ongoing effects." +
                        IfAmped(inCombat,"You gain two motes instead of one when you Cast the Spell and each time the wheel gains another mote."),
                        Target.Uncastable(), spellLevel, null)
                    .WithActionCost(-2).WithSoundEffect(SfxName.FieryBurst)
                    .WithEffectOnSelf((action, self) =>
                    {
                        self.Overhead("Entropic Wheel", Color.Black, self + $" casts {{b}}{action.Name}{{/b}}.",
                            action.Name + " {icon:Reaction}",
                            action.Description, action.Traits);
                        self.AddQEffect(EntropicWheel(amped ? 2 : 1, amped, spellLevel));
                        self.Actions.UseUpReaction();
                        return Task.CompletedTask;
                    });
                entropicWheel.PsychicAmpInformation = spellInformation.PsychicAmpInformation;
                if (spellInformation.PsychicAmpInformation == null) return entropicWheel;
                entropicWheel.Traits.Add(Trait.Psi);
                return entropicWheel;
            });
        ModManager.ReplaceExistingSpell(SpellId.WarpStep, 0, (owner, level, inCombat, spellInformation) =>
        {
            PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
            bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
            int speed = 1;
            if (owner != null)
            {
                List<int> bonuses = [];
                foreach (QEffect qf in owner.QEffects)
                {
                    if (qf.BonusToAllSpeeds != null && qf.BonusToAllSpeeds.Invoke(qf)!.BonusType is BonusType.Status)
                        bonuses.Add(qf.BonusToAllSpeeds.Invoke(qf)!.Amount);
                }

                int bonus = 2;
                if (bonuses.Count > 0 && bonuses.Max() <= 2)
                    bonus -= bonuses.Max();
                if (bonuses.Count > 0 && bonuses.Max() > 2)
                    bonus = 0;
                speed = (owner.Speed + bonus) * 2;
            }

            bool psyche = psychicAmpInformation != null;
            CombatAction warpStepSpell = Spells.CreateModern(IllustrationName.WarpStep, "Warp Step",
                [
                    Trait.Cantrip, Trait.Conjuration, Trait.Arcane, Trait.Occult, Trait.Level1PsychicCantrip,
                    Trait.Manipulate
                ],
                "When you walk, the earth warps beneath your feet—your steps extend, distance contracts, and everything is just a little bit closer.",
                (amped && level >= 4 ? $"{{Blue}}{{b}}Range{{/b}} {speed * 5} feet{{/Blue}}\n\n" : "") +
                $"You gain a +{(psyche ? "{Blue}10{/Blue}" : "5")}-foot status bonus to your Speed until the end of your turn. {(amped && level >= 4 ? "{Blue}You then teleport to a location within range{/Blue}." : "You then Stride twice.")}"
                                                                    + $"{(psyche && !inCombat ? "\n\n{Blue}{b}Amp{/b} Space contracts with hardly a thought, letting you Cast the Spell as a single action.{/Blue}"
                                                                                : "")}",
                Target.Self(), level, null).WithSoundEffect(SfxName.Footsteps).WithActionCost(amped ? 1 : 2);
            warpStepSpell.WithEffectOnSelf(async (action, selectedCreature) =>
            {
                QEffect warpStep = new("Warp Step",
                    $"You have a +{(psyche ? "10" : "5")} status bonus to Speed.",
                    ExpirationCondition.ExpiresAtEndOfSourcesTurn, selectedCreature,
                    (Illustration)IllustrationName.WarpStep)
                {
                    CountsAsBeneficialToSource = true,
                    DoNotShowUpOverhead = true,
                    BonusToAllSpeeds =
                        (Func<QEffect, Bonus>)(_ => new Bonus(psyche ? 2 : 1, BonusType.Status, "Warp Step"))
                };
                selectedCreature.AddQEffect(warpStep);
                if (!amped || level < 4)
                {
                    if (!await selectedCreature.StrideAsync("Choose where to Stride with Warp Step. (1/2)",
                            allowCancel: true))
                    {
                        selectedCreature.RemoveAllQEffects(qf => qf == warpStep);
                        action.RevertRequested = true;
                    }
                    else
                    {
                        int num = await selectedCreature.StrideAsync("Choose where to Stride with Warp Step. (2/2)",
                            allowPass: true)
                            ? 1
                            : 0;
                    }
                }
            });
            if (amped && level >= 4)
            {
                warpStepSpell.Target = Target.TileYouCanSeeAndTeleportTo(speed);
                warpStepSpell.WithEffectOnChosenTargets(async (creature, targets) =>
                {
                    await CommonSpellEffects.Teleport(creature, targets.ChosenTile!);
                }).Traits.Add(Trait.Teleportation);
            }
            warpStepSpell.PsychicAmpInformation = spellInformation.PsychicAmpInformation;
            if (spellInformation.PsychicAmpInformation == null) return warpStepSpell;
            warpStepSpell.Traits.Add(Trait.Psi);
            warpStepSpell.Description += inCombat ? "" :
                "\n{Blue}{b}Amp Heightened (4th){/b} Instead of striding, you teleport to a space within your line of sight and line of effect with a range equal to your double your Speed (after applying the status bonus from warp step). This grants the spell the teleportation trait.{/Blue}";
            return warpStepSpell;
        });
        ModManager.ReplaceExistingSpell(SpellId.PhaseBolt, 0, (owner, level, inCombat, spellInformation) =>
        {
            PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
            bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
            bool psyche = psychicAmpInformation != null;
            CombatAction phaseBolt = Spells.CreateModern(IllustrationName.PhaseBolt, "Phase Bolt",
                    [
                        Trait.Attack, Trait.Cantrip, Trait.Evocation, Trait.Arcane, Trait.Occult,
                        Trait.Level1PsychicCantrip
                    ],
                    "You point your finger and project a bolt of magical energy that stutters in and out of phase until it reaches the target.",
                    "Make a spell attack roll. Reduce any circumstances bonuses of the target by 2 for this attack." +
                    S.FourDegreesOfSuccessReverse(null, null,
                        $"{S.HeightenedVariable(amped ? 1+(level-1)*2 : level, 1)}d4+{S.SpellcastingModifier(owner, spellInformation)} piercing damage.",
                        "Double damage.") +
                    (psyche
                        ? "\n{Blue}Your phase bolt temporarily sends the target's cover out of phase if it hits. On a success, reduce the target's circumstance bonus to AC (if any) by 1 until the beginning of your next turn.{/Blue}" +
                          (!inCombat ? " {Blue}Your phase bolt also gains the following amp.{/Blue}" +
                                      "\n\n{Blue}{b}Amp{b} Your bolt leaves phase completely, becoming invisible and intangible until it's already embedded in the target— giving the impression it simply teleported itself into place. The target is flat-footed against the attack. Additionally, the bolt ignores an amount of Hardness or resistance to piercing damage equal to half the spell's level. On a critical success, the target can't be affected by teleportation effects until the start of your next turn.{/Blue}"
                              : "")
                        : ""),
                    Target.Ranged(6), level, null)
                .WithSpellAttackRoll()
                .WithGoodnessAgainstEnemy((t, _, _) =>
                    t.OwnerAction.SpellLevel * 2.5f + t.OwnerAction.SpellcastingSource!.SpellcastingAbilityModifier)
                .WithSoundEffect(SfxName.PhaseBolt)
                .WithHeighteningOfDamageEveryLevel(level, 1, inCombat, "1d4")
                .WithEffectOnEachTarget(async (spell, caster, target, ck) =>
                {
                    QEffect reduceResist = new()
                    {
                        StateCheck = _ =>
                        {
                            Resistance? pierceResist = target.WeaknessAndResistance.Resistances.FirstOrDefault(res => res.DamageKind == DamageKind.Piercing);
                            if (pierceResist is { Value: > 0 })
                            {
                                pierceResist.Value = Math.Max(0, pierceResist.Value - level/2);
                            }
                        }
                    };
                    QEffect flatFoot = QEffect.FlatFooted("Amped phase bolt");
                    if (amped)
                    {
                        target.AddQEffect(flatFoot);
                        target.AddQEffect(reduceResist);
                    }
                    await target.Battle.GameLoop.StateCheck();
                    await CommonSpellEffects.DealAttackRollDamage(spell, caster, target, ck,
                        DiceFormula.FromText(
                            $"{(amped ? (1+(level-1)*2).ToString() : level.ToString())}d4+{spell.SpellcastingSource!.SpellcastingAbilityModifier.ToString()}",
                            "Phase Bolt"), DamageKind.Piercing);
                    flatFoot.ExpiresAt = ExpirationCondition.Immediately;
                    reduceResist.ExpiresAt = ExpirationCondition.Immediately;
                    if (psyche && ck >= CheckResult.Success)
                    {
                        target.AddQEffect(Phased(caster));
                    }
                    if (amped && ck == CheckResult.CriticalSuccess)
                        target.AddQEffect(QEffect.TraitImmunity(Trait.Teleportation).WithExpirationAtStartOfSourcesTurn(caster, 1));
                });
            phaseBolt.PsychicAmpInformation = spellInformation.PsychicAmpInformation;
            if (spellInformation.PsychicAmpInformation == null) return phaseBolt;
            phaseBolt.Traits.Add(Trait.Psi);
            phaseBolt.Description += inCombat ? "" : "\n{Blue}{b}Amp Heightened (+1){/b} The bolt's damage increases by 2d4 instead of 1d4.{/Blue}";
            return phaseBolt;
        });
        // MSpellIds.DistortionLens = ModManager.RegisterNewSpell("DistortionLens", 0,
        //     (_, caster, spellLevel, inCombat, spellInformation) =>
        //     {
        //         PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
        //         bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
        //         CombatAction distortionLens = Spells.CreateModern(IllustrationName.AeonStoneBase, "Distortion Lens", [Trait.Cantrip, Trait.Conjuration, Trait.Psychic, Trait.VerbalOnly, Trait.Level1PsychicCantrip],
        //             "You create a magical lens that distorts space as best suits you.", 
        //             "You create the lens in a space in range, even suspended in midair. If your or an ally's ranged attack passes through the lens, the attack gains an additional 10 feet of range; if an enemy's ranged attack would pass through the lens, it requires an additional 10 feet of range to move through, though the enemy knows before using its ability whether the interference puts the target out of range. An ally whose space overlaps the lens can increase the range of its ranged attacks, but an enemy whose space overlaps the lens doesn't reduce the range of its ranged attacks." +
        //             "\n\nThe first time each round you Sustain the Spell, you can choose to relocate it to another square within range. The lens disappears if you cast distortion lens again." +
        //             "\n\n{b}Amp{/b} The lens can interfere with creatures as well as attacks. Once during a Medium or smaller ally's move action, the ally can move into and out of the lens's square without that square counting against the total distance moved. Conversely, the lens is difficult terrain for your enemies.", Target.Tile((creature, tile) => tile.DistanceTo(creature.Occupies) <= 6), spellLevel, null)
        //             .WithActionCost(1).WithSoundEffect(SfxName.PhaseBolt)
        //             .WithEffectOnChosenTargets((spell, self, targets) =>
        //             {
        //                 Tile? tile = targets.ChosenTile;
        //
        //             });
        //         distortionLens.PsychicAmpInformation = spellInformation.PsychicAmpInformation;
        //         if (spellInformation.PsychicAmpInformation == null) return distortionLens;
        //         distortionLens.Traits.Add(Trait.Psi);
        //         return distortionLens;
        //     });
        MSpellIds.GhostlyShift = ModManager.RegisterNewSpell("GhostlyShift", 0,
            (_, caster, spellLevel, inCombat, spellInformation) =>
            {
                PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
                bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
                CombatAction ghostlyShift = Spells.CreateModern(IllustrationName.Blink, "Ghostly Shift", [Trait.Cantrip, Trait.Conjuration, Trait.Psychic, Trait.VerbalOnly, Trait.Level1PsychicCantrip, Trait.SpellWithDuration],
                    "The target's body becomes insubstantial as they partially phase out.", 
                    $"The target gains resistance {S.HeightenedVariable((spellLevel+1)/2, 2)} to all damage (except force). The effect lasts as long as you Sustain the spell." +
                    (amped ? $"\n\n{{Blue}}Once each round when they Stride, Swim, or Fly, they can pass through one creature's space during that action without needing to attempt a check to Tumble Through, though they can't end their turn in its space. When they pass through a creature's space in this way, you deal {S.HeightenedVariable((spellLevel + 1) / 2, 2)}d6 force damage to that creature, with a basic Fortitude save.{{/Blue}}" : "") +
                    (inCombat ? "" : $"\n\n{{Blue}}{{b}}Amp{{/b}} Once each round when they Stride, Swim, or Fly, they can pass through one creature's space during that action without needing to attempt a check to Tumble Through, though they can't end their turn in its space. When they pass through a creature's space in this way, you deal {S.HeightenedVariable((spellLevel + 1) / 2, 2)}d6 force damage to that creature, with a basic Fortitude save.{{/Blue}}"),
                    Target.RangedFriend(6), spellLevel, null)
                    .WithActionCost(1).WithSoundEffect(SfxName.PhaseBolt).WithHeighteningNumerical(spellLevel, 3, inCombat, 2, "The resistance increases by 1.")
                    .WithEffectOnChosenTargets((spell, self, targets) =>
                    {
                        Creature ally = targets.ChosenCreature!;
                        QEffect ghost = new()
                        {
                            Name = "Ghostly Shift",
                            Illustration = spell.Illustration,
                            Description = $"You gain resistance {(spellLevel+1)/2} to all damage (except force).",
                            Id = amped ? MQEffectIds.GhostThrough : MQEffectIds.Ghosted,
                            StateCheck = _ =>
                            {
                                self.AddQEffect(QEffect.DamageResistanceAllExcept((spellLevel+1)/2, DamageKind.Force).WithExpirationEphemeral());
                            },
                            AfterYouTakeAction = async (_, action) =>
                            {
                                if (amped && action.ActionId == MActionIds.GhostThrough && !self.HasEffect(MQEffectIds.Ghost))
                                {
                                    if (action.ChosenTargets.ChosenCreature is {} enemy)
                                    {
                                        action.Owner.Overhead("Ghost Through", Color.Black, $"{action.Owner} ghosts through {enemy.Name}.");
                                        CheckResult result = CommonSpellEffects.RollSpellSavingThrow(enemy, spell, Defense.Fortitude);
                                       await CommonSpellEffects.DealBasicDamage(spell, self, enemy, result,
                                            DiceFormula.FromText((spellLevel+1)/2 + "d6",
                                                "Ghostly Shift"), DamageKind.Force);
                                       self.AddQEffect(new QEffect(ExpirationCondition.ExpiresAtStartOfYourTurn)
                                           { Id = MQEffectIds.Ghost });
                                    }
                                }
                            }, 
                            YouBeginAction = (effect, action) =>
                            {
                                if (action.ActionId == ActionId.TumbleThrough && !self.HasEffect(MQEffectIds.Ghost))
                                {
                                    action.WithActionId(MActionIds.GhostThrough);
                                    action.WithActiveRollSpecification(null);
                                        
                                }
                                return Task.CompletedTask;
                            },
                            CannotExpireThisTurn = true,
                            ExpiresAt = ExpirationCondition.ExpiresAtEndOfYourTurn,
                        };
                        ghost.WithExpirationSustained(spell, self);
                        ally.AddQEffect(ghost);
                        return Task.CompletedTask;
                    });
                ghostlyShift.PsychicAmpInformation = spellInformation.PsychicAmpInformation;
                if (spellInformation.PsychicAmpInformation == null) return ghostlyShift;
                ghostlyShift.Traits.Add(Trait.Psi);
                ghostlyShift.Description += inCombat
                    ? ""
                    : "\n{Blue}{b}Amp Heightened (+2){/b} Increase the damage dealt by phasing through a creature by 1d6.{/Blue}";
                return ghostlyShift;
            });
        MSpellIds.TesseractTunnel = ModManager.RegisterNewSpell("TesseractTunnel", 0,
            (_, caster, spellLevel, inCombat, spellInformation) =>
            {
                PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
                bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
                int speed = 0;
                if (caster != null) speed = caster.Speed;
                CombatAction tesseractTunnel = Spells.CreateModern(IllustrationName.DimensionDoor, "Tesseract Tunnel", [Trait.Cantrip, Trait.Conjuration, Trait.Psychic, Trait.Teleportation, Trait.NoHeightening, Trait.SpellWithDuration],
                    "You race from point to point, tearing open a tunnel in space.",
                    (amped ? "{Blue}You create a portal in your square and in a square within a range equal to your Speed; you can choose to immediately teleport to the portal not in your square.{/Blue}" : "You create a portal in your current space and then stride, creating another portal in the space you end your Stride.") +
                    $" Any creature that enters {(amped ? "{Blue}either portal{/Blue}" : "the first portal")} can immediately transport itself to the {(amped ? "{Blue}other{/Blue}" : "exit")} portal as part of its move action, which adds the teleportation trait to its movement." +
                    $"\n\n{(amped ? "" : "The tunnel lasts as long as you Sustain the spell. ")}You can have only one tesseract tunnel open at a time; creating another causes the first to immediately close.{(amped ? " You can Dismiss the spell." : "")}" +
                    IfAmped(inCombat,"Physical movement is no longer necessary to connect two points in space. When you cast tesseract tunnel, you simply create a tunnel that ends in a square within a range equal to your Speed; you can choose to immediately teleport to the space at the far end of the tunnel. Furthermore, the tunnel can be traversed in both directions, instead of only from entrance to exit. Finally, the spell's duration changes to 1 minute. You can Dismiss the spell."), 
                    amped ? Target.TileYouCanSeeAndTeleportTo(speed) : Target.Self(), spellLevel, null)
                    .WithActionCost(2).WithSoundEffect(SfxName.PhaseBolt);
                if (amped)
                {
                    tesseractTunnel.WithEffectOnChosenTargets(async (spell, self, targets) =>
                    {
                        Tile? exit = targets.ChosenTile;
                        Tile entrance = self.Occupies;
                        if (exit != null)
                        {
                            TileQEffect entranceQf = Teleporter(exit, amped, self);
                            TileQEffect exitQf = Teleporter(entrance, amped, self);
                            QEffect tunnelQf = TunnelQf(spell, entranceQf, exitQf, self, entrance, exit, amped);
                            entrance.AddQEffect(entranceQf);
                            exit.AddQEffect(exitQf);
                            self.AddQEffect(tunnelQf);
                            if (await self.AskForConfirmation(IllustrationName.DimensionDoor,
                                    "Would you like to teleport to the end point of your tunnel?", "Yes"))
                                await CommonSpellEffects.Teleport(self, exit);
                        }
                    });
                }
                else
                {
                    tesseractTunnel.WithEffectOnEachTarget(async (spell, self, _, _) =>
                    {
                        Tile entrance = self.Occupies;
                        if (await self.StrideAsync("Choose where to stride to to open the exit portal",
                                allowCancel: true))
                        {
                            Tile exit = self.Occupies;
                            TileQEffect entranceQf = Teleporter(exit, amped, self);
                            TileQEffect exitQf = TeleportExit();
                            QEffect tunnelQf = TunnelQf(spell, entranceQf, exitQf, self, entrance, exit, amped);
                            tunnelQf.ExpiresAt =  ExpirationCondition.ExpiresAtEndOfYourTurn;
                            tunnelQf.WithExpirationSustained(spell, self);
                            entrance.AddQEffect(entranceQf);
                            exit.AddQEffect(exitQf);
                            self.AddQEffect(tunnelQf);
                        }
                        else spell.RevertRequested = true;
                    });
                }
                tesseractTunnel.PsychicAmpInformation = spellInformation.PsychicAmpInformation;
                if (spellInformation.PsychicAmpInformation == null) return tesseractTunnel;
                tesseractTunnel.Traits.Add(Trait.Psi);
                return tesseractTunnel;
            });
        MSpellIds.ThoughtfulGift = ModManager.RegisterNewSpell("ThoughtfulGift", 1,
            (_, _, spellLevel, inCombat, _) =>
            {
                CombatAction thoughtfulGift = Spells.CreateModern(new ModdedIllustration("CMAssets/Gift.png"), "Thoughtful Gift", [Trait.Conjuration, Trait.Arcane, Trait.Divine, Trait.Occult, Trait.SomaticOnly, Trait.Teleportation],
                    "You share what you have with someone else.", 
                    "You teleport one item held in your hand to an ally. The item appears instantly in the ally's hand, if they have a free hand, or at their feet if they don't", Target.RangedFriend(spellLevel >= 3 ? 100 : 24).WithAdditionalConditionOnTargetCreature((self, friend) => self == friend ? Usability.NotUsableOnThisCreature("Cannot target yourself.") : Usability.Usable), spellLevel, null)
                    .WithActionCost(1).WithSoundEffect(SfxName.PhaseBolt).WithHeightenedAtSpecificLevel(spellLevel, 3, inCombat, "The spell's range increases to 500 feet.")
                    .WithEffectOnChosenTargets(async (spell, self, targets) =>
                    {
                        Creature? friend = targets.ChosenCreature;
                        if (friend != null)
                        {
                            List<string> heldItems = [];
                            heldItems.AddRange(self.HeldItems.Select(heldItem => heldItem.Name));
                            ChoiceButtonOption choice = await self.AskForChoiceAmongButtons(spell.Illustration,
                                "Which item would you like to teleport?", heldItems.ToArray());
                            switch (friend.HasFreeHand)
                            {
                                case false:
                                    friend.DropItem(self.HeldItems[choice.Index]);
                                    self.HeldItems.RemoveAt(choice.Index);
                                    break;
                                case true:
                                    friend.AddHeldItem(self.HeldItems[choice.Index]);
                                    self.HeldItems.RemoveAt(choice.Index);
                                    break;
                            }
                        }
                    });
                return thoughtfulGift;
            });
        ModManager.ReplaceExistingSpell(SpellId.AttackHints, 0, (caster, level, inCombat, spellInformation) =>
        {
            PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
            bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
            CombatAction omnidirectionalScan = Spells.CreateModern(new ModdedIllustration("CMAssets/OmniScan.png"), "Omnidirectional Scan", [Trait.Cantrip, Trait.Divination, Trait.Psychic, Trait.VerbalOnly, Trait.UnaffectedByConcealment],
                "You quickly view your surroundings from a variety of angles, your senses constructing an overall mental picture.", 
                "You can Seek a 30 foot emanation.\n\nRegardless of whether you Seek, target one ally and one enemy in the area; if a target is concealed or hidden from you, you automatically succeed at the flat check to target it with this spell. You prepare to {tooltip:aid}Aid{/} the target ally on an attack roll against the target enemy. If you take this Aid reaction, you use your spell attack modifier and proficiency rank on your check to Aid. If you critically fail the roll to Aid, you get a failure instead." +
                (!amped ? "" : $"\n\n{{Blue}}The target enemy takes a -1 circumstance penalty on its next saving throw. You and all allies within 30 feet gain a +1 circumstance bonus to attacks and damage against the creature. If you take the Aid reaction you prepared for and succeed at your roll, these bonuses increase to +2 on the attack you're Aiding. On a critical success, they increase to {(caster != null && caster.Proficiencies.Get(Trait.Spell) == Proficiency.Legendary ? "4" : "3")}{{/Blue}}") +
                (inCombat ? "" : "\n\n{Blue}{b}Amp{/b} The target enemy takes a -1 circumstance penalty on its next saving throw. You and all allies within 30 feet gain a +1 circumstance bonus to attacks and damage against the creature.{/Blue}" +
                 "\n\n{Blue}If you take the Aid reaction you prepared for and succeed at your roll, these bonuses increase to +2 on the attack you're Aiding. On a critical success, they increase to +3, or to +4 if you have legendary proficiency in spell attack rolls.{/Blue}"), Target.MultipleCreatureTargets(Target.Ranged(6), Target.RangedFriend(6).WithAdditionalConditionOnTargetCreature((self, ally) => self == ally ? Usability.NotUsableOnThisCreature("Cannot target yourself") : Usability.Usable)), level, null)
                .WithActionCost(1).WithSoundEffect(SfxName.PositivePing)
                .WithEffectOnChosenTargets(async (spell, self, targets) =>
                {
                    CombatAction seek = CreateSeek(IllustrationName.EnergyEmanation, "Seek (30-foot-emanation)", self);
                    if (self.Battle.AllCreatures.Any(cr =>
                        {
                            if (!cr.EnemyOf(self))
                                return false;
                            if (cr.DetectionStatus.Undetected)
                                return true;
                            return cr.DetectionStatus.IsHiddenToAnEnemy && !cr.HasEffect(QEffectId.Invisible);
                        }) || self.Battle.Encounter is S2E5EscapeRoom || self.Battle.Encounter.AllowsSeek)
                    {
                        seek.ActionCost = 0;
                        await self.Battle.GameLoop.FullCast(seek);
                    }
                    Creature? friend = targets.ChosenCreatures.Find(cr => cr.FriendOf(self));
                    Creature? enemy =  targets.ChosenCreatures.Find(cr => cr.EnemyOf(self));
                    if (friend != null && enemy != null)
                    {
                        QEffect omniPrep = ConsciousMind.PrepareOmniAid(self, friend, enemy);
                        QEffect omniReact = ConsciousMind.ReceiveOmniAid(omniPrep, amped, enemy, spell);
                        self.AddQEffect(omniPrep);
                        friend.AddQEffect(omniReact);
                        if (amped)
                        {
                            QEffect ampBonus = new();
                            ampBonus.AddGrantingOfTechnical(cr => cr.FriendOf(self) && cr.DistanceTo(self) <= 6, qfTech =>
                            {
                                if (!qfTech.Owner.HasEffect(MQEffectIds.OmniBonus))
                                {
                                    qfTech.Owner.AddQEffect(new QEffect("Amped Bonus",
                                        $"You gain a +1 circumstance bonus to attacks and damage against {enemy.Name}.",
                                        ExpirationCondition.ExpiresAtStartOfSourcesTurn, self,
                                        IllustrationName.AttackHints)
                                    {
                                        BonusToAttackRolls = (_, action, target) =>
                                            action.HasTrait(Trait.Attack) && target == enemy
                                                ? new Bonus(1, BonusType.Circumstance, "Amped Bonus")
                                                : null,
                                        BonusToDamage = (_, _, target) =>
                                            target == enemy
                                                ? new Bonus(1, BonusType.Circumstance, "Amped Bonus")
                                                : null,
                                        Id = MQEffectIds.OmniBonus
                                    });
                                }
                            });
                            QEffect ampedPenalty = new()
                            {
                                BonusToDefenses = (_, action, defense) => action?.SavingThrow != null && defense.IsSavingThrow() ? new Bonus(-1, BonusType.Circumstance, "Omnidirectional Scan") : null,
                                AfterYouMakeSavingThrow = (effect, _, _) =>
                                {
                                    effect.ExpiresAt = ExpirationCondition.Immediately;
                                }
                            };
                            enemy.AddQEffect(ampedPenalty);
                            self.AddQEffect(ampBonus);
                            await self.Battle.GameLoop.StateCheck();
                            ampBonus.ExpiresAt = ExpirationCondition.Immediately;
                        }
                    }
                });
            omnidirectionalScan.PsychicAmpInformation = spellInformation.PsychicAmpInformation;
            if (spellInformation.PsychicAmpInformation == null) return omnidirectionalScan;
            omnidirectionalScan.Traits.Add(Trait.Psi);
            return omnidirectionalScan;
        });
        ModManager.ReplaceExistingSpell(SpellId.Guidance, 0, (caster, level, inCombat, spellInformation) =>
        {
            PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
            bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
            CreatureTarget creatureTarget2 = Target.RangedFriend(6);
            string description4 = "The target gains a +1 status bonus to one attack roll, Perception check, saving throw, or skill check until the start of your next turn.\r\n\r\nThe target chooses which roll to use the bonus on before rolling.\r\n\r\nThe target is then temporarily immune to Guidance for the rest of the encounter.";
            if (spellInformation.PsychicAmpInformation != null)
            {
                creatureTarget2 = Target.RangedFriend(24);
                creatureTarget2.OverriddenFullTargetLine = "{b}Range{/b} {Blue}120 feet{/Blue}";
                if (amped)
                    description4 = "The target gains a +1 status bonus to one attack roll, Perception check, saving throw, or skill check until the start of your next turn.\r\n\r\nThe target chooses which roll to use the bonus on before rolling.\r\n\r\n{Blue}You can use this also on a target who already received {i}guidance{/i} this encounter.{/Blue}" +
                                   "\n\n{Blue}You can cast an amped {i}guidance{/i} spell as a reaction triggered when your ally fails or critically fails an attack roll, Perception check, saving throw, or skill check, and the bonus from {i}guidance{/i} would change the failure to a success or the critical failure to a normal failure. The bonus from {i}guidance{/i} applies retroactively to their check.{/Blue}";
                description4 += IfAmped(inCombat, "Amped {i}guidance{/i} doesn't cause the target to be immune to further {i}guidance{/i} and can be used on targets with immunity to {i}guidance.{/i}"+
                                                  "\n\n{Blue}You can cast an amped {i}guidance{/i} spell as a reaction triggered when your ally fails or critically fails an attack roll, Perception check, saving throw, or skill check, and the bonus from {i}guidance{/i} would change the failure to a success or the critical failure to a normal failure. The bonus from {i}guidance{/i} applies retroactively to their check.{/Blue}");
            }
            CombatAction guidance = Spells.CreateModern(IllustrationName.Guidance, "Guidance", [Trait.Cantrip, Trait.Divination, Trait.Divine, Trait.Occult, Trait.Primal, Trait.Level1PsychicCantrip, Trait.NoHeightening], 
                "You ask for divine guidance.", description4, creatureTarget2, level, null)
                .WithActionCost(1).WithActionId(amped ? ActionId.None : ActionId.Guidance).WithSoundEffect(SfxName.Guidance)
                .WithEffectOnEachTarget((spell, caster, target, result) =>
            {
                target.AddQEffect(new QEffect("Guidance", "You gain a +1 status bonus to one attack roll, Perception check, saving throw or skill check.", ExpirationCondition.ExpiresAtStartOfSourcesTurn, caster, (Illustration) IllustrationName.Guidance)
                {
                    BeforeYourSavingThrow = (Func<QEffect, CombatAction, Creature, Task>) (async (effect, action, you) =>
                    {
                        if (!await you.Battle.AskForConfirmation(you, IllustrationName.Guidance, $"You're about to make a saving throw against {action.Name}.\nUse the +1 status bonus from guidance?", "Use the bonus"))
                            return;
                        effect.ExpiresAt = ExpirationCondition.Immediately;
                        you.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                        {
                            BonusToDefenses = ((Func<QEffect, CombatAction, Defense, Bonus>) ((qf, _, df) => new Bonus(1, BonusType.Status, "Guidance")))!
                        });
                    }),
                    BeforeYourActiveRoll = (Func<QEffect, CombatAction, Creature, Task>) (async (effect, action, innerTarget) =>
                    {
                        Creature you = effect.Owner;
                        if (!await you.Battle.AskForConfirmation(you, IllustrationName.Guidance, $"You're about to use {action.Name} against {innerTarget?.ToString()}.\nUse the +1 status bonus from guidance?", "Use the bonus"))
                        {
                            you = null!;
                        }
                        else
                        {
                            effect.ExpiresAt = ExpirationCondition.Immediately;
                            you.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                            {
                                BonusToAttackRolls = ((Func<QEffect, CombatAction, Creature, Bonus>) ((qf, combatAction, actualTarget) => (actualTarget != innerTarget || action != combatAction ? null : new Bonus(1, BonusType.Status, "Guidance"))!))!
                            });
                            you = null!;
                        }
                    })
                });
                if (amped)
                    return Task.CompletedTask;
                target.AddQEffect(QEffect.ImmunityToTargeting(ActionId.Guidance));
                return Task.CompletedTask;
            });
            guidance.PsychicAmpInformation = spellInformation.PsychicAmpInformation;
            if (spellInformation.PsychicAmpInformation == null) return guidance;
            guidance.Traits.Add(Trait.Psi);
            return guidance;
        });
    }
    //use for specific actions
    private static QEffect ForbiddenThought(int spellLevel, string actionName, Creature caster, CombatAction spell,
        bool amped)
    {
        return new QEffect(ExpirationCondition.ExpiresAtEndOfYourTurn)
        {
            YouBeginAction = (effect, combatAction) =>
            {
                Creature self = effect.Owner;
                if (combatAction.Name == actionName)
                {
                    CheckResult savingThrow = CommonSpellEffects.RollSpellSavingThrow(self, spell, Defense.Will);
                    CommonSpellEffects.DealBasicDamage(spell, caster, self, savingThrow, 1 + spellLevel + "d6",
                        DamageKind.Mental);
                    if (amped && savingThrow <= CheckResult.Failure)
                        self.AddQEffect(QEffect.Stunned(1));
                    self.AddQEffect(QEffect.ImmunityToTargeting(MActionIds.ForbiddenThought));
                    self.AddQEffect(QEffect.ImmunityToCondition(MQEffectIds.ForbiddenThought));
                    effect.ExpiresAt = ExpirationCondition.Ephemeral;
                }

                return Task.CompletedTask;
            },
            Illustration = IllustrationName.Confusion,
            Name = "Forbidden Thought - " + actionName,
            Description =
                $"If the target uses the specified action, it will take {1 + spellLevel}d6 damage (with a basic Will save).",
            Id = MQEffectIds.ForbiddenThought
        };
    }

    //use for spells
    private static QEffect ForbiddenThought(int spellLevel, Creature caster, CombatAction spell, bool amped)
    {
        return new QEffect(ExpirationCondition.ExpiresAtEndOfYourTurn)
        {
            YouBeginAction = (effect, combatAction) =>
            {
                Creature self = effect.Owner;
                if (combatAction.SpellInformation != null)
                {
                    CheckResult savingThrow = CommonSpellEffects.RollSpellSavingThrow(self, spell, Defense.Will);
                    CommonSpellEffects.DealBasicDamage(spell, caster, self, savingThrow, 1 + spellLevel + "d6",
                        DamageKind.Mental);
                    if (amped && savingThrow <= CheckResult.Failure)
                        self.AddQEffect(QEffect.Stunned(1));
                    self.AddQEffect(QEffect.ImmunityToTargeting(MActionIds.ForbiddenThought));
                    self.AddQEffect(QEffect.ImmunityToCondition(MQEffectIds.ForbiddenThought));
                    effect.ExpiresAt = ExpirationCondition.Ephemeral;
                }

                return Task.CompletedTask;
            },
            Illustration = IllustrationName.Confusion,
            Name = "Forbidden Thought - Spell",
            Description =
                $"If the target uses the specified action, it will take {1 + spellLevel}d6 damage (with a basic Will save).",
            Id = MQEffectIds.ForbiddenThought
        };
    }

    //use for strikes
    private static QEffect ForbiddenThought(int spellLevel, Trait trait, Creature caster, CombatAction spell,
        bool amped)
    {
        return new QEffect(ExpirationCondition.ExpiresAtEndOfYourTurn)
        {
            YouBeginAction = (effect, combatAction) =>
            {
                Creature self = effect.Owner;
                if (combatAction.Traits.Contains(trait))
                {
                    CheckResult savingThrow = CommonSpellEffects.RollSpellSavingThrow(self, spell, Defense.Will);
                    CommonSpellEffects.DealBasicDamage(spell, caster, self, savingThrow, 1 + spellLevel + "d6",
                        DamageKind.Mental);
                    if (amped && savingThrow <= CheckResult.Failure)
                        self.AddQEffect(QEffect.Stunned(1));
                    self.AddQEffect(QEffect.ImmunityToTargeting(MActionIds.ForbiddenThought));
                    self.AddQEffect(QEffect.ImmunityToCondition(MQEffectIds.ForbiddenThought));
                    effect.ExpiresAt = ExpirationCondition.Ephemeral;
                }

                return Task.CompletedTask;
            },
            Illustration = IllustrationName.Confusion,
            Name = "Forbidden Thought - Strike",
            Description =
                $"If the target uses the specified action, it will take {1 + spellLevel}d6 damage (with a basic Will save).",
            Id = MQEffectIds.ForbiddenThought
        };
    }

    //use for stride
    private static QEffect ForbiddenThought(int spellLevel, ActionId actionId, Creature caster, CombatAction spell,
        bool amped)
    {
        return new QEffect(ExpirationCondition.ExpiresAtEndOfYourTurn)
        {
            YouBeginAction = (effect, combatAction) =>
            {
                Creature self = effect.Owner;
                if (combatAction.ActionId == actionId || combatAction.ActionId == ActionId.StepByStepStride)
                {
                    CheckResult savingThrow = CommonSpellEffects.RollSpellSavingThrow(self, spell, Defense.Will);
                    CommonSpellEffects.DealBasicDamage(spell, caster, self, savingThrow, 1 + spellLevel + "d6",
                        DamageKind.Mental);
                    if (amped && savingThrow <= CheckResult.Failure)
                        self.AddQEffect(QEffect.Stunned(1));
                    self.AddQEffect(QEffect.ImmunityToTargeting(MActionIds.ForbiddenThought));
                    self.AddQEffect(QEffect.ImmunityToCondition(MQEffectIds.ForbiddenThought));
                    effect.ExpiresAt = ExpirationCondition.Ephemeral;
                }

                return Task.CompletedTask;
            },
            Illustration = IllustrationName.Confusion,
            Name = "Forbidden Thought - Stride",
            Description =
                $"If the target uses the specified action, it will take {1 + spellLevel}d6 damage (with a basic Will save).",
            Id = MQEffectIds.ForbiddenThought
        };
    }

    private static QEffect EntropicWheel(int motes, bool amped, int spellLevel)
    {
        return new QEffect
        {
            Id = MQEffectIds.EntropicWheel,
            Name = "Entropic Wheel",
            Value = motes,
            Description =
                "When you Cast the Spell, the wheel has one mote of thermal energy, and when you use a cold or fire effect or deal cold or fire damage, the wheel spins, siphoning off a bit of energy and gaining another mote. The wheel can't gain motes more than once on a given turn, and the maximum number of motes is equal to entropic wheel's level." +
                "\n\nWhen you Cast a Spell that deals fire damage, the target also takes cold damage equal to the number of motes in the entropic wheel. When you Cast a Spell that deals cold damage, the target also takes fire damage equal to the number of motes in the entropic wheel. This applies only to the initial damage of the spell, not to any persistent damage or ongoing effects.",
            Illustration = MIllustrations.FireColdWheel,
            AfterYouTakeAction = (wheel, action) =>
            {
                if ((action.HasTrait(Trait.Fire) || action.HasTrait(Trait.Cold)) &&
                    !wheel.Owner.HasEffect(MQEffectIds.EntropicBlock) && wheel.Value < spellLevel)
                {
                    var toMaxMotes = spellLevel - wheel.Value;
                    wheel.Value += amped && toMaxMotes >= 2 ? 2 : 1;
                    wheel.Owner.AddQEffect(new QEffect(ExpirationCondition.ExpiresAtEndOfAnyTurn)
                        { Id = MQEffectIds.EntropicBlock });
                }

                return Task.CompletedTask;
            },
            AfterYouDealDamageOfKind = (self, _, damageKind, _) =>
            {
                QEffect wheel = self.FindQEffect(MQEffectIds.EntropicWheel)!;
                if (!self.HasEffect(MQEffectIds.EntropicBlock) && damageKind is DamageKind.Fire or DamageKind.Cold &&
                    wheel.Value < spellLevel)
                {
                    var toMaxMotes = spellLevel - wheel.Value;
                    wheel.Value += amped && toMaxMotes >= 2 ? 2 : 1;
                    self.AddQEffect(new QEffect(ExpirationCondition.ExpiresAtEndOfAnyTurn)
                        { Id = MQEffectIds.EntropicBlock });
                }

                return Task.CompletedTask;
            },
            YouDealDamageEvent = (wheel, damage) =>
            {
                if (damage.KindedDamages.Any(kindedDamage =>
                        kindedDamage.DamageKind is DamageKind.Fire or DamageKind.Cold)
                    && damage.CombatAction?.SpellInformation != null)
                {
                    wheel.StateCheckLayer = 1;
                    switch (damage.KindedDamages.FirstOrDefault(dk =>
                                dk.DamageKind is DamageKind.Fire or DamageKind.Cold)!.DamageKind)
                    {
                        case DamageKind.Fire:
                            damage.KindedDamages.Add(new KindedDamage(
                                DiceFormula.FromText(wheel.Value.ToString(), "Entropic Wheel"), DamageKind.Cold));
                            break;
                        case DamageKind.Cold:
                            damage.KindedDamages.Add(new KindedDamage(
                                DiceFormula.FromText(wheel.Value.ToString(), "Entropic Wheel"), DamageKind.Fire));
                            break;
                    }
                }

                return Task.CompletedTask;
            }
        };
    }
    private static QEffect Phased(Creature self)
    {
        return new QEffect
        {
            Illustration = IllustrationName.PhaseBolt,
            Name = "Out of Phase",
            Description = "Circumstance bonuses to AC are reduced by 1.",
            Source = self,
            ExpiresAt = ExpirationCondition.ExpiresAtStartOfSourcesTurn,
            YouAreTargeted = (effect, action) =>
            {
                if (action.ActiveRollSpecification == null ||
                    action.ActiveRollSpecification.TaggedDetermineDC.InvolvedDefense != Defense.AC) return Task.CompletedTask;
                if (action
                    .ActiveRollSpecification.DetermineDC.Invoke(action, action.Owner, effect.Owner).Bonuses.Any(bonus => bonus is {Amount: >= 1, BonusType: BonusType.Circumstance}))
                {
                    effect.Owner.AddQEffect(new QEffect()
                    {
                        BonusToDefenses = (_, _, defense) => defense == Defense.AC ? new Bonus(-1, BonusType.Untyped, "Out of Phase") : null,
                        Id = MQEffectIds.Phase
                    });
                }
                return Task.CompletedTask;
            },
            AfterYouAreTargeted = (effect, _) =>
            {
                if (effect.Owner.FindQEffect(MQEffectIds.Phase) is { } phase)
                    phase.ExpiresAt = ExpirationCondition.Immediately;
                return Task.CompletedTask;
            }
        };
    }

    private static TileQEffect Teleporter(Tile exit, bool amped, Creature owner)
    {
        return new TileQEffect
        {
            AfterCreatureEntersHere = async creature =>
            {
                if ( /*exit.PrimaryOccupant != null ||*/ creature.EnemyOf(owner) || (creature.AnimationData.LongMovement?.Path != null && !Equals(creature.AnimationData.LongMovement?.Path.LastOrDefault(), creature.Occupies))) return;
                if (await creature.AskForConfirmation(IllustrationName.DimensionDoor,
                        "Would you like to teleport to the exit portal?", "Yes"))
                    await CommonSpellEffects.Teleport(creature, exit);
                if (creature.FindQEffect(MQEffectIds.Complete) is  { } qff && creature.Speed > qff.Value)
                {
                    int move = qff.Owner.Speed -  qff.Value;
                    QEffect setSpeed = new QEffect() {BonusToAllSpeeds = _ => new Bonus(move - creature.Speed, BonusType.Untyped, "Finish Movement")};
                    qff.Owner.AddQEffect(setSpeed);
                    await qff.Owner.StrideAsync("Finish your stride?",
                        allowCancel: true);
                    setSpeed.ExpiresAt = ExpirationCondition.Immediately;
                    qff.Value = 0;
                }

                if (exit.AdditionalOccupant != null)
                {
                   await exit.AdditionalOccupant.SingleTileMove(exit.GetShuntoffTile(exit.AdditionalOccupant), null);
                }
            },
            Illustration = IllustrationName.DimensionDoor,
            Name = "Tesseract Entrance" + (amped ? " and Exit" : ""),
            VisibleDescription = "When you enter this tile you may teleport to the exit"+(amped ? ", this tile may also be teleported to from the exit." : "."),
        };
    }

    private static TileQEffect TeleportExit()
    {
        return new TileQEffect
        {
            Illustration = new TintedIllustration(IllustrationName.DimensionDoor, Color.Crimson),
            Name = "Tesseract Exit",
            VisibleDescription = "This is the exit for the Tesseract Tunnel."
        };
    }

    private static QEffect TunnelQf(CombatAction spell, TileQEffect entranceQf, TileQEffect exitQf, Creature self, Tile entrance, Tile exit, bool amped)
    {
        QEffect qEffect = new()
        {
            YouBeginAction = (effect, action) =>
            {
                if (action.SpellId != spell.SpellId) return Task.CompletedTask;
                entranceQf.ExpiresAt = ExpirationCondition.Immediately;
                exitQf.ExpiresAt = ExpirationCondition.Immediately;
                effect.ExpiresAt = ExpirationCondition.Immediately;
                return Task.CompletedTask;
            },
            CannotExpireThisTurn = true,
            WhenExpires = _ =>
            {
                entranceQf.ExpiresAt = ExpirationCondition.Immediately;
                exitQf.ExpiresAt = ExpirationCondition.Immediately;
            },
            Id = MQEffectIds.Tunnel,
            ProvideContextualAction = _ =>
            {
                if (!amped) return null;
                return new ActionPossibility(new CombatAction(self, spell.Illustration,
                    "Dismiss Tesseract Tunnel", [Trait.Concentrate],
                    "Close the tunnel, removing the portals.", Target.Self()).WithActionCost(1).WithEffectOnSelf(cr =>
                {
                    QEffect? effect = cr.FindQEffect(MQEffectIds.Tunnel);
                    if (effect != null) effect.ExpiresAt = ExpirationCondition.Immediately;
                }));
            },
            StateCheckWithVisibleChanges = qf =>
            {
                qf.AddGrantingOfTechnical(cr => cr.FriendOf(self), qfTech =>
                {
                    qfTech.StateCheckWithVisibleChanges = effect =>
                    {
                        if (!effect.Owner.HasEffect(MQEffectIds.Complete))
                            effect.Owner.AddQEffect(new QEffect()
                            {
                                Value = 0,
                                StateCheckWithVisibleChanges = qff =>
                                {
                                    Creature innerSelf = qff.Owner;
                                    if (innerSelf.AnimationData.LongMovement?.Path != null)
                                    {
                                        int move = 0;
                                        var diagonals = 0;
                                        for (var index = 0;
                                             index < innerSelf.AnimationData.LongMovement.Path
                                                 .Count;
                                             index++)
                                        {
                                            Tile tile =
                                                innerSelf.AnimationData.LongMovement.Path[index];
                                            var tiles = innerSelf.AnimationData.LongMovement.Path.ToList();
                                            if (tile.GetWalkDifficulty(innerSelf) >= 1)
                                                move += tile.GetWalkDifficulty(innerSelf);
                                            switch (index)
                                            {
                                                case >= 1 when tiles.Count > 1:
                                                {
                                                    if (Equals(tile.Neighbours.BottomLeft?.Tile,
                                                            tiles[index - 1]) ||
                                                        Equals(tile.Neighbours.BottomRight?.Tile,
                                                            tiles[index - 1]) ||
                                                        Equals(tile.Neighbours.TopLeft?.Tile,
                                                            tiles[index - 1]) ||
                                                        Equals(tile.Neighbours.TopRight?.Tile,
                                                            tiles[index - 1]))
                                                        diagonals += 1;
                                                    break;
                                                }
                                                case 0 when tiles.Count > 1:
                                                {
                                                    if (Equals(tile.Neighbours.BottomLeft?.Tile,
                                                            innerSelf.AnimationData.LongMovement.OriginalTile) ||
                                                        Equals(tile.Neighbours.BottomRight?.Tile,
                                                            innerSelf.AnimationData.LongMovement.OriginalTile) ||
                                                        Equals(tile.Neighbours.TopLeft?.Tile,
                                                            innerSelf.AnimationData.LongMovement.OriginalTile) ||
                                                        Equals(tile.Neighbours.TopRight?.Tile,
                                                            innerSelf.AnimationData.LongMovement.OriginalTile))
                                                        diagonals += 1;
                                                    break;
                                                }
                                            }
                                        }
                                        if (diagonals > 1) move += diagonals / 2;
                                        qff.Value = move;
                                    }
                                    return Task.CompletedTask;
                                },
                                AfterYouTakeAction = (qff, cAction) =>
                                {
                                    if (cAction.ActionId is ActionId.Stride
                                            or ActionId.StepByStepStride or ActionId.Step &&
                                        !Equals(qff.Owner.Occupies, exit) && !Equals(qff.Owner.Occupies, entrance))
                                        qff.Value = 0;
                                    return Task.CompletedTask;
                                },
                                Id = MQEffectIds.Complete,
                            });
                        return Task.CompletedTask;
                    };
                });
                return Task.CompletedTask;
            }
        };
        return qEffect;
    }
    private static CombatAction CreateSeek(IllustrationName illustrationName, string name, Creature self)
    {
      return new CombatAction(self, illustrationName, name, [Trait.Secret, Trait.Basic, Trait.IsNotHostile, Trait.DoesNotBreakStealth, Trait.AttackDoesNotTargetAC, Trait.UsesPerception],
          "Make a check against against the Stealth DCs of any undetected or hidden creatures in the area, and any hidden secrets or traps in the area." + 
          S.FourDegreesOfSuccess("The creature or trap stops being Undetected, and becomes Observed to you.", "If the creature is Undetected, it stops being Undetected; otherwise, if it's Hidden to you, it becomes Observed. A trap becomes Observed.", null, null), 
          Target.Emanation(6).WithIncludeOnlyIf((target, enemy) => enemy.EnemyOf(target.OwnerAction.Owner) && enemy.DetectionStatus.IsHiddenToAnEnemy))
          .WithActionId(ActionId.Seek)
          .WithActiveRollSpecification(new ActiveRollSpecification(Checks.Perception(), TaggedChecks.DefenseDC(Defense.Stealth)))
          .WithEffectOnEachTarget((_, caster, target, result) =>
          {
              switch (result)
          {
              case CheckResult.Success:
                  if (target.DetectionStatus.Undetected)
                  {
                      target.DetectionStatus.Undetected = false;
                      target.Overhead("detected", Color.Yellow, target + " is no longer undetected.");
                      break;
                  }
                  if (!target.DetectionStatus.HiddenTo.Remove(caster))
                      break;
                  target.Overhead("seen", Color.Black, $"{target} is no longer hidden to {caster}.");
                  break;
              case CheckResult.CriticalSuccess:
                  if (target.DetectionStatus.Undetected)
                  {
                      target.DetectionStatus.Undetected = false;
                      target.Overhead("detected", Color.Yellow, target + " is no longer undetected.");
                  }
                  if (!target.DetectionStatus.HiddenTo.Remove(caster))
                      break;
                  target.Overhead("seen", Color.Black, $"{target} is no longer hidden to {caster}.");
                  break;
          }

              return Task.CompletedTask;
          }).WithEffectOnChosenTargets(async (spell, caster, targets) =>
          {
              foreach (Tile tile in targets.ChosenTiles)
              {
                  foreach (TileQEffect tileQEffect in (IEnumerable<TileQEffect>) tile.TileQEffects)
                  {
                      if (tileQEffect.SeekDC != 0)
                      {
                          Creature noncombatCreature = Creature.CreateNoncombatCreature((Illustration) IllustrationName.DisarmTrap, "Something Hidden", (IList<Trait>) new List<Trait>(1)
                          {
                              Trait.Pseudocreature
                          });
                          noncombatCreature.Occupies = tile;
                          noncombatCreature.Battle = tile.Battle;
                          CheckBreakdown breakdown = CombatActionExecution.BreakdownAttack(new CombatAction(caster, (Illustration) IllustrationName.Seek, name,
                              [Trait.UsesPerception], "", (Target) Target.Self()).WithActionId(ActionId.Seek).WithActiveRollSpecification(new ActiveRollSpecification(Checks.Perception(), Checks.FlatDC(tileQEffect.SeekDC))), noncombatCreature);
                          CheckBreakdownResult breakdownResult = new CheckBreakdownResult(breakdown);
                          if (breakdownResult.CheckResult >= CheckResult.Success)
                          {
                              tile.TileOverhead(breakdownResult.CheckResult.HumanizeTitleCase2(), Color.LightBlue, $"{caster} rolls {breakdownResult.CheckResult.HumanizeTitleCase2()} on {name}.", name, breakdown.DescribeWithFinalRollTotal(breakdownResult));
                              await tileQEffect.WhenSeeked.InvokeIfNotNull();
                          }
                      }
                  }
              }
          });
    }
    private static string IfAmped(bool inCombat, string description)
    {
        return !inCombat ? $"\n\n{{Blue}}{{b}}Amp{{/b}} {description}{{/Blue}}" : "";
    }
}