namespace WorldBox.Core.Economy;

/// <summary>
/// Сколько товара даёт земля за один прогон экономики.
/// Все числа считаются на один тайл владений и потом множатся на число тайлов.
/// </summary>
/// <param name="FoodPerFertileTile">Еда с плодородного тайла.</param>
/// <param name="FoodPerTile">Еда с любого тайла: собирательство и охота.</param>
/// <param name="ResourcePerTile">Добыча с тайла, на котором есть ресурс.</param>
/// <param name="SettlementBonus">Насколько одно поселение ускоряет всю добычу народа.</param>
/// <param name="EraBonus">Насколько каждая эпоха ускоряет добычу.</param>
public sealed record EconomyProductionSettings(
    float FoodPerFertileTile,
    float FoodPerTile,
    float ResourcePerTile,
    float SettlementBonus,
    float EraBonus);

/// <summary>Сколько товара съедается за один прогон.</summary>
/// <param name="FoodPerPerson">Еда на одного агента.</param>
/// <param name="GoodsPerPerson">Прочие товары на одного агента.</param>
/// <param name="GoodsPerSettlement">Прочие товары на одно поселение: стройка и ремонт.</param>
/// <param name="DemandFloorTiles">
/// Добавка к весу спроса, будто у народа есть столько тайлов товара.
/// Спрос на сырьё делится по своей земле, и без этой добавки товар, которого в земле нет вовсе,
/// вообще не покупался бы на рынке.
/// </param>
public sealed record EconomyConsumptionSettings(
    float FoodPerPerson,
    float GoodsPerPerson,
    float GoodsPerSettlement,
    float DemandFloorTiles);

/// <summary>Склады: сколько можно держать и сколько пропадает.</summary>
/// <param name="PerTile">Вместимость на один тайл владений.</param>
/// <param name="PerSettlement">Вместимость на одно поселение.</param>
/// <param name="SpoilShare">Какая доля запаса портится за прогон.</param>
/// <param name="StartFoodShare">Какую долю склада еды народ имеет на старте партии.</param>
/// <param name="GranaryPerPerson">Сколько еды город держит в своём амбаре на одного жителя.</param>
/// <param name="GranaryFillShare">Какую долю амбара город успевает довезти со склада народа за один прогон.</param>
/// <param name="SiegeDrainShare">Какую долю амбара город проедает за один прогон осады.</param>
public sealed record EconomyStorageSettings(
    float PerTile,
    float PerSettlement,
    float SpoilShare,
    float StartFoodShare,
    float GranaryPerPerson,
    float GranaryFillShare,
    float SiegeDrainShare);

/// <summary>Цены. Цена растёт от дефицита и падает от избытка.</summary>
/// <param name="Base">Базовая цена товара со стоимостью 1 при полном складе.</param>
/// <param name="Min">Нижний предел множителя цены.</param>
/// <param name="Max">Верхний предел множителя цены.</param>
/// <param name="CoverTarget">Сколько прогонов запаса считается нормой.</param>
/// <param name="ScarcityWeight">Насколько сильно дефицит толкает цену вверх.</param>
/// <param name="Smoothing">С какой скоростью цена идёт к новому значению: 1 — сразу.</param>
public sealed record EconomyPriceSettings(
    float Base,
    float Min,
    float Max,
    float CoverTarget,
    float ScarcityWeight,
    float Smoothing);

/// <summary>Торговые пути между поселениями разных народов.</summary>
/// <param name="MaxRoutes">Сколько путей держим одновременно.</param>
/// <param name="RangeTiles">Дальность сухопутного пути в тайлах.</param>
/// <param name="SeaRangeTiles">Дальность морского пути в тайлах.</param>
/// <param name="SeaEra">С какой эпохи народ водит корабли.</param>
/// <param name="RebuildTicks">Раз во сколько тиков сеть путей собирается заново.</param>
/// <param name="FlowShare">Какая доля разницы запасов едет по пути за прогон.</param>
/// <param name="FlowCap">Потолок перевозки по одному пути за прогон.</param>
/// <param name="ResearchBonusPerRoute">Насколько один путь ускоряет исследования.</param>
/// <param name="ResearchBonusMax">Потолок торгового ускорения исследований.</param>
/// <param name="ProfitShare">Какая доля цены груза остаётся в богатстве народа.</param>
public sealed record EconomyTradeSettings(
    int MaxRoutes,
    int RangeTiles,
    int SeaRangeTiles,
    int SeaEra,
    int RebuildTicks,
    float FlowShare,
    float FlowCap,
    float ResearchBonusPerRoute,
    float ResearchBonusMax,
    float ProfitShare);
