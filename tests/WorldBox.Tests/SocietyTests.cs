using WorldBox.Core;
using WorldBox.Core.Economy;
using WorldBox.Core.Eras;
using WorldBox.Core.People;
using WorldBox.Core.Simulation;
using WorldBox.Core.Society;
using WorldBox.Core.Tribes;
using WorldBox.Core.War;
using WorldBox.Core.World;
using Xunit;

namespace WorldBox.Tests;

/// <summary>
/// Проверки общества: таблица правил, склады вер и культур, раскол большой веры,
/// давление недовольных городов на власть и повторимость длинного прогона.
/// Мир для точных проверок собирается вручную: случайная география ловила бы не общество, а карту.
/// </summary>
public sealed class SocietyTests
{
    /// <summary>Маленький мир для проверок веры: города должны быть соседями.</summary>
    private const int FaithWorld = 40;

    /// <summary>Столько лет нужно, чтобы большая вера успела разойтись и расколоться.</summary>
    private const int SchismTicks = 4000;

    [Fact]
    public void TableLoadsWithSaneNumbers()
    {
        SocietyTable table = LoadSociety();

        Assert.True(table.Religion.MaxReligions > 0, "В мире не может быть ни одной веры.");
        Assert.True(table.Religion.MinPeople > 0, "Вера рождается в городе без людей.");
        Assert.True(table.Religion.SchismMinFollowers > 0, "Раскол разрешён вере без последователей.");
        Assert.True(
            table.Religion.YoungRuns > 0,
            "Молодая вера ничем не защищена: расколы будут гаснуть в тот же прогон.");
        Assert.True(table.Culture.MaxCultures > 0, "В мире не может быть ни одной культуры.");
        Assert.True(table.Culture.Languages > 0, "Культурам не из чего брать язык.");
        Assert.True(table.Ideology.ChangeRuns > 0, "Власть пересматривалась бы каждый прогон.");
        Assert.True(
            table.Stability.UnrestWeight > 0f,
            "Недовольство городов не влияет на стабильность.");
        Assert.True(
            table.Stability.CollapseThreshold > 0f && table.Stability.CollapseThreshold < 1f,
            "Порог смуты вне пределов 0..1: " + table.Stability.CollapseThreshold);
        Assert.True(table.FormCount > 0, "В таблице нет ни одной формы власти.");

        int tribeForm = table.Find("tribe");
        Assert.True(tribeForm >= 0, "Формы власти tribe нет в таблице.");
        Assert.True(table.Allows(tribeForm, 0), "Молодому народу нечем управлять в нулевой эпохе.");
        Assert.True(
            table.EraFit(tribeForm, table.FormMinEra[tribeForm])
                > table.EraFit(tribeForm, table.FormMaxEra[tribeForm] + 3),
            "Форма власти вне своего времени подходит не хуже, чем в своё.");
        Assert.Equal("ideology.none", table.NameKeyOf(SocietyState.NoIdeology));
        Assert.Equal("ideology.tribe", table.NameKeyOf(tribeForm));

        SocietyTable? broken = SocietyTable.Parse("{}", out string error);
        Assert.True(broken == null, "Пустой JSON прочитался как таблица общества.");
        Assert.Contains("society.json", error);
    }

    /// <summary>
    /// Форма власти привязана к эпохе: рано её не взять, а старая со временем подходит всё хуже.
    /// Открытую форму никто не запрещает — устаревание считается соответствием, а не запретом.
    /// </summary>
    [Fact]
    public void PowerFormFollowsEra()
    {
        SocietyTable table = LoadSociety();

        int early = table.Find("tribe");
        int late = table.Find("technocracy");
        Assert.True(early >= 0, "Формы власти tribe нет в таблице.");
        Assert.True(late >= 0, "Формы власти technocracy нет в таблице.");

        Assert.True(table.Allows(early, 0), "Молодой народ остался без власти.");
        Assert.True(
            table.EraFit(early, 9) < table.EraFit(early, 1),
            "Племенная власть в девятой эпохе подходит не хуже, чем в первой: " + table.EraFit(early, 9));
        Assert.True(table.Allows(late, 10), "В последней эпохе нет своей формы власти.");
        Assert.False(table.Allows(late, 3), "Технократия появилась в бронзе.");
    }

    /// <summary>Склад вер помнит родителя раскола и возвращает освободившиеся места.</summary>
    [Fact]
    public void ReligionStoreTracksSchismsAndReusesSlots()
    {
        var religions = new ReligionStore(8);

        short mother = religions.Found("Старая вера", 1, 1, ReligionStore.None, Dogma.Order, 100);
        Assert.True(mother >= 0, "Первая вера не получила места на складе.");
        religions.Followers[mother] = 900;
        religions.Settlements[mother] = 5;

        short child = religions.Found("Новая вера", 2, 1, mother, Dogma.Knowledge | Dogma.Trade, 200);
        Assert.True(child >= 0, "Расколу не нашлось места на складе.");
        religions.Followers[child] = 300;
        religions.Settlements[child] = 2;

        Assert.Equal(2, religions.Count);
        Assert.Equal(1, religions.SchismCount());
        Assert.Equal(mother, religions.Parent[child]);
        Assert.Equal(mother, religions.Largest());
        Assert.Equal("Новая вера", religions.NameOf(child));
        Assert.Equal(Dogma.Knowledge | Dogma.Trade, religions.DogmasOf(child));
        Assert.True(religions.IsAlive(child), "Свежая вера считается мёртвой.");
        Assert.False(religions.IsAlive(ReligionStore.None), "Пустая вера считается живой.");

        Assert.True(religions.Remove(child), "Вера не убралась со склада.");
        Assert.Equal(1, religions.Count);
        Assert.Equal(0, religions.SchismCount());

        short reborn = religions.Found("Третья вера", 3, 2, ReligionStore.None, Dogma.War, 300);
        Assert.True(reborn >= 0, "Третьей вере не нашлось места.");
        Assert.True(
            religions.HighWater <= 2,
            "Склад вер не переиспользовал освободившееся место: " + religions.HighWater);
        Assert.Equal(ReligionStore.None, religions.Parent[reborn]);
        Assert.Equal(3, religions.Born);
    }

    /// <summary>Склад культур держит язык и тоже возвращает места.</summary>
    [Fact]
    public void CultureStoreKeepsLanguageAndReusesSlots()
    {
        var cultures = new CultureStore(4);

        short first = cultures.Found("Эрины", 1, 2, 50);
        short second = cultures.Found("Тавры", 2, 4, 60);
        Assert.True(first >= 0 && second >= 0, "Культурам не нашлось места на складе.");
        cultures.People[first] = 700;
        cultures.People[second] = 200;

        Assert.Equal(2, cultures.Count);
        Assert.Equal(2, (int)cultures.LanguageOf(first));
        Assert.Equal(4, (int)cultures.LanguageOf(second));
        Assert.Equal(0, (int)cultures.LanguageOf(CultureStore.None));
        Assert.Equal(first, cultures.Largest());
        Assert.Equal("Тавры", cultures.NameOf(second));

        Assert.True(cultures.Remove(first), "Культура не убралась со склада.");
        Assert.False(cultures.IsAlive(first), "Убранная культура осталась живой.");
        Assert.Equal(second, cultures.Largest());

        short third = cultures.Found("Сиаи", 3, 1, 70);
        Assert.True(third >= 0, "Третьей культуре не нашлось места.");
        Assert.True(
            cultures.HighWater <= 2,
            "Склад культур не переиспользовал место: " + cultures.HighWater);
    }

    /// <summary>Строки погибших народов и городов чистятся: иначе новый владелец слота получит чужую веру.</summary>
    [Fact]
    public void StateForgetsDeadRowsAndChangesChecksum()
    {
        var state = new SocietyState(8, 16);

        state.Religion[3] = 2;
        state.Culture[3] = 1;
        state.Ideology[3] = 4;
        state.Stability[3] = 0.7f;
        state.SettlementReligion[5] = 2;
        state.SettlementCulture[5] = 1;

        ulong filled = state.Checksum();
        int version = state.Version;
        state.Touch();
        Assert.NotEqual(version, state.Version);

        state.ForgetTribe(3);
        Assert.Equal(ReligionStore.None, state.Religion[3]);
        Assert.Equal(CultureStore.None, state.Culture[3]);
        Assert.Equal(SocietyState.NoIdeology, state.Ideology[3]);

        state.ForgetSettlement(5);
        Assert.Equal(ReligionStore.None, state.SettlementReligion[5]);
        Assert.Equal(CultureStore.None, state.SettlementCulture[5]);
        Assert.NotEqual(filled, state.Checksum());

        state.Clear();
        Assert.Equal(0f, state.Stability[3]);
    }

    /// <summary>
    /// Критерий среза: недовольные города расшатывают власть. Раньше все слагаемые
    /// шли только в плюс и стабильность у всех стояла в потолке, а смут не бывало вовсе.
    /// </summary>
    [Fact]
    public void UnrestPullsStabilityDown()
    {
        WorldState world = FlatWorld(FaithWorld, 4242);
        var tribes = new TribeStore();
        var settlements = new SettlementStore();

        short calm = tribes.Create("Тихие", 1, 8, 8);
        short loud = tribes.Create("Шумные", 2, 32, 32);

        int calmSeat = settlements.Found(8, 8, calm, "Тишина", 0);
        int loudSeat = settlements.Found(32, 32, loud, "Гул", 0);
        settlements.People[calmSeat] = 200;
        settlements.People[loudSeat] = 200;
        settlements.Unrest[calmSeat] = 0f;
        settlements.Unrest[loudSeat] = 1f;

        SocietyTable table = LoadSociety();
        var religions = new ReligionStore();
        var cultures = new CultureStore();
        var state = new SocietyState(tribes.Capacity, settlements.Capacity);
        var society = new SocietySystem(table, tribes, settlements, state, religions, cultures);

        society.Tick(world);

        Assert.True(
            society.StabilityOf(loud) < society.StabilityOf(calm),
            "Недовольный город не давит на власть: "
                + society.StabilityOf(loud) + " против " + society.StabilityOf(calm));
        Assert.True(
            society.StabilityOf(calm) > 0f && society.StabilityOf(calm) <= 1f,
            "Стабильность спокойного народа вне пределов 0..1: " + society.StabilityOf(calm));
        Assert.True(
            society.StabilityOf(loud) >= 0f,
            "Стабильность ушла ниже нуля: " + society.StabilityOf(loud));
        Assert.True(society.IdeologyOf(calm) >= 0, "Народ остался без формы власти.");
        Assert.True(
            table.Allows(society.IdeologyOf(calm), tribes.Era[calm]),
            "Форма власти не подходит эпохе народа.");
        Assert.True(cultures.IsAlive(state.Culture[calm]), "Народ остался без культуры.");
    }

    /// <summary>
    /// Критерий среза: большая вера раскалывается, и раскол доживает хотя бы до пересчёта.
    /// Раньше господствующая вера перекрашивала одинокий город обратно в тот же прогон.
    /// </summary>
    [Fact]
    public void BigFaithSplitsAndSchismSurvives()
    {
        WorldState world = FlatWorld(FaithWorld, 777);
        var tribes = new TribeStore();
        var settlements = new SettlementStore();

        short tribe = tribes.Create("Староверы", 1, FaithWorld / 2, FaithWorld / 2);
        tribes.Era[tribe] = 4;

        for (int i = 0; i < 8; i++)
        {
            int x = (FaithWorld / 2) - 6 + ((i % 4) * 4);
            int y = (FaithWorld / 2) - 3 + ((i / 4) * 6);
            int seat = settlements.Found(x, y, tribe, "Город " + (i + 1), 0);
            Assert.True(seat >= 0, "Городу номер " + i + " не нашлось места.");
            settlements.People[seat] = 120;
        }

        SocietyTable table = LoadSociety();
        var religions = new ReligionStore();
        var cultures = new CultureStore();
        var state = new SocietyState(tribes.Capacity, settlements.Capacity);
        var society = new SocietySystem(table, tribes, settlements, state, religions, cultures);
        var loop = new SimulationLoop(world, society);

        int peakFaiths = 0;
        int peakSchisms = 0;
        for (int step = 0; step < SchismTicks / 100; step++)
        {
            loop.RunTicks(100);
            peakFaiths = Math.Max(peakFaiths, religions.Count);
            peakSchisms = Math.Max(peakSchisms, religions.SchismCount());
        }

        Assert.True(society.TotalBirths > 0, "За четыре тысячи лет не родилось ни одной веры.");
        Assert.True(peakFaiths > 1, "В народе ни разу не было двух вер сразу: " + peakFaiths);
        Assert.True(society.TotalSchisms > 0, "Большая вера ни разу не раскололась.");
        Assert.True(
            peakSchisms > 0,
            "Раскол не дожил до пересчёта: молодая вера ничем не защищена.");
    }

    /// <summary>
    /// Длинный живой мир: у каждого народа есть культура и власть по эпохе,
    /// города не держатся мёртвых вер, а два одинаковых прогона дают одни и те же суммы.
    /// </summary>
    [Fact]
    public void SocietyGrowsAndStaysRepeatable()
    {
        SocietyTable table = LoadSociety();

        SocietySystem first = RunSociety(
            20260912,
            out ReligionStore religionsA,
            out CultureStore culturesA,
            out SocietyState stateA,
            out SettlementStore settlementsA,
            out TribeStore tribesA);

        Assert.True(first.TotalBirths > 0, "За девять веков не родилось ни одной веры.");
        Assert.True(first.ReligionCount > 0, "Все веры вымерли.");
        Assert.True(first.CultureCount > 0, "Все культуры исчезли.");
        Assert.True(
            first.AverageStability > 0f && first.AverageStability <= 1f,
            "Средняя стабильность вне пределов 0..1: " + first.AverageStability);

        int seen = 0;
        for (short tribe = 1; tribe < tribesA.Capacity; tribe++)
        {
            if (!tribesA.Alive[tribe])
            {
                continue;
            }

            seen++;
            Assert.True(culturesA.IsAlive(stateA.Culture[tribe]), "Народ " + tribe + " остался без культуры.");
            Assert.True(stateA.Ideology[tribe] >= 0, "Народ " + tribe + " остался без власти.");
            Assert.True(
                table.Allows(stateA.Ideology[tribe], tribesA.Era[tribe]),
                "Власть народа " + tribe + " не подходит его эпохе.");
            Assert.True(
                stateA.Stability[tribe] >= 0f && stateA.Stability[tribe] <= 1f,
                "Стабильность народа " + tribe + " вне пределов 0..1: " + stateA.Stability[tribe]);
        }

        Assert.True(seen > 0, "В мире не осталось ни одного народа.");

        for (int i = 0; i < settlementsA.HighWater; i++)
        {
            if (!settlementsA.Alive[i])
            {
                continue;
            }

            short faith = stateA.SettlementReligion[i];
            Assert.True(
                faith == ReligionStore.None || religionsA.IsAlive(faith),
                "Город " + i + " верит в мёртвую веру.");

            short culture = stateA.SettlementCulture[i];
            Assert.True(
                culture == CultureStore.None || culturesA.IsAlive(culture),
                "Город " + i + " держится мёртвой культуры.");
        }

        SocietySystem second = RunSociety(
            20260912,
            out ReligionStore religionsB,
            out CultureStore culturesB,
            out SocietyState stateB,
            out SettlementStore settlementsB,
            out TribeStore _);

        Assert.Equal(religionsA.Checksum(), religionsB.Checksum());
        Assert.Equal(culturesA.Checksum(), culturesB.Checksum());
        Assert.Equal(stateA.Checksum(), stateB.Checksum());
        Assert.Equal(settlementsA.Checksum(), settlementsB.Checksum());
        Assert.Equal(first.TotalBirths, second.TotalBirths);
        Assert.Equal(first.TotalConversions, second.TotalConversions);
        Assert.Equal(first.TotalAssimilations, second.TotalAssimilations);
        Assert.Equal(first.TotalIdeologyChanges, second.TotalIdeologyChanges);
    }

    private static SocietyTable LoadSociety()
    {
        SocietyTable? table = SocietyTable.Load(out string error);
        Assert.True(table != null, "Таблица общества не прочиталась: " + error);
        return table!;
    }

    /// <summary>Сплошная суша: проверки веры не должны зависеть от случайной географии.</summary>
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

    /// <summary>Собирает живой мир с людьми, городами, хозяйством и обществом и прогоняет его.</summary>
    private static SocietySystem RunSociety(
        int seed,
        out ReligionStore religions,
        out CultureStore cultures,
        out SocietyState state,
        out SettlementStore settlements,
        out TribeStore tribes)
    {
        const int size = 96;
        const int ticks = 900;

        EconomyTable? economyTable = EconomyTable.Load(out string economyError);
        Assert.True(economyTable != null, "Таблица товаров не прочиталась: " + economyError);

        WorldMap map = WorldGenerator.Generate(size, size, seed);
        var world = new WorldState(size, size, seed);
        world.SetMap(map);

        var people = new Population(Population.DefaultCapacity, size, size);
        tribes = new TribeStore();
        settlements = new SettlementStore();
        var territory = new Territory(size, size, tribes.Capacity);
        var tech = new TribeTech(tribes.Capacity);

        TribeSeeder.Seed(world, people, tribes, settlements, territory, 5, 60);

        var market = new TribeMarket(tribes.Capacity, economyTable!.Count);
        var routes = new TradeNetwork(economyTable.Trade.MaxRoutes);
        var diplomacy = new Diplomacy(tribes.Capacity);

        // Без эпохи и казны общество проверять нечего: в палеолите вера ещё не рождается.
        for (short tribe = 1; tribe < tribes.Capacity; tribe++)
        {
            if (tribes.Alive[tribe])
            {
                tribes.Era[tribe] = 4;
                market.Wealth[tribe] = 20000f;
            }
        }

        religions = new ReligionStore();
        cultures = new CultureStore();
        state = new SocietyState(tribes.Capacity, settlements.Capacity);

        var populationSystem = new PopulationSystem(people, null, tech);
        var settlementSystem = new SettlementSystem(people, tribes, settlements, territory);
        var economySystem = new EconomySystem(economyTable, tribes, settlements, territory, market, routes, diplomacy);
        var society = new SocietySystem(
            LoadSociety(),
            tribes,
            settlements,
            state,
            religions,
            cultures,
            market,
            routes,
            diplomacy);

        var loop = new SimulationLoop(world, populationSystem, settlementSystem, economySystem, society);
        loop.RunTicks(ticks);
        return society;
    }
}
