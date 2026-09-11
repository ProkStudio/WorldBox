using WorldBox.Core.Art;
using WorldBox.Core.World;
using Xunit;

namespace WorldBox.Tests;

/// <summary>
/// Проверки арта. Сами картинки тестами не проверить, но можно проверить главное:
/// палитра покрывает все биомы, сетка везде одна и та же, а выбор варианта рисунка
/// детерминирован — иначе один и тот же мир выглядел бы при каждом запуске по-разному.
/// </summary>
public sealed class ArtTests
{
    [Fact]
    public void PaletteCoversEveryBiome()
    {
        Assert.Equal(Biomes.Count, ArtPalette.BiomeCount);
        Assert.Equal(4, ArtPalette.Shades);
    }

    [Fact]
    public void TileGridIsSixteenPixels()
    {
        Assert.Equal(16, TileArt.TileSize);
        Assert.Equal(TileArt.TileSize * TileArt.TileSize, TileArt.Pixels);
        Assert.Equal(4, TileArt.Variants);
        Assert.Equal(ArtPalette.Shades, TileArt.Shades);
    }

    [Fact]
    public void DecorAndIconsShareTheTileGrid()
    {
        Assert.Equal(TileArt.TileSize, DecorArt.Size);
        Assert.Equal(TileArt.TileSize, IconArt.Size);
        Assert.Equal(3, DecorArt.Variants);
        Assert.Equal(29, IconArt.KindCount);
    }

    [Fact]
    public void TileVariantIsStableAndInRange()
    {
        for (int y = 0; y < 40; y++)
        {
            for (int x = 0; x < 40; x++)
            {
                int variant = TileArt.VariantFor(x, y);
                Assert.InRange(variant, 0, TileArt.Variants - 1);
                Assert.Equal(variant, TileArt.VariantFor(x, y));
            }
        }
    }

    [Fact]
    public void NothingGrowsInWater()
    {
        for (int b = 0; b < Biomes.Count; b++)
        {
            var biome = (Biome)b;
            if (Biomes.IsWater(biome))
            {
                Assert.Equal(0, DecorArt.DensityPercent(biome));
            }
        }
    }

    [Fact]
    public void ForestsAreDenserThanMeadows()
    {
        int forest = DecorArt.DensityPercent(Biome.TemperateForest);
        int rainforest = DecorArt.DensityPercent(Biome.Rainforest);
        int meadow = DecorArt.DensityPercent(Biome.Grassland);
        int desert = DecorArt.DensityPercent(Biome.Desert);

        Assert.Equal(82, forest);
        Assert.Equal(88, rainforest);
        Assert.Equal(14, meadow);
        Assert.True(forest > meadow, "Лес должен быть гуще луга.");
        Assert.True(meadow > desert, "На лугу должно расти больше, чем в пустыне.");
    }

    [Fact]
    public void EveryBiomeHasSaneDensity()
    {
        for (int b = 0; b < Biomes.Count; b++)
        {
            int density = DecorArt.DensityPercent((Biome)b);
            Assert.InRange(density, 0, 100);
        }
    }
}
