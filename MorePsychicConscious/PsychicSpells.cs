using Dawnsbury.Audio;
using Dawnsbury.Core;
using Dawnsbury.Core.Animations;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Coroutines.Options;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Damage;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Targeting.TargetingRequirements;
using Dawnsbury.Core.Mechanics.Targeting.Targets;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Roller;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Display.Text;
using Dawnsbury.Modding;

namespace MorePsychicConscious;

public abstract class PsychicSpells : ModData
{
    public static void RegisterSpells()
    {
        MSpellIds.Message = ModManager.RegisterNewSpell("Message", 0, (_, caster, spellLevel, inCombat, spellInformation) =>
        {
            PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
            bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
            CombatAction message = Spells.CreateModern(IllustrationName.Divination, "Message", 
                    [Trait.Auditory, Trait.Cantrip, Trait.Concentrate, Trait.Illusion, Trait.Linguistic, Trait.Mental, Trait.Level1PsychicCantrip, Trait.Arcane, Trait.Occult, Trait.Divine, Trait.VerbalOnly], 
                    "You send a message of hope to one of your allies.", "One ally in range gains a +1 circumstance bonus to their next will saving throw before the start of your next turn."+(psychicAmpInformation != null ? "\nYou don't need a straight line of effect or line of sight to cast message as long as you know the target's space and there is an unblocked path of 120 feet or less that can reach them. It also gains the following amp." +
                        "\n\n\n{b}Amp{b} The target of the message can immediately spend its reaction to Step or Stride.\n{b}Amp Heightened (4th){/b} The target of the message can choose to Shove, Strike, or Trip with its reaction instead." : ""), Target.RangedFriend(spellLevel >= 3 ? 100 : 24).WithAdditionalConditionOnTargetCreature((self, target) => target == self ? Usability.NotUsableOnThisCreature("You cannot message yourself.") : Usability.Usable),spellLevel, null)
                .WithActionCost(1).WithHeightenedAtSpecificLevel(spellLevel, 3, inCombat, "The spell's range increases to 500 feet.").WithEffectOnChosenTargets(async (_, targets) =>
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
                        Description = "You gain a +1 circumstance bonus to your next will save.",
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
            message.PsychicAmpInformation = spellInformation.PsychicAmpInformation;
            if (spellInformation.PsychicAmpInformation != null)
            {
                message.Traits.Add(Trait.Psi);
                message.Target = new CreatureTarget(RangeKind.Ranged, new FriendCreatureTargetingRequirement(),
                    (_, _, _) => spellLevel >= 3 ? 100 : 24);
                if (amped)
                {
                    message.Target = new CreatureTarget(RangeKind.Ranged, new FriendCreatureTargetingRequirement(),
                        (_, _, _) => spellLevel >= 3 ? 100 : 24).WithAdditionalConditionOnTargetCreature((_, ally) =>
                        ally.Actions.CanTakeReaction()
                            ? Usability.Usable
                            : Usability.NotUsable("Ally cannot use a reaction."));
                }
            }
            return message;
        });
        MSpellIds.ForbiddenThought = ModManager.RegisterNewSpell("Forbidden Thought", 0,
            (_, _, spellLevel, inCombat, spellInformation) =>
            {
                PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
                bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
                CombatAction forbiddenThought = Spells.CreateModern(IllustrationName.Confusion, "Forbidden Thought", [Trait.Cantrip, Trait.Enchantment, Trait.Mental, Trait.Level1PsychicCantrip, Trait.Psychic],
                    "You place a psychic lock in a foe's mind that prevents it from a specific action.", $"Choose “Strike,” “Stride,” “Cast a Spell,” or a specific action you know the creature to have (such as “Breath Weapon” against a dragon). If the creature attempts that action on its next turn, it must surmount your lock to do so, causing it to take {S.HeightenedVariable(1+spellLevel, 0)+"d6"} mental damage (with a basic Will save). The target is then temporarily immune for 1 minute." +
                    $"\n\n{{b}}Amp{{/b}} Your telepathic push is particularly shocking to those who resist it. If the target fails its save, it's also stunned 1.",
                    Target.Ranged(6), spellLevel, null)
                    .WithActionCost(2).WithHeighteningOfDamageEveryLevel(spellLevel, 1, inCombat, "1d6").WithActionId(MActionIds.ForbiddenThought)
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
                        specific.Add("Cancel");
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
                CombatAction shatterMind = Spells.CreateModern(IllustrationName.BrainDrain, "Shatter Mind", [Trait.Cantrip, Trait.Evocation, Trait.Mental, Trait.Psychic, Trait.Level1PsychicCantrip], "You telepathically assail the minds of your foes.",
                    $"Deal {S.HeightenedVariable(spellLevel, 0) + (amped ? "d10":"d4")} mental damage to all enemies in the area, with a basic Will save." +
                    $"\n\n{{b}}Amp{{/b}} You increase the area of the spell to your choice of a 30-foot cone or 60-foot cone (use the toggle in other maneuvers), and the damage dice for the spell change to d10s. Creatures that fail are stupefied 1 until the start of your next turn (or stupefied 2 on a critical failure).", Target.Cone(amped ? owner != null && owner.HasEffect(MQEffectIds.ShatterMind60) ? 12 : 6 : 3), spellLevel, SpellSavingThrow.Basic(Defense.Will))
                    .WithActionCost(2).WithHeighteningOfDamageEveryLevel(spellLevel, 3, inCombat, amped ? "1d10" :"1d4");
                shatterMind.WithEffectOnEachTarget(async (spell, caster, target, result) =>
                {
                    await CommonSpellEffects.DealBasicDamage(spell, caster, target, result, amped ? spellLevel+"d10" : spellLevel+"d4", DamageKind.Mental);
                    if (result <= CheckResult.Failure && amped) target.AddQEffect(QEffect.Stupefied(result == CheckResult.CriticalFailure ? 2 : 1).WithExpirationAtStartOfSourcesTurn(caster, 1));
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
                        $"Deal {(amped ? S.HeightenedVariable(level < 3 ? 1 : level < 5 ? 3 : level < 7 ? 5 : level < 9 ? 7 : 9, 0) + "d10" : (level >= 3 ? S.HeightenedVariable((level - 1) / 2, 0) + "d6+" : "") + S.SpellcastingModifier(owner, spellInformation))} mental damage.\n\nIf the target critically fails the save, it is also stunned 1."
                        + (psychicAmpInformation != null
                            ? "\n\n{b}Amp{/b} Your spell cracks the target's mental defenses, leaving it susceptible to further psychic attack. The spell's damage changes to 1d10. If the target fails its Will save, until the end of its next turn, it gains weakness 1 to mental damage and takes a –1 status penalty to Will saves. On a critical failure, the weakness is 3 (in addition to the target being stunned 1). The weakness applies before daze deals damage." +
                              "\n{b}Amp Heightened (+2){/b} The spell's damage increases by 2d10, and the weakness on a failure or critical failure increases by 1."
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
                            int num2 = level < 5 ? 2 : level < 7 ? 4 : level < 9 ? 6 : 8;
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
                daze.Target = Target.Ranged(24);
                return daze;
            });
        }
        else
        {
            MSpellIds.AltDaze = ModManager.RegisterNewSpell("PsiDaze", 0, (_, _, level, inCombat, spellInformation) =>
                {
                PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
                bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
                CombatAction daze = Spells.CreateModern(IllustrationName.Daze, "Daze",
                        [Trait.Cantrip, Trait.Enchantment, Trait.Mental, Trait.Nonlethal, Trait.Level1PsychicCantrip],
                        "You cloud the target's mind and daze it with a mental jolt.",
                        $"Deal {(amped ? S.HeightenedVariable(level < 3 ? 1 : level < 5 ? 3 : level < 7 ? 5 : level < 9 ? 7 : 9, 0) + "d10" : S.HeightenedVariable((level + 1) / 2, 0) + "d6")} mental damage.\n\nIf the target critically fails the save, it is also stunned 1."
                        + (psychicAmpInformation != null
                            ? "\n\n{b}Amp{/b} Your spell cracks the target's mental defenses, leaving it susceptible to further psychic attack. The spell's damage changes to 1d10. If the target fails its Will save, until the end of its next turn, it gains weakness 1 to mental damage and takes a –1 status penalty to Will saves. On a critical failure, the weakness is 3 (in addition to the target being stunned 1). The weakness applies before daze deals damage." +
                              "\n{b}Amp Heightened (+2){/b} The spell's damage increases by 2d10, and the weakness on a failure or critical failure increases by 1."
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
                            var num2 = level < 5 ? 2 : level < 7 ? 4 : level < 9 ? 6 : 8;
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
                daze.Target = Target.Ranged(24);
                return daze;
            });
        }
        ModManager.ReplaceExistingSpell(SpellId.ProduceFlame, 0 , (owner, level, inCombat, spellInformation) =>
        {
            PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
            bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
            CombatAction produceFlame = Spells.CreateModern(IllustrationName.ProduceFlame, "Produce Flame", [Trait.Attack, Trait.Cantrip, Trait.Evocation, Trait.Fire, Trait.Arcane, Trait.Primal, Trait.VersatileMelee, Trait.Level1PsychicCantrip], 
                "A small ball of flame appears in the palm of your hand, and you lash out with it.", 
                $"Make a spell attack roll. The flame deals {S.HeightenedVariable(level, 1)}{(amped ? "d10" : "d4+")}{(!amped ? S.SpellcastingModifier(owner, spellInformation) : "")} fire damage.{S.FourDegreesOfSuccessReverse(null, null, "Full damage.", $"Double damage, and {S.HeightenedVariable(level, 1)}d4 persistent fire damage.")}\n\n{{b}}Special: Versatile Melee.{{/b}} If you're adjacent to the target, this becomes a melee spell attack and benefits from flanking."
                + (psychicAmpInformation != null
                    ? " When using produce flame as a melee attack, increase the damage dice of the initial damage (but not the persistent damage) from d4s to d6s.\nYour produce flame also gains the following amp." +
                      "\n\n{b}Amp{/b} You project pure heat that causes a target to combust. The initial damage changes to 1d10 fire damage (not adding your ability modifier) plus 1 fire splash damage. When using amped produce flame as a melee attack, increase the damage dice of the initial damage from d10s to d12s. You are not harmed by splash damage from amped produce flame." +
                      "\n{b}Amp Heightened (+1){/b} Instead of using produce flame's normal heightened entry, the initial damage increases by 1d10 (1d12 for melee) and the splash damage increases by 1. The persistent fire damage on a critical hit increases by 1d4."
                    : ""), 
                Target.Ranged(6), level, null)
                .WithSpellAttackRoll().WithSoundEffect(SfxName.FireRay)
                .WithHeighteningNumerical(level, 1, inCombat, 1, "Increase the damage by 1d4 and the persistent damage on a critical hit by 1d4.")
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
                    await CommonSpellEffects.DealAttackRollDamage(spell, caster, target, result, diceExpression, DamageKind.Fire);
                    if (amped && result >= CheckResult.Failure)
                    {
                        foreach (Creature enemy in target.Battle.AllCreatures.Where(v => v != caster && (v.IsAdjacentTo(target) || v == target)))
                        {
                            await CommonSpellEffects.DealDirectSplashDamage(spell, DiceFormula.FromText(level.ToString(), "Amped Produce Flame"), enemy, DamageKind.Fire);
                        }
                    }
                    if (result != CheckResult.CriticalSuccess)
                        return;
                    target.AddQEffect(QEffect.PersistentDamage(spell.SpellLevel + "d4", DamageKind.Fire));
                });
            produceFlame.PsychicAmpInformation = spellInformation.PsychicAmpInformation;
            if (spellInformation.PsychicAmpInformation == null) return produceFlame;
            produceFlame.Traits.Add(Trait.Psi);
            return produceFlame;
        });
        ModManager.ReplaceExistingSpell(SpellId.RayOfFrost, 0, (owner, level, inCombat, spellInformation) =>
        {
            PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
            bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
            CombatAction rayOfFrost = Spells.CreateModern(IllustrationName.RayOfFrost, "Ray of Frost", [Trait.Attack, Trait.Cantrip, Trait.Cold, Trait.Evocation, Trait.Arcane, Trait.Primal],
                "You blast an icy ray.", "Make a spell attack roll." + S.FourDegreesOfSuccessReverse(null, null, $"{S.HeightenedVariable(level, 1)}{(amped ? "d10" : "d4+")}{(!amped ? S.SpellcastingModifier(owner, spellInformation) : "")} cold damage.", "Double damage, and the target takes a –10-foot status penalty to its Speeds for 1 round.")
                + (psychicAmpInformation != null
                    ? "\n\nYour ray of frost reaches even further. The range increases to 180 feet. Your ray of frost also gains the following amp." +
                      "\n\n{b}Amp{/b} You drain thermal energy at a distance, using what you plunder to replenish yourself. The ray deals 1d10 cold damage. You gain temporary Hit Points equal to half the damage the target takes (after applying resistances and the like). You lose any remaining temporary Hit Points after 1 minute." +
                      "\n{b}Amp Heightened (+1){/b} The initial damage increases by 1d10 instead of 1d4."
                    : ""), 
                Target.Ranged(psychicAmpInformation != null ? 36 : 24), level, null).WithSpellAttackRoll().WithSoundEffect(SfxName.RayOfFrost).WithGoodness((tg, _, _) => level * 2.5f + tg.OwnerAction.SpellcastingSource!.SpellcastingAbilityModifier).WithProjectileCone(IllustrationName.RayOfFrost, 15, ProjectileKind.Ray)
                .WithEffectOnEachTarget(async (action, caster, target, checkResult) =>
                {
                    int spellcastingAbilityModifier = action.SpellcastingSource!.SpellcastingAbilityModifier;
                    if (amped && checkResult >= CheckResult.Success) target.AddQEffect(new QEffect
                    {
                       AfterYouTakeAmountOfDamageOfKind = (effect, combatAction, amount, _) =>
                       {
                           if (combatAction!.SpellId == action.SpellId)
                           {
                               caster.GainTemporaryHP(amount/2);
                               effect.ExpiresAt = ExpirationCondition.Ephemeral;
                           }
                           return Task.CompletedTask;
                       }
                    });
                    await CommonSpellEffects.DealAttackRollDamage(action, caster, target, checkResult, DiceFormula.FromText($"{level.ToString()}{(amped ? "d10" : "d4+")}{(!amped ? spellcastingAbilityModifier.ToString() : "")}", "Ray of Frost"), DamageKind.Cold);
                    if (checkResult != CheckResult.CriticalSuccess)
                        return;
                    target.AddQEffect(new QEffect("Speed reduced", "You have a -10-foot status penalty to your Speeds for 1 round.", ExpirationCondition.CountsDownAtStartOfSourcesTurn, caster, (Illustration) IllustrationName.RayOfFrost)
                    {
                        BonusToAllSpeeds = (Func<QEffect, Bonus>) (_ => new Bonus(-2, BonusType.Circumstance, "ray of frost"))
                    });
                }).WithHeighteningNumerical(level, 1, inCombat, 1, "The damage increases by 1d4.");
            rayOfFrost.PsychicAmpInformation = spellInformation.PsychicAmpInformation;
            if (spellInformation.PsychicAmpInformation == null) return rayOfFrost;
            rayOfFrost.Traits.Add(Trait.Psi);
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
                        $"The target gains resistance {2 * spellLevel} against fire damage and resistance {2 * spellLevel} against cold damage until the start of your next turn.\n\n{{b}}Amp{{/b}} The spell's duration becomes 1 minute.",
                        Target.RangedFriend(12), spellLevel, null)
                    .WithActionCost(1).WithHeighteningNumerical(spellLevel, 1, inCombat, 1, "Each resistance increases by 2.")
                    .WithEffectOnEachTarget((_, creature, target, _) =>
                    {
                        QEffect resist = QEffect.DamageResistance(DamageKind.Fire, spellLevel * 2).WithExpirationAtStartOfSourcesTurn(creature, amped ? 10 : 1);
                        resist.Illustration = new SideBySideIllustration(IllustrationName.ResistFire, IllustrationName.ResistCold);
                        resist.Name = "Thermal Stasis";
                        resist.Description = $"You have resistance {2 * spellLevel} against fire damage and resistance {2 * spellLevel} against cold damage";
                        target.AddQEffect(resist);
                        target.AddQEffect(QEffect.DamageResistance(DamageKind.Cold, spellLevel * 2).WithExpirationAtStartOfSourcesTurn(creature, amped ? 10 : 1));
                        return Task.CompletedTask;
                    });
                thermalStasis.PsychicAmpInformation = spellInformation.PsychicAmpInformation;
                if (spellInformation.PsychicAmpInformation == null) return thermalStasis;
                thermalStasis.Traits.Add(Trait.Psi);
                return thermalStasis;
            });
        MSpellIds.EntropicWheel = ModManager.RegisterNewSpell("EntropicWheel", 0,
            (_, _, spellLevel, _, spellInformation) =>
            {
                PsychicAmpInformation? psychicAmpInformation = spellInformation.PsychicAmpInformation;
                bool amped = (psychicAmpInformation != null ? (psychicAmpInformation.Amped ? 1 : 0) : 0) != 0;
                CombatAction entropicWheel = Spells.CreateModern(MIllustrations.FireColdWheel, "Entropic Wheel", [Trait.Cantrip, Trait.Cold, Trait.Evocation, Trait.Fire, Trait.Psychic, Trait.VerbalOnly], "You stockpile thermal energy in a magical wheel-like construct that lets you burn opponents with cold or freeze their bodies with heat.", 
                    "Cast as a reaction after dealing cold or fire damage.\nWhen you Cast the Spell, the wheel has one mote of thermal energy, and when you use a cold or fire effect or deal cold or fire damage, the wheel spins, siphoning off a bit of energy and gaining another mote. The wheel can't gain motes more than once on a given turn, and the maximum number of motes is equal to entropic wheel's level.\n\nWhen you Cast a Spell that deals fire damage, the target also takes cold damage equal to the number of motes in the entropic wheel. When you Cast a Spell that deals cold damage, the target also takes fire damage equal to the number of motes in the entropic wheel. This applies only to the initial damage of the spell, not to any persistent damage or ongoing effects." +
                    "\n\n{b}Amp{/b} You gain two motes instead of one when you Cast the Spell and each time the wheel gains another mote.", /*Target.Self().WithAdditionalRestriction(cr => !cr.Actions.CanTakeReaction() ? "You must be able to take reactions" : !cr.HasEffect(QEffectIds.EntropicStart) ? "The last action you have taken must have dealt cold or fire damage." : null)*/ Target.Uncastable(), spellLevel, null)
                    .WithActionCost(-2)
                    .WithEffectOnSelf((_, self) =>
                    {
                        self.AddQEffect(EntropicWheel(amped ? 2 : 1, amped, spellLevel));
                        self.Actions.UseUpReaction();
                        return Task.CompletedTask;
                    });
                entropicWheel.PsychicAmpInformation = spellInformation.PsychicAmpInformation;
                if (spellInformation.PsychicAmpInformation == null) return entropicWheel;
                entropicWheel.Traits.Add(Trait.Psi);
                return entropicWheel;
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
                    self.AddQEffect(QEffect.ImmunityToTargeting(MActionIds.ForbiddenThought));
                    self.AddQEffect(QEffect.ImmunityToCondition(MQEffectIds.ForbiddenThought));
                    effect.ExpiresAt = ExpirationCondition.Ephemeral;
                }
                return Task.CompletedTask;
            },
            Illustration = IllustrationName.Confusion,
            Name = "Forbidden Thought - "+actionName,
            Description = $"If the target uses the specified action, it will take {1+spellLevel}d6 damage (with a basic Will save).",
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
                    CommonSpellEffects.DealBasicDamage(spell, caster, self, savingThrow, 1+spellLevel+"d6", DamageKind.Mental);
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
            Description = $"If the target uses the specified action, it will take {1+spellLevel}d6 damage (with a basic Will save).",
            Id = MQEffectIds.ForbiddenThought
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
                    self.AddQEffect(QEffect.ImmunityToTargeting(MActionIds.ForbiddenThought));
                    self.AddQEffect(QEffect.ImmunityToCondition(MQEffectIds.ForbiddenThought));
                    effect.ExpiresAt = ExpirationCondition.Ephemeral;
                }
                return Task.CompletedTask;
            },
            Illustration = IllustrationName.Confusion,
            Name = "Forbidden Thought - Strike",
            Description = $"If the target uses the specified action, it will take {1+spellLevel}d6 damage (with a basic Will save).",
            Id = MQEffectIds.ForbiddenThought
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
                    self.AddQEffect(QEffect.ImmunityToTargeting(MActionIds.ForbiddenThought));
                    self.AddQEffect(QEffect.ImmunityToCondition(MQEffectIds.ForbiddenThought));
                    effect.ExpiresAt = ExpirationCondition.Ephemeral;
                }
                return Task.CompletedTask;
            },
            Illustration = IllustrationName.Confusion,
            Name = "Forbidden Thought - Stride",
            Description = $"If the target uses the specified action, it will take {1+spellLevel}d6 damage (with a basic Will save).",
            Id = MQEffectIds.ForbiddenThought
        };
    }

    private static QEffect EntropicWheel(int motes, bool amped, int spellLevel)
    {
        return new QEffect()
        {
            Id = MQEffectIds.EntropicWheel,
            Name = "Entropic Wheel",
            Value = motes,
            Description = "When you Cast the Spell, the wheel has one mote of thermal energy, and when you use a cold or fire effect or deal cold or fire damage, the wheel spins, siphoning off a bit of energy and gaining another mote. The wheel can't gain motes more than once on a given turn, and the maximum number of motes is equal to entropic wheel's level." +
                          "\n\nWhen you Cast a Spell that deals fire damage, the target also takes cold damage equal to the number of motes in the entropic wheel. When you Cast a Spell that deals cold damage, the target also takes fire damage equal to the number of motes in the entropic wheel. This applies only to the initial damage of the spell, not to any persistent damage or ongoing effects.",
            Illustration = MIllustrations.ColdFireWheel,
            AfterYouTakeAction = (wheel, action) =>
            {
                if ((action.HasTrait(Trait.Fire) || action.HasTrait(Trait.Cold)) && !wheel.Owner.HasEffect(MQEffectIds.EntropicBlock) && wheel.Value < spellLevel)
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
                if (damage.KindedDamages.Any(kindedDamage => kindedDamage.DamageKind is DamageKind.Fire or DamageKind.Cold)
                    && damage.CombatAction?.SpellInformation != null)
                {
                    wheel.StateCheckLayer = 1;
                    switch (damage.KindedDamages.FirstOrDefault(dk => dk.DamageKind is DamageKind.Fire or DamageKind.Cold)!.DamageKind)
                    {
                        case DamageKind.Fire:
                            damage.KindedDamages.Add(new KindedDamage(DiceFormula.FromText(wheel.Value.ToString(), "Entropic Wheel"), DamageKind.Cold));
                            break;
                        case DamageKind.Cold:
                            damage.KindedDamages.Add(new KindedDamage(DiceFormula.FromText(wheel.Value.ToString(), "Entropic Wheel"), DamageKind.Fire));
                            break;
                    }
                }
                return Task.CompletedTask;
            }

        };
    }
}