using Content.Client.Paper.UI;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Imperial.EngineCompatibility;

[TestFixture]
public sealed class PaperMarginMigrationTest
{
    [Test]
    public async Task AsymmetricPaperMarginsLoadAndKeepTheirOriginalEdges()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Client.WaitAssertion(() =>
        {
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var book = (PaperVisualsComponent) prototypes.Index<EntityPrototype>("BookBase").Components["PaperVisuals"].Component;
            Assert.Multiple(() =>
            {
                Assert.That(book.BackgroundPatchMargin.Left, Is.EqualTo(23));
                Assert.That(book.BackgroundPatchMargin.Top, Is.EqualTo(15));
                Assert.That(book.BackgroundPatchMargin.Right, Is.EqualTo(14));
                Assert.That(book.BackgroundPatchMargin.Bottom, Is.EqualTo(16));
            });

            var paper = (PaperVisualsComponent) prototypes.Index<EntityPrototype>("PaperArtifactAnalyzer").Components["PaperVisuals"].Component;
            Assert.Multiple(() =>
            {
                Assert.That(paper.HeaderMargin.Top, Is.EqualTo(16));
                Assert.That(paper.HeaderMargin.Bottom, Is.Zero);
                Assert.That(paper.ContentMargin.Top, Is.Zero);
                Assert.That(paper.ContentMargin.Bottom, Is.EqualTo(16));
            });
        });
        await pair.CleanReturnAsync();
    }
}
