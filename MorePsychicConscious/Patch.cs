using Dawnsbury.Core;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Creatures.Parts;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Display.Illustrations;
using HarmonyLib;
using Microsoft.Xna.Framework;

namespace MorePsychicConscious;

[HarmonyPatch(typeof(Checks), nameof(Checks.RollTumbleThrough))]
internal static class PatchTumbleThrough
{
    internal static bool Prefix(Creature movingCreature, Creature defendingCreature, ref Task<bool> __result)
    {
        if (movingCreature.HasEffect(ModData.MQEffectIds.GhostThrough) &&
            !movingCreature.HasEffect(ModData.MQEffectIds.Ghost))
        {
            __result = Patch.AltTumbleThrough(movingCreature, defendingCreature);
            return false;
        }
        return true;
    }
}
internal static class Patch
{
    internal static async Task<bool> AltTumbleThrough(Creature movingCreature, Creature defendingCreature)
        {
            CombatAction? combatActionTumbleThrough = new CombatAction(movingCreature,
                (Illustration)IllustrationName.Tumble64, "Tumble through", [
                    Trait.Move,
                    Trait.Basic,
                    Trait.AttackDoesNotTargetAC,
                    Trait.UnaffectedByConcealment,
                    Trait.DoNotShowOverheadOfCheckResult,
                    Trait.IsNotHostile,
                    Trait.DoNotShowOverheadOfActionName,
                    Trait.ActionDoesNotRequireLegalTarget,
                    Trait.DoNotShowInCombatLog
                ],
                "Make an Acrobatics check against the Reflex DC of an enemy you want to move through during your Stride.\r\n• On a success, you move through that enemy, counting its space as though it were difficult terrain.\r\n• On a failure, you don't enter its space and your movement ends immediately.",
                (Target)Target.Touch())
            .WithActiveRollSpecification(new ActiveRollSpecification(TaggedChecks.SkillCheck(Skill.Acrobatics),
                TaggedChecks.DefenseDC(Defense.Reflex))).WithActionCost(0).WithActionId(ActionId.TumbleThrough);
            int num = await movingCreature.Battle.GameLoop.FullCast(combatActionTumbleThrough,
                ChosenTargets.CreateSingleTarget(defendingCreature))
                ? 1
                : 0;
        bool flag1 = combatActionTumbleThrough.CheckResult >= CheckResult.Success;
        bool flag2 = flag1;
        combatActionTumbleThrough = null;
        return flag2;
        }
}