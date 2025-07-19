using Dawnsbury.Audio;
using Dawnsbury.Auxiliary;
using Dawnsbury.Core;
using Dawnsbury.Core.Animations;
using Dawnsbury.Core.CharacterBuilder;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb.Archetypes;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Coroutines.Options;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Damage;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Roller;
using Dawnsbury.Display;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Display.Text;
using Dawnsbury.Modding;
using Microsoft.Xna.Framework;
using static MorePsychicConscious.ConsciousMindFeat;

namespace MorePsychicConscious;

public abstract class ConsciousMind :  ModData
{
    private static Feat SilentWhisperFeat {get; set;} = null!;
    private static Feat OscillatingWave { get; set; } = null!;

    internal static IEnumerable<Feat> GetFeats()
    {
        SilentWhisperFeat  = CreateConsciousMindFeat2(MFeatNames.SilentWhisper, "Every mind murmurs constantly, and the static from those nearby reverberates in your head like wind through leaves. What better focus for your magic then, than the very thoughts of those around you? Your versatile telepathic abilities let you soothe and communicate with your allies or control your enemies.",
            [SpellId.Command, SpellId.TouchOfIdiocy, SpellId.Heroism, SpellId.Confusion, SpellId.SynapticPulse], MSpellIds.Message, ModManager.TryParse("Remaster", out Trait _) ? MSpellIds.AltDaze : SpellId.Daze, MSpellIds.ForbiddenThought, MSpellIds.ShatterMind);
        yield return SilentWhisperFeat;
        OscillatingWave = CreateConsciousMindFeat2(MFeatNames.OscillatingWave,
            "At the heart of all things is energy. Though it may change its form or resting place, it is eternal, fundamental, the purest of building blocks. Your powers let you shift energy—either concentrating it in a single point to explosive end or freezing objects by plundering it away— in an endless oscillation of temperature.",
            [SpellId.BurningHands, SpellId.ScorchingRay, SpellId.Fireball, SpellId.FireShield, SpellId.ConeOfCold], SpellId.RayOfFrost, SpellId.ProduceFlame, MSpellIds.ThermalStasis, MSpellIds.EntropicWheel);
        CreateWaveLogic(OscillatingWave);
        yield return OscillatingWave;
        yield return new TrueFeat(ModManager.RegisterFeatName("PsiDevelopmentAlt", "Psi Development - Standard Cantrip"), 6, "You've found a new mental form.", "You gain the standard psi cantrip you didn't select from your conscious mind. You gain the benefits and the amp for this second psi cantrip. Increase the number of Focus Points in your focus pool by 1.", [])
            .WithAvailableAsArchetypeFeat(Trait.Psychic).WithPrerequisite(values => values.Tags.ContainsKey("PsychicOtherCantrip"), "You must have another standard cantrip to select.")
            .WithRulesTextCreator((Func<CharacterSheet, string>) (sheet => sheet.Calculated.Tags.TryGetValueAs("PsychicOtherCantrip", out SpellId spellId1) ? $"You gain the standard psi cantrip you did not select from your conscious mind — {AllSpells.CreateSpellLink(spellId1, Trait.Psychic)}. You gain the benefits and the amp for this second psi cantrip. Increase the number of Focus Points in your focus pool by 1." : ""))
            .WithOnSheet(values =>
        {
            if (!values.Tags.TryGetValueAs("PsychicOtherCantrip", out SpellId spellId2) || !values.SpellRepertoires.ContainsKey(Trait.Psychic))
                return;
            ++values.FocusPointCount;
            values.SpellRepertoires[Trait.Psychic].SpellsKnown.Add(AllSpells.CreateModernSpell(spellId2, null, values.MaximumSpellLevel, false, new SpellInformation
            {
                ClassOfOrigin = Trait.Psychic,
                PsychicAmpInformation = new PsychicAmpInformation()
            }));
        });
    }
    internal static IEnumerable<Feat> GetSubFeats()
    {
        SpellId psiCantrip1 = ModManager.TryParse("Remaster", out Trait _) ? MSpellIds.AltDaze : SpellId.Daze;
        Feat whisperDaze = new Feat(ModManager.RegisterFeatName("SilentWhisperDaze", "Silent Whisper - Daze"), SilentWhisperFeat.FlavorText, $"You gain 1 focus point and the psi cantrip {AllSpells.CreateSpellLink(psiCantrip1, Trait.Psychic)}", [], null)
            .WithIllustration(IllustrationName.Daze).WithRulesBlockForSpell(psiCantrip1)
            .WithOnSheet(values =>
            {
                if (!values.SpellRepertoires.ContainsKey(Trait.Psychic))
                    return;
                ++values.FocusPointCount;
                values.SpellRepertoires[Trait.Psychic].SpellsKnown.Add(AllSpells.CreateModernSpell(psiCantrip1, null, values.MaximumSpellLevel, false, new SpellInformation
                {
                    ClassOfOrigin = Trait.Psychic,
                    PsychicAmpInformation = new PsychicAmpInformation()
                }));
                values.Tags["PsychicUniqueCantrip"] = MSpellIds.ForbiddenThought;
                values.Tags["PsychicOtherCantrip"] = MSpellIds.Message;
            });
        yield return whisperDaze;
        SpellId psiCantrip2 = MSpellIds.Message;
        Feat whisperMessage = new Feat(ModManager.RegisterFeatName("SilentWhisperMessage", "Silent Whisper - Message"), SilentWhisperFeat.FlavorText, $"You gain 1 focus point and the psi cantrip {AllSpells.CreateSpellLink(psiCantrip2, Trait.Psychic)}", [], null)
            .WithIllustration(IllustrationName.Divination2).WithRulesBlockForSpell(psiCantrip2)
            .WithOnSheet(values =>
            {
                if (!values.SpellRepertoires.ContainsKey(Trait.Psychic))
                    return;
                ++values.FocusPointCount;
                values.SpellRepertoires[Trait.Psychic].SpellsKnown.Add(AllSpells.CreateModernSpell(psiCantrip2, null, values.MaximumSpellLevel, false, new SpellInformation
                {
                    ClassOfOrigin = Trait.Psychic,
                    PsychicAmpInformation = new PsychicAmpInformation()
                }));
                values.Tags["PsychicUniqueCantrip"] = MSpellIds.ForbiddenThought;
                values.Tags["PsychicOtherCantrip"] = psiCantrip1;
            });
        yield return whisperMessage;
        SpellId psiCantrip3 = SpellId.ProduceFlame;
        Feat produceFlame = new Feat(ModManager.RegisterFeatName("OscillatingWavePF", "Oscillating Wave - Produce Flame"), OscillatingWave.FlavorText, $"You gain 1 focus point and the psi cantrip {AllSpells.CreateSpellLink(psiCantrip3, Trait.Psychic)}", [], null)
            .WithIllustration(IllustrationName.ProduceFlame).WithRulesBlockForSpell(psiCantrip3)
            .WithOnSheet(values =>
            {
                if (!values.SpellRepertoires.ContainsKey(Trait.Psychic))
                    return;
                ++values.FocusPointCount;
                values.SpellRepertoires[Trait.Psychic].SpellsKnown.Add(AllSpells.CreateModernSpell(psiCantrip3, null, values.MaximumSpellLevel, false, new SpellInformation
                {
                    ClassOfOrigin = Trait.Psychic,
                    PsychicAmpInformation = new PsychicAmpInformation()
                }));
                values.Tags["PsychicUniqueCantrip"] = MSpellIds.ThermalStasis;
                values.Tags["PsychicOtherCantrip"] = SpellId.RayOfFrost;
            });
        yield return produceFlame;
        SpellId psiCantrip4 = SpellId.RayOfFrost;
        Feat rayOfFrost = new Feat(ModManager.RegisterFeatName("OscillatingWaveRoF", "Oscillating Wave - Ray of Frost"), OscillatingWave.FlavorText, $"You gain 1 focus point and the psi cantrip {AllSpells.CreateSpellLink(psiCantrip4, Trait.Psychic)}", [], null)
            .WithIllustration(IllustrationName.RayOfFrost).WithRulesBlockForSpell(psiCantrip4)
            .WithOnSheet(values =>
            {
                if (!values.SpellRepertoires.ContainsKey(Trait.Psychic))
                    return;
                ++values.FocusPointCount;
                values.SpellRepertoires[Trait.Psychic].SpellsKnown.Add(AllSpells.CreateModernSpell(psiCantrip4, null, values.MaximumSpellLevel, false, new SpellInformation
                {
                    ClassOfOrigin = Trait.Psychic,
                    PsychicAmpInformation = new PsychicAmpInformation()
                }));
                values.Tags["PsychicUniqueCantrip"] = MSpellIds.ThermalStasis;
                values.Tags["PsychicOtherCantrip"] = SpellId.ProduceFlame;
            });
        yield return rayOfFrost;
    }

    internal static IEnumerable<Feat> ParallelFeats()
    {
        List<Spell> spells = [AllSpells.CreateModernSpell(SpellId.ProduceFlame, null, 0, false,  new SpellInformation()),
            AllSpells.CreateModernSpell(SpellId.RayOfFrost, null, 0, false,  new SpellInformation()),
            AllSpells.CreateModernSpell(MSpellIds.ThermalStasis, null, 0, false,  new SpellInformation()),
            AllSpells.CreateModernSpell(ModManager.TryParse("Remaster", out Trait _) ? MSpellIds.AltDaze : SpellId.Daze, null, 0, false,  new SpellInformation()),
            AllSpells.CreateModernSpell(MSpellIds.Message, null, 0, false,  new SpellInformation()),
            AllSpells.CreateModernSpell(MSpellIds.ForbiddenThought, null, 0, false,  new SpellInformation())];
        foreach (Spell spell in spells)
        {
            yield return new Feat(ModManager.RegisterFeatName(spell.Name, spell.Name), null,
                    $"You can cast {spell.ToSpellLink()}.", [], null)
                .WithRulesBlockForSpell(spell.SpellId, Trait.Psychic).WithIllustration(spell.Illustration)
                .WithPrerequisite(
                    values =>
                        !values.SpellRepertoires.TryGetValue(Trait.Psychic, out SpellRepertoire? spellRepertoire1) ||
                        !spellRepertoire1.SpellsKnown.Any(sk =>
                            sk.SpellId == spell.SpellId && sk.CombatActionSpell.PsychicAmpInformation == null),
                    "You have already selected this spell as a non-psi cantrip.\n")
                .WithEquivalent(values =>
                    values.SpellRepertoires.TryGetValue(Trait.Psychic, out SpellRepertoire? spellRepertoire2) &&
                    spellRepertoire2.SpellsKnown.Any(sk =>
                        sk.SpellId == spell.SpellId && sk.CombatActionSpell.PsychicAmpInformation != null))
                .WithOnSheet(
                    values => values.SpellRepertoires[Trait.Psychic]
                        .SpellsKnown
                        .Add(AllSpells.CreateModernSpell(spell.SpellId, null, values.MaximumSpellLevel, false,
                            new SpellInformation()
                            {
                                ClassOfOrigin = Trait.Psychic,
                                PsychicAmpInformation = new PsychicAmpInformation()
                            })));
        }
    }
//use this to add Conservation of Energy
    private static void CreateWaveLogic(Feat wave)
    {
        wave.WithOnCreature(cr =>
        {
            cr.AddQEffect(OscillatingWaveSetup());
            if (cr.Level >= 6)
                cr.AddQEffect(new QEffect
                {
                    AfterYouDealDamageOfKind = async (self, _, damageKind, _) =>
                    {
                        if (damageKind is DamageKind.Fire or DamageKind.Cold && self.Actions.CanTakeReaction() && !self.HasEffect(MQEffectIds.EntropicWheel)) 
                        {
                            List<string> options = [];
                            CombatAction? wheel = null;
                            CombatAction? wheelAmp = null;
                            if (Possibilities.Create(self).Filter(ap =>
                                {
                                    if (ap.CombatAction.SpellId != MSpellIds.EntropicWheel)
                                        return false;
                                    if (ap.CombatAction.SpellInformation is { PsychicAmpInformation.Amped: true })
                                        return false;
                                    ap.CombatAction.ActionCost = 0;
                                    ap.CombatAction.Target = Target.Self();
                                    ap.RecalculateUsability();
                                    return true;
                                }).CreateActions(true).FirstOrDefault() is CombatAction entropicWheel)
                            {
                                options.Add(entropicWheel.Name);
                                wheel = entropicWheel;
                            }
                            if (Possibilities.Create(self).Filter(ap =>
                                {
                                    if (ap.CombatAction.SpellId != MSpellIds.EntropicWheel)
                                        return false;
                                    if (ap.CombatAction.SpellInformation is { PsychicAmpInformation.Amped: false })
                                        return false;
                                    ap.CombatAction.ActionCost = 0;
                                    ap.CombatAction.Target = Target.Self();
                                    ap.RecalculateUsability();
                                    return true;
                                }).CreateActions(true).FirstOrDefault() is CombatAction entropicWheelAmp)
                            {
                                options.Add(entropicWheelAmp.Name);
                                wheelAmp = entropicWheelAmp;
                            }
                            options.Add("pass");
                            ChoiceButtonOption choice = await self.AskForChoiceAmongButtons(IllustrationName.ResistEnergy, "Cast entropic wheel as a reaction?", options.ToArray());
                            if (options[choice.Index] == "pass")
                                return;
                            if (options[choice.Index] == wheel?.Name)
                            {
                                self.AddQEffect(new QEffect(ExpirationCondition.ExpiresAtEndOfAnyTurn)
                                    { Id = MQEffectIds.EntropicBlock });
                                await self.Battle.GameLoop.FullCast(wheel);
                            }
                            if (options[choice.Index] == wheelAmp?.Name)
                            {
                                self.AddQEffect(new QEffect(ExpirationCondition.ExpiresAtEndOfAnyTurn)
                                    { Id = MQEffectIds.EntropicBlock });
                                await self.Battle.GameLoop.FullCast(wheelAmp);
                            }
                        }
                    }
                });
        });
        wave.RulesText += "\n\n{b}Conservation of Energy{/b} Energy can't be created or destroyed, only transferred or changed. Whenever you use your magic to add or remove energy, you must then balance it with the opposing force. The first time in an encounter that you cast a granted spell or a standard psi cantrip from your conscious mind, decide whether you're adding energy or removing it. Once you add energy, you must remove energy the next time you cast one of these spells. When an encounter ends, you restore yourself to a neutral state, allowing you to once again freely choose whether you add or remove energy on your next spell." +
                          "\n\n{b}Adding Energy{/b}: The ability gains the fire trait, any damage it deals is fire damage, and any resistance it grants is to cold damage. It loses any traits matching damage types it no longer deals." +
                          "\n{b}Removing Energy{b}: The ability gains the cold trait, any damage it deals is cold damage, and any resistance it grants is to fire damage. It loses any traits matching damage types it no longer deals." +
                          "\n{b}Mindshift{/b}: When you use an action that has the mindshift trait, you can choose to add or remove energy to it instead of making it mental.";
    }
    internal static QEffect ShatterMindToggle()
    {
        return new QEffect
        {
            ProvideActionIntoPossibilitySection = (effect, section) =>
            {
                if (section.PossibilitySectionId != PossibilitySectionId.OtherManeuvers) return null;
                Creature self = effect.Owner;
                if (!self.HasEffect(MQEffectIds.ShatterMind60))
                {
                    return new ActionPossibility(new CombatAction(self, IllustrationName.BrainDrain,
                        "Amped Shatter Mind - 60 foot cone", [Trait.Basic, Trait.DoesNotBreakStealth],
                        "Amped Shatter Mind will have a 60 foot cone.", Target.Self()).WithActionCost(0).WithEffectOnSelf(cr => cr.AddQEffect(new QEffect {Id = MQEffectIds.ShatterMind60})));
                }
                return new ActionPossibility(new CombatAction(self, IllustrationName.BrainDrain,
                    "Amped Shatter Mind - 30 foot cone", [Trait.Basic, Trait.DoesNotBreakStealth],
                    "Amped Shatter Mind will have a 30 foot cone.", Target.Self()).WithActionCost(0).WithEffectOnSelf(cr => cr.RemoveAllQEffects(qf => qf.Id == MQEffectIds.ShatterMind60)));
            }
        };
    }

    private static QEffect OscillatingWaveSetup()
    {
        return new QEffect
        {
            StartOfCombat = effect =>
            {
                effect.Owner.AddQEffect(new QEffect(ExpirationCondition.ExpiresAtEndOfYourTurn)
                {
                    ProvideContextualAction = qf =>
                    {
                        CombatAction oscillate = new CombatAction(qf.Owner, MIllustrations.ColdFireWheel,
                                "Conservation of Energy", [Trait.Basic, Trait.DoesNotBreakStealth],
                                "Choose to add energy or remove energy from spells granted by your conscious mind or a standard psi cantrip. Adding energy changes the damage dealt by those spells to fire, removing energy changes the damage dealt to cold.",
                                Target.Self())
                            .WithActionCost(0)
                            .WithEffectOnSelf(async (_, self) =>
                            {
                                List<string> choices = ["Add energy", "Remove energy"];
                                ChoiceButtonOption choice = await self.AskForChoiceAmongButtons(MIllustrations.ColdFireWheel, "Do you want to add energy or remove it?", choices.ToArray());
                                string id = choices[choice.Index];
                                switch (id)
                                {
                                    case "Add energy":
                                        self.AddQEffect(OscillatingWaveFire());
                                        qf.ExpiresAt = ExpirationCondition.Immediately;
                                        break;
                                    case "Remove energy":
                                        self.AddQEffect(OscillatingWaveCold());
                                        qf.ExpiresAt = ExpirationCondition.Immediately;
                                        break;
                                }
                            });
                        return new ActionPossibility(oscillate);
                    },
                    Id = MQEffectIds.OscillatingWaveToggle
                });
                return Task.CompletedTask;
            },
            AfterYouDealDamageOfKind = (self, spell, damageKind, _) =>
            {
                if (self.HasEffect(MQEffectIds.OscillatingWaveFire) ||
                    self.HasEffect(MQEffectIds.OscillatingWaveCold) ||
                    !IsWaveSpell(spell.SpellId)) return Task.CompletedTask;
                switch (damageKind)
                {
                    case DamageKind.Fire:
                        self.AddQEffect(OscillatingWaveCold());
                        self.RemoveAllQEffects(qf => qf.Id ==  MQEffectIds.OscillatingWaveToggle);
                        break;
                    case DamageKind.Cold:
                        self.AddQEffect(OscillatingWaveFire());
                        self.RemoveAllQEffects(qf => qf.Id ==  MQEffectIds.OscillatingWaveToggle);
                        break;
                }
                return Task.CompletedTask;
            },
            AfterYouTakeAction = (effect, action) =>
            {
                Creature self = effect.Owner;
                if (self.HasEffect(MQEffectIds.OscillatingWaveFire) ||
                    self.HasEffect(MQEffectIds.OscillatingWaveCold) ||
                    action.SpellId != SpellId.FireShield) return Task.CompletedTask;
                self.AddQEffect(OscillatingWaveCold());
                self.RemoveAllQEffects(qf => qf.Id ==  MQEffectIds.OscillatingWaveToggle);
                return Task.CompletedTask;
            },
            YouAcquireQEffect = (wave, effectToCheck) =>
            {
                int lvl = wave.Owner.MaximumSpellRank;
                if (effectToCheck.Name == "Psi Burst {icon:Action}")
                    return new QEffect("Psi Burst {icon:Action}", $"Once per round, if your psyche is unleashed, you can deal {S.HeightenedVariable(lvl, 1)}d4 damage to target creature within 30 feet (basic Reflex save mitigates).")
                    {
                        ProvideMainAction = (Func<QEffect, Possibility?>) (qfSelf =>
                        {
                            if (!qfSelf.Owner.HasEffect(QEffectId.PsycheUnleashed) || qfSelf.Owner.Actions.ActionHistoryThisTurn.Any(t => t.ActionId == ActionId.PsiBurst))
                                return null;
                            return new ActionPossibility(new CombatAction(qfSelf.Owner, IllustrationName.PsiBurst, "Psi Burst", [Trait.Concentrate, Trait.Evocation, Trait.Mindshift, Trait.Occult, Trait.Psyche, Trait.Psychic], 
                                $"{{i}}With a passing thought, you direct violent psychic energies at a nearby creature.{{/i}}\n\n{{b}}Saving throw{{/b}} basic Reflex\n{{b}}Range{{/b}} 30 feet\n\nDeal {S.HeightenedVariable(lvl, 1)}d4 bludgeoning or mental damage.", Target.Ranged(6))
                            {
                                SpellcastingSource = qfSelf.Owner.Spellcasting?.GetSourceByOrigin(Trait.Psychic)
                            }.WithActionId(ActionId.PsiBurst).WithSoundEffect(SfxName.Mental).WithProjectileCone(IllustrationName.PsiBurst, 15, ProjectileKind.Cone).WithSpellSavingThrow(Defense.Reflex)
                                .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                            {
                                List<DamageKind> damages = [DamageKind.Bludgeoning, DamageKind.Mental];
                                if (wave.Owner.HasFeat(MFeatNames.OscillatingWave))
                                {
                                    damages.Add(DamageKind.Fire);
                                    damages.Add(DamageKind.Cold);
                                }
                                DamageKind damageKind = target.WeaknessAndResistance.WhatDamageKindIsBestAgainstMe(damages);
                                await CommonSpellEffects.DealBasicDamage(spell, caster, target, result, lvl + "d4", damageKind);
                            })).WithPossibilityGroup("Abilities");
                        })
                    };
                if (effectToCheck.Description == "Your Strikes deal 1d6 extra force or mental damage.")
                {
                    bool flag = wave.Owner.HasEffect(QEffectId.PsycheUnleashed);
                    return new QEffect("Psi Strikes", "Your Strikes deal 1d6 extra force or mental damage.",
                        flag ? ExpirationCondition.Never : ExpirationCondition.ExpiresAtEndOfAnyTurn, wave.Owner,
                        (Illustration)IllustrationName.DragonClaws)
                    {
                        Key = "PsiStrikes",
                        AddExtraStrikeDamage = (Func<CombatAction, Creature, (DiceFormula, DamageKind)?>)((_, defender) =>
                            {
                                List<DamageKind> damages = [DamageKind.Force, DamageKind.Mental];
                                if (wave.Owner.HasFeat(MFeatNames.OscillatingWave))
                                {
                                    damages.Add(DamageKind.Fire);
                                    damages.Add(DamageKind.Cold);
                                }
                                DamageKind damageKind =
                                    defender.WeaknessAndResistance.WhatDamageKindIsBestAgainstMe(damages);
                                return (DiceFormula.FromText("1d6", "Psi Strikes"), damageKind);
                            })
                    };
                }
                return effectToCheck;
            },
            YouBeginAction = (effect, action) =>
            {
                Creature self = effect.Owner;
                if (IsWaveSpell(action.SpellId) && self.HasEffect(MQEffectIds.OscillatingWaveFire))
                {
                    if (action.HasTrait(Trait.Cold))
                    {
                        action.Traits.Remove(Trait.Cold);
                        action.Traits.Add(Trait.Fire);
                    }
                }
                if (IsWaveSpell(action.SpellId) && self.HasEffect(MQEffectIds.OscillatingWaveCold))
                {
                    if (action.HasTrait(Trait.Fire))
                    {
                        action.Traits.Remove(Trait.Fire);
                        action.Traits.Add(Trait.Cold);
                    }
                }
                if (action.SpellId == SpellId.FireShield && effect.Owner.HasEffect(MQEffectIds.OscillatingWaveCold))
                {
                    effect.Owner.AddQEffect(new QEffect
                    {
                        YouAcquireQEffect = (wave, effectToCheck) =>
                        {
                            int level = action.SpellLevel;
                            int resist = (level - 4) / 2 * 5 + 5;
                            int fireShieldDamage = (level - 4) / 2 + 2;
                            if (effectToCheck.Name == $"{DamageKind.Cold.HumanizeTitleCase2()} resistance {resist.ToString()}")
                            {
                                wave.ExpiresAt = ExpirationCondition.EphemeralAtEndOfImmediateAction;
                                return QEffect.DamageResistance(DamageKind.Fire, resist);
                            }
                            if (effectToCheck.Name == "Fire Shield")
                            {
                                return new QEffect("Ice Shield", $"Creatures who hit you with a melee attack take {fireShieldDamage}d6 cold damage each time they do {{i}}(no save).{{/i}}", ExpirationCondition.Never, effect.Owner, new TintedIllustration(IllustrationName.FireShield, Color.RoyalBlue))
                                {
                                    AfterYouAreTargeted = (Func<QEffect, CombatAction, Task>) (async (qEffect, combatAction) =>
                                    {
                                        if (combatAction.ChosenTargets.CheckResults.GetValueOrDefault(qEffect.Owner) < CheckResult.Success || !combatAction.HasTrait(Trait.Attack) || !combatAction.HasTrait(Trait.Melee))
                                            return;
                                        await CommonSpellEffects.DealDirectDamage(action, DiceFormula.FromText($"{fireShieldDamage}d6", "Ice Shield"), combatAction.Owner, CheckResult.Failure, DamageKind.Cold);
                                    })
                                };
                            }
                            return effectToCheck;
                        }
                    });
                }
                return Task.CompletedTask;
            }
        };
    }

    private static QEffect OscillatingWaveFire()
    {
        return new QEffect
        {
            Id = MQEffectIds.OscillatingWaveFire,
            Illustration = MIllustrations.FireWheel,
            Name = "Conservation of Energy - Fire",
            Description = "You add energy to your spells. All standard psi cantrips and spells granted by your conscious mind deal fire damage instead of their original damage type.",
            YouDealDamageEvent = (effect, damage) =>
            {
                if (damage.CombatAction != null && IsWaveSpell(damage.CombatAction.SpellId))
                {
                    effect.StateCheckLayer = 0;
                    DiceFormula? dice = damage.KindedDamages[0].DiceFormula;
                    if (dice != null)
                    {
                        damage.KindedDamages[0] = new KindedDamage(dice, DamageKind.Fire);
                    }
                    effect.ExpiresAt = ExpirationCondition.EphemeralAtEndOfImmediateAction;
                    effect.WhenExpires = qf => qf.Owner.AddQEffect(OscillatingWaveCold());
                }
                return Task.CompletedTask;
            },
            AfterYouTakeAction = (effect, action) =>
            {
                if (action.SpellId == SpellId.FireShield)
                {
                    effect.ExpiresAt = ExpirationCondition.EphemeralAtEndOfImmediateAction;
                    effect.WhenExpires = qf => qf.Owner.AddQEffect(OscillatingWaveCold());
                }
                return Task.CompletedTask;
            }
        };
    }
    private static QEffect OscillatingWaveCold()
    {
        return new QEffect
        {
            Id = MQEffectIds.OscillatingWaveCold,
            Illustration = MIllustrations.ColdWheel,
            Name = "Conservation of Energy - Cold",
            Description = "You remove energy from your spells. All standard psi cantrips and spells granted by your conscious mind deal cold damage instead of their original damage type.",
            YouDealDamageEvent = (effect, damage) =>
            {
                if (damage.CombatAction != null && IsWaveSpell(damage.CombatAction.SpellId))
                {
                    effect.StateCheckLayer = 0;
                    DiceFormula? dice = damage.KindedDamages[0].DiceFormula;
                    if (dice != null)
                    {
                        damage.KindedDamages[0] = new KindedDamage(dice, DamageKind.Cold);
                    }
                    effect.ExpiresAt = ExpirationCondition.EphemeralAtEndOfImmediateAction;
                    effect.WhenExpires = qf => qf.Owner.AddQEffect(OscillatingWaveFire());
                }
                return Task.CompletedTask;
            },
            AfterYouTakeAction = (effect, action) =>
            {
                if (action.SpellId == SpellId.FireShield)
                {
                    effect.ExpiresAt = ExpirationCondition.EphemeralAtEndOfImmediateAction;
                    effect.WhenExpires = qf => qf.Owner.AddQEffect(OscillatingWaveFire());
                }
                return Task.CompletedTask;
            }
        };
    }
    private static bool IsWaveSpell(SpellId spell)
    {
        return spell is SpellId.ConeOfCold or SpellId.Fireball or SpellId.ScorchingRay
            or SpellId.BurningHands or SpellId.RayOfFrost or SpellId.ProduceFlame;
    }
} 