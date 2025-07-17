using System.Drawing;
using Dawnsbury;
using Dawnsbury.Audio;
using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Coroutines.Options;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Targeting.TargetingRequirements;
using Dawnsbury.Core.Mechanics.Targeting.Targets;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Roller;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Display.Text;
using Dawnsbury.Modding;
using Color = Microsoft.Xna.Framework.Color;

namespace MorePsychicConscious;

public abstract class PsychicSpells : ModData
{
    public static void RegisterSpells()
    {
        SpellIds.Message = ModManager.RegisterNewSpell("Message", 0, (_, caster, spellLevel, _, spellInformation) =>
        {
            PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
            bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
            CombatAction message = Spells.CreateModern(IllustrationName.Divination, "Message", 
                    [Trait.Auditory, Trait.Cantrip, Trait.Concentrate, Trait.Illusion, Trait.Linguistic, Trait.Mental, Trait.Level1PsychicCantrip, Trait.Arcane, Trait.Occult, Trait.Divine, Trait.VerbalOnly], 
                    "You send a message of hope to one of your allies.", "One ally in range gains a +1 circumstance bonus to their next will saving throw before the start of your next turn.", Target.RangedFriend(spellLevel >= 3 ? 100 : 24).WithAdditionalConditionOnTargetCreature((self, target) => target == self ? Usability.NotUsableOnThisCreature("You cannot message yourself.") : Usability.Usable),spellLevel, null)
                .WithActionCost(1).WithHeightenedAtSpecificLevel(spellLevel, 3, false, "The spell's range increases to 500 feet.").WithEffectOnChosenTargets(async (_, targets) =>
                {
                    Creature ally = targets.ChosenCreature!;
                    ally.AddQEffect(new QEffect(ExpirationCondition.ExpiresAtStartOfYourTurn)
                    {
                        BeforeYourSavingThrow = (effect, action, self) =>
                        {
                            if (action.ActiveRollSpecification?.TaggedDetermineDC.InvolvedDefense == Defense.Will)
                            {
                                self.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                                {
                                    BonusToDefenses = (_, _, defense) =>
                                        defense == Defense.Will ? new Bonus(1, BonusType.Circumstance, "Message") : null,
                                });
                                effect.ExpiresAt = ExpirationCondition.Immediately;
                            }
                            return Task.CompletedTask;
                        },
                        Illustration = IllustrationName.Divination,
                        Name = "Message",
                        Description = "You gain a +1 circumstance bonus to your next will save."
                    });
                    if (amped)
                    {
                        List<string> optionNames = ["Cancel"];
                        if (!ally.HasEffect(QEffectId.Restrained) && !ally.HasEffect(QEffectId.Immobilized) && !ally.HasEffect(QEffectId.Paralyzed) && !ally.HasEffect(QEffectId.Grabbed) && ally.Speed > 0)
                            optionNames.Add("Step or Stride");
                        //create action
                        CombatAction? shove = Possibilities.Create(ally).Filter(ap =>
                        {
                            if (ap.CombatAction.ActionId != ActionId.Shove)
                                return false;
                            ap.CombatAction.ActionCost = 0;
                            ap.RecalculateUsability();
                            return true;
                        }).CreateActions(true).FirstOrDefault(pw => pw.Action.ActionId == ActionId.Shove) as CombatAction;
                        if (shove != null && spellLevel >= 4 && shove.CanBeginToUse(ally))
                            optionNames.Add("Shove");
                        CombatAction? trip = Possibilities.Create(ally).Filter(ap =>
                        {
                            if (ap.CombatAction.ActionId != ActionId.Trip)
                                return false;
                            ap.CombatAction.ActionCost = 0;
                            ap.RecalculateUsability();
                            return true;
                        }).CreateActions(true).FirstOrDefault(pw => pw.Action.ActionId == ActionId.Trip) as CombatAction;
                        if (trip != null && spellLevel >= 4 && trip.CanBeginToUse(ally))
                            optionNames.Add("Trip");
                        CombatAction strike = ally.CreateStrike(ally.PrimaryWeaponIncludingRanged!).WithActionCost(0);
                        if (CommonRulesConditions.CouldMakeStrike(ally, ally.PrimaryWeaponIncludingRanged!))
                            optionNames.Add("Strike");
                        ChoiceButtonOption chosenOption = await ally.AskForChoiceAmongButtons(IllustrationName.Reaction, spellLevel >= 4 ? "Choose to Step/Stride, Shove, Strike, Trip, or Cancel. Canceling will refund the focus point." : "Choose to Step/Stride or Cancel. Canceling will refund the focus point.", optionNames.ToArray());
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
                                if (!await ally.StrideAsync("Choose where to stride or step.", true, allowCancel: true))
                                    ++caster!.Spellcasting!.FocusPoints;
                                else ally.Actions.UseUpReaction();
                                break;
                        }
                    }
                });
            if (message.SpellInformation?.PsychicAmpInformation != null)
            {
                message.Target = new CreatureTarget(RangeKind.Ranged, new FriendCreatureTargetingRequirement(),
                    (_, _, _) => spellLevel >= 3 ? 100 : 24);
                message.Description += "\nYou don't need a straight line of effect or line of sight to cast message as long as you know the target's space and there is an unblocked path of 120 feet or less that can reach them. It also gains the following amp." +
                                       "\n\n\n{b}Amp{b} The target of the message can immediately spend its reaction to Step or Stride.\n{b}Amp Heightened (4th){/b} The target of the message can choose to Shove, Strike, or Trip with its reaction instead.";
                if (amped)
                {
                    message.Description = spellLevel >= 4 ? "One ally in range gains a +1 circumstance bonus to their next will saving throw the start of your next turn. That ally can then use a reaction to Step, Stride, Shove, Strike, or Trip." : "One ally in range gains a +1 circumstance bonus to their next will saving throw the start of your next turn. That ally can then use a reaction to Step or Stride.";
                    message.Target = new CreatureTarget(RangeKind.Ranged, new FriendCreatureTargetingRequirement(),
                        (_, _, _) => spellLevel >= 3 ? 100 : 24).WithAdditionalConditionOnTargetCreature((_, ally) =>
                        ally.Actions.CanTakeReaction()
                            ? Usability.Usable
                            : Usability.NotUsable("Ally cannot use a reaction."));
                }
            }
            return message;
        });
        SpellIds.ForbiddenThought = ModManager.RegisterNewSpell("Forbidden Thought", 0,
            (_, _, spellLevel, inCombat, spellInformation) =>
            {
                PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
                bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
                CombatAction forbiddenThought = Spells.CreateModern(IllustrationName.Confusion, "Forbidden Thought", [Trait.Cantrip, Trait.Enchantment, Trait.Mental, Trait.Level1PsychicCantrip, Trait.Psychic],
                    "You place a psychic lock in a foe's mind that prevents it from a specific action.", $"Choose “Strike,” “Stride,” “Cast a Spell,” or a specific action you know the creature to have (such as “Breath Weapon” against a dragon). If the creature attempts that action on its next turn, it must surmount your lock to do so, causing it to take {1+spellLevel}d6 mental damage (with a basic Will save). The target is then temporarily immune for 1 minute." +
                    $"\n\n{{b}}Amp{{/b}} Your telepathic push is particularly shocking to those who resist it. If the target fails its save, it's also stunned 1.",
                    Target.Ranged(6), spellLevel, null)
                    .WithActionCost(2).WithHeighteningOfDamageEveryLevel(spellLevel, 1, inCombat, "1d6").WithActionId(ActionIds.ForbiddenThought)
                    .WithEffectOnChosenTargets(async (spell, self, targets) =>
                    {
                        List<string> options = ["Cancel", "Strike", "Stride", "Cast a Spell", "Specific"];
                        List<string> specific = [];
                        Creature enemy = targets.ChosenCreature!;
                        foreach (ICombatAction combatAction in targets.ChosenCreature!.Possibilities.CreateActions(true).Where(action => action is CombatAction && !action.Action.Traits.Contains(Trait.Basic)))
                        {
                            CombatAction action = (CombatAction)combatAction;
                            specific.Add(action.Name);
                        }
                        if (specific.Count == 0)
                            options.Remove("Specific");
                        else specific.Add("Cancel");
                        ChoiceButtonOption chosenOption = await self.AskForChoiceAmongButtons(IllustrationName.QuestionMark, "Choose an option for forbidden thought", options.ToArray());
                        switch (options[chosenOption.Index])
                        {
                            case "Cancel":
                                if (amped) ++self.Spellcasting!.FocusPoints;
                                spell.RevertRequested = true;
                                break;
                            case "Specific":
                                ChoiceButtonOption specificOption = await self.AskForChoiceAmongButtons(IllustrationName.QuestionMark, "Choose a Specific Action", specific.ToArray());
                                if (specific[specificOption.Index] != "Cancel")
                                    enemy.AddQEffect(ForbiddenThought(spellLevel, specific[specificOption.Index], self, spell, amped));
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
                return forbiddenThought;
            });
        SpellIds.ShatterMind = ModManager.RegisterNewSpell("Shatter Mind", 0,
            (_, _, spellLevel, inCombat, spellInformation) =>
            {
                PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
                bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
                CombatAction shatterMind = Spells.CreateModern(IllustrationName.BrainDrain, "Shatter Mind", [Trait.Cantrip, Trait.Evocation, Trait.Mental, Trait.Psychic, Trait.Level1PsychicCantrip], "You telepathically assail the minds of your foes.",
                    "You deal 3d4 mental damage to all enemies in the area, with a basic Will save.", Target.Cone(amped ? 6 : 3), spellLevel, SpellSavingThrow.Basic(Defense.Will))
                    .WithActionCost(2).WithHeighteningOfDamageEveryLevel(spellLevel, 3, inCombat, amped ? "1d10" :"1d4");
                if (amped)
                {
                    shatterMind.WithVariants([
                    new SpellVariant("30ft", "30 foot cone", new SideBySideIllustration(IllustrationName.BrainDrain, new PlainTextPortraitIllustration(Color.Red, "30"))),
                    new SpellVariant("60ft", "60 foot cone", new SideBySideIllustration(IllustrationName.BrainDrain, new PlainTextPortraitIllustration(Color.Red, "60"))).WithNewTarget(Target.Cone(12).WithOverriddenFullTargetLine("60 foot cone"))
                    ]);
                }
                shatterMind.WithEffectOnEachTarget(async (spell, caster, target, result) =>
                {
                    await CommonSpellEffects.DealBasicDamage(spell, caster, target, result, amped ? spellLevel+"d10" : spellLevel+"d4", DamageKind.Mental);
                    if (result <= CheckResult.Failure && amped) target.AddQEffect(QEffect.Stupefied(result == CheckResult.CriticalFailure ? 2 : 1).WithExpirationAtStartOfSourcesTurn(caster, 1));
                });
                return shatterMind;
            });
        ModManager.ReplaceExistingSpell(SpellId.Daze, 0, (owner, level, inCombat, spellInformation) =>
        {
            PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
            bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
            CombatAction daze = Spells.CreateModern(IllustrationName.Daze, "Daze",
                    [Trait.Cantrip, Trait.Enchantment, Trait.Mental, Trait.Nonlethal, Trait.Arcane, Trait.Divine,
                        Trait.Occult, Trait.Level1PsychicCantrip],
                    "You cloud the target's mind and daze it with a mental jolt.",
                    $"Deal {(level >= 3 ? S.HeightenedVariable((level - 1) / 2, 0) + "d6+" : "")}{S.SpellcastingModifier(owner, spellInformation)} mental damage.\n\nIf the target critically fails the save, it is also stunned 1.",
                    Target.Ranged(12), level, SpellSavingThrow.Basic(Defense.Will))
                .WithSoundEffect(SfxName.Mental)
                .WithHeighteningNumerical(level, 1, inCombat, 2, amped ? "The damage increases by 1d10" : "The damage increases by 1d6.")
                .WithGoodness((target, _, _) => target.OwnerAction.SpellcastingSource!.SpellcastingAbilityModifier + 1)
                .WithEffectOnEachTarget(async (power, self, target, result) =>
                {
                    int num = power.SpellcastingSource!.SpellcastingAbilityModifier;
                    DiceFormula diceFormula1 = DiceFormula.FromText(num.ToString(), "Spellcasting ability modifier");
                    if (power.SpellLevel >= 3)
                    {
                        ComplexDiceFormula complexDiceFormula = new();
                        List<DiceFormula> list = complexDiceFormula.List;
                        num = (level - 1) / 2;
                        DiceFormula diceFormula2 = DiceFormula.FromText(num + (amped ? "d10" : "d6"), "Daze");
                        list.Add(diceFormula2);
                        complexDiceFormula.List.Add(diceFormula1);
                        diceFormula1 = complexDiceFormula;
                    }
                    if (result <= CheckResult.Failure && amped)
                    {
                        target.AddQEffect(QEffect.DamageWeakness(DamageKind.Mental, (result == CheckResult.CriticalFailure ? 2 : 0)+(level+1)/2).WithExpirationAtEndOfOwnerTurn());
                    }
                    await CommonSpellEffects.DealBasicDamage(power, self, target, result, diceFormula1,
                        DamageKind.Mental);
                    if (result != CheckResult.CriticalFailure)
                        return;
                    target.AddQEffect(QEffect.Stunned(1));
                });
            if (daze.SpellInformation?.PsychicAmpInformation != null)
            {
                daze.Target = Target.Ranged(24);
                daze.Description += "\n\n{b}Amp{/b} Your spell cracks the target's mental defenses, leaving it susceptible to further psychic attack. The spell's damage changes to 1d10. If the target fails its Will save, until the end of its next turn, it gains weakness 1 to mental damage and takes a –1 status penalty to Will saves. On a critical failure, the weakness is 3 (in addition to the target being stunned 1). The weakness applies before daze deals damage.\nAmp Heightened (+2) The spell's damage increases by 2d10, and the weakness on a failure or critical failure increases by 1.";
            }
            return daze;
        });
    }
    //use for specific actions
    private static QEffect ForbiddenThought(int spellLevel, string actionName, Creature caster, CombatAction spell, bool amped)
    {
        return new QEffect(ExpirationCondition.ExpiresAtEndOfYourTurn)
        {
            YouBeginAction = (effect, combatAction) =>
            {
                Creature self = effect.Owner;
                if (combatAction.Name == actionName)
                {
                    CheckResult savingThrow = CommonSpellEffects.RollSpellSavingThrow(self, spell, Defense.Will);
                    CommonSpellEffects.DealBasicDamage(spell, caster, self, savingThrow, 1+spellLevel+"d6", DamageKind.Mental);
                    if (amped && savingThrow <= CheckResult.Failure)
                        self.AddQEffect(QEffect.Stunned(1));
                    self.AddQEffect(QEffect.ImmunityToTargeting(ActionIds.ForbiddenThought));
                    self.AddQEffect(QEffect.ImmunityToCondition(QEffectIds.ForbiddenThought));
                    effect.ExpiresAt = ExpirationCondition.Ephemeral;
                }
                return Task.CompletedTask;
            },
            Illustration = IllustrationName.Confusion,
            Name = "Forbidden Thought",
            Description = $"If the target uses a specified action, it will take {1+spellLevel}d6 damage (with a basic Will save).",
            Id = QEffectIds.ForbiddenThought
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
                    CommonSpellEffects.DealBasicDamage(spell, caster, self, savingThrow, 1+spellLevel+"d6", DamageKind.Mental);
                    if (amped && savingThrow <= CheckResult.Failure)
                        self.AddQEffect(QEffect.Stunned(1));
                    self.AddQEffect(QEffect.ImmunityToTargeting(ActionIds.ForbiddenThought));
                    self.AddQEffect(QEffect.ImmunityToCondition(QEffectIds.ForbiddenThought));
                    effect.ExpiresAt = ExpirationCondition.Ephemeral;
                }
                return Task.CompletedTask;
            },
            Illustration = IllustrationName.Confusion,
            Name = "Forbidden Thought",
            Description = $"If the target uses a specified action, it will take {1+spellLevel}d6 damage (with a basic Will save).",
            Id = QEffectIds.ForbiddenThought
        };
    }
    //use for strikes
    private static QEffect ForbiddenThought(int spellLevel,Trait trait, Creature caster, CombatAction spell, bool amped)
    {
        return new QEffect(ExpirationCondition.ExpiresAtEndOfYourTurn)
        {
            YouBeginAction = (effect, combatAction) =>
            {
                Creature self = effect.Owner;
                if (combatAction.Traits.Contains(trait))
                {
                    CheckResult savingThrow = CommonSpellEffects.RollSpellSavingThrow(self, spell, Defense.Will);
                    CommonSpellEffects.DealBasicDamage(spell, caster, self, savingThrow, 1+spellLevel+"d6", DamageKind.Mental);
                    if (amped && savingThrow <= CheckResult.Failure)
                        self.AddQEffect(QEffect.Stunned(1));
                    self.AddQEffect(QEffect.ImmunityToTargeting(ActionIds.ForbiddenThought));
                    self.AddQEffect(QEffect.ImmunityToCondition(QEffectIds.ForbiddenThought));
                    effect.ExpiresAt = ExpirationCondition.Ephemeral;
                }
                return Task.CompletedTask;
            },
            Illustration = IllustrationName.Confusion,
            Name = "Forbidden Thought",
            Description = $"If the target uses a specified action, it will take {1+spellLevel}d6 damage (with a basic Will save).",
            Id = QEffectIds.ForbiddenThought
        };
    }
    //use for stride
    private static QEffect ForbiddenThought(int spellLevel, ActionId actionId,Creature caster, CombatAction spell, bool amped)
    {
        return new QEffect(ExpirationCondition.ExpiresAtEndOfYourTurn)
        {
            YouBeginAction = (effect, combatAction) =>
            {
                Creature self = effect.Owner;
                if (combatAction.ActionId == actionId || combatAction.ActionId == ActionId.StepByStepStride)
                {
                    CheckResult savingThrow = CommonSpellEffects.RollSpellSavingThrow(self, spell, Defense.Will);
                    CommonSpellEffects.DealBasicDamage(spell, caster, self, savingThrow, 1+spellLevel+"d6", DamageKind.Mental);
                    if (amped && savingThrow <= CheckResult.Failure)
                        self.AddQEffect(QEffect.Stunned(1));
                    self.AddQEffect(QEffect.ImmunityToTargeting(ActionIds.ForbiddenThought));
                    self.AddQEffect(QEffect.ImmunityToCondition(QEffectIds.ForbiddenThought));
                    effect.ExpiresAt = ExpirationCondition.Ephemeral;
                }
                return Task.CompletedTask;
            },
            Illustration = IllustrationName.Confusion,
            Name = "Forbidden Thought",
            Description = $"If the target uses a specified action, it will take {1+spellLevel}d6 damage (with a basic Will save).",
            Id = QEffectIds.ForbiddenThought
        };
    }
}