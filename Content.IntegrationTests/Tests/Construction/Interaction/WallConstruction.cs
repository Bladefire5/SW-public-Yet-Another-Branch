using Content.IntegrationTests.Tests.Interaction;
using Robust.Shared.GameObjects; //spellward

namespace Content.IntegrationTests.Tests.Construction.Interaction;

public sealed class WallConstruction : InteractionTest
{
    public const string Girder = "Girder";
    public const string WallSolid = "WallSolid";
    public const string Wall = "Wall";

    [Test]
    public async Task ConstructWall()
    {
        await StartConstruction(Wall);
        await InteractUsing(Steel, 2);
        Assert.That(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands)), Is.Null);
        ClientAssertPrototype(Girder, Target);
        Assert.That(Comp<TransformComponent>().Anchored, Is.True); //spellward
        await InteractUsing(Steel, 2);
        Assert.That(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands)), Is.Null);
        AssertPrototype(WallSolid);
        Assert.That(Comp<TransformComponent>().Anchored, Is.True); //spellward
    }

    [Test]
    public async Task DeconstructWall()
    {
        await StartDeconstruction(WallSolid);
        await InteractUsing(Weld);
        AssertPrototype(Girder);
        Assert.That(Comp<TransformComponent>().Anchored, Is.True); //spellward
        await Interact(Wrench, Screw);
        AssertDeleted();
        await AssertEntityLookup((Steel, 4));
    }
}
