using System.Globalization;
using System.Text;
using WorldBox.Core;
using WorldBox.Core.Rulers;
using WorldBox.Core.Simulation;
using WorldBox.Core.Society;
using WorldBox.Core.Tribes;
using WorldBox.Core.World;
using Xunit;

namespace WorldBox.Tests;

/// <summary>
/// Проверки среза «люди истории»: наследование, смута без наследника, глубина рода,
/// влияние черт правителя, срок жизни героев и повторимость истории.
///
/// Мир собирается вручную и маленьким: правителям нужен только народ с городом.
/// </summary>
public sealed class RulerTests
{
    /// <summary>Карта нужна лишь для порядка: сами правители в неё не смотрят.</summary>
    private const int MapSize = 48;

    /// <summary>Столько тиков хватает, чтобы род дорос до глубины дерева династии.</summary>
    private const int LineTicks = 6000;

    /// <summary>Формы власти, где правителя каждый раз выбирают заново.</summary>
    private static readonly string[] ElectedForms = { "republic", "democracy", "technocracy" };

    /// <summary>Формы власти, где над престолом всегда висит переворот.</summary>
    private static readonly string[] StrengthForms = { "tribe", "chiefdom", "totalitarian" };

    /// <summary>Как передаётся власть в проверяемом народе.</summary>
    private enum FormKind
    {
        /// <summary>Общества нет совсем: без него двор тоже обязан жить.</summary>
        None = 0,

        /// <summary>По крови.</summary>
        Blood = 1,

        /// <summary>По выборам.</summary>
        Elected = 2,

        /// <summary>По силе.</summary>
        Strength = 3,
    }

    [Fact]
    public void TableLoadsWithSaneNumbers()
    {
        RulerTable table = LoadRulers();

        Assert.True(table.MaxRulers >= 64, "Память истории слишком мала: " + table.MaxRulers);
        Assert.True(table.Life.MinLifeYears >= 5, "Правители живут неправдоподобно мало.");
        Assert.True(
            table.Life.MaxLifeYears >= table.Life.MinLifeYears,
            "Верхняя граница жизни ниже нижней.");
        Assert.True(
            table.Life.AdultYears > 0 && table.Life.AdultYears < table.Life.MinLifeYears,
            "Совершеннолетие выпадает из срока жизни: " + table.Life.AdultYears);
        Assert.True(
            table.Heirs.HeirChancePerRun > 0f,
            "Наследники не рождаются вовсе: каждая смерть станет смутой.");
        Assert.True(table.Heirs.MaxChildren >= 1, "Детей в семье не бывает совсем.");
        Assert.True(table.Heirs.CrisisUnrest > 0f, "Смута ничего не делает с городами.");
        Assert.True(
            table.TraitRules.MaxTraits >= table.TraitRules.MinTraits,
            "Черт максимум меньше минимума.");
        Assert.True(table.TraitRules.MaxTraits <= Traits.Count, "Черт больше, чем их вообще есть.");
        Assert.True(table.Heroes.MaxHeroes > 0, "Герои выключены совсем.");
        Assert.True(table.Heroes.LifeRuns >= 1, "Герой умирает раньше, чем появляется.");
    }

    [Fact]
    public void TableRefusesBrokenNumbers()
    {
        Assert.Null(RulerTable.Parse(Json(children: 0), out string childrenError));
        Assert.Contains("maxChildren", childrenError, StringComparison.Ordinal);

        Assert.Null(RulerTable.Parse(Json(lifeRuns: 0), out string heroError));
        Assert.Contains("lifeRuns", heroError, StringComparison.Ordinal);

        Assert.Null(RulerTable.Parse(Json(adultYears: 999), out string adultError));
        Assert.Contains("adultYears", adultError, StringComparison.Ordinal);

        Assert.Null(RulerTable.Parse("{\"version\":7}", out string versionError));
        Assert.Contains("version", versionError, StringComparison.Ordinal);
    }

    [Fact]
    public void BloodSuccessionKeepsTheHouse()
    {
        Court court = NewCourt(Table(Json(heirChance: 1f, minLife: 90, maxLife: 140)), FormKind.Blood);
        court.Loop.RunTicks(4000);

        Assert.Equal(1, court.Rulers.Thrones);
        Assert.True(court.Rulers.TotalReigns >= 4, "Власть почти не менялась: " + court.Rulers.TotalReigns);
        Assert.True(court.Rulers.TotalHeirs > 0, "Дети в правящем доме не рождались.");
        Assert.False(string.IsNullOrEmpty(court.Rulers.HouseOf(court.Tribe)), "У правящего дома нет имени.");
        Assert.True(
            court.Rulers.HousesOf(court.Tribe) < court.Rulers.ReignsOf(court.Tribe),
            "Дом менялся с каждым правителем: наследование по крови не работает.");
    }

    [Fact]
    public void ElectedFormFoundsNewHouses()
    {
        Court court = NewCourt(Table(Json(heirChance: 1f, minLife: 90, maxLife: 140)), FormKind.Elected);
        court.Loop.RunTicks(2000);

        Assert.True(court.Rulers.TotalElections >= 3, "Республика почти не выбирала: " + court.Rulers.TotalElections);
        Assert.Equal(0L, court.Rulers.TotalCoups);
        Assert.True(
            court.Rulers.HousesOf(court.Tribe) >= (int)court.Rulers.TotalElections,
            "Выборы не приводили к новому дому.");
    }

    [Fact]
    public void StrongmanFormTakesPowerByForce()
    {
        Court court = NewCourt(Table(Json(heirChance: 1f, coup: 1f, minLife: 90, maxLife: 140)), FormKind.Strength);
        court.Loop.RunTicks(2000);

        Assert.True(court.Rulers.TotalCoups >= 3, "При вечной угрозе власть ни разу не взяли силой.");
        Assert.Equal(0L, court.Rulers.TotalCrises);
        Assert.Equal(court.Rulers.CoupsOf(court.Tribe), (int)court.Rulers.TotalCoups);
    }

    [Fact]
    public void DeathWithoutHeirRaisesUnrest()
    {
        Court court = NewCourt(
            Table(Json(heirChance: 0f, children: 1, minLife: 20, maxLife: 30, crisisUnrest: 0.3f)),
            FormKind.Blood);
        court.Loop.RunTicks(300);

        Assert.True(court.Rulers.TotalCrises >= 3, "Смерть без наследника прошла без смуты.");
        Assert.True(court.Rulers.CrisesOf(court.Tribe) >= 3, "Народ не запомнил свои смуты.");
        Assert.True(court.Cities.Unrest[0] > 0f, "Города не заметили смуты во дворе.");
    }

    [Fact]
    public void DynastyGrowsFiveGenerations()
    {
        Court court = NewCourt(Table(Json(heirChance: 1f, minLife: 90, maxLife: 140)), FormKind.Blood);
        court.Loop.RunTicks(LineTicks);

        Assert.True(
            court.Rulers.DeepestGeneration >= 5,
            "Род не дорос до пятого поколения: " + court.Rulers.DeepestGeneration);

        int walk = court.Rulers.RulerOf(court.Tribe);
        int depth = 0;

        while (walk != RulerStore.None && depth < 32)
        {
            Assert.False(string.IsNullOrEmpty(court.People.NameOf(walk)), "У человека в роду нет имени.");
            depth++;
            walk = court.People.ParentOf(walk);
        }

        Assert.True(depth >= 5, "Цепочка предков короче пяти звеньев: " + depth);
    }

    [Fact]
    public void TraitsChangeTheWorld()
    {
        Court rich = NewCourt(
            Table(Json(heirChance: 1f, minLife: 90, maxLife: 140, minTraits: 2, builderFood: 6f, scholarProgress: 4f)),
            FormKind.Blood);
        Court bare = NewCourt(
            Table(Json(heirChance: 1f, minLife: 90, maxLife: 140, minTraits: 2, builderFood: 0f, scholarProgress: 0f)),
            FormKind.Blood);

        rich.Loop.RunTicks(1200);
        bare.Loop.RunTicks(1200);

        Assert.True(
            rich.Cities.Food[0] > bare.Cities.Food[0],
            "Черта «строитель» не дала городам ни зерна.");
        Assert.NotNull(rich.Society);
        Assert.True(
            MathF.Abs(rich.Society!.Stability[rich.Tribe] - 0.5f) > 0.001f,
            "Черты правителей не тронули устойчивость власти.");
    }

    [Fact]
    public void HeroesComeAndGo()
    {
        Court court = NewCourt(
            Table(Json(heirChance: 1f, minLife: 90, maxLife: 140, maxHeroes: 3, heroChance: 1f, lifeRuns: 1)),
            FormKind.None);
        court.Loop.RunTicks(200);

        Assert.True(court.Rulers.TotalHeroes >= 3, "Герои не появлялись: " + court.Rulers.TotalHeroes);
        Assert.True(
            court.Heroes.Count <= 3,
            "Героев больше, чем позволяет таблица: " + court.Heroes.Count);
        Assert.True(court.Rulers.TotalHeroes > court.Heroes.Count, "Ни один герой не ушёл со сцены.");
    }

    [Fact]
    public void HistoryIsRepeatable()
    {
        RulerTable table = LoadRulers();
        Court first = NewCourt(table, FormKind.Blood, 9001);
        Court second = NewCourt(table, FormKind.Blood, 9001);

        first.Loop.RunTicks(1500);
        second.Loop.RunTicks(1500);

        Assert.Equal(first.People.Checksum(), second.People.Checksum());
        Assert.Equal(first.Dynasty.Checksum(), second.Dynasty.Checksum());
        Assert.Equal(first.Heroes.Checksum(), second.Heroes.Checksum());
        Assert.Equal(first.Rulers.TotalReigns, second.Rulers.TotalReigns);
    }

    [Fact]
    public void StoreForgetsOldButKeepsLine()
    {
        var store = new RulerStore(16);
        int root = store.Add("Основатель", "Дом Зари", 0, 1, Trait.None, RulerStore.None, 1, 0L, 0f, 40f);
        int child = store.Add("Наследник", "Дом Зари", 0, 1, Trait.None, root, 2, 10L, 50f, 40f);

        Assert.True(store.Kill(root, 20L, 60f), "Основатель не умер.");
        store.Keep[root] = true;
        store.Keep[child] = true;

        for (int i = 0; i < 40; i++)
        {
            int guest = store.Add(
                "Гость " + (i + 1),
                "Дом Гостей",
                0,
                1,
                Trait.None,
                RulerStore.None,
                1,
                30L + i,
                100f + i,
                10f);

            Assert.True(guest != RulerStore.None, "Кольцо истории отказалось брать нового человека.");
            store.Kill(guest, 40L + i, 200f + i);
        }

        Assert.True(store.Forgotten > 0, "Старые записи не вытеснялись: кольцо не работает.");
        Assert.Equal("Основатель", store.NameOf(root));
        Assert.Equal("Наследник", store.NameOf(child));
        Assert.Equal(root, store.ParentOf(child));
    }

    private static RulerTable LoadRulers()
    {
        RulerTable? table = RulerTable.Load(out string error);
        Assert.True(table != null, "Таблица правителей не прочиталась: " + error);
        return table!;
    }

    private static SocietyTable LoadSociety()
    {
        SocietyTable? table = SocietyTable.Load(out string error);
        Assert.True(table != null, "Таблица общества не прочиталась: " + error);
        return table!;
    }

    private static RulerTable Table(string json)
    {
        RulerTable? table = RulerTable.Parse(json, out string error);
        Assert.True(table != null, "Проверочная таблица не разобралась: " + error);
        return table!;
    }

    /// <summary>
    /// Таблица правителей строкой: так проверки не зависят от боевых чисел в data/
    /// и каждая проверка крутит ровно одну ручку.
    /// </summary>
    private static string Json(
        float heirChance = 0.6f,
        int children = 3,
        int adultYears = 16,
        int minLife = 40,
        int maxLife = 70,
        float violent = 0f,
        float unrestDeath = 0.25f,
        float coup = 0f,
        float crisisUnrest = 0.25f,
        int minTraits = 1,
        int maxTraits = 3,
        float builderFood = 2f,
        float scholarProgress = 1.5f,
        int maxHeroes = 8,
        float heroChance = 0.2f,
        int lifeRuns = 3)
    {
        var text = new StringBuilder(640);

        text.Append("{\"version\":1,\"maxRulers\":256,");
        text.Append("\"life\":{\"minLifeYears\":").Append(minLife);
        text.Append(",\"maxLifeYears\":").Append(maxLife);
        text.Append(",\"adultYears\":").Append(adultYears);
        text.Append(",\"violentDeathChance\":").Append(Num(violent));
        text.Append(",\"unrestDeathWeight\":").Append(Num(unrestDeath)).Append("},");
        text.Append("\"heirs\":{\"heirChancePerRun\":").Append(Num(heirChance));
        text.Append(",\"maxChildren\":").Append(children);
        text.Append(",\"coupChance\":").Append(Num(coup));
        text.Append(",\"crisisUnrest\":").Append(Num(crisisUnrest));
        text.Append(",\"crisisRuns\":3},");
        text.Append("\"traits\":{\"minTraits\":").Append(minTraits);
        text.Append(",\"maxTraits\":").Append(maxTraits);
        text.Append(",\"cruelUnrest\":0.03,\"justUnrest\":0.03");
        text.Append(",\"builderFood\":").Append(Num(builderFood));
        text.Append(",\"scholarProgress\":").Append(Num(scholarProgress));
        text.Append(",\"conquerorAggression\":0.15,\"piousStability\":0.06,\"cruelStability\":0.05},");
        text.Append("\"heroes\":{\"maxHeroes\":").Append(maxHeroes);
        text.Append(",\"chancePerRun\":").Append(Num(heroChance));
        text.Append(",\"lifeRuns\":").Append(lifeRuns);
        text.Append(",\"commanderAggression\":0.1,\"inventorProgress\":2.0");
        text.Append(",\"prophetUnrest\":0.03,\"explorerFood\":2.0}}");

        return text.ToString();
    }

    private static string Num(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>Первая форма власти нужного вида из боевого data/society.json.</summary>
    private static int FindForm(SocietyTable table, FormKind kind)
    {
        for (int i = 0; i < table.FormCount; i++)
        {
            string id = table.FormId[i];
            bool elected = Array.IndexOf(ElectedForms, id) >= 0;
            bool strength = Array.IndexOf(StrengthForms, id) >= 0;

            if (kind == FormKind.Elected && elected)
            {
                return i;
            }

            if (kind == FormKind.Strength && strength)
            {
                return i;
            }

            if (kind == FormKind.Blood && !elected && !strength)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Маленький мир с одним народом и столицей: больше двору ничего не нужно.</summary>
    private static Court NewCourt(RulerTable table, FormKind form, int seed = 4242)
    {
        WorldMap map = WorldGenerator.Generate(MapSize, MapSize, seed);
        var world = new WorldState(MapSize, MapSize, seed);
        world.SetMap(map);

        var tribes = new TribeStore(8);
        short tribe = tribes.Create("Двор", 1, MapSize / 2, MapSize / 2);
        tribes.People[tribe] = 900;
        tribes.Settlements[tribe] = 1;

        var cities = new SettlementStore(16);
        int seat = cities.Found(MapSize / 2, MapSize / 2, tribe, "Столица", 0L);
        cities.People[seat] = 600;

        var people = new RulerStore(table.MaxRulers);
        var heroes = new HeroStore(Math.Max(1, table.Heroes.MaxHeroes));
        var dynasty = new DynastyState(tribes.Capacity);

        SocietyTable? societyTable = null;
        SocietyState? societyState = null;

        if (form != FormKind.None)
        {
            societyTable = LoadSociety();
            societyState = new SocietyState(tribes.Capacity, cities.Capacity);

            int index = FindForm(societyTable, form);
            Assert.True(index >= 0, "В data/society.json нет формы власти для проверки: " + form);

            societyState.Ideology[tribe] = index;
            societyState.Stability[tribe] = 0.5f;
            societyState.Unity[tribe] = 0.5f;
            societyState.Faith[tribe] = 0.5f;
        }

        var rulers = new RulerSystem(table, tribes, cities, people, heroes, dynasty, societyTable, societyState);
        return new Court(world, tribes, cities, people, heroes, dynasty, rulers, societyState, tribe);
    }

    /// <summary>Двор для проверок: мир, народ, города и все хранилища людей истории.</summary>
    private sealed class Court
    {
        internal Court(
            WorldState world,
            TribeStore tribes,
            SettlementStore cities,
            RulerStore people,
            HeroStore heroes,
            DynastyState dynasty,
            RulerSystem rulers,
            SocietyState? society,
            short tribe)
        {
            Sim = world;
            Tribes = tribes;
            Cities = cities;
            People = people;
            Heroes = heroes;
            Dynasty = dynasty;
            Rulers = rulers;
            Society = society;
            Tribe = tribe;
            Loop = new SimulationLoop(world, rulers);
        }

        internal WorldState Sim { get; }

        internal TribeStore Tribes { get; }

        internal SettlementStore Cities { get; }

        internal RulerStore People { get; }

        internal HeroStore Heroes { get; }

        internal DynastyState Dynasty { get; }

        internal RulerSystem Rulers { get; }

        internal SocietyState? Society { get; }

        internal short Tribe { get; }

        internal SimulationLoop Loop { get; }
    }
}
