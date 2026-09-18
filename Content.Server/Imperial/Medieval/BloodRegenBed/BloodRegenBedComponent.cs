namespace Content.Server.Imperial.Medieval.BloodRegenBed
{
    [RegisterComponent]
    public sealed partial class BloodRegenBedComponent : Component
    {
        [DataField("bloodRegenMultiplier", required: true)]
        public float BloodRegenMultiplier = 10.0f; // Добавляем 10 единиц крови каждые 5 секунд
        
        // imperial medieval - how much bleeding severity is staunched per tick. 0 = no change to vanilla behaviour
        [DataField("bleedReduction")]
        public float BleedReduction = 0f;
    }
}
