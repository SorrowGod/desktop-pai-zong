using DesktopPet.App.Pet;

namespace DesktopPet.Tests;

[TestClass]
public sealed class SpriteCatalogTests
{
    [TestMethod]
    public void SpecialFourthRowAnimationsUseTheirAssignedColumns()
    {
        Assert.AreEqual(2, SpriteCatalog.GetDefinitionForTesting(PetAnimationState.Drag).StartColumn);
        Assert.AreEqual(3, SpriteCatalog.GetDefinitionForTesting(PetAnimationState.Reaction).StartColumn);
        Assert.AreEqual(4, SpriteCatalog.GetDefinitionForTesting(PetAnimationState.Reaction).FrameCount);
    }
}
