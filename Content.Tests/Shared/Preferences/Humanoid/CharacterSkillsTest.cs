using Content.Shared.Preferences;
using NUnit.Framework;

namespace Content.Tests.Shared.Preferences.Humanoid;

[TestFixture]
public sealed class CharacterSkillsTest
{
    [Test]
    public void EquivalentSkillSelectionsCompareByValueRegardlessOfInsertionOrder()
    {
        var original = new HumanoidCharacterProfile { Skills = new() { ["Strength"] = 12, ["Agility"] = 8 } };
        var restored = new HumanoidCharacterProfile { Skills = new() { ["Agility"] = 8, ["Strength"] = 12 } };
        Assert.That(restored.MemberwiseEquals(original), Is.True);

        restored.Skills["Strength"] = 13;
        Assert.That(restored.MemberwiseEquals(original), Is.False);
        restored.Skills.Remove("Strength");
        restored.Skills["Endurance"] = 12;
        Assert.That(restored.MemberwiseEquals(original), Is.False);
    }

    [Test]
    public void ClonedProfilesOwnTheirSkillSelections()
    {
        var original = new HumanoidCharacterProfile { Skills = new() { ["Strength"] = 12 } };
        var edited = original.Clone();
        Assert.That(edited.MemberwiseEquals(original), Is.True);
        edited.Skills["Strength"] = 15;
        edited.Skills["Agility"] = 8;
        Assert.That(original.Skills["Strength"], Is.EqualTo(12));
        Assert.That(original.Skills.ContainsKey("Agility"), Is.False);
        Assert.That(edited.MemberwiseEquals(original), Is.False);
    }

    [Test]
    public void EmptySkillSelectionsCompareByValue()
    {
        Assert.That(new HumanoidCharacterProfile().MemberwiseEquals(new HumanoidCharacterProfile()), Is.True);
    }
}
