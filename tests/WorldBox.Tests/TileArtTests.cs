using WorldBox.Core.Art;
using WorldBox.Core.World;
using Xunit;

namespace WorldBox.Tests;

public class TileArtTests
{
    [Fact]
    public void ClassTableCoversEveryBiome()
    {
        Assert.Equal(Biomes.Count, TileArt.ClassCount);
    }

    [Fact]
    public void ShadesStayInRange()
    {
        var tile = new byte[TileArt.Pixels];

        for (int index = 0; index < Biomes.Count; index++)
        {
            for (int variant = 0; variant < TileArt.Variants; variant++)
            {
                TileArt.Build((Biome)index, variant, 0, tile);
                foreach (byte shade in tile)
                {
                    Assert.True(shade < TileArt.Shades);
                }
            }
        }
    }

    [Fact]
    public void BuildIsDeterministic()
    {
        var first = new byte[TileArt.Pixels];
        var second = new byte[TileArt.Pixels];

        TileArt.Build((Biome)10, 2, 1, first);
        TileArt.Build((Biome)10, 2, 1, second);

        Assert.Equal(first, second);
    }

    [Fact]
    public void WaterHasMovingFrames()
    {
        var ocean = (Biome)1;
        Assert.True(TileArt.IsAnimated(ocean));
        Assert.Equal(TileArt.Frames, TileArt.FrameCount(ocean));

        var first = new byte[TileArt.Pixels];
        var second = new byte[TileArt.Pixels];
        TileArt.Build(ocean, 0, 0, first);
        TileArt.Build(ocean, 0, 2, second);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void LandDoesNotAnimate()
    {
        var grassland = (Biome)10;
        Assert.False(TileArt.IsAnimated(grassland));
        Assert.Equal(1, TileArt.FrameCount(grassland));
    }

    [Fact]
    public void VariantsLookDifferent()
    {
        var first = new byte[TileArt.Pixels];
        var second = new byte[TileArt.Pixels];

        TileArt.Build((Biome)10, 0, 0, first);
        TileArt.Build((Biome)10, 1, 0, second);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void EveryTileHasDetailButIsNotNoise()
    {
        var tile = new byte[TileArt.Pixels];

        for (int index = 0; index < Biomes.Count; index++)
        {
            TileArt.Build((Biome)index, 0, 0, tile);

            int detail = 0;
            foreach (byte shade in tile)
            {
                if (shade != 0)
                {
                    detail++;
                }
            }

            Assert.InRange(detail, 4, TileArt.Pixels - 4);
        }
    }

    [Fact]
    public void VariantForIsStableAndInRange()
    {
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                int variant = TileArt.VariantFor(x, y);
                Assert.InRange(variant, 0, TileArt.Variants - 1);
                Assert.Equal(variant, TileArt.VariantFor(x, y));
            }
        }
    }
}
