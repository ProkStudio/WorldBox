namespace WorldBox.Core.Eras;

/// <summary>
/// Что народ получает от торговли. Система эпох спрашивает это у экономики,
/// но работает и без неё: без рынка доступ к чужим ресурсам даёт только общая граница.
/// Интерфейс живёт рядом с эпохами, чтобы Core.Eras не знал про Core.Economy.
/// </summary>
public interface ITradeAccess
{
    /// <summary>Маска ресурсов, которые народ получает по торговым путям.</summary>
    int TradeResourcesOf(int tribe);

    /// <summary>Насколько торговля ускоряет исследования: 0 — никак.</summary>
    float ResearchBonusOf(int tribe);
}
