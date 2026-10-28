using Content.Client.Guidebook;
using Content.Client.Guidebook.Richtext;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Imperial.EngineCompatibility;

[TestFixture]
public sealed class GuideMarginMigrationTest
{
    [TestCase("8", 8, 8, 8, 8)]
    [TestCase("0 8", 0, 8, 0, 8)]
    [TestCase("1 2 3 4", 1, 2, 3, 4)]
    public async Task BoxMarginsUseTheUpstreamEmbedThicknessSyntax(
        string margin, int left, int top, int right, int bottom)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Client.WaitAssertion(() =>
        {
            var parser = pair.Client.ResolveDependency<DocumentParsingManager>();
            using var root = new Control();
            Assert.That(parser.TryAddMarkup(root,
                $"<Box Orientation=\"Vertical\" Margin=\"{margin}\"/><ColorBox Margin=\"{margin}\"/>"), Is.True);
            Assert.That(root.ChildCount, Is.EqualTo(2));
            Assert.That(root.GetChild(0), Is.TypeOf<Box>());
            Assert.That(root.GetChild(1), Is.TypeOf<ColorBox>());
            foreach (var control in root.Children)
                Assert.That(control.Margin, Is.EqualTo(new Thickness(left, top, right, bottom)));
        });
        await pair.CleanReturnAsync();
    }
}
