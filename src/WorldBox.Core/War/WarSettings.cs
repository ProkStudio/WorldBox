namespace WorldBox.Core.War;

/// <summary>Армии: сколько отрядов народ выставляет, чем за них платит и как быстро войско ходит.</summary>
/// <param name="MaxArmies">Сколько отрядов держим на карте одновременно.</param>
/// <param name="MenPerArmy">Численность свежего отряда.</param>
/// <param name="MenPerThousandPeople">Сколько воинов народ поднимает с тысячи жителей.</param>
/// <param name="GoldPerMan">Цена набора одного воина.</param>
/// <param name="UpkeepPerMan">Плата за одного воина за прогон.</param>
/// <param name="MarchTilesPerRun">Сколько тайлов отряд проходит за прогон.</param>
/// <param name="RetreatTiles">На сколько тайлов отходит разбитый отряд.</param>
/// <param name="MoraleStart">Мораль свежего отряда.</param>
/// <param name="MoraleRecovery">Насколько мораль растёт за прогон на своей земле.</param>
/// <param name="SupplyLossPerRun">Насколько падает снабжение на чужой земле за прогон.</param>
/// <param name="StarveLossShare">Какую долю людей теряет отряд, на который не хватило казны.</param>
/// <param name="StarveMoraleDrop">Насколько падает мораль такого отряда.</param>
/// <param name="DisbandMen">Ниже какой численности отряд расходится.</param>
public sealed record ArmySettings(
    int MaxArmies,
    int MenPerArmy,
    float MenPerThousandPeople,
    float GoldPerMan,
    float UpkeepPerMan,
    int MarchTilesPerRun,
    int RetreatTiles,
    float MoraleStart,
    float MoraleRecovery,
    float SupplyLossPerRun,
    float StarveLossShare,
    float StarveMoraleDrop,
    int DisbandMen);

/// <summary>Бой: из чего складывается сила отряда и чем кончается встреча.</summary>
/// <param name="EraPowerWeight">Насколько военная сила эпохи умножает отряд.</param>
/// <param name="MoraleWeight">Насколько мораль добавляет силы.</param>
/// <param name="TerrainDefense">Прибавка за бой на своей земле.</param>
/// <param name="SupplyFloor">Нижний предел вклада снабжения: голодное войско всё же драётся.</param>
/// <param name="LossShareWinner">Какую долю людей теряет победитель.</param>
/// <param name="LossShareLoser">Какую долю людей теряет проигравший.</param>
/// <param name="MoraleHitLoser">Насколько падает мораль проигравшего.</param>
/// <param name="MoraleGainWinner">Насколько растёт мораль победителя.</param>
/// <param name="RetreatMorale">Ниже какой морали отряд отступает к себе домой.</param>
/// <param name="CiviliansPerSoldier">Сколько жителей умирает на одного погибшего воина.</param>
/// <param name="LossRadius">В каком радиусе от места боя ищем погибших жителей.</param>
public sealed record BattleSettings(
    float EraPowerWeight,
    float MoraleWeight,
    float TerrainDefense,
    float SupplyFloor,
    float LossShareWinner,
    float LossShareLoser,
    float MoraleHitLoser,
    float MoraleGainWinner,
    float RetreatMorale,
    float CiviliansPerSoldier,
    int LossRadius);

/// <summary>Осада города и что бывает после взятия.</summary>
/// <param name="ProgressPerRun">Насколько полное войско продвигает осаду за прогон.</param>
/// <param name="ReliefPerRun">Насколько осада спадает, когда войско ушло.</param>
/// <param name="WallsPerLevel">Насколько каждый уровень поселения замедляет осаду.</param>
/// <param name="MenForFullProgress">Сколько людей нужно для полной скорости осады.</param>
/// <param name="StarveMinProgress">С какого хода осады голод может сдать город.</param>
/// <param name="StarvePerPerson">Ниже какого запаса еды на жителя город сдаётся.</param>
/// <param name="SackShare">Какую долю казны и жителей теряет взятый город.</param>
/// <param name="AnnexRadius">В каком радиусе земля меняет владельца вместе с городом.</param>
public sealed record SiegeSettings(
    float ProgressPerRun,
    float ReliefPerRun,
    float WallsPerLevel,
    int MenForFullProgress,
    float StarveMinProgress,
    float StarvePerPerson,
    float SackShare,
    int AnnexRadius);

/// <summary>Причины войн и условия мира. Без усталости и предела длины войны шли бы вечно.</summary>
/// <param name="CheckRuns">Раз во сколько прогонов пересматриваются отношения.</param>
/// <param name="MinEra">С какой эпохи народ вообще воюет.</param>
/// <param name="LandHunger">Насколько теснота толкает к войне.</param>
/// <param name="ResourceGreed">Насколько толкает нужда в чужом сырье.</param>
/// <param name="Ambition">Постоянная добавка: амбиции правителя.</param>
/// <param name="AllyJoin">Вероятность, что союзник вступит в чужую войну.</param>
/// <param name="DeclarationsPerCheck">Сколько войн может начаться за один пересмотр.</param>
/// <param name="MaxChancePerCheck">Потолок вероятности объявления войны за пересмотр.</param>
/// <param name="ExhaustionPerSoldier">Насколько один погибший воин копит усталость от войны.</param>
/// <param name="ExhaustionDecay">Насколько усталость спадает за прогон.</param>
/// <param name="PeaceExhaustion">При какой усталости сторона идёт на мир.</param>
/// <param name="MaxWarRuns">Сколько прогонов война живёт максимум.</param>
/// <param name="TruceRuns">Сколько прогонов держится перемирие после мира.</param>
public sealed record WarCauseSettings(
    int CheckRuns,
    int MinEra,
    float LandHunger,
    float ResourceGreed,
    float Ambition,
    float AllyJoin,
    int DeclarationsPerCheck,
    float MaxChancePerCheck,
    float ExhaustionPerSoldier,
    float ExhaustionDecay,
    float PeaceExhaustion,
    int MaxWarRuns,
    int TruceRuns);

/// <summary>Восстания на захваченных землях.</summary>
/// <param name="UnrestOnCapture">Сколько недовольства даёт взятие города.</param>
/// <param name="UnrestDecay">Насколько недовольство спадает за прогон.</param>
/// <param name="ChancePerRun">Вероятность бунта за прогон.</param>
/// <param name="MinUnrest">Ниже этого недовольства бунта не будет.</param>
/// <param name="FreeTribeChance">Вероятность, что бунт даст новый народ, а не вернёт старого владельца.</param>
public sealed record RevoltSettings(
    float UnrestOnCapture,
    float UnrestDecay,
    float ChancePerRun,
    float MinUnrest,
    float FreeTribeChance);
