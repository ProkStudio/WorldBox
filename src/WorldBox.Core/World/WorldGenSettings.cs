namespace WorldBox.Core.World;

/// <summary>Ручки генератора мира. Значения по умолчанию подобраны под карту 512x512.</summary>
public struct WorldGenSettings
{
    /// <summary>Доля суши от всей карты.</summary>
    public float LandFraction;

    /// <summary>Частота континентов: меньше — крупнее материки.</summary>
    public float ContinentScale;

    /// <summary>Сила горных хребтов в итоговой высоте.</summary>
    public float MountainStrength;

    /// <summary>Какая доля суши становится руслами рек.</summary>
    public float RiverFraction;

    /// <summary>Сколько тайлов вглубь материка ещё чувствуется влага океана.</summary>
    public int OceanMoistureReach;

    /// <summary>Как сильно горы запирают дождь с наветренной стороны.</summary>
    public float RainShadow;

    public static WorldGenSettings Default => new WorldGenSettings
    {
        LandFraction = 0.34f,
        ContinentScale = 0.0045f,
        MountainStrength = 0.30f,
        RiverFraction = 0.020f,
        OceanMoistureReach = 70,
        RainShadow = 1.15f,
    };
}
