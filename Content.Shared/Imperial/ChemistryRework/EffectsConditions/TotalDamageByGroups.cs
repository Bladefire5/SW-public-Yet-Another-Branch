using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Localizations;
using Robust.Shared.Prototypes;

namespace Content.Shared.Chemistry.ReagentEffectConditions
{
    public sealed partial class TotalDamageByGroups : EntityEffectCondition
    {
        [DataField("max")]
        public FixedPoint2 Max = FixedPoint2.MaxValue;

        [DataField("min")]
        public FixedPoint2 Min = FixedPoint2.Zero;

        [DataField("damageTypes")]
        public List<ProtoId<DamageTypePrototype>> DamageTypes = new();

        [DataField("damageGroups")]
        public List<ProtoId<DamageGroupPrototype>> DamageGroups = new();

        public override bool Condition(EntityEffectBaseArgs args)
        {
            if (!args.EntityManager.TryGetComponent<DamageableComponent>(args.TargetEntity, out var entityDamage)) return false;
            FixedPoint2 totalDamage = new();

            DamageTypes.ForEach(type => totalDamage += entityDamage.Damage[type]);
            DamageGroups.ForEach(group => totalDamage += entityDamage.DamagePerGroup[group]);

            return totalDamage > Min && totalDamage < Max;
        }

        public override string GuidebookExplanation(IPrototypeManager prototype)
        {
            return Loc.GetString("reagent-effect-condition-guidebook-total-by-groups-damage",
                ("max", Max == FixedPoint2.MaxValue ? (float) int.MaxValue : Max.Float()),
                ("min", Min.Float()),
                ("groups", ContentLocalizationManager.FormatList(DamageGroups)),
                ("types", ContentLocalizationManager.FormatList(DamageTypes))
            );
        }
    }
}
