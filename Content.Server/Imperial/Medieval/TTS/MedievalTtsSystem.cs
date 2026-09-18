using System.Linq;
using System.Reflection;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.TTS;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.TTS;

public sealed class MedievalTtsSystem : EntitySystem
{
    private const string VoicePrototypeKind = "ttsVoice";

    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public void ApplyProfileVoice(EntityUid uid, HumanoidCharacterProfile profile, bool randomProfile)
    {
        var voicePrototypes = GetVoicePrototypes();
        var voice = randomProfile
            ? null
            : GetProfileVoice(profile);

        if (string.IsNullOrWhiteSpace(voice) || voicePrototypes.All(prototype => prototype.ID != voice))
            voice = GetRandomVoice(voicePrototypes, profile.Sex);

        if (string.IsNullOrWhiteSpace(voice))
            return;

        var provider = EnsureComp<TTSProviderComponent>(uid);
        provider.Voice = voice;

        var voicePrototypeId = provider.GetType().GetProperty(
            "VoicePrototypeId",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (voicePrototypeId is { CanWrite: true } &&
            voicePrototypeId.PropertyType == typeof(string))
        {
            voicePrototypeId.SetValue(provider, voice);
        }

        Dirty(uid, provider);
    }

    private List<IPrototype> GetVoicePrototypes()
    {
        if (!_prototypeManager.TryGetKindType(VoicePrototypeKind, out var prototypeType))
            return new List<IPrototype>();

        try
        {
            return _prototypeManager.EnumeratePrototypes(prototypeType).ToList();
        }
        catch (InvalidOperationException)
        {
            return new List<IPrototype>();
        }
    }

    private string? GetRandomVoice(IReadOnlyList<IPrototype> prototypes, Sex sex)
    {
        var voices = prototypes
            .Where(prototype => IsRandomVoice(prototype, sex))
            .Select(prototype => prototype.ID)
            .ToList();

        return voices.Count == 0
            ? null
            : _random.Pick(voices);
    }

    private static bool IsRandomVoice(IPrototype prototype, Sex sex)
    {
        if (!TryGetValue<Sex>(prototype, "Sex", out var voiceSex) || voiceSex != sex)
            return false;

        if (TryGetValue<bool>(prototype, "IsAI", out var isAi) && isAi ||
            TryGetValue<bool>(prototype, "SponsorOnly", out var sponsorOnly) && sponsorOnly ||
            TryGetValue<bool>(prototype, "CanRandomDrop", out var canRandomDrop) && !canRandomDrop ||
            TryGetValue<bool>(prototype, "RoundStart", out var roundStart) && !roundStart ||
            TryGetValue<int>(prototype, "SponsorTier", out var sponsorTier) && sponsorTier > 1)
        {
            return false;
        }

        return true;
    }

    private static string? GetProfileVoice(HumanoidCharacterProfile profile)
    {
        if (TryGetValue<string>(profile, "TTSVoice", out var ttsVoice))
            return ttsVoice;

        if (TryGetValue<string>(profile, "VoicePrototypeId", out var voicePrototypeId))
            return voicePrototypeId;

        return TryGetValue<string>(profile, "SelectedVoiceProtoId", out var selectedVoicePrototypeId)
            ? selectedVoicePrototypeId
            : null;
    }

    private static bool TryGetValue<T>(object instance, string name, out T value)
    {
        var type = instance.GetType();
        var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property?.GetValue(instance) is T propertyValue)
        {
            value = propertyValue;
            return true;
        }

        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field?.GetValue(instance) is T fieldValue)
        {
            value = fieldValue;
            return true;
        }

        value = default!;
        return false;
    }
}
