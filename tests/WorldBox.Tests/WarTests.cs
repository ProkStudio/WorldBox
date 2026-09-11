using WorldBox.Core;
using WorldBox.Core.Economy;
using WorldBox.Core.Eras;
using WorldBox.Core.People;
using WorldBox.Core.Simulation;
using WorldBox.Core.Tribes;
using WorldBox.Core.War;
using WorldBox.Core.World;
using Xunit;

namespace WorldBox.Tests;

/// <summary>
/// Проверки войны: отряды, дипломатия, бой, осада, восстания и повторимость прогона.
/// Мир для боевых проверок собирается вручную — сплошная суша без случайной географии,
/// иначе отряд упирался бы в море и проверка ловила бы не бой, а карту.
/// </summary>
public sealed class WarTests
{
    /// <summary>Маленький мир: проверки должны идти быстро.</summary>
    private const int BattleWorld = 24;

    private const int SiegeWorld = 32;

    /// <summary>Столько людей народу мало для набора: проверка боя не должна ловить новые отряды.</summary>
    private const int TooFewForLevy = 100;

    [Fact]
    public void TableLoadsWithSaneNumbers()
    {
        WarTable table = LoadWar();

        Assert.True(table.Armies.MenPerArmy > 0, "Отряд нулевой численности.");
        Assert.True(table.Armies.MaxArmies > 0, "На карте не может быть ни одного отряда.");
        Assert.True(table.Armies.DisbandMen < table.Armies.MenPerArmy, "Свежий отряд сразу расходится.");
        Assert.True(table.Battle.LossShareLoser > table.Battle.LossShareWinner, "Проигравший теряет меньше победителя.");
        Assert.True(table.Siege.AnnexRadius > 0, "Взятый город не приносит земли.");
        Assert.True(table.War.TruceRuns > 0, "После мира можно сразу объявить новую войну.");
        Assert.True(table.War.MaxWarRuns > table.War.CheckRuns, "Война кончается раньше первого пересмотра отношений.");
        Assert.InRange(table.Revolt.FreeTribeChance, 0f, 1f);
        Assert.InRange(table.Revolt.MinUnrest, 0f, 1f);
    }

    [Fact]
    public void ArmyStoreReusesFreedSlots()
    {
        var armies = new ArmyStore(4);

        int first = armies.Raise(1, 1, 1, 50, 0.8f);
        int second = armies.Raise(2, 2, 1, 50, 0.8f);
        Assert.Equal(2, armies.Count);
        Assert.Equal(100, armies.MenOf(1));

        Assert.True(armies.Disband(first));
        int third = armies.Raise(3, 3, 2, 30, 0.5f);

        Assert.Equal(first, third);
        Assert.Equal(2, armies.Count);
        Assert.Equal(1, armies.CountOf(1));
        Assert.Equal(50, armies.MenOf(1));
        Assert.Equal(30, armies.MenOf(2));
        Assert.Equal(ArmyStore.NoTarget, armies.Target[second]);

        // Пустой отряд не поднимается: иначе казна платила бы за ноль воинов.
        Assert.Equal(-1, armies.Raise(4, 4, 1, 0, 0.5f));
    }

    [Fact]
    public void DiplomacyIsSymmetricAndKeepsTruce()
    {
        var diplomacy = new Diplomacy(8);

        Assert.True(diplomacy.Declare(1, 2, 10));
        Assert.True(diplomacy.IsAtWar(2, 1));
        Assert.Equal(1, diplomacy.Wars[1]);
        Assert.Equal(1, diplomacy.WarCount);
        Assert.Equal(10L, diplomacy.SinceRun(2, 1));
        Assert.False(diplomacy.Declare(2, 1, 12));

        Assert.True(diplomacy.MakePeace(1, 2, 40));
        Assert.False(diplomacy.IsAtWar(1, 2));
        Assert.Equal(0, diplomacy.Wars[2]);
        Assert.True(diplomacy.TruceActive(2, 1, 39), "Перемирие кончилось раньше срока.");
        Assert.False(diplomacy.TruceActive(2, 1, 40));

        Assert.True(diplomacy.SetAlliance(1, 3));
        Assert.True(diplomacy.IsAlly(3, 1));

        // Народ исчез — его войны и союзы не должны достаться новому хозяину слота.
        diplomacy.Forget(1);
        Assert.False(diplomacy.IsAlly(3, 1));
        Assert.Equal(0, diplomacy.Wars[1]);
    }

    /// <summary>
    /// Требование замысла: передовое малое войско обычно бьёт большое отсталое.
    /// Пятьдесят человек современной эпохи против четырёхсот из палеолита.
    /// </summary>
    [Fact]
    public void AdvancedArmyBeatsBiggerBackwardOne()
    {
        WorldState world = FlatWorld(BattleWorld, 101);
        var tribes = new TribeStore();
        var settlements = new SettlementStore();
        var territory = new Territory(BattleWorld, BattleWorld, tribes.Capacity);
        var people = new Population(64, BattleWorld, BattleWorld);
        var armies = new ArmyStore(16);
        var diplomacy = new Diplomacy(tribes.Capacity);

        short advanced = tribes.Create("Передовые", 1, 4, 12);
        short backward = tribes.Create("Отсталые", 2, 20, 12);
        tribes.Era[advanced] = 9;
        tribes.Era[backward] = 0;
        tribes.People[advanced] = TooFewForLevy;
        tribes.People[backward] = TooFewForLevy;
        Assert.True(diplomacy.Declare(advanced, backward, 0));

        int small = armies.Raise(12, 12, advanced, 50, 0.8f);
        int big = armies.Raise(13, 12, backward, 400, 0.8f);

        var war = new WarSystem(LoadWar(), LoadEras(), people, tribes, settlements, territory, armies, diplomacy);
        var loop = new SimulationLoop(world, war);
        loop.RunTicks(10);

        Assert.True(war.TotalBattles > 0, "Отряды стояли рядом, а боя не было.");
        Assert.True(armies.Alive[small], "Передовой отряд погиб от вчетверо большего числа дикарей.");

        int smallLost = 50 - armies.Men[small];
        int bigLost = 400 - armies.Men[big];
        Assert.True(
            bigLost > smallLost * 5,
            "Отсталое войско потеряло не больше передового: " + bigLost + " против " + smallLost + ".");
    }

    /// <summary>
    /// Требование замысла: города берут осадой, и политическая карта после этого меняется —
    /// земля вокруг города переходит новому хозяину.
    /// </summary>
    [Fact]
    public void SiegeTakesCityAndLandChangesOwner()
    {
        WorldState world = FlatWorld(SiegeWorld, 202);
        var tribes = new TribeStore();
        var settlements = new SettlementStore();
        var territory = new Territory(SiegeWorld, SiegeWorld, tribes.Capacity);
        var people = new Population(64, SiegeWorld, SiegeWorld);
        var armies = new ArmyStore(16);
        var diplomacy = new Diplomacy(tribes.Capacity);

        short attacker = tribes.Create("Осаждающие", 1, 4, 16);
        short defender = tribes.Create("Горожане", 2, 20, 16);
        tribes.Era[attacker] = 4;
        tribes.Era[defender] = 1;
        tribes.People[attacker] = TooFewForLevy;
        tribes.People[defender] = TooFewForLevy;
        tribes.Settlements[attacker] = 0;
        tribes.Settlements[defender] = 1;

        int city = settlements.Found(20, 16, defender, "Крепость", 0L);
        settlements.People[city] = 50;
        settlements.Food[city] = 0f;
        Claim(territory, world, 20, 16, 3, defender);
        int nearTile = world.Index(22, 16);
        Assert.Equal(defender, territory.OwnerAt(nearTile));

        Assert.True(diplomacy.Declare(attacker, defender, 0));
        armies.Raise(19, 16, attacker, 200, 0.9f);

        var war = new WarSystem(LoadWar(), LoadEras(), people, tribes, settlements, territory, armies, diplomacy);
        var loop = new SimulationLoop(world, war);
        loop.RunTicks(120);

        Assert.True(war.TotalCaptures > 0, "Голодный город без гарнизона не сдался за сто двадцать тиков.");
        Assert.Equal(attacker, settlements.Tribe[city]);
        Assert.True(settlements.Captured[city] > 0L, "У города не отмечен тик взятия.");
        Assert.True(settlements.Unrest[city] > 0.3f, "Взятый город принял нового хозяина без недовольства.");
        Assert.Equal(attacker, territory.OwnerAt(nearTile));
        Assert.Equal(0f, settlements.Siege[city]);
    }

    /// <summary>Требование замысла: войны кончаются сами. Уставшая сторона идёт на мир.</summary>
    [Fact]
    public void ExhaustedSideMakesPeaceByItself()
    {
        WorldState world = FlatWorld(BattleWorld, 303);
        var tribes = new TribeStore();
        var settlements = new SettlementStore();
        var territory = new Territory(BattleWorld, BattleWorld, tribes.Capacity);
        var people = new Population(64, BattleWorld, BattleWorld);
        var armies = new ArmyStore(16);
        var diplomacy = new Diplomacy(tribes.Capacity);
        WarTable table = LoadWar();

        short first = tribes.Create("Уставшие", 1, 4, 4);
        short second = tribes.Create("Свежие", 2, 18, 18);
        tribes.Era[first] = 3;
        tribes.Era[second] = 3;
        tribes.People[first] = TooFewForLevy;
        tribes.People[second] = TooFewForLevy;

        // У обоих есть города: иначе мир заключился бы по другому правилу — стороне нечего терять.
        settlements.Found(4, 4, first, "Свой", 0L);
        settlements.Found(18, 18, second, "Чужой", 0L);
        tribes.Settlements[first] = 1;
        tribes.Settlements[second] = 1;

        Assert.True(diplomacy.Declare(first, second, 0));
        diplomacy.Exhaustion[first] = table.War.PeaceExhaustion + 0.2f;

        var war = new WarSystem(table, LoadEras(), people, tribes, settlements, territory, armies, diplomacy);
        var loop = new SimulationLoop(world, war);
        loop.RunTicks(120);

        Assert.False(diplomacy.IsAtWar(first, second), "Уставшая сторона не заключила мир.");
        Assert.True(diplomacy.PeaceCount > 0);
        Assert.Equal(0, war.ActiveWars);
        Assert.True(diplomacy.TruceActive(first, second, 0L), "После мира не встало перемирие.");
    }

    /// <summary>Второй способ кончить войну: побеждённому нечего защищать.</summary>
    [Fact]
    public void WarEndsWhenSideHasNoSettlementsLeft()
    {
        WorldState world = FlatWorld(BattleWorld, 404);
        var tribes = new TribeStore();
        var settlements = new SettlementStore();
        var territory = new Territory(BattleWorld, BattleWorld, tribes.Capacity);
        var people = new Population(64, BattleWorld, BattleWorld);
        var armies = new ArmyStore(16);
        var diplomacy = new Diplomacy(tribes.Capacity);

        short winner = tribes.Create("Победитель", 1, 4, 4);
        short loser = tribes.Create("Разбитый", 2, 18, 18);
        tribes.Era[winner] = 3;
        tribes.Era[loser] = 3;
        tribes.People[winner] = TooFewForLevy;
        tribes.People[loser] = TooFewForLevy;
        settlements.Found(4, 4, winner, "Столица", 0L);
        tribes.Settlements[winner] = 1;
        tribes.Settlements[loser] = 0;

        Assert.True(diplomacy.Declare(winner, loser, 0));

        var war = new WarSystem(LoadWar(), LoadEras(), people, tribes, settlements, territory, armies, diplomacy);
        var loop = new SimulationLoop(world, war);
        loop.RunTicks(120);

        Assert.False(diplomacy.IsAtWar(winner, loser), "Война идёт с тем, у кого нет ни одного города.");
    }

    /// <summary>Требование замысла: захваченная земля иногда бунтует.</summary>
    [Fact]
    public void CapturedCityRevoltsAgainstConqueror()
    {
        WorldState world = FlatWorld(SiegeWorld, 505);
        var tribes = new TribeStore();
        var settlements = new SettlementStore();
        var territory = new Territory(SiegeWorld, SiegeWorld, tribes.Capacity);
        var people = new Population(256, SiegeWorld, SiegeWorld);
        var armies = new ArmyStore(16);
        var diplomacy = new Diplomacy(tribes.Capacity);

        short holder = tribes.Create("Завоеватель", 1, 4, 4);
        short origin = tribes.Create("Прежние", 2, 16, 16);
        tribes.Era[holder] = 4;
        tribes.Era[origin] = 4;
        tribes.People[holder] = TooFewForLevy;
        tribes.People[origin] = TooFewForLevy;
        tribes.Settlements[holder] = 1;
        tribes.Settlements[origin] = 0;

        int city = settlements.Found(16, 16, holder, "Взятый", 5L);
        settlements.People[city] = 40;
        settlements.Radius[city] = 2;
        settlements.Origin[city] = origin;
        settlements.Captured[city] = 5L;
        settlements.Unrest[city] = 1f;
        Claim(territory, world, 16, 16, 2, holder);

        var war = new WarSystem(LoadWar(), LoadEras(), people, tribes, settlements, territory, armies, diplomacy);
        var loop = new SimulationLoop(world, war);
        loop.RunTicks(400);

        Assert.True(war.TotalRevolts > 0, "Город с полным недовольством не поднял бунта за четыреста тиков.");
        Assert.NotEqual(holder, settlements.Tribe[city]);
        short rebel = settlements.Tribe[city];
        Assert.True(tribes.Alive[rebel], "Город достался мёртвому народу.");
        Assert.Equal(rebel, territory.OwnerAt(16, 16));
        Assert.True(settlements.Unrest[city] < 1f, "После бунта недовольство осталось прежним.");
    }

    /// <summary>
    /// Повторимость: два одинаковых мира обязаны дать одни и те же войны, отряды и города.
    /// Без этого летопись и разбор партии теряют смысл.
    /// </summary>
    [Fact]
    public void WarRunIsRepeatable()
    {
        WarSystem firstWar = RunWorld(4242, out ArmyStore firstArmies, out Diplomacy firstDiplomacy, out SettlementStore firstSettlements, out Population firstPeople);
        WarSystem secondWar = RunWorld(4242, out ArmyStore secondArmies, out Diplomacy secondDiplomacy, out SettlementStore secondSettlements, out Population secondPeople);

        Assert.Equal(firstArmies.Checksum(), secondArmies.Checksum());
        Assert.Equal(firstDiplomacy.Checksum(), secondDiplomacy.Checksum());
        Assert.Equal(firstSettlements.Checksum(), secondSettlements.Checksum());
        Assert.Equal(firstPeople.Checksum(), secondPeople.Checksum());
        Assert.Equal(firstWar.TotalBattles, secondWar.TotalBattles);
        Assert.Equal(firstWar.TotalCaptures, secondWar.TotalCaptures);
        Assert.Equal(firstWar.TotalDeaths, secondWar.TotalDeaths);

        // Проверка бессмысленна, если в этом мире вообще не воевали.
        Assert.True(firstDiplomacy.DeclarationCount > 0, "В мире не случилось ни одной войны.");
        Assert.True(firstWar.PeakArmies > 0, "Никто не поднял ни одного отряда.");
    }

    /// <summary>Сплошная суша: проверки боя и осады не должны зависеть от случайной географии.</summary>
    private static WorldState FlatWorld(int size, int seed)
    {
        var map = new WorldMap(size, size);
        for (int index = 0; index < map.TileCount; index++)
        {
            map.BiomeAt[index] = (byte)Biome.Grassland;
            map.Fertility[index] = 0.5f;
        }

        map.Recount();

        var world = new WorldState(size, size, seed);
        world.SetMap(map);
        return world;
    }

    private static void Claim(Territory territory, WorldState world, int centerX, int centerY, int radius, short tribe)
    {
        for (int y = centerY - radius; y <= centerY + radius; y++)
        {
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                if (world.InBounds(x, y))
                {
                    territory.Claim(world.Index(x, y), tribe);
                }
            }
        }
    }

    private static WarTable LoadWar()
    {
        WarTable? table = WarTable.Load(out string error);
        Assert.True(table != null, "Таблица войны не прочиталась: " + error);
        return table!;
    }

    private static EraTable LoadEras()
    {
        EraTable? table = EraTable.Load(out string error);
        Assert.True(table != null, "Таблица эпох не прочиталась: " + error);
        return table!;
    }

    /// <summary>Собирает живой мир со всеми системами и прогоняет его, чтобы войны начались сами.</summary>
    private static WarSystem RunWorld(
        int seed,
        out ArmyStore armies,
        out Diplomacy diplomacy,
        out SettlementStore settlements,
        out Population people)
    {
        const int size = 96;
        const int ticks = 900;

        EconomyTable? economyTable = EconomyTable.Load(out string economyError);
        Assert.True(economyTable != null, "Таблица товаров не прочиталась: " + economyError);

        WorldMap map = WorldGenerator.Generate(size, size, seed);
        var world = new WorldState(size, size, seed);
        world.SetMap(map);

        people = new Population(Population.DefaultCapacity, size, size);
        var tribes = new TribeStore();
        settlements = new SettlementStore();
        var territory = new Territory(size, size, tribes.Capacity);
        var tech = new TribeTech(tribes.Capacity);

        TribeSeeder.Seed(world, people, tribes, settlements, territory, 5, 60);

        var market = new TribeMarket(tribes.Capacity, economyTable!.Count);
        var routes = new TradeNetwork(economyTable.Trade.MaxRoutes);
        WarTable warTable = LoadWar();
        armies = new ArmyStore(warTable.Armies.MaxArmies);
        diplomacy = new Diplomacy(tribes.Capacity);

        // Народам дают эпоху и казну: в палеолите без денег войн не бывает, а проверять надо войну.
        for (short tribe = 1; tribe < tribes.Capacity; tribe++)
        {
            if (tribes.Alive[tribe])
            {
                tribes.Era[tribe] = 4;
                market.Wealth[tribe] = 20000f;
            }
        }

        var populationSystem = new PopulationSystem(people, null, tech);
        var settlementSystem = new SettlementSystem(people, tribes, settlements, territory);
        var economySystem = new EconomySystem(economyTable, tribes, settlements, territory, market, routes, diplomacy);
        var warSystem = new WarSystem(warTable, LoadEras(), people, tribes, settlements, territory, armies, diplomacy, market);

        var loop = new SimulationLoop(world, populationSystem, settlementSystem, economySystem, warSystem);
        loop.RunTicks(ticks);
        return warSystem;
    }
}
