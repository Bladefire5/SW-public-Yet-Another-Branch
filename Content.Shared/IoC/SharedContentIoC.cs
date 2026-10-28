using Content.Shared.Humanoid.Markings;
using Content.Shared.Imperial.Entry;
using Content.Shared.Localizations;
using Content.Shared.Imperial.Medieval.Poise.LightAttack; // spellward

namespace Content.Shared.IoC
{
    public static class SharedContentIoC
    {
        public static void Register()
        {
            IoCManager.Register<MarkingManager, MarkingManager>();
            IoCManager.Register<ContentLocalizationManager, ContentLocalizationManager>();
            // spellward - persist the visual packet router across client worlds.
            IoCManager.Register<AdvancedLightAttackNetwork, AdvancedLightAttackNetwork>();

            ImperialEntry.IoCRegister();
        }
    }
}
