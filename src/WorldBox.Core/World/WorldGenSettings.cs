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

    /// <summary>Где лежит пояс пассатных пустынь: 0 — экватор, 1 — полюс.</summary>
    public float DryBeltLatitude;

    /// <summary>Ширина этого пояса по широте.</summary>
    public float DryBeltWidth;

    /// <summary>Насколько сильно пояс сушит землю под собой: 0 — никак, 1 — насухо.</summary>
    public float DryBeltStrength;

    /// <summary>Какая доля суши становится горами.</summary>
    public float MountainShare;

    /// <summary>Какая доля суши становится вершинами (входит в MountainShare).</summary>
    public float PeakShare;

    public static WorldGenSettings Default => new WorldGenSettings
    {
        LandFraction = 0.34f,
        ContinentScale = 0.0045f,
        MountainStrength = 0.30f,
        RiverFraction = 0.020f,
        OceanMoistureReach = 70,
        RainShadow = 1.15f,
        DryBeltLatitude = 0.42f,
        DryBeltWidth = 0.18f,
        DryBeltStrength = 0.55f,
        MountainShare = 0.14f,
        PeakShare = 0.035f,
    };
}
