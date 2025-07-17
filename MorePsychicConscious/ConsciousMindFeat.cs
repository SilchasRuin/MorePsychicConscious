using System.Runtime.InteropServices;
using Dawnsbury.Auxiliary;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.Mechanics.Enumerations;

namespace MorePsychicConscious;

public abstract class ConsciousMindFeat
{
    public static SpellId StandardPsiCantrip {get; set;}
    public static SpellId StandardPsiCantrip2 {get; set;}
    public static SpellId UniquePsiCantrip {get; set;}
  
    public static Feat CreateConsciousMindFeat2(
    FeatName name,
    string flavorText,
    SpellId[] grantedSpells,
    SpellId standardPsiCantrip,
    SpellId standardPsiCantrip2,
    SpellId uniquePsiCantrip,
    SpellId? deeperCantrip)
  {
    string rulesText = $"You know additional spells and psi cantrips. Your psi cantrips are more powerful than normal, and you can make them even more powerful by spending a focus point to cast them \"amped\".\r\n\r\n{{b}}Extra spells known{{/b}} {string.Join(", ", grantedSpells.Select((Func<SpellId, int, string>) ((gs, index) => $"{(index + 1).Ordinalize2()}: {AllSpells.CreateModernSpellTemplate(gs, Trait.Psychic).ToSpellLink()}")))}\n{{b}}Psi cantrips{{/b}} {AllSpells.CreateModernSpellTemplate(standardPsiCantrip, Trait.Psychic).ToSpellLink()}, {AllSpells.CreateModernSpellTemplate(standardPsiCantrip2, Trait.Psychic).ToSpellLink()}, {AllSpells.CreateModernSpellTemplate(uniquePsiCantrip, Trait.Psychic).ToSpellLink()}, deeper (unlocked at character level 6): {(deeperCantrip.HasValue ? AllSpells.CreateModernSpellTemplate(deeperCantrip.Value, Trait.Psychic).ToSpellLink() : "{i}none{/i}")}";
    Feat feat = new(name, flavorText, rulesText, [Trait.ConsciousMind], null);
    StandardPsiCantrip = standardPsiCantrip;
    StandardPsiCantrip2 = standardPsiCantrip2;
    UniquePsiCantrip = uniquePsiCantrip;
    feat.WithOnSheet(sheet =>
    {
      SpellRepertoire repertoire = sheet.SpellRepertoires[Trait.Psychic];
      Spell psiCantrip1 = AllSpells.CreateModernSpell(standardPsiCantrip, null, sheet.MaximumSpellLevel, false, new SpellInformation()
      {
        ClassOfOrigin = Trait.Psychic,
        PsychicAmpInformation = new PsychicAmpInformation()
      });
      repertoire.SpellsKnown.Add(psiCantrip1);
      Spell psiCantrip2 = AllSpells.CreateModernSpell(standardPsiCantrip2, null, sheet.MaximumSpellLevel, false, new SpellInformation()
      {
        ClassOfOrigin = Trait.Psychic,
        PsychicAmpInformation = new PsychicAmpInformation()
      });
      repertoire.SpellsKnown.Add(psiCantrip2);
      Spell psiCantripU = AllSpells.CreateModernSpell(uniquePsiCantrip, null, sheet.MaximumSpellLevel, false, new SpellInformation()
      {
        ClassOfOrigin = Trait.Psychic,
        PsychicAmpInformation = new PsychicAmpInformation()
      });
      repertoire.SpellsKnown.Add(psiCantripU);
      for (int index = 0; index < grantedSpells.Length; ++index)
        repertoire.SpellsKnown.Add(AllSpells.CreateModernSpellTemplate(grantedSpells[index], Trait.Psychic, index + 1));
      if (deeperCantrip != null)
      {
        Spell psiCantripD = AllSpells.CreateModernSpell(deeperCantrip.Value, null, sheet.MaximumSpellLevel, false, new SpellInformation()
        {
          ClassOfOrigin = Trait.Psychic,
          PsychicAmpInformation = new PsychicAmpInformation()
        });
        sheet.AddAtLevel(6, _ =>
        {
          repertoire.SpellsKnown.Add(psiCantripD);
        });
      }
    });
    return feat;
  }
}