using WorldBox.Core.Eras;
using WorldBox.Core.Simulation;
using WorldBox.Core.Society;
using WorldBox.Core.Tribes;

namespace WorldBox.Core.Rulers;

/// <summary>
/// Люди истории. Раз в десять тиков система смотрит на престолы: кто состарился и умер,
/// кто унаследовал власть, где наследника не нашлось и началась смута, где власть взяли силой
/// и кого из героев успел выдвинуть народ.
///
/// Как именно передаётся власть, решает форма правления из таблицы общества:
/// монархия и церковь ждут старшего взрослого ребёнка, республика выбирает нового человека,
/// племя и диктатура живут под постоянной угрозой переворота.
///
/// Черты правителя не украшение: они сразу попадают в мир — недовольствием в городах,
/// запасом еды, скоростью знания и крепостью державы. Именно поэтому система стоит в цикле
/// ПОСЛЕ общества: стабильность уже пересчитана, и правитель добавляет к ней своё.
///
/// Случайность — свой поток <see cref="RngStream"/>, строки создаются только при рождении человека.
/// </summary>
public sealed class RulerSystem : ISimulationSystem
{
    /// <summary>Как часто смотрим на престолы. Дворцовые дела не требуют ежетикового внимания.</summary>
    public const int TicksPerRun = 10;

    private const int RngStream = 8837;

    /// <summary>Сколько поколений линии бережём от вытеснения, чтобы дерево дома не обрывалось.</summary>
    private const int LineDepth = 8;

    private const float UnrestForCommander = 0.25f;
    private const float FaithForProphet = 0.6f;

    /// <summary>Насколько старше совершеннолетия бывает человек со стороны.</summary>
    private const float AdultSpread = 25f;

    /// <summary>Новому правителю оставляем хотя бы пару лет: иначе он умирает в день коронации.</summary>
    private const float MinYearsLeft = 6f;

    private readonly RulerTable _table;
    private readonly TribeStore _tribes;
    private readonly SettlementStore _settlements;
    private readonly RulerStore _rulers;
    private readonly HeroStore _heroes;
    private readonly DynastyState _state;
    private readonly SocietyTable? _societyTable;
    private readonly SocietyState? _society;
    private readonly CultureStore? _cultures;
    private readonly TribeTech? _tech;

    private readonly int _tribeCapacity;
    private readonly float[] _unrestAdd;
    private readonly float[] _foodAdd;
    private readonly float[] _progressAdd;
    private readonly float[] _stabilityAdd;
    private readonly float[] _aggressionAdd;
    private readonly float[] _unrestOf;
    private readonly int[] _cityCount;

    private Rng? _rng;
    private long _tick;
    private float _year;

    public RulerSystem(
        RulerTable table,
        TribeStore tribes,
        SettlementStore settlements,
        RulerStore rulers,
        HeroStore heroes,
        DynastyState state,
        SocietyTable? societyTable = null,
        SocietyState? society = null,
        CultureStore? cultures = null,
        TribeTech? tech = null)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(tribes);
        ArgumentNullException.ThrowIfNull(settlements);
        ArgumentNullException.ThrowIfNull(rulers);
        ArgumentNullException.ThrowIfNull(heroes);
        ArgumentNullException.ThrowIfNull(state);

        _table = table;
        _tribes = tribes;
        _settlements = settlements;
        _rulers = rulers;
        _heroes = heroes;
        _state = state;
        _societyTable = societyTable;
        _society = society;
        _cultures = cultures;
        _tech = tech;

        _tribeCapacity = Math.Min(tribes.Capacity, state.TribeCapacity);
        _unrestAdd = new float[_tribeCapacity];
        _foodAdd = new float[_tribeCapacity];
        _progressAdd = new float[_tribeCapacity];
        _stabilityAdd = new float[_tribeCapacity];
        _aggressionAdd = new float[_tribeCapacity];
        _unrestOf = new float[_tribeCapacity];
        _cityCount = new int[_tribeCapacity];
    }

    public string Name => "Правители";

    public int Interval => TicksPerRun;

    /// <summary>Сколько раз система отработала.</summary>
    public long Run { get; private set; }

    /// <summary>Сколько людей истории живо сейчас: правители, дети, братья.</summary>
    public int LivingRulers { get; private set; }

    /// <summary>Сколько престолов занято.</summary>
    public int Thrones { get; private set; }

    /// <summary>Средний возраст правящих в годах.</summary>
    public float AverageAge { get; private set; }

    /// <summary>Самая глубокая династия мира: номер поколения на престоле.</summary>
    public int DeepestGeneration { get; private set; }

    public int HeroCount { get; private set; }

    public long TotalReigns { get; private set; }

    public long TotalDeaths { get; private set; }

    /// <summary>Сколько правителей убила смута, а не старость.</summary>
    public long TotalViolentDeaths { get; private set; }

    public long TotalHeirs { get; private set; }

    /// <summary>Сколько раз наследника не нашлось.</summary>
    public long TotalCrises { get; private set; }

    public long TotalCoups { get; private set; }

    public long TotalElections { get; private set; }

    public long TotalHouses { get; private set; }

    public long TotalHeroes { get; private set; }

    /// <summary>Сколько смертей было на последнем прогоне.</summary>
    public int LastDeaths { get; private set; }

    /// <summary>Сколько коронаций было на последнем прогоне.</summary>
    public int LastCrownings { get; private set; }

    /// <summary>Сколько старых записей вытеснила память: история глубже этого уже не видна.</summary>
    public int Forgotten => _rulers.Forgotten;

    public void Tick(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);

        Rng rng = _rng ??= world.Rng.Fork(RngStream);
        Run++;
        _tick = world.Tick;
        _year = (float)world.Year;
        LastDeaths = 0;
        LastCrownings = 0;

        Array.Clear(_unrestAdd, 0, _unrestAdd.Length);
        Array.Clear(_foodAdd, 0, _foodAdd.Length);
        Array.Clear(_progressAdd, 0, _progressAdd.Length);
        Array.Clear(_stabilityAdd, 0, _stabilityAdd.Length);
        Array.Clear(_aggressionAdd, 0, _aggressionAdd.Length);

        Recycle();
        MeasureUnrest();
        MarkLine();
        Reap(rng);
        Succeed(rng);
        BirthHeirs(rng);
        NameHeirs();
        RetireHeroes();
        SpawnHeroes(rng);
        ApplyEffects();
        Measure();
    }

    /// <summary>Номер записи правящего человека или <see cref="DynastyState.NoRuler"/>.</summary>
    public int RulerOf(int tribe)
        => (uint)tribe < (uint)_tribeCapacity ? _state.Ruler[tribe] : DynastyState.NoRuler;

    public string RulerNameOf(int tribe) => _rulers.NameOf(RulerOf(tribe));

    public string HouseOf(int tribe)
        => (uint)tribe < (uint)_tribeCapacity ? _state.House[tribe] : string.Empty;

    public Trait TraitsOf(int tribe) => _rulers.TraitsOf(RulerOf(tribe));

    /// <summary>Прозвище по главной черте правителя.</summary>
    public string EpithetOf(int tribe) => RulerNames.Epithet(TraitsOf(tribe));

    public int GenerationOf(int tribe)
        => (uint)tribe < (uint)_tribeCapacity ? _state.Generation[tribe] : 0;

    public int CrisesOf(int tribe)
        => (uint)tribe < (uint)_tribeCapacity ? _state.Crises[tribe] : 0;

    public int CoupsOf(int tribe)
        => (uint)tribe < (uint)_tribeCapacity ? _state.Coups[tribe] : 0;

    public int ReignsOf(int tribe)
        => (uint)tribe < (uint)_tribeCapacity ? _state.Reigns[tribe] : 0;

    public int HousesOf(int tribe)
        => (uint)tribe < (uint)_tribeCapacity ? _state.Houses[tribe] : 0;

    public int HeirOf(int tribe)
        => (uint)tribe < (uint)_tribeCapacity ? _state.Heir[tribe] : DynastyState.NoRuler;

    /// <summary>Возраст правителя в годах.</summary>
    public float AgeOf(int tribe) => _rulers.AgeOf(RulerOf(tribe), _year);

    public int HeroesOf(int tribe) => _heroes.CountOf((short)tribe);

    /// <summary>Надбавка к готовности воевать от черт правителя и живых полководцев.</summary>
    public float AggressionBonusOf(int tribe)
        => (uint)tribe < (uint)_tribeCapacity ? _aggressionAdd[tribe] : 0f;

    /// <summary>Надбавка к знанию за прогон.</summary>
    public float ResearchBonusOf(int tribe)
        => (uint)tribe < (uint)_tribeCapacity ? _progressAdd[tribe] : 0f;

    /// <summary>Надбавка к стабильности: багочестие держит, жестокость расшатывает.</summary>
    public float StabilityBonusOf(int tribe)
        => (uint)tribe < (uint)_tribeCapacity ? _stabilityAdd[tribe] : 0f;

    /// <summary>Народ погиб: его люди и герои уходят из памяти, иначе новый народ получит чужой дом.</summary>
    private void Recycle()
    {
        for (int tribe = 1; tribe < _tribeCapacity; tribe++)
        {
            if (_tribes.Alive[tribe])
            {
                continue;
            }

            if (_state.Ruler[tribe] == DynastyState.NoRuler && _state.Reigns[tribe] == 0)
            {
                continue;
            }

            for (int i = 0; i < _rulers.HighWater; i++)
            {
                if (_rulers.Used[i] && _rulers.Tribe[i] == tribe)
                {
                    _rulers.Forget(i);
                }
            }

            for (int h = 0; h < _heroes.HighWater; h++)
            {
                if (_heroes.Alive[h] && _heroes.Tribe[h] == tribe)
                {
                    _heroes.Remove(h);
                }
            }

            _state.ForgetTribe(tribe);
            _state.Touch();
        }
    }

    /// <summary>Среднее недовольство по городам народа: из него растёт риск насильственной смерти.</summary>
    private void MeasureUnrest()
    {
        Array.Clear(_unrestOf, 0, _unrestOf.Length);
        Array.Clear(_cityCount, 0, _cityCount.Length);

        int limit = Math.Min(_settlements.HighWater, _settlements.Capacity);
        for (int i = 0; i < limit; i++)
        {
            if (!_settlements.Alive[i])
            {
                continue;
            }

            short tribe = _settlements.Tribe[i];
            if ((uint)tribe >= (uint)_tribeCapacity)
            {
                continue;
            }

            _unrestOf[tribe] += _settlements.Unrest[i];
            _cityCount[tribe]++;
        }

        for (int tribe = 1; tribe < _tribeCapacity; tribe++)
        {
            if (_cityCount[tribe] > 0)
            {
                _unrestOf[tribe] /= _cityCount[tribe];
            }
        }
    }

    /// <summary>Помечает предков нынешних правителей: такую запись память не вытеснит.</summary>
    private void MarkLine()
    {
        Array.Clear(_rulers.Keep, 0, _rulers.Keep.Length);

        for (int tribe = 1; tribe < _tribeCapacity; tribe++)
        {
            int walk = _state.Ruler[tribe];
            for (int depth = 0; depth < LineDepth && walk != RulerStore.None; depth++)
            {
                _rulers.Keep[walk] = true;
                walk = _rulers.ParentOf(walk);
            }
        }
    }

    /// <summary>Старость и смута: кто умер — тот умер, престол остаётся пустым до передачи власти.</summary>
    private void Reap(Rng rng)
    {
        RulerLifeSettings life = _table.Life;

        for (int i = 0; i < _rulers.HighWater; i++)
        {
            if (!_rulers.IsLiving(i))
            {
                continue;
            }

            short tribe = _rulers.Tribe[i];
            bool reigning = (uint)tribe < (uint)_tribeCapacity && _state.Ruler[tribe] == i;
            float age = _year - _rulers.BornYear[i];
            bool old = age >= _rulers.Life[i];
            bool violent = false;

            if (!old && reigning)
            {
                float risk = life.ViolentDeathChance + (life.UnrestDeathWeight * _unrestOf[tribe]);
                violent = rng.Chance(risk);
            }

            if (!old && !violent)
            {
                continue;
            }

            _rulers.Kill(i, _tick, _year);
            TotalDeaths++;
            LastDeaths++;
            if (violent)
            {
                TotalViolentDeaths++;
            }

            if (!reigning)
            {
                continue;
            }

            _rulers.Uncrown(i, _tick);
            _state.Previous[tribe] = i;
            _state.Ruler[tribe] = DynastyState.NoRuler;
            _state.Heir[tribe] = DynastyState.NoRuler;
            _state.Touch();
        }
    }

    /// <summary>Передача власти: кровь, выборы или сила. Без наследника — смута.</summary>
    private void Succeed(Rng rng)
    {
        RulerHeirSettings rules = _table.Heirs;

        for (int tribe = 1; tribe < _tribeCapacity; tribe++)
        {
            if (!_tribes.Alive[tribe] || _state.Ruler[tribe] != DynastyState.NoRuler)
            {
                continue;
            }

            // Первое восшествие — не смута: двора у народа просто ещё не было.
            if (_state.Reigns[tribe] == 0 && _state.Previous[tribe] == DynastyState.NoRuler)
            {
                int founder = Recruit(tribe, rng, true);
                if (founder != RulerStore.None)
                {
                    Crown(tribe, founder);
                }

                continue;
            }

            Succession kind = SuccessionOf(tribe);

            if (kind == Succession.Elected)
            {
                int elected = Recruit(tribe, rng, true);
                if (elected != RulerStore.None)
                {
                    TotalElections++;
                    Crown(tribe, elected);
                }

                continue;
            }

            int heir = PickHeir(tribe, _state.Previous[tribe]);
            bool coup = kind == Succession.Strength && rng.Chance(rules.CoupChance);

            if (heir != RulerStore.None && !coup)
            {
                Crown(tribe, heir);
                continue;
            }

            if (coup)
            {
                TotalCoups++;
                _state.Coups[tribe]++;
            }
            else
            {
                // Ни детей, ни братьев: города узнают об этом ростом недовольства.
                TotalCrises++;
                _state.Crises[tribe]++;
                _state.CrisisRun[tribe] = Run;
                _unrestAdd[tribe] += rules.CrisisUnrest;
            }

            int taker = Recruit(tribe, rng, true);
            if (taker != RulerStore.None)
            {
                Crown(tribe, taker);
            }
        }
    }

    /// <summary>Дети правящего дома: без них любая смерть сразу становится кризисом.</summary>
    private void BirthHeirs(Rng rng)
    {
        RulerHeirSettings rules = _table.Heirs;
        RulerLifeSettings life = _table.Life;

        for (int tribe = 1; tribe < _tribeCapacity; tribe++)
        {
            if (!_tribes.Alive[tribe])
            {
                continue;
            }

            int ruler = _state.Ruler[tribe];
            if (!_rulers.IsLiving(ruler) || _rulers.Children[ruler] >= rules.MaxChildren)
            {
                continue;
            }

            if (_year - _rulers.BornYear[ruler] < life.AdultYears)
            {
                continue;
            }

            if (!rng.Chance(rules.HeirChancePerRun))
            {
                continue;
            }

            byte language = _rulers.Language[ruler];
            int child = _rulers.Add(
                RulerNames.Person(rng, language),
                _rulers.House[ruler],
                language,
                (short)tribe,
                Traits.Roll(rng, _table.TraitRules.MinTraits, _table.TraitRules.MaxTraits),
                ruler,
                _rulers.Generation[ruler] + 1,
                _tick,
                _year,
                RollLife(rng, 0f));

            if (child != RulerStore.None)
            {
                TotalHeirs++;
                _state.Touch();
            }
        }
    }

    /// <summary>Кого двор считает наследником при живом правителе: нужно только для окна.</summary>
    private void NameHeirs()
    {
        for (int tribe = 1; tribe < _tribeCapacity; tribe++)
        {
            int ruler = _state.Ruler[tribe];
            _state.Heir[tribe] = ruler == DynastyState.NoRuler
                ? DynastyState.NoRuler
                : PickHeir(tribe, ruler);
        }
    }

    /// <summary>Герои смертны: после своего срока они уходят, и надбавка исчезает вместе с ними.</summary>
    private void RetireHeroes()
    {
        long span = (long)_table.Heroes.LifeRuns * TicksPerRun;

        for (int h = 0; h < _heroes.HighWater; h++)
        {
            if (_heroes.Alive[h] && _tick - _heroes.Born[h] >= span)
            {
                _heroes.Remove(h);
            }
        }
    }

    /// <summary>Народ выдвигает героя. Какого именно — решают его беды, а не чистый случай.</summary>
    private void SpawnHeroes(Rng rng)
    {
        RulerHeroSettings rules = _table.Heroes;

        for (int tribe = 1; tribe < _tribeCapacity; tribe++)
        {
            if (_heroes.Count >= rules.MaxHeroes || !_heroes.HasRoom)
            {
                return;
            }

            if (!_tribes.Alive[tribe] || !rng.Chance(rules.ChancePerRun))
            {
                continue;
            }

            int seat = CapitalOf(tribe);
            if (seat < 0)
            {
                continue;
            }

            HeroKind kind = PickKind(tribe, rng);
            int hero = _heroes.Add(
                RulerNames.Person(rng, LanguageOf(tribe)),
                kind,
                (short)tribe,
                _settlements.X[seat],
                _settlements.Y[seat],
                _tick,
                _year);

            if (hero != HeroStore.None)
            {
                TotalHeroes++;
            }
        }
    }

    /// <summary>Черты и герои попадают в мир: еда, знание, недовольство и стабильность.</summary>
    private void ApplyEffects()
    {
        RulerTraitSettings rules = _table.TraitRules;
        RulerHeroSettings heroes = _table.Heroes;

        for (int tribe = 1; tribe < _tribeCapacity; tribe++)
        {
            if (!_tribes.Alive[tribe])
            {
                continue;
            }

            Trait traits = _rulers.TraitsOf(_state.Ruler[tribe]);

            if (Traits.Has(traits, Trait.Cruel))
            {
                _unrestAdd[tribe] += rules.CruelUnrest;
                _stabilityAdd[tribe] -= rules.CruelStability;
            }

            if (Traits.Has(traits, Trait.Just))
            {
                _unrestAdd[tribe] -= rules.JustUnrest;
            }

            if (Traits.Has(traits, Trait.Builder))
            {
                _foodAdd[tribe] += rules.BuilderFood;
            }

            if (Traits.Has(traits, Trait.Scholar))
            {
                _progressAdd[tribe] += rules.ScholarProgress;
            }

            if (Traits.Has(traits, Trait.Conqueror))
            {
                _aggressionAdd[tribe] += rules.ConquerorAggression;
            }

            if (Traits.Has(traits, Trait.Pious))
            {
                _stabilityAdd[tribe] += rules.PiousStability;
            }
        }

        for (int h = 0; h < _heroes.HighWater; h++)
        {
            if (!_heroes.Alive[h])
            {
                continue;
            }

            short tribe = _heroes.Tribe[h];
            if ((uint)tribe >= (uint)_tribeCapacity)
            {
                continue;
            }

            switch ((HeroKind)_heroes.Kind[h])
            {
                case HeroKind.Commander:
                    _aggressionAdd[tribe] += heroes.CommanderAggression;
                    break;
                case HeroKind.Inventor:
                    _progressAdd[tribe] += heroes.InventorProgress;
                    break;
                case HeroKind.Prophet:
                    _unrestAdd[tribe] -= heroes.ProphetUnrest;
                    break;
                default:
                    _foodAdd[tribe] += heroes.ExplorerFood;
                    break;
            }
        }

        if (_tech != null)
        {
            int limit = Math.Min(_tribeCapacity, _tech.Capacity);
            for (int tribe = 1; tribe < limit; tribe++)
            {
                if (_tribes.Alive[tribe] && _progressAdd[tribe] > 0f)
                {
                    _tech.Progress[tribe] += _progressAdd[tribe];
                }
            }
        }

        if (_society != null)
        {
            int limit = Math.Min(_tribeCapacity, _society.TribeCapacity);
            for (int tribe = 1; tribe < limit; tribe++)
            {
                if (!_tribes.Alive[tribe] || _stabilityAdd[tribe] == 0f)
                {
                    continue;
                }

                if (_society.Ideology[tribe] == SocietyState.NoIdeology)
                {
                    continue;
                }

                _society.Stability[tribe] = Math.Clamp(_society.Stability[tribe] + _stabilityAdd[tribe], 0f, 1f);
            }
        }

        int cities = Math.Min(_settlements.HighWater, _settlements.Capacity);
        for (int i = 0; i < cities; i++)
        {
            if (!_settlements.Alive[i])
            {
                continue;
            }

            short tribe = _settlements.Tribe[i];
            if ((uint)tribe >= (uint)_tribeCapacity)
            {
                continue;
            }

            float unrest = _unrestAdd[tribe];
            if (unrest != 0f)
            {
                _settlements.Unrest[i] = Math.Clamp(_settlements.Unrest[i] + unrest, 0f, 1f);
            }

            float food = _foodAdd[tribe];
            if (food > 0f)
            {
                _settlements.Food[i] += food;
            }
        }
    }

    private void Measure()
    {
        int thrones = 0;
        int deepest = 0;
        float ageSum = 0f;

        for (int tribe = 1; tribe < _tribeCapacity; tribe++)
        {
            int ruler = _state.Ruler[tribe];
            if (ruler == DynastyState.NoRuler)
            {
                continue;
            }

            thrones++;
            ageSum += _year - _rulers.BornYear[ruler];
            if (_state.Generation[tribe] > deepest)
            {
                deepest = _state.Generation[tribe];
            }
        }

        Thrones = thrones;
        LivingRulers = _rulers.Living;
        AverageAge = thrones > 0 ? ageSum / thrones : 0f;
        DeepestGeneration = deepest;
        HeroCount = _heroes.Count;
    }

    /// <summary>Старший взрослый ребёнок, иначе взрослый родственник того же дома, иначе никто.</summary>
    private int PickHeir(int tribe, int previous)
    {
        int adult = _table.Life.AdultYears;
        string house = _state.House[tribe];

        int child = RulerStore.None;
        float childAge = -1f;
        int kin = RulerStore.None;
        float kinAge = -1f;

        for (int i = 0; i < _rulers.HighWater; i++)
        {
            if (!_rulers.IsLiving(i) || _rulers.Tribe[i] != tribe || i == previous)
            {
                continue;
            }

            float age = _year - _rulers.BornYear[i];
            if (age < adult)
            {
                continue;
            }

            if (previous != RulerStore.None && _rulers.Parent[i] == previous)
            {
                if (age > childAge)
                {
                    childAge = age;
                    child = i;
                }

                continue;
            }

            if (house.Length > 0
                && string.Equals(_rulers.House[i], house, StringComparison.Ordinal)
                && age > kinAge)
            {
                kinAge = age;
                kin = i;
            }
        }

        return child != RulerStore.None ? child : kin;
    }

    /// <summary>Человек со стороны: избранный, самозванец или основатель нового дома.</summary>
    private int Recruit(int tribe, Rng rng, bool newHouse)
    {
        byte language = LanguageOf(tribe);
        string house = newHouse || _state.House[tribe].Length == 0
            ? RulerNames.House(rng, language)
            : _state.House[tribe];

        float adult = _table.Life.AdultYears;
        float age = adult + (rng.NextFloat() * AdultSpread);

        int index = _rulers.Add(
            RulerNames.Person(rng, language),
            house,
            language,
            (short)tribe,
            Traits.Roll(rng, _table.TraitRules.MinTraits, _table.TraitRules.MaxTraits),
            RulerStore.None,
            1,
            _tick,
            _year - age,
            RollLife(rng, age));

        if (index != RulerStore.None)
        {
            _state.Houses[tribe]++;
            TotalHouses++;
        }

        return index;
    }

    /// <summary>Сколько лет отмерено. Человеку со стороны даём запас поверх его возраста.</summary>
    private float RollLife(Rng rng, float age)
    {
        RulerLifeSettings life = _table.Life;
        float span = life.MinLifeYears + (rng.NextFloat() * (life.MaxLifeYears - life.MinLifeYears));
        return MathF.Max(span, age + MinYearsLeft);
    }

    private void Crown(int tribe, int index)
    {
        _state.Ruler[tribe] = index;
        _state.House[tribe] = _rulers.House[index];
        _state.Generation[tribe] = _rulers.Generation[index];
        _state.CrownedRun[tribe] = Run;
        _state.Reigns[tribe]++;
        _state.Heir[tribe] = DynastyState.NoRuler;
        _rulers.Crown(index, _tick);
        TotalReigns++;
        LastCrownings++;
        _state.Touch();
    }

    /// <summary>Форма власти решает, как передаётся престол. Без общества правит тот, кто сильнее.</summary>
    private Succession SuccessionOf(int tribe)
    {
        if (_societyTable == null || _society == null)
        {
            return Succession.Strength;
        }

        int form = _society.Ideology[tribe];
        if ((uint)form >= (uint)_societyTable.FormCount)
        {
            return Succession.Strength;
        }

        return _societyTable.FormId[form] switch
        {
            "republic" or "democracy" or "technocracy" => Succession.Elected,
            "tribe" or "chiefdom" or "totalitarian" => Succession.Strength,
            _ => Succession.Blood,
        };
    }

    /// <summary>Язык имён берётся у культуры народа, чтобы двор звучал как его земля.</summary>
    private byte LanguageOf(int tribe)
    {
        if (_cultures != null && _society != null)
        {
            short culture = _society.Culture[tribe];
            if (_cultures.IsAlive(culture))
            {
                return _cultures.Language[culture];
            }
        }

        return (byte)(tribe % RulerNames.Languages);
    }

    /// <summary>Самый людный город народа: там и престол, и герои.</summary>
    private int CapitalOf(int tribe)
    {
        int best = -1;
        int bestPeople = -1;
        int limit = Math.Min(_settlements.HighWater, _settlements.Capacity);

        for (int i = 0; i < limit; i++)
        {
            if (!_settlements.Alive[i] || _settlements.Tribe[i] != tribe)
            {
                continue;
            }

            if (_settlements.People[i] > bestPeople)
            {
                bestPeople = _settlements.People[i];
                best = i;
            }
        }

        return best;
    }

    /// <summary>Какого героя выдвигает народ: бунт зовёт полководца, разлад веры — пророка.</summary>
    private HeroKind PickKind(int tribe, Rng rng)
    {
        if (_unrestOf[tribe] > UnrestForCommander)
        {
            return HeroKind.Commander;
        }

        if (_society != null && _society.Faith[tribe] > 0f && _society.Faith[tribe] < FaithForProphet)
        {
            return HeroKind.Prophet;
        }

        if (_tech != null && tribe < _tech.Capacity && _tech.Progress[tribe] > 0f && rng.Chance(0.5f))
        {
            return HeroKind.Inventor;
        }

        return HeroKind.Explorer;
    }

    /// <summary>Способ передачи власти.</summary>
    private enum Succession : byte
    {
        /// <summary>По крови: старший взрослый ребёнок или родня.</summary>
        Blood = 0,

        /// <summary>По силе: над престолом всегда висит угроза переворота.</summary>
        Strength = 1,

        /// <summary>По выборам: каждый раз новый человек и новый дом.</summary>
        Elected = 2,
    }
}
