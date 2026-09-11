using WorldBox.Core.Art;
using WorldBox.Core.World;
using Xunit;

namespace WorldBox.Tests;

public class EdgeArtTests
{
    [Fact]
    public void PriorityTableCoversEveryTexture()
    {
        Assert.Equal(Enum.GetValues<TileTexture>().Length, EdgeArt.PriorityCount);
    }

    [Fact]
    public void WaterOutranksLandAndSandOutranksGrass()
    {
        Assert.True(EdgeArt.Priority(TileTexture.Water) > EdgeArt.Priority(TileTexture.Forest));
        Assert.True(EdgeArt.Priority(TileTexture.Sand) > EdgeArt.Priority(TileTexture.Grass));
        Assert.True(EdgeArt.Priority((Biome)1) > EdgeArt.Priority((Biome)10));
    }

    [Fact]
    public void EdgeStaysNearItsOwnBorder()
    {
        var mask = new byte[TileArt.Pixels];

        for (int variant = 0; variant < EdgeArt.Variants; variant++)
        {
            EdgeArt.Build(0, variant, mask);

            for (int y = 0; y < TileArt.TileSize; y++)
            {
                for (int x = 0; x < TileArt.TileSize; x++)
                {
                    if (mask[(y * TileArt.TileSize) + x] != 0)
                    {
                        Assert.True(y < EdgeArt.MaxDepth);
                    }
                }
            }
        }
    }

    [Fact]
    public void EdgeCoversItsWholeSide()
    {
        var mask = new byte[TileArt.Pixels];

        EdgeArt.Build(0, 0, mask);
        for (int x = 0; x < TileArt.TileSize; x++)
        {
            Assert.True(mask[x] != 0);
        }

        EdgeArt.Build(3, 0, mask);
        for (int y = 0; y < TileArt.TileSize; y++)
        {
            Assert.True(mask[y * TileArt.TileSize] != 0);
        }
    }

    [Fact]
    public void BuildIsDeterministic()
    {
        var first = new byte[TileArt.Pixels];
        var second = new byte[TileArt.Pixels];

        EdgeArt.Build(2, 3, first);
        EdgeArt.Build(2, 3, second);

        Assert.Equal(first, second);
    }

    [Fact]
    public void VariantForIsStableAndInRange()
    {
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                for (int side = 0; side < EdgeArt.Sides; side++)
                {
                    int variant = EdgeArt.VariantFor(x, y, side);
                    Assert.InRange(variant, 0, EdgeArt.Variants - 1);
                    Assert.Equal(variant, EdgeArt.VariantFor(x, y, side));
                }
            }
        }
    }
}
