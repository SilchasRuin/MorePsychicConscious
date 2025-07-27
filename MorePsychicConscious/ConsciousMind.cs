using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using Dawnsbury.Audio;
using Dawnsbury.Auxiliary;
using Dawnsbury.Core;
using Dawnsbury.Core.Animations;
using Dawnsbury.Core.CharacterBuilder;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb.Archetypes;
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
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Roller;
using Dawnsbury.Display;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Display.Text;
using Dawnsbury.IO;
using Dawnsbury.Modding;
using Dawnsbury.Phases.Menus;
using Microsoft.Xna.Framework;
using static MorePsychicConscious.ConsciousMindFeat;

namespace MorePsychicConscious;

public abstract class ConsciousMind : ModData
{
    private static Feat SilentWhisperFeat { get; set; } = null!;
    private static Feat OscillatingWave { get; set; } = null!;
    private static Feat UnboundStep { get; set; } = null!;

    internal static IEnumerable<Feat> GetFeats()
    {
        SilentWhisperFeat = CreateConsciousMindFeat2(MFeatNames.SilentWhisper,
            "Every mind murmurs constantly, and the static from those nearby reverberates in your head like wind through leaves. What better focus for your magic then, than the very thoughts of those around you? Your versatile telepathic abilities let you soothe and communicate with your allies or control your enemies.",
            [SpellId.Command, ModManager.TryParse("Stupefy", out SpellId stupefy) ? stupefy : SpellId.TouchOfIdiocy, SpellId.Heroism, SpellId.Confusion, SpellId.SynapticPulse],
            MSpellIds.Message, ModManager.TryParse("Remaster", out Trait _) ? MSpellIds.AltDaze : SpellId.Daze,
            MSpellIds.ForbiddenThought, MSpellIds.ShatterMind);
        yield return SilentWhisperFeat;
        OscillatingWave = CreateConsciousMindFeat2(MFeatNames.OscillatingWave,
            "At the heart of all things is energy. Though it may change its form or resting place, it is eternal, fundamental, the purest of building blocks. Your powers let you shift energy—either concentrating it in a single point to explosive end or freezing objects by plundering it away— in an endless oscillation of temperature.",
            [SpellId.BurningHands, SpellId.ScorchingRay, SpellId.Fireball, SpellId.FireShield, SpellId.ConeOfCold],
            SpellId.RayOfFrost, SpellId.ProduceFlame, MSpellIds.ThermalStasis, MSpellIds.EntropicWheel);
        CreateWaveLogic(OscillatingWave);
        yield return OscillatingWave;
        UnboundStep = CreateConsciousMindFeat2(MFeatNames.UnboundStep,
            "The mind can flit from thought to thought; why too shouldn't you? You focus on motion in a higher order of spatial dimensions, not just the typical three, allowing for versatile abilities that alter positioning.",
            [
                MSpellIds.ThoughtfulGift, SpellId.Blur, SpellId.DeflectCriticalHit, SpellId.DimensionDoor,
                SpellId.BlinkCharge
            ], SpellId.WarpStep, SpellId.PhaseBolt, MSpellIds.TesseractTunnel, MSpellIds.GhostlyShift);
        yield return UnboundStep;
        yield return new TrueFeat(
                ModManager.RegisterFeatName("PsiDevelopmentAlt", "Psi Development - Standard Cantrip"), 6,
                "You've found a new mental form.",
                "You gain the standard psi cantrip you didn't select from your conscious mind. You gain the benefits and the amp for this second psi cantrip. Increase the number of Focus Points in your focus pool by 1.",
                [])
            .WithAvailableAsArchetypeFeat(Trait.Psychic).WithPrerequisite(
                values => values.Tags.ContainsKey("PsychicOtherCantrip"),
                "You must have another standard cantrip to select.")
            .WithRulesTextCreator((Func<CharacterSheet, string>)(sheet =>
                sheet.Calculated.Tags.TryGetValueAs("PsychicOtherCantrip", out SpellId spellId1)
                    ? $"You gain the standard psi cantrip you did not select from your conscious mind — {AllSpells.CreateSpellLink(spellId1, Trait.Psychic)}. You gain the benefits and the amp for this second psi cantrip. Increase the number of Focus Points in your focus pool by 1."
                    : ""))
            .WithOnSheet(values =>
            {
                if (!values.Tags.TryGetValueAs("PsychicOtherCantrip", out SpellId spellId2) ||
                    !values.SpellRepertoires.ContainsKey(Trait.Psychic))
                    return;
                ++values.FocusPointCount;
                values.SpellRepertoires[Trait.Psychic].SpellsKnown.Add(AllSpells.CreateModernSpell(spellId2, null,
                    values.MaximumSpellLevel, false, new SpellInformation
                    {
                        ClassOfOrigin = Trait.Psychic,
                        PsychicAmpInformation = new PsychicAmpInformation()
                    }));
            });
        yield return Psychic.CreateSubconsciousMind(ModManager.RegisterFeatName("GatheredLore", "Gathered Lore"),
            "Many psychics are self-taught, frantically improvising how to best control their abilities before their power overwhelms them. Not you. Tutored by a mentor or classically trained at a facility for psychic development, you've learned techniques and teachings for best harnessing the mind, cataloging each expression of psychic power with a specific teaching, anecdote, or phrase." +
            "\n\nYour thought components are mantras you associate with a given spell, which you mentally repeat as you cast. You might silently utter a teaching of resilience as you spin force into a barrier or hear the first three notes of a traveling song as you slip through space. Mantra components often produce runes or symbols from your learnings that are unique to each spell you cast, causing your spell manifestations to resemble those of conventional spellcasters to a much greater degree than those of other psychics.",
            Ability.Intelligence, caster =>
            {
                return new CombatAction(caster, IllustrationName.NarratorBook, "Recall the Teachings",
                        [Trait.Divination, Trait.Occult, Trait.Psyche, Trait.Psychic],
                        "{i}The heightened power of your psyche lets you recall every lesson you've ever learned. You search your mind for the right teaching, which at first seems cryptic but comes into clarity when it's most relevant.{/i}" +
                        "\n\nUntil the start of your next turn, you count as having prepared to {tooltip:aid}Aid{/} all allies within 30 feet of you. If you use the Aid reaction to help one of them during that time, you roll an Occultism check for Aid as you recall a lesson to help them. Most lessons take the form of short axioms, parables, or sayings, meaning that conveying them to your ally usually grants your Aid reaction the auditory and linguistic traits.",
                        Target.Self())
                    .WithActionCost(1).WithSoundEffect(SfxName.BookOpen).WithActionId(MActionIds.PrepareToAid)
                    .WithEffectOnSelf((_, self) =>
                    {
                        QEffect prepare = PrepareRecallAid(self);
                        self.AddQEffect(prepare);
                        return Task.CompletedTask;
                    });
            });
    }

    internal static IEnumerable<Feat> GetSubFeats()
    {
        SpellId psiCantrip1 = ModManager.TryParse("Remaster", out Trait _) ? MSpellIds.AltDaze : SpellId.Daze;
        Feat whisperDaze = new Feat(ModManager.RegisterFeatName("SilentWhisperDaze", "Silent Whisper - Daze"),
                SilentWhisperFeat.FlavorText,
                $"You gain 1 focus point and the psi cantrip {AllSpells.CreateSpellLink(psiCantrip1, Trait.Psychic)}",
                [], null)
            .WithIllustration(IllustrationName.Daze).WithRulesBlockForSpell(psiCantrip1)
            .WithOnSheet(values =>
            {
                if (!values.SpellRepertoires.ContainsKey(Trait.Psychic))
                    return;
                ++values.FocusPointCount;
                values.SpellRepertoires[Trait.Psychic].SpellsKnown.Add(AllSpells.CreateModernSpell(psiCantrip1, null,
                    values.MaximumSpellLevel, false, new SpellInformation
                    {
                        ClassOfOrigin = Trait.Psychic,
                        PsychicAmpInformation = new PsychicAmpInformation()
                    }));
                values.Tags["PsychicUniqueCantrip"] = MSpellIds.ForbiddenThought;
                values.Tags["PsychicOtherCantrip"] = MSpellIds.Message;
            });
        yield return whisperDaze;
        SpellId psiCantrip2 = MSpellIds.Message;
        Feat whisperMessage = new Feat(ModManager.RegisterFeatName("SilentWhisperMessage", "Silent Whisper - Message"),
                SilentWhisperFeat.FlavorText,
                $"You gain 1 focus point and the psi cantrip {AllSpells.CreateSpellLink(psiCantrip2, Trait.Psychic)}",
                [], null)
            .WithIllustration(MIllustrations.Message).WithRulesBlockForSpell(psiCantrip2)
            .WithOnSheet(values =>
            {
                if (!values.SpellRepertoires.ContainsKey(Trait.Psychic))
                    return;
                ++values.FocusPointCount;
                values.SpellRepertoires[Trait.Psychic].SpellsKnown.Add(AllSpells.CreateModernSpell(psiCantrip2, null,
                    values.MaximumSpellLevel, false, new SpellInformation
                    {
                        ClassOfOrigin = Trait.Psychic,
                        PsychicAmpInformation = new PsychicAmpInformation()
                    }));
                values.Tags["PsychicUniqueCantrip"] = MSpellIds.ForbiddenThought;
                values.Tags["PsychicOtherCantrip"] = psiCantrip1;
            });
        yield return whisperMessage;
        SpellId psiCantrip3 = SpellId.ProduceFlame;
        Feat produceFlame =
            new Feat(ModManager.RegisterFeatName("OscillatingWavePF", "Oscillating Wave - Produce Flame"),
                    OscillatingWave.FlavorText,
                    $"You gain 1 focus point and the psi cantrip {AllSpells.CreateSpellLink(psiCantrip3, Trait.Psychic)}",
                    [], null)
                .WithIllustration(IllustrationName.ProduceFlame).WithRulesBlockForSpell(psiCantrip3)
                .WithOnSheet(values =>
                {
                    if (!values.SpellRepertoires.ContainsKey(Trait.Psychic))
                        return;
                    ++values.FocusPointCount;
                    values.SpellRepertoires[Trait.Psychic].SpellsKnown.Add(AllSpells.CreateModernSpell(psiCantrip3,
                        null, values.MaximumSpellLevel, false, new SpellInformation
                        {
                            ClassOfOrigin = Trait.Psychic,
                            PsychicAmpInformation = new PsychicAmpInformation()
                        }));
                    values.Tags["PsychicUniqueCantrip"] = MSpellIds.ThermalStasis;
                    values.Tags["PsychicOtherCantrip"] = SpellId.RayOfFrost;
                });
        yield return produceFlame;
        SpellId psiCantrip4 = SpellId.RayOfFrost;
        Feat rayOfFrost = new Feat(ModManager.RegisterFeatName("OscillatingWaveRoF", "Oscillating Wave - Ray of Frost"),
                OscillatingWave.FlavorText,
                $"You gain 1 focus point and the psi cantrip {AllSpells.CreateSpellLink(psiCantrip4, Trait.Psychic)}",
                [], null)
            .WithIllustration(IllustrationName.RayOfFrost).WithRulesBlockForSpell(psiCantrip4)
            .WithOnSheet(values =>
            {
                if (!values.SpellRepertoires.ContainsKey(Trait.Psychic))
                    return;
                ++values.FocusPointCount;
                values.SpellRepertoires[Trait.Psychic].SpellsKnown.Add(AllSpells.CreateModernSpell(psiCantrip4, null,
                    values.MaximumSpellLevel, false, new SpellInformation
                    {
                        ClassOfOrigin = Trait.Psychic,
                        PsychicAmpInformation = new PsychicAmpInformation()
                    }));
                values.Tags["PsychicUniqueCantrip"] = MSpellIds.ThermalStasis;
                values.Tags["PsychicOtherCantrip"] = SpellId.ProduceFlame;
            });
        yield return rayOfFrost;
        SpellId psiCantrip5 = SpellId.WarpStep;
        Feat warpStep = new Feat(ModManager.RegisterFeatName("UnboundStepWS", "Unbound Step - Warp Step"),
                UnboundStep.FlavorText,
                $"You gain 1 focus point and the psi cantrip {AllSpells.CreateSpellLink(psiCantrip5, Trait.Psychic)}",
                [], null)
            .WithIllustration(IllustrationName.WarpStep).WithRulesBlockForSpell(psiCantrip5)
            .WithOnSheet(values =>
            {
                if (!values.SpellRepertoires.ContainsKey(Trait.Psychic))
                    return;
                ++values.FocusPointCount;
                values.SpellRepertoires[Trait.Psychic].SpellsKnown.Add(AllSpells.CreateModernSpell(psiCantrip5, null,
                    values.MaximumSpellLevel, false, new SpellInformation
                    {
                        ClassOfOrigin = Trait.Psychic,
                        PsychicAmpInformation = new PsychicAmpInformation()
                    }));
                values.Tags["PsychicUniqueCantrip"] = MSpellIds.TesseractTunnel;
                values.Tags["PsychicOtherCantrip"] = SpellId.PhaseBolt;
            });
        yield return warpStep;
        SpellId psiCantrip6 = SpellId.PhaseBolt;
        Feat phaseBolt = new Feat(ModManager.RegisterFeatName("UnboundStepPB", "Unbound Step - Phase Bolt"),
                UnboundStep.FlavorText,
                $"You gain 1 focus point and the psi cantrip {AllSpells.CreateSpellLink(psiCantrip6, Trait.Psychic)}",
                [], null)
            .WithIllustration(IllustrationName.PhaseBolt).WithRulesBlockForSpell(psiCantrip6)
            .WithOnSheet(values =>
            {
                if (!values.SpellRepertoires.ContainsKey(Trait.Psychic))
                    return;
                ++values.FocusPointCount;
                values.SpellRepertoires[Trait.Psychic].SpellsKnown.Add(AllSpells.CreateModernSpell(psiCantrip6, null,
                    values.MaximumSpellLevel, false, new SpellInformation
                    {
                        ClassOfOrigin = Trait.Psychic,
                        PsychicAmpInformation = new PsychicAmpInformation()
                    }));
                values.Tags["PsychicUniqueCantrip"] = MSpellIds.TesseractTunnel;
                values.Tags["PsychicOtherCantrip"] = SpellId.WarpStep;
            });
        yield return phaseBolt;
    }

    internal static IEnumerable<Feat> ParallelFeats()
    {
        List<Spell> spells =
        [
            AllSpells.CreateModernSpell(SpellId.ProduceFlame, null, 0, false, new SpellInformation()),
            AllSpells.CreateModernSpell(SpellId.RayOfFrost, null, 0, false, new SpellInformation()),
            AllSpells.CreateModernSpell(MSpellIds.ThermalStasis, null, 0, false, new SpellInformation()),
            AllSpells.CreateModernSpell(ModManager.TryParse("Remaster", out Trait _) ? MSpellIds.AltDaze : SpellId.Daze,
                null, 0, false, new SpellInformation()),
            AllSpells.CreateModernSpell(MSpellIds.Message, null, 0, false, new SpellInformation()),
            AllSpells.CreateModernSpell(MSpellIds.ForbiddenThought, null, 0, false, new SpellInformation()),
            AllSpells.CreateModernSpell(SpellId.WarpStep, null, 0, false, new SpellInformation()),
            AllSpells.CreateModernSpell(SpellId.PhaseBolt, null, 0, false, new SpellInformation()),
            AllSpells.CreateModernSpell(MSpellIds.TesseractTunnel, null, 0, false, new SpellInformation())
        ];
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
                .WithOnSheet(values => values.SpellRepertoires[Trait.Psychic]
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
                        if (damageKind is DamageKind.Fire or DamageKind.Cold && self.Actions.CanTakeReaction() &&
                            !self.HasEffect(MQEffectIds.EntropicWheel))
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
                            ChoiceButtonOption choice = await self.AskForChoiceAmongButtons(
                                IllustrationName.ResistEnergy, "Cast entropic wheel as a reaction?", options.ToArray());
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
        wave.RulesText +=
            "\n\n{b}Conservation of Energy{/b} Energy can't be created or destroyed, only transferred or changed. Whenever you use your magic to add or remove energy, you must then balance it with the opposing force. The first time in an encounter that you cast a granted spell or a standard psi cantrip from your conscious mind, decide whether you're adding energy or removing it. Once you add energy, you must remove energy the next time you cast one of these spells. When an encounter ends, you restore yourself to a neutral state, allowing you to once again freely choose whether you add or remove energy on your next spell." +
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
                            "Amped Shatter Mind - 60 foot cone", [Trait.Basic, Trait.DoesNotBreakStealth, Trait.DoNotShowOverheadOfActionName],
                            "Amped Shatter Mind will have a 60 foot cone.", Target.Self()).WithActionCost(0)
                        .WithEffectOnSelf(cr => cr.AddQEffect(new QEffect { Id = MQEffectIds.ShatterMind60 })));
                }
                return new ActionPossibility(new CombatAction(self, IllustrationName.BrainDrain,
                        "Amped Shatter Mind - 30 foot cone", [Trait.Basic, Trait.DoesNotBreakStealth, Trait.DoNotShowOverheadOfActionName],
                        "Amped Shatter Mind will have a 30 foot cone.", Target.Self()).WithActionCost(0)
                    .WithEffectOnSelf(cr => cr.RemoveAllQEffects(qf => qf.Id == MQEffectIds.ShatterMind60)));
            }
        };
    }

    private static QEffect OscillatingWaveSetup()
    {
        return new QEffect
        {
            StartOfCombat = effect =>
            {
                effect.Owner.AddQEffect(new QEffect
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
                                ChoiceButtonOption choice = await self.AskForChoiceAmongButtons(
                                    MIllustrations.ColdFireWheel, "Do you want to add energy or remove it?",
                                    choices.ToArray());
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
                        self.RemoveAllQEffects(qf => qf.Id == MQEffectIds.OscillatingWaveToggle);
                        break;
                    case DamageKind.Cold:
                        self.AddQEffect(OscillatingWaveFire());
                        self.RemoveAllQEffects(qf => qf.Id == MQEffectIds.OscillatingWaveToggle);
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
                self.RemoveAllQEffects(qf => qf.Id == MQEffectIds.OscillatingWaveToggle);
                return Task.CompletedTask;
            },
            YouAcquireQEffect = (wave, effectToCheck) =>
            {
                int lvl = wave.Owner.MaximumSpellRank;
                if (effectToCheck.Name == "Psi Burst {icon:Action}")
                    return new QEffect("Psi Burst {icon:Action}",
                        $"Once per round, if your psyche is unleashed, you can deal {S.HeightenedVariable(lvl, 1)}d4 damage to target creature within 30 feet (basic Reflex save mitigates).")
                    {
                        ProvideMainAction = (Func<QEffect, Possibility?>)(qfSelf =>
                        {
                            if (!qfSelf.Owner.HasEffect(QEffectId.PsycheUnleashed) ||
                                qfSelf.Owner.Actions.ActionHistoryThisTurn.Any(t => t.ActionId == ActionId.PsiBurst))
                                return null;
                            return new ActionPossibility(new CombatAction(qfSelf.Owner, IllustrationName.PsiBurst,
                                    "Psi Burst",
                                    [
                                        Trait.Concentrate, Trait.Evocation, Trait.Mindshift, Trait.Occult, Trait.Psyche,
                                        Trait.Psychic
                                    ],
                                    $"{{i}}With a passing thought, you direct violent psychic energies at a nearby creature.{{/i}}\n\n{{b}}Saving throw{{/b}} basic Reflex\n{{b}}Range{{/b}} 30 feet\n\nDeal {S.HeightenedVariable(lvl, 1)}d4 bludgeoning or mental damage.",
                                    Target.Ranged(6))
                                {
                                    SpellcastingSource = qfSelf.Owner.Spellcasting?.GetSourceByOrigin(Trait.Psychic)
                                }.WithActionId(ActionId.PsiBurst).WithSoundEffect(SfxName.Mental)
                                .WithProjectileCone(IllustrationName.PsiBurst, 15, ProjectileKind.Cone)
                                .WithSpellSavingThrow(Defense.Reflex)
                                .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                                {
                                    List<DamageKind> damages = [DamageKind.Bludgeoning, DamageKind.Mental];
                                    if (wave.Owner.HasFeat(MFeatNames.OscillatingWave))
                                    {
                                        damages.Add(DamageKind.Fire);
                                        damages.Add(DamageKind.Cold);
                                    }

                                    DamageKind damageKind =
                                        target.WeaknessAndResistance.WhatDamageKindIsBestAgainstMe(damages);
                                    await CommonSpellEffects.DealBasicDamage(spell, caster, target, result, lvl + "d4",
                                        damageKind);
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
                        AddExtraStrikeDamage =
                            (Func<CombatAction, Creature, (DiceFormula, DamageKind)?>)((_, defender) =>
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
                            if (effectToCheck.Name ==
                                $"{DamageKind.Cold.HumanizeTitleCase2()} resistance {resist.ToString()}")
                            {
                                wave.ExpiresAt = ExpirationCondition.EphemeralAtEndOfImmediateAction;
                                return QEffect.DamageResistance(DamageKind.Fire, resist);
                            }

                            if (effectToCheck.Name == "Fire Shield")
                            {
                                return new QEffect("Ice Shield",
                                    $"Creatures who hit you with a melee attack take {fireShieldDamage}d6 cold damage each time they do {{i}}(no save).{{/i}}",
                                    ExpirationCondition.Never, effect.Owner,
                                    new TintedIllustration(IllustrationName.FireShield, Color.RoyalBlue))
                                {
                                    AfterYouAreTargeted =
                                        (Func<QEffect, CombatAction, Task>)(async (qEffect, combatAction) =>
                                        {
                                            if (combatAction.ChosenTargets.CheckResults
                                                    .GetValueOrDefault(qEffect.Owner) < CheckResult.Success ||
                                                !combatAction.HasTrait(Trait.Attack) ||
                                                !combatAction.HasTrait(Trait.Melee))
                                                return;
                                            await CommonSpellEffects.DealDirectDamage(action,
                                                DiceFormula.FromText($"{fireShieldDamage}d6", "Ice Shield"),
                                                combatAction.Owner, CheckResult.Failure, DamageKind.Cold);
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
            Description =
                "You add energy to your spells. All standard psi cantrips and spells granted by your conscious mind deal fire damage instead of their original damage type.",
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
            Description =
                "You remove energy from your spells. All standard psi cantrips and spells granted by your conscious mind deal cold damage instead of their original damage type.",
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

    private static QEffect PrepareRecallAid(Creature aider)
    {
        return new QEffect("Prepared to Aid",
            "You are prepared to aid, using occultism, any creature who was within range when you used {b}Recall the Teachings{/b}.",
            ExpirationCondition.ExpiresAtStartOfYourTurn, aider, IllustrationName.NarratorBook)
        {
            DoNotShowUpOverhead = true,
            Id = MQEffectIds.LibraryAid,
            StateCheck = qf =>
            {
                qf.AddGrantingOfTechnical(cr => cr.FriendOfAndNotSelf(qf.Owner) && cr.DistanceTo(qf.Owner) <= 6, qfTech =>
                {
                    qfTech.Owner.AddQEffect(ReceiveRecallAid(qf));
                });
            }
        };
    }

    private static QEffect ReceiveRecallAid(QEffect preparation)
    {
        return new QEffect("Receiving Aid", "N/A", ExpirationCondition.Ephemeral, preparation.Source)
        {
            Tag = preparation,
            BeforeYourActiveRoll = async (qfThis, aidableAction, defender) =>
            {
                if (
                    qfThis.Source is not { } aider // Aid provider must still exist
                    || !qfThis.Owner.FriendOf(aider) // Self must be friend of source
                    || qfThis.Tag is not QEffect preparation2
                    || aidableAction.ActiveRollSpecification == null
                )
                    return;
                string checkName = "attack";
                if (aidableAction.ActiveRollSpecification.TaggedDetermineBonus.InvolvedSkill is {} skill)
                    checkName = skill.ToString();
                if (await aider.AskToUseReaction(
                        $"{{b}}Aid {{icon:Reaction}}{{/b}}\n{qfThis.Owner.Name} is about to make {AorAn(checkName)} {checkName}. Attempt to aid their check?"))
                {
                    await RollAidReaction(aider, qfThis.Owner, aidableAction, Trait.Occultism);
                    qfThis.ExpiresAt = ExpirationCondition.Immediately;
                    preparation2.ExpiresAt = ExpirationCondition.Immediately;
                }
            }

        };
    }
    internal static QEffect PrepareOmniAid(Creature aider, Creature aidee, Creature enemy)
    {
        return new QEffect("Prepared to Aid",
            $"When {{red}}{enemy.Name}{{/red}} is attacked by {{blue}}{aidee.Name}{{/blue}}"+", make a spell attack roll as a {icon:Reaction} to aid their check.",
            ExpirationCondition.ExpiresAtStartOfYourTurn, aider, IllustrationName.Seek)
        {
            DoNotShowUpOverhead = true,
            Id = MQEffectIds.LibraryAid,
        };
    }

    internal static QEffect ReceiveOmniAid(QEffect preparation, bool amped, Creature enemy, CombatAction spell)
    {
        return new QEffect("Receiving Aid", "N/A", ExpirationCondition.ExpiresAtStartOfSourcesTurn, preparation.Source)
        {
            Tag = preparation,
            BeforeYourActiveRoll = async (qfThis, aidableAction, defender) =>
            {
                if (
                    qfThis.Source is not { } aider // Aid provider must still exist
                    || !qfThis.Owner.FriendOf(aider) // Self must be friend of source
                    || qfThis.Tag is not QEffect preparation2
                    || aidableAction.ActiveRollSpecification == null
                    || !aidableAction.Traits.Contains(Trait.Attack)
                    || aidableAction.ChosenTargets.ChosenCreature != enemy // action must target the enemy
                )
                    return;
                string checkName = "attack";
                if (aidableAction.ActiveRollSpecification.TaggedDetermineBonus.InvolvedSkill is {} skill)
                    checkName = skill.ToString();
                if (await aider.AskToUseReaction(
                        $"{{b}}Aid {{icon:Reaction}}{{/b}}\n{qfThis.Owner.Name} is about to make {AorAn(checkName)} {checkName}. Attempt to aid their check?"))
                {
                    await RollAidReaction(aider, qfThis.Owner, aidableAction, Trait.Spell, amped, spell);
                    qfThis.ExpiresAt = ExpirationCondition.Immediately;
                    preparation2.ExpiresAt = ExpirationCondition.Immediately;
                }
            }

        };
    }
    private static async Task<bool> RollAidReaction(Creature aider, Creature aidee, CombatAction aidableAction, Trait trait, bool amped = false, CombatAction? spell = null)
    {
        // Target ally with skill check, or target enemy with attack check.
        CombatAction aid = CreateAidReaction(aider, aidableAction, trait, amped, spell);
        if (spell == null)
        {
            aid.Traits.AddRange([Trait.Auditory, Trait.Linguistic]);
            aid.WithSoundEffect(SfxName.BookOpen);
        }
        if (spell != null) aid.WithSoundEffect(SfxName.PhaseBolt);
        return await aider.Battle.GameLoop.FullCast(aid,
            ChosenTargets.CreateSingleTarget(aidee));
    }

    private static CombatAction CreateAidReaction(Creature aider, CombatAction aidableAction, Trait trait, bool amped = false, CombatAction? spell = null)
    {
        Proficiency rank = aider.Proficiencies.Get(trait);
        Skill? skill = Skills.TraitToSkill(trait);
        CombatAction aid = new CombatAction(aider, IllustrationName.NarratorBook, "Aid Reaction", [],
            "{b}Aid{b} {icon:Reaction}\n{b}Trigger{/b} An ally is about to attempt a check, and you prepared to aid that ally's check.\n{b}Effect{/b} Attempt the same check you prepared to aid with a DC of " +
            AidDC() + "." + S.FourDegreesOfSuccess(
                $"You grant your ally a +{(!amped ? CriticalBonusFromProficiency(rank) : rank == Proficiency.Legendary ? 4 : 3)} circumstance bonus to the triggering check.",
                $"You grant your ally a +{(amped ? 2 : 1)} circumstance bonus to the triggering check.",
                "No effect.",
                amped ? "" :"Your ally takes a -1 circumstance penalty to the triggering check."), Target.RangedCreature(100))
            .WithActionCost(0)
            .WithActionId(MActionIds.AidReaction)
            .WithEffectOnEachTarget((_, _, _, result) =>
            {
                if (result == CheckResult.Failure)
                    return Task.CompletedTask;
                if (spell != null && result == CheckResult.CriticalFailure)
                    return Task.CompletedTask;
                int bonus = result switch
                {
                    CheckResult.CriticalSuccess => CriticalBonusFromProficiency(rank),
                    CheckResult.Success => 1,
                    _ => -1 // Crit fail
                };
                if (amped)
                {
                    bonus = result switch
                    {
                        CheckResult.CriticalSuccess => rank == Proficiency.Legendary ? 4 : 3,
                        _ => 2 
                    };
                }
                aidableAction.Owner.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                {
                    BonusToAttackRolls = (_, aidedAction, _) =>
                        aidedAction == aidableAction
                            ? new Bonus(bonus, BonusType.Circumstance, $"Aid {result.HumanizeLowerCase2()}")
                            : null
                });
                if (amped && spell != null)
                {
                    aidableAction.Owner.AddQEffect(new QEffect()
                    {
                        BonusToDamage = (_, aidedAction, _) =>
                            aidedAction == aidableAction
                                ? new Bonus(bonus, BonusType.Circumstance, $"Aid {result.HumanizeLowerCase2()}")
                                : null,
                        AfterYouTakeAction = (effect, action) =>
                        {
                            if (action == aidableAction)
                            {
                                effect.ExpiresAt = ExpirationCondition.Immediately;
                            }
                            return Task.CompletedTask;
                        }
                    });
                }
                return Task.CompletedTask;
            });
        if (skill != null)
        {
            Skill occult = (Skill) skill;
            aid.WithActiveRollSpecification(new ActiveRollSpecification(TaggedChecks.SkillCheck(occult),
                Checks.FlatDC(AidDC())));
        }
        else if (spell != null)
        {
            aid.WithActiveRollSpecification(new ActiveRollSpecification(new TaggedCalculatedNumberProducer((_, creature, _) =>
            {
                List<int> nums = [];
                if (creature.Spellcasting?.Sources != null && creature.PersistentCharacterSheet != null)
                {
                    foreach (SpellcastingSource source in creature.Spellcasting.Sources.Where(source => source.ClassOfOrigin == Trait.Psychic))
                    {
                        nums.Add(source.SpellcastingAbilityModifier + creature.Proficiencies.Get(Trait.Spell).ToNumber(creature.ProficiencyLevel));
                    } 
                }
                int modifier = nums.Max();
                List<Bonus> bonuses = [];
                foreach (QEffect effect in creature.QEffects)
                {
                    Bonus? bonus = effect.BonusToAttackRolls?.Invoke(effect, spell, null);
                    if (bonus != null)
                        bonuses.Add(bonus);
                }
                return new CalculatedNumber(modifier, "Spell Attack Roll", bonuses!);
            }), Checks.FlatDC(AidDC())));
        }
        return aid;
    }

    internal static QEffect SetupReactionForGuidance(Creature self)
    {
        QEffect setupReaction = new("Reactive Guidance", "Reactive Guidance", ExpirationCondition.Never, self);
        setupReaction.AddGrantingOfTechnical(cr => cr.FriendOfAndNotSelf(self) && cr.DistanceTo(self) <= 24 && self.CanSee(cr), qfTech =>
        {
            qfTech.StateCheck = qf =>
            {
                qf.Owner.AddQEffect(ReactionGuidance(self));
            };
        });
        return setupReaction;
    }
    private static QEffect ReactionGuidance(Creature caster)
    {
        CombatAction? ampedGuidance = Possibilities.Create(caster).Filter(ap =>
        {
            if (ap.CombatAction.SpellInformation == null || ap.CombatAction.SpellId != SpellId.Guidance || ap.CombatAction.SpellInformation.PsychicAmpInformation is not { Amped: true })
                return false;
            ap.CombatAction.ActionCost = 0;
            ap.RecalculateUsability();
            return true;
        }).CreateActions(true).FirstOrDefault() as CombatAction;
        return new QEffect
        {
            BeforeYourActiveRoll = (effect, action, target) =>
            {
                Creature self = effect.Owner;
                var status = 0;
                List<int> statusBonuses = [];
                var bonusEnumerable = action.ActiveRollSpecification?.DetermineBonus(action, self, target).Bonuses;
                if (bonusEnumerable != null)
                    foreach (Bonus? bonus in bonusEnumerable)
                    {
                        if (bonus is { BonusType: BonusType.Status })
                            statusBonuses.Add(bonus.Amount);
                    }
                if (statusBonuses.Count > 0)
                    status = statusBonuses.Max();
                if (ampedGuidance == null ||
                    action.ActiveRollSpecification == null || status >= 1 ||
                    caster.Spellcasting is not { FocusPoints: >= 1 }) return Task.CompletedTask;
                QEffect checkRoll = new()
                {
                    RerollActiveRoll = async (eff, breakdown, combatAction, _) =>
                    {
                        if (combatAction.ActiveRollSpecification != null)
                        {
                            var defense = combatAction.ActiveRollSpecification.TaggedDetermineDC
                                .CalculatedNumberProducer.Invoke(combatAction, self, target).TotalNumber;
                            var fail = defense - breakdown.TotalRollValue;
                            if (((breakdown.CheckResult == CheckResult.Failure && fail == 1) || (breakdown.CheckResult == CheckResult.CriticalFailure && fail == 10)) && breakdown.D20Roll != 1)
                            {
                                if (await caster.AskToUseReaction(
                                        $"{self.Name}'s {action.Name} vs {target.Name} is about to {(breakdown.CheckResult == CheckResult.CriticalFailure ? "critically fail" : "fail")}. Use a reaction to cast amped guidance to turn the {breakdown.CheckResult.ToString()} into a {breakdown.CheckResult.ImproveByOneStep().ToString()}?"))
                                {
                                    await caster.Battle.GameLoop.FullCast(new CombatAction(caster, ampedGuidance.Illustration, ampedGuidance.Name, ampedGuidance.Traits.Append(Trait.DoNotShowInCombatLog).Append(Trait.DoNotShowOverheadOfActionName).ToArray(), ampedGuidance.Description, ampedGuidance.Target).WithActionCost(0).WithSoundEffect((SfxName)ampedGuidance.SoundEffectName!), ChosenTargets.CreateSingleTarget(self));
                                    caster.Overhead("Amped Guidance", Color.Black, caster + " casts {b}Amped Guidance{/b}.",
                                        ampedGuidance.Name + " {icon.Reaction}",
                                        ampedGuidance.Description, ampedGuidance.Traits);
                                    self.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                                    {
                                        BonusToAttackRolls = (_, _, _) =>
                                            new Bonus(1, BonusType.Status, "Amped Guidance")
                                    });
                                    caster.Spellcasting!.FocusPoints -= 1;
                                    eff.ExpiresAt = ExpirationCondition.Ephemeral;
                                    return RerollDirection.KeepRollButRedoCalculation;
                                }
                            }
                        }
                        eff.ExpiresAt = ExpirationCondition.Ephemeral;
                        return RerollDirection.DoNothing;
                    }
                };
                self.AddQEffect(checkRoll);
                return Task.CompletedTask;
            },
            BeforeYourSavingThrow = (effect, action, _) =>
            {
                Creature self = effect.Owner;
                var status = 0;
                List<int> statusBonuses = [];
                List<Bonus?> bonusEnumerable = [];
                foreach (QEffect qf in self.QEffects)
                {
                    if (qf.BonusToDefenses == null) continue;
                    if (action.SavingThrow == null) continue;
                    Bonus? bonus = qf.BonusToDefenses.Invoke(qf, action, action.SavingThrow.Defense);
                    bonusEnumerable.Add(bonus);
                }
                if (bonusEnumerable.Count > 0)
                    foreach (Bonus? bonus in bonusEnumerable)
                    {
                        if (bonus is { BonusType: BonusType.Status })
                            statusBonuses.Add(bonus.Amount);
                    }
                if (statusBonuses.Count > 0)
                    status = statusBonuses.Max();
                if (status >= 1 || ampedGuidance == null ||
                    caster.Spellcasting is not { FocusPoints: >= 1 }) return Task.CompletedTask;
                QEffect savingThrow = new()
                {
                    RerollSavingThrow = async (qEffect, breakdown, combatAction) =>
                    {
                        if (combatAction.SavingThrow != null)
                        {
                            var dc = combatAction.SavingThrow.DC.Invoke(combatAction.Owner);
                            var roll = breakdown.TotalRollValue;
                            var fail = dc - roll;
                            if (((breakdown.CheckResult == CheckResult.Failure && fail == 1) || (breakdown.CheckResult == CheckResult.CriticalFailure && fail == 10)) && breakdown.D20Roll != 1)
                            {
                                if (await caster.AskToUseReaction(
                                        $"{self.Name} is about to {(breakdown.CheckResult == CheckResult.CriticalFailure ? "critically fail" : "fail")} a saving throw against {action.Name}. Use a reaction to cast amped guidance to turn the {breakdown.CheckResult.ToString()} into a {breakdown.CheckResult.ImproveByOneStep().ToString()}?"))
                                {
                                    await caster.Battle.GameLoop.FullCast(new CombatAction(caster, ampedGuidance.Illustration, ampedGuidance.Name, ampedGuidance.Traits.Append(Trait.DoNotShowInCombatLog).Append(Trait.DoNotShowOverheadOfActionName).ToArray(), ampedGuidance.Description, ampedGuidance.Target).WithActionCost(0).WithSoundEffect((SfxName)ampedGuidance.SoundEffectName!), ChosenTargets.CreateSingleTarget(self));
                                    caster.Overhead("Amped Guidance", Color.Black, caster + " casts {b}Amped Guidance{/b}.",
                                        ampedGuidance.Name + " {icon.Reaction}",
                                        ampedGuidance.Description, ampedGuidance.Traits);
                                    self.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                                    {
                                        BonusToDefenses = (_, _, _) =>
                                            new Bonus(1, BonusType.Status, "Amped Guidance")
                                    });
                                    caster.Spellcasting!.FocusPoints -= 1;
                                    qEffect.ExpiresAt = ExpirationCondition.Ephemeral;
                                    return RerollDirection.KeepRollButRedoCalculation;
                                }
                            }
                        }
                        qEffect.ExpiresAt = ExpirationCondition.Ephemeral;
                        return RerollDirection.DoNothing;
                    }
                };
                self.AddQEffect(savingThrow);
                return Task.CompletedTask;
            },
            ExpiresAt = ExpirationCondition.Ephemeral
        };
    }
    private static string AorAn(string checkName)
    {
        return checkName.ToUpper()[0] switch
        {
            'A' => "an",
            'E' => "an",
            'I' => "an",
            'O' => "an",
            'U' => "an",
            _ => "a"
        };
    }
    internal static int AidDC()
    {
        return PlayerProfile.Instance.IsBooleanOptionEnabled("MoreBasicActions.AidDCIs15") ? 15 : 20;
    }

    private static int CriticalBonusFromProficiency(Proficiency rank)
    {
        switch (rank)
        {
            case Proficiency.Legendary:
                return 4;
            case Proficiency.Master:
                return 3;
            default:
                return 2;
        }
    }
}