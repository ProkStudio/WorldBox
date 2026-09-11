using WorldBox.Core.Economy;
using WorldBox.Core.Simulation;
using WorldBox.Core.Tribes;
using WorldBox.Core.War;

namespace WorldBox.Core.Society;

/// <summary>
/// Общество: религия, культура и форма власти.
///
/// Главная мысль среза: государство и вера — разные слои. Вера живёт в городах,
/// ползёт по торговым путям и через границу, а двор лишь признаёт ту, в которую верит большинство.
/// Из-за этого карта религий никогда не совпадает с картой государств.
///
/// Система никого не убивает сама. Недовольство чужой верой, чужой культурой и падение
/// стабильности копится в поле Unrest поселений, а в бунты и откол земель его превращает
/// уже готовая система войны. Так гражданская война получается одним механизмом с обычным бунтом.
///
/// Карту система не сканирует вообще: все проходы идут по народам (десятки), поселениям
/// (сотни) и торговым путям (сотни). Аллокаций в тике нет, кроме названий новых вер и культур.
/// </summary>
public sealed class SocietySystem : ISimulationSystem
{
    /// <summary>Раз во сколько тиков думает общество. Вера меняется медленнее хлеба и войны.</summary>
    public const int TicksPerRun = 10;

    /// <summary>Свой поток случайности: соседние системы не сбивают решения общества.</summary>
    private const int RngStream = 7717;

    /// <summary>Сколько цветов в палитре народов: ими же красятся веры и культуры.</summary>
    private const int PaletteColors = 12;

    /// <summary>На каком расстоянии города считаются соседями для распространения веры.</summary>
    private const int NeighbourReach = 14;

    /// <summary>Насколько далеко раскол забирает округу вокруг города-зачинщика.</summary>
    private const int SchismReach = 24;

    private readonly SocietyTable _table;
    private readonly TribeStore _tribes;
    private readonly SettlementStore _settlements;
    private readonly SocietyState _state;
    private readonly ReligionStore _religions;
    private readonly CultureStore _cultures;
    private readonly TribeMarket? _market;
    private readonly TradeNetwork? _routes;
    private readonly Diplomacy? _diplomacy;

    private readonly int _tribeCapacity;
    private readonly int _religionCapacity;
    private readonly int _cultureCapacity;

    /// <summary>Голоса за веру внутри одного народа: плоская таблица «народы на религии».</summary>
    private readonly int[] _faithVotes;

    /// <summary>Голоса за культуру внутри одного народа.</summary>
    private readonly int[] _cultureVotes;

    /// <summary>Вес народа в голосах: люди плюс один голос от каждого города.</summary>
    private readonly int[] _weightOf;

    /// <summary>Сколько недовольства добавить городам каждого народа в этом прогоне.</summary>
    private readonly float[] _unrestAdd;

    /// <summary>Среднее недовольство городов народа: улица давит на стабильность.</summary>
    private readonly float[] _unrestOf;

    /// <summary>Сколько городов у народа: делитель для среднего недовольства.</summary>
    private readonly int[] _seatsOf;

    private Rng? _rng;

    /// <summary>Тик последнего прогона: по нему считается возраст молодых вер.</summary>
    private long _tick;

    public SocietySystem(
        SocietyTable table,
        TribeStore tribes,
        SettlementStore settlements,
        SocietyState state,
        ReligionStore religions,
        CultureStore cultures,
        TribeMarket? market = null,
        TradeNetwork? routes = null,
        Diplomacy? diplomacy = null)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(tribes);
        ArgumentNullException.ThrowIfNull(settlements);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(religions);
        ArgumentNullException.ThrowIfNull(cultures);

        _table = table;
        _tribes = tribes;
        _settlements = settlements;
        _state = state;
        _religions = religions;
        _cultures = cultures;
        _market = market;
        _routes = routes;
        _diplomacy = diplomacy;

        _tribeCapacity = Math.Min(tribes.Capacity, state.TribeCapacity);
        _religionCapacity = religions.Capacity;
        _cultureCapacity = cultures.Capacity;

        _faithVotes = new int[_tribeCapacity * _religionCapacity];
        _cultureVotes = new int[_tribeCapacity * _cultureCapacity];
        _weightOf = new int[_tribeCapacity];
        _unrestAdd = new float[_tribeCapacity];
        _unrestOf = new float[_tribeCapacity];
        _seatsOf = new int[_tribeCapacity];
    }

    public string Name => "Общество";

    public int Interval => TicksPerRun;

    /// <summary>Номер прогона общества. По нему считается срок жизни власти.</summary>
    public long Run { get; private set; }

    /// <summary>Сколько вер живо сейчас.</summary>
    public int ReligionCount { get; private set; }

    /// <summary>Сколько культур живо сейчас.</summary>
    public int CultureCount { get; private set; }

    /// <summary>Сколько живых вер родилось расколом.</summary>
    public int SchismCount { get; private set; }

    /// <summary>Средняя стабильность по живым народам.</summary>
    public float AverageStability { get; private set; }

    public int LastBirths { get; private set; }

    public int LastSchisms { get; private set; }

    public int LastConversions { get; private set; }

    public int LastAssimilations { get; private set; }

    public int TotalBirths { get; private set; }

    public int TotalSchisms { get; private set; }

    public int TotalConversions { get; private set; }

    public int TotalAssimilations { get; private set; }

    /// <summary>Сколько раз культура меньшинства стала культурой государства.</summary>
    public int TotalAbsorbs { get; private set; }

    public int TotalIdeologyChanges { get; private set; }

    /// <summary>Сколько раз государство срывалось в гражданскую войну.</summary>
    public int TotalCollapses { get; private set; }

    /// <summary>Стабильность народа от 0 до 1.</summary>
    public float StabilityOf(int tribe)
        => (uint)tribe < (uint)_tribeCapacity ? _state.Stability[tribe] : 0f;

    /// <summary>Номер формы власти или -1.</summary>
    public int IdeologyOf(int tribe)
        => (uint)tribe < (uint)_tribeCapacity ? _state.Ideology[tribe] : SocietyState.NoIdeology;

    /// <summary>Ключ перевода формы власти народа.</summary>
    public string IdeologyNameKeyOf(int tribe) => _table.NameKeyOf(IdeologyOf(tribe));

    /// <summary>Тяжесть налогов при текущей власти. Готово для будущих срезов и для окна общества.</summary>
    public float TaxesOf(int tribe)
    {
        int form = IdeologyOf(tribe);
        return (uint)form < (uint)_table.FormCount ? _table.FormTaxes[form] : 0f;
    }

    /// <summary>Надбавка к науке от власти и догмата знания.</summary>
    public float ResearchFactorOf(int tribe)
    {
        int form = IdeologyOf(tribe);
        float value = (uint)form < (uint)_table.FormCount ? _table.FormResearch[form] : 0f;
        if ((uint)tribe < (uint)_tribeCapacity
            && (_religions.DogmasOf(_state.Religion[tribe]) & Dogma.Knowledge) != Dogma.None)
        {
            value += 0.05f;
        }

        return value;
    }

    /// <summary>Готовность воевать от власти и воинственного догмата.</summary>
    public float AggressionOf(int tribe)
    {
        int form = IdeologyOf(tribe);
        float value = (uint)form < (uint)_table.FormCount ? _table.FormAggression[form] : 0f;
        if ((uint)tribe < (uint)_tribeCapacity
            && (_religions.DogmasOf(_state.Religion[tribe]) & Dogma.War) != Dogma.None)
        {
            value += 0.1f;
        }

        return MathF.Min(1f, value);
    }

    /// <summary>Название веры двора или пустая строка.</summary>
    public string StateFaithNameOf(int tribe)
        => (uint)tribe < (uint)_tribeCapacity ? _religions.NameOf(_state.Religion[tribe]) : string.Empty;

    /// <summary>Название культуры народа или пустая строка.</summary>
    public string CultureNameOf(int tribe)
        => (uint)tribe < (uint)_tribeCapacity ? _cultures.NameOf(_state.Culture[tribe]) : string.Empty;

    public void Tick(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);

        Rng rng = _rng ??= world.Rng.Fork(RngStream);
        Run++;
        _tick = world.Tick;

        LastBirths = 0;
        LastSchisms = 0;
        LastConversions = 0;
        LastAssimilations = 0;

        Recycle();
        SeedTribes(world, rng);
        Recount(world);
        BirthReligions(world, rng);
        SpreadByTrade(rng);
        SpreadByNeighbours(rng);
        ConvertConquered(rng);
        SplitFaiths(world, rng);
        Assimilate(rng);
        Recount(world);
        AbsorbCultures();
        ChooseIdeologies(rng);
        Stabilize();

        ReligionCount = _religions.Count;
        CultureCount = _cultures.Count;
        SchismCount = _religions.SchismCount();
    }

    /// <summary>Освобождает строки погибших народов и городов: иначе новый владелец слота получит чужую веру.</summary>
    private void Recycle()
    {
        for (int tribe = 1; tribe < _tribeCapacity; tribe++)
        {
            if (_tribes.Alive[tribe])
            {
                continue;
            }

            if (_state.Religion[tribe] == ReligionStore.None
                && _state.Culture[tribe] == CultureStore.None
                && _state.Ideology[tribe] == SocietyState.NoIdeology)
            {
                continue;
            }

            _state.ForgetTribe(tribe);
            _state.Touch();
        }

        int limit = SettlementLimit();
        for (int i = 0; i < limit; i++)
        {
            if (_settlements.Alive[i])
            {
                continue;
            }

            if (_state.SettlementReligion[i] == ReligionStore.None
                && _state.SettlementCulture[i] == CultureStore.None)
            {
                continue;
            }

            _state.ForgetSettlement(i);
            _state.Touch();
        }
    }

    /// <summary>Новому народу нужна своя культура и какая-то власть, иначе ему нечем делиться с соседями.</summary>
    private void SeedTribes(WorldState world, Rng rng)
    {
        for (int tribe = 1; tribe < _tribeCapacity; tribe++)
        {
            if (!_tribes.Alive[tribe])
            {
                continue;
            }

            if (!_cultures.IsAlive(_state.Culture[tribe]))
            {
                _state.Culture[tribe] = MakeCulture(world, rng, tribe);
                _state.Touch();
            }

            if (_state.Ideology[tribe] == SocietyState.NoIdeology)
            {
                _state.Ideology[tribe] = PickIdeology(tribe, rng);
                _state.IdeologyRun[tribe] = Run;
                _state.Touch();
            }
        }

        int limit = SettlementLimit();
        for (int i = 0; i < limit; i++)
        {
            if (!_settlements.Alive[i])
            {
                continue;
            }

            short tribe = _settlements.Tribe[i];
            if ((uint)tribe >= (uint)_tribeCapacity || !_tribes.Alive[tribe])
            {
                continue;
            }

            if (!_cultures.IsAlive(_state.SettlementCulture[i]))
            {
                _state.SettlementCulture[i] = _state.Culture[tribe];
                _state.Touch();
            }

            if (!_religions.IsAlive(_state.SettlementReligion[i]) && _religions.IsAlive(_state.Religion[tribe]))
            {
                _state.SettlementReligion[i] = _state.Religion[tribe];
                _state.Touch();
            }
        }
    }

    private short MakeCulture(WorldState world, Rng rng, int tribe)
    {
        if (!_cultures.HasRoom || _cultures.Count >= _table.Culture.MaxCultures)
        {
            return _cultures.Largest();
        }

        byte language = (byte)rng.NextInt(_table.Culture.Languages);
        string name = SocietyNames.Culture(rng, language);
        return _cultures.Found(name, _tribes.ColorIndex[tribe], language, world.Tick);
    }

    /// <summary>
    /// Пересчёт: сколько у кого последователей, какая вера большинства и насколько народ един.
    /// Голос города — его люди плюс один: тогда даже пустой город влияет, а доли остаются от 0 до 1.
    /// </summary>
    private void Recount(WorldState world)
    {
        Array.Clear(_faithVotes, 0, _faithVotes.Length);
        Array.Clear(_cultureVotes, 0, _cultureVotes.Length);
        Array.Clear(_weightOf, 0, _weightOf.Length);

        for (int f = 0; f < _religions.HighWater; f++)
        {
            _religions.Followers[f] = 0;
            _religions.Settlements[f] = 0;
        }

        for (int c = 0; c < _cultures.HighWater; c++)
        {
            _cultures.People[c] = 0;
            _cultures.Settlements[c] = 0;
        }

        int limit = SettlementLimit();
        for (int i = 0; i < limit; i++)
        {
            if (!_settlements.Alive[i])
            {
                continue;
            }

            short tribe = _settlements.Tribe[i];
            if ((uint)tribe >= (uint)_tribeCapacity || !_tribes.Alive[tribe])
            {
                continue;
            }

            int weight = Math.Max(0, _settlements.People[i]) + 1;
            _weightOf[tribe] += weight;

            short faith = _state.SettlementReligion[i];
            if (_religions.IsAlive(faith))
            {
                _religions.Followers[faith] += Math.Max(0, _settlements.People[i]);
                _religions.Settlements[faith]++;
                _faithVotes[(tribe * _religionCapacity) + faith] += weight;
            }
            else if (faith != ReligionStore.None)
            {
                _state.SettlementReligion[i] = ReligionStore.None;
            }

            short culture = _state.SettlementCulture[i];
            if (_cultures.IsAlive(culture))
            {
                _cultures.People[culture] += Math.Max(0, _settlements.People[i]);
                _cultures.Settlements[culture]++;
                _cultureVotes[(tribe * _cultureCapacity) + culture] += weight;
            }
            else if (culture != CultureStore.None)
            {
                _state.SettlementCulture[i] = CultureStore.None;
            }
        }

        for (int tribe = 1; tribe < _tribeCapacity; tribe++)
        {
            if (!_tribes.Alive[tribe])
            {
                continue;
            }

            int weight = _weightOf[tribe];
            short crown = (short)Best(_faithVotes, tribe * _religionCapacity, _religionCapacity, out int crownVotes);
            if (_state.Religion[tribe] != crown)
            {
                _state.Religion[tribe] = crown;
                _state.Touch();
            }

            _state.Faith[tribe] = weight > 0 ? (float)crownVotes / weight : 0f;

            short culture = _state.Culture[tribe];
            int cultureVotes = _cultures.IsAlive(culture)
                ? _cultureVotes[(tribe * _cultureCapacity) + culture]
                : 0;
            _state.Unity[tribe] = weight > 0 ? (float)cultureVotes / weight : 0f;
        }

        ForgetEmptyFaiths(world);
        ForgetEmptyCultures(world);
    }

    /// <summary>Вера без единого города угасает и освобождает слот новым учениям.</summary>
    private void ForgetEmptyFaiths(WorldState world)
    {
        for (int f = 0; f < _religions.HighWater; f++)
        {
            if (!_religions.Alive[f] || _religions.Settlements[f] > 0)
            {
                continue;
            }

            if (_religions.Founded[f] == world.Tick)
            {
                continue;
            }

            _religions.Remove((short)f);
            _state.Touch();
        }
    }

    private void ForgetEmptyCultures(WorldState world)
    {
        for (int c = 0; c < _cultures.HighWater; c++)
        {
            if (!_cultures.Alive[c] || _cultures.Settlements[c] > 0)
            {
                continue;
            }

            if (_cultures.Founded[c] == world.Tick || IsCrownCulture((short)c))
            {
                continue;
            }

            _cultures.Remove((short)c);
            _state.Touch();
        }
    }

    private bool IsCrownCulture(short culture)
    {
        for (int tribe = 1; tribe < _tribeCapacity; tribe++)
        {
            if (_tribes.Alive[tribe] && _state.Culture[tribe] == culture)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Рождение веры: чаще всего в голод и на войне, когда людям нужно объяснение.</summary>
    private void BirthReligions(WorldState world, Rng rng)
    {
        SocietyReligionSettings rules = _table.Religion;

        for (int tribe = 1; tribe < _tribeCapacity; tribe++)
        {
            if (!_religions.HasRoom || _religions.Count >= rules.MaxReligions)
            {
                return;
            }

            if (!_tribes.Alive[tribe] || _tribes.Era[tribe] < rules.MinEra)
            {
                continue;
            }

            float chance = rules.BirthChancePerRun;
            if (_market != null && tribe < _market.Capacity && _market.IsHungry(tribe))
            {
                chance += rules.CrisisBonus;
            }

            if (_diplomacy != null && tribe < _diplomacy.Capacity && _diplomacy.Wars[tribe] > 0)
            {
                chance += rules.CrisisBonus;
            }

            if (!rng.Chance(chance))
            {
                continue;
            }

            int seat = PickSeatOf(tribe, rules.MinPeople, rng);
            if (seat < 0)
            {
                continue;
            }

            byte language = LanguageOf(_state.Culture[tribe]);
            string name = SocietyNames.Religion(rng, language);
            byte color = (byte)rng.NextInt(PaletteColors);
            short faith = _religions.Found(
                name,
                color,
                (short)tribe,
                ReligionStore.None,
                RollDogmas(rng, _tribes.Era[tribe]),
                world.Tick);

            if (faith == ReligionStore.None)
            {
                continue;
            }

            _state.SettlementReligion[seat] = faith;
            _religions.Settlements[faith] = 1;
            _religions.Followers[faith] = Math.Max(0, _settlements.People[seat]);
            LastBirths++;
            TotalBirths++;
            _state.Touch();
        }
    }

    /// <summary>Вера едет с торговыми обозами в обе стороны пути.</summary>
    private void SpreadByTrade(Rng rng)
    {
        if (_routes == null)
        {
            return;
        }

        float chance = _table.Religion.TradeSpreadChance;
        int limit = SettlementLimit();

        for (int route = 0; route < _routes.Count; route++)
        {
            int from = _routes.FromSettlement[route];
            int to = _routes.ToSettlement[route];
            if ((uint)from >= (uint)limit || (uint)to >= (uint)limit)
            {
                continue;
            }

            if (!_settlements.Alive[from] || !_settlements.Alive[to])
            {
                continue;
            }

            TryConvert(from, to, chance, rng);
            TryConvert(to, from, chance, rng);
        }
    }

    /// <summary>Вера переходит к ближайшим городам, в том числе через границу.</summary>
    private void SpreadByNeighbours(Rng rng)
    {
        int limit = SettlementLimit();
        if (limit <= 1)
        {
            return;
        }

        float chance = _table.Religion.NeighbourSpreadChance;

        for (int i = 0; i < limit; i++)
        {
            if (!_settlements.Alive[i] || _state.SettlementReligion[i] == ReligionStore.None)
            {
                continue;
            }

            int other = rng.NextInt(limit);
            if (other == i || !_settlements.Alive[other])
            {
                continue;
            }

            int dx = _settlements.X[i] - _settlements.X[other];
            int dy = _settlements.Y[i] - _settlements.Y[other];
            if ((dx * dx) + (dy * dy) > NeighbourReach * NeighbourReach)
            {
                continue;
            }

            TryConvert(i, other, chance, rng);
        }
    }

    /// <summary>Завоёванные города ломают или злятся: вера двора давит на чужую.</summary>
    private void ConvertConquered(Rng rng)
    {
        SocietyReligionSettings rules = _table.Religion;
        int limit = SettlementLimit();

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

            short crown = _state.Religion[tribe];
            if (!_religions.IsAlive(crown) || crown == _state.SettlementReligion[i])
            {
                continue;
            }

            bool conquered = _settlements.Captured[i] != 0L;
            float chance = conquered ? rules.ConquestSpreadChance : rules.ConquestSpreadChance * 0.35f;
            if ((_religions.DogmasOf(crown) & Dogma.Order) != Dogma.None)
            {
                chance *= 1.4f;
            }

            if (rng.Chance(chance))
            {
                _state.SettlementReligion[i] = crown;
                LastConversions++;
                TotalConversions++;
                _state.Touch();
                continue;
            }

            _settlements.Unrest[i] = MathF.Min(1f, _settlements.Unrest[i] + rules.ForeignFaithUnrest);
        }
    }

    /// <summary>Раскол: большая вера отпускает от себя толк, и город с ним начинает шуметь.</summary>
    private void SplitFaiths(WorldState world, Rng rng)
    {
        SocietyReligionSettings rules = _table.Religion;
        int high = _religions.HighWater;

        for (int f = 0; f < high; f++)
        {
            if (!_religions.HasRoom || _religions.Count >= rules.MaxReligions)
            {
                return;
            }

            if (!_religions.Alive[f] || _religions.Followers[f] < rules.SchismMinFollowers)
            {
                continue;
            }

            if (!rng.Chance(rules.SchismChancePerRun))
            {
                continue;
            }

            int seat = PickDissentSeat((short)f, rng);
            if (seat < 0)
            {
                continue;
            }

            short child = _religions.Found(
                SocietyNames.Schism(rng, _religions.NameOf((short)f)),
                (byte)((_religions.ColorIndex[f] + 1 + rng.NextInt(PaletteColors - 1)) % PaletteColors),
                _settlements.Tribe[seat],
                (short)f,
                Flip(_religions.DogmasOf((short)f), rng),
                world.Tick);

            if (child == ReligionStore.None)
            {
                continue;
            }

            _state.SettlementReligion[seat] = child;
            _religions.Settlements[child] = 1;
            _religions.Followers[child] = Math.Max(0, _settlements.People[seat]);
            _settlements.Unrest[seat] = MathF.Min(1f, _settlements.Unrest[seat] + rules.SchismUnrest);
            SpreadSchism((short)f, child, seat, rules.SchismUnrest);
            LastSchisms++;
            TotalSchisms++;
            _state.Touch();
        }
    }

    /// <summary>
    /// Раскол забирает не один город, а округу: близкие города той же веры, если они свои
    /// тому же народу или уже шумят. Одиночный город господствующая вера перекрашивала
    /// обратно в тот же прогон, и расколы не жили.
    /// </summary>
    private int SpreadSchism(short parent, short child, int seat, float unrest)
    {
        int limit = SettlementLimit();
        short homeTribe = _settlements.Tribe[seat];
        int taken = 0;
        int people = 0;

        for (int i = 0; i < limit; i++)
        {
            if (i == seat || !_settlements.Alive[i] || _state.SettlementReligion[i] != parent)
            {
                continue;
            }

            int dx = _settlements.X[i] - _settlements.X[seat];
            int dy = _settlements.Y[i] - _settlements.Y[seat];
            if ((dx * dx) + (dy * dy) > SchismReach * SchismReach)
            {
                continue;
            }

            if (_settlements.Tribe[i] != homeTribe && _settlements.Unrest[i] < 0.2f)
            {
                continue;
            }

            _state.SettlementReligion[i] = child;
            _settlements.Unrest[i] = MathF.Min(1f, _settlements.Unrest[i] + unrest);
            people += Math.Max(0, _settlements.People[i]);
            taken++;
        }

        _religions.Settlements[child] += taken;
        _religions.Followers[child] += people;
        return taken;
    }

    /// <summary>Молодая вера ещё горит: её города не переубедить чужим проповедником.</summary>
    private bool IsYoung(short faith)
        => _religions.IsAlive(faith)
            && _tick - _religions.Founded[faith] < (long)_table.Religion.YoungRuns * TicksPerRun;

    /// <summary>Ассимиляция: чужой город либо становится своим, либо накапливает недовольство.</summary>
    private void Assimilate(Rng rng)
    {
        SocietyCultureSettings rules = _table.Culture;
        int limit = SettlementLimit();

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

            short crown = _state.Culture[tribe];
            if (!_cultures.IsAlive(crown))
            {
                continue;
            }

            short local = _state.SettlementCulture[i];
            if (local == crown)
            {
                continue;
            }

            if (!_cultures.IsAlive(local))
            {
                _state.SettlementCulture[i] = crown;
                _state.Touch();
                continue;
            }

            float chance = rules.AssimilationChance + (rules.AssimilationPerEra * _tribes.Era[tribe]);
            if (rng.Chance(chance))
            {
                _state.SettlementCulture[i] = crown;
                LastAssimilations++;
                TotalAssimilations++;
                _state.Touch();
                continue;
            }

            _settlements.Unrest[i] = MathF.Min(1f, _settlements.Unrest[i] + rules.ForeignUnrest);
        }
    }

    /// <summary>Если культура двора осталась в меньшинстве, государство перенимает культуру большинства.</summary>
    private void AbsorbCultures()
    {
        float share = _table.Culture.AbsorbShare;

        for (int tribe = 1; tribe < _tribeCapacity; tribe++)
        {
            if (!_tribes.Alive[tribe] || _weightOf[tribe] <= 0 || _state.Unity[tribe] >= share)
            {
                continue;
            }

            int best = Best(_cultureVotes, tribe * _cultureCapacity, _cultureCapacity, out int votes);
            if (best < 0 || best == _state.Culture[tribe])
            {
                continue;
            }

            _state.Culture[tribe] = (short)best;
            _state.Unity[tribe] = (float)votes / _weightOf[tribe];
            TotalAbsorbs++;
            _state.Touch();
        }
    }

    /// <summary>Форма власти пересматривается раз в несколько прогонов, а не каждый год.</summary>
    private void ChooseIdeologies(Rng rng)
    {
        SocietyIdeologySettings rules = _table.Ideology;

        for (int tribe = 1; tribe < _tribeCapacity; tribe++)
        {
            if (!_tribes.Alive[tribe] || Run - _state.IdeologyRun[tribe] < rules.ChangeRuns)
            {
                continue;
            }

            _state.IdeologyRun[tribe] = Run;
            int best = PickIdeology(tribe, rng);
            if (best < 0 || best == _state.Ideology[tribe] || !rng.Chance(rules.SwitchChance))
            {
                continue;
            }

            _state.Ideology[tribe] = best;
            TotalIdeologyChanges++;
            _state.Touch();
        }
    }

    /// <summary>
    /// Выбор формы власти по цифрам таблицы, без зашитых имён: главное — соответствие эпохе,
    /// дальше вера, единство, война и голод. Небольшой случайный разброс разводит похожие народы.
    /// </summary>
    private int PickIdeology(int tribe, Rng rng)
    {
        int era = _tribes.Era[tribe];
        float faith = _state.Faith[tribe];
        float unity = _state.Unity[tribe];
        float stability = _state.Stability[tribe];
        float hunger = _market != null && tribe < _market.Capacity ? _market.Famine[tribe] : 0f;
        bool atWar = _diplomacy != null && tribe < _diplomacy.Capacity && _diplomacy.Wars[tribe] > 0;

        int best = -1;
        float top = float.MinValue;

        for (int form = 0; form < _table.FormCount; form++)
        {
            if (!_table.Allows(form, era))
            {
                continue;
            }

            float score = _table.EraFit(form, era) + _table.FormStability[form];
            score += faith * (_table.FormStability[form] - _table.FormResearch[form]);
            score += unity * (_table.FormResearch[form] - (_table.FormAggression[form] * 0.5f));
            score += atWar ? _table.FormAggression[form] * 0.6f : _table.FormAggression[form] * -0.2f;
            score += (1f - stability) * _table.FormTaxes[form] * 0.5f;
            score -= hunger * _table.FormTaxes[form];
            score += rng.NextFloat() * 0.15f;

            if (score > top)
            {
                top = score;
                best = form;
            }
        }

        return best;
    }

    /// <summary>
    /// Стабильность и её последствия. Система не разваливает государства сама:
    /// она лишь льёт недовольство в города, а бунты и отколы делает система войны.
    /// </summary>
    private void Stabilize()
    {
        SocietyStabilitySettings rules = _table.Stability;
        Array.Clear(_unrestAdd, 0, _unrestAdd.Length);
        MeasureUnrest();

        float total = 0f;
        int counted = 0;

        for (int tribe = 1; tribe < _tribeCapacity; tribe++)
        {
            if (!_tribes.Alive[tribe])
            {
                continue;
            }

            int form = _state.Ideology[tribe];
            float hunger = _market != null && tribe < _market.Capacity ? _market.Famine[tribe] : 0f;
            float war = _diplomacy != null && tribe < _diplomacy.Capacity ? _diplomacy.Exhaustion[tribe] : 0f;
            float formBonus = (uint)form < (uint)_table.FormCount ? _table.FormStability[form] : 0f;

            float value = rules.Base
                + (rules.UnityWeight * Swing(_state.Unity[tribe]))
                + (rules.FaithWeight * Swing(_state.Faith[tribe]))
                + (rules.EraFitWeight * Swing(_table.EraFit(form, _tribes.Era[tribe])))
                + formBonus
                - (rules.HungerWeight * hunger)
                - (rules.WarWeight * war)
                - (rules.UnrestWeight * _unrestOf[tribe]);

            value = Math.Clamp(value, 0f, 1f);
            _state.Stability[tribe] = value;
            total += value;
            counted++;

            if (value >= rules.CollapseThreshold)
            {
                _state.LowRuns[tribe] = 0;
                continue;
            }

            _state.LowRuns[tribe]++;
            float missing = (rules.CollapseThreshold - value) / MathF.Max(0.01f, rules.CollapseThreshold);
            _unrestAdd[tribe] = rules.UnrestPerPoint * missing;

            if (_state.LowRuns[tribe] >= rules.CollapseRuns)
            {
                _state.LowRuns[tribe] = 0;
                _unrestAdd[tribe] += rules.CollapseUnrest;
                TotalCollapses++;
            }
        }

        AverageStability = counted > 0 ? total / counted : 0f;
        ApplyUnrest();
    }

    /// <summary>
    /// Доля от 0 до 1 превращается в размах от −единицы до плюс единицы: половина — это ноль,
    /// а не подарок. Иначе все слагаемые шли только в плюс и стабильность стояла в потолке.
    /// </summary>
    private static float Swing(float share) => (Math.Clamp(share, 0f, 1f) * 2f) - 1f;

    /// <summary>Среднее недовольство городов по народам: чем громче улица, тем слабее власть.</summary>
    private void MeasureUnrest()
    {
        Array.Clear(_unrestOf, 0, _unrestOf.Length);
        Array.Clear(_seatsOf, 0, _seatsOf.Length);

        int limit = SettlementLimit();
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
            _seatsOf[tribe]++;
        }

        for (int tribe = 1; tribe < _tribeCapacity; tribe++)
        {
            if (_seatsOf[tribe] > 0)
            {
                _unrestOf[tribe] /= _seatsOf[tribe];
            }
        }
    }

    private void ApplyUnrest()
    {
        int limit = SettlementLimit();

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

            float add = _unrestAdd[tribe];
            if (add <= 0f)
            {
                continue;
            }

            _settlements.Unrest[i] = MathF.Min(1f, _settlements.Unrest[i] + add);
        }
    }

    /// <summary>Переводит город в веру соседа, если тот убедительнее.</summary>
    private void TryConvert(int source, int target, float chance, Rng rng)
    {
        short faith = _state.SettlementReligion[source];
        if (!_religions.IsAlive(faith))
        {
            return;
        }

        short current = _state.SettlementReligion[target];
        if (current == faith)
        {
            return;
        }

        float weight = chance;
        if ((_religions.DogmasOf(faith) & Dogma.Trade) != Dogma.None)
        {
            weight *= 1.5f;
        }

        if (!_religions.IsAlive(current))
        {
            weight *= 2f;
        }
        else if (IsYoung(current))
        {
            // Молодую веру не сбить с толку: иначе раскол гаснет в тот же прогон.
            return;
        }
        else
        {
            float mine = _religions.Followers[faith];
            float theirs = _religions.Followers[current];
            weight *= MathF.Max(0.1f, mine / MathF.Max(1f, mine + theirs)) * 2f;
        }

        if (!rng.Chance(weight))
        {
            return;
        }

        _state.SettlementReligion[target] = faith;
        LastConversions++;
        TotalConversions++;
        _state.Touch();
    }

    /// <summary>Случайный город народа не меньше заданного размера. Выбор без списков и аллокаций.</summary>
    private int PickSeatOf(int tribe, int minPeople, Rng rng)
    {
        int limit = SettlementLimit();
        int chosen = -1;
        int seen = 0;

        for (int i = 0; i < limit; i++)
        {
            if (!_settlements.Alive[i] || _settlements.Tribe[i] != tribe || _settlements.People[i] < minPeople)
            {
                continue;
            }

            seen++;
            if (rng.NextInt(seen) == 0)
            {
                chosen = i;
            }
        }

        return chosen;
    }

    /// <summary>
    /// Город для раскола. Сначала ищем там, где вера чужая для двора или где уже шумят,
    /// и только потом берём любой: так расколы падают туда, где и должны.
    /// </summary>
    private int PickDissentSeat(short faith, Rng rng)
    {
        int limit = SettlementLimit();
        int dissent = -1;
        int dissentSeen = 0;
        int any = -1;
        int anySeen = 0;

        for (int i = 0; i < limit; i++)
        {
            if (!_settlements.Alive[i] || _state.SettlementReligion[i] != faith)
            {
                continue;
            }

            anySeen++;
            if (rng.NextInt(anySeen) == 0)
            {
                any = i;
            }

            short tribe = _settlements.Tribe[i];
            bool foreignCrown = (uint)tribe < (uint)_tribeCapacity && _state.Religion[tribe] != faith;
            if (!foreignCrown && _settlements.Unrest[i] < 0.2f)
            {
                continue;
            }

            dissentSeen++;
            if (rng.NextInt(dissentSeen) == 0)
            {
                dissent = i;
            }
        }

        return dissent >= 0 ? dissent : any;
    }

    private byte LanguageOf(short culture)
        => _cultures.IsAlive(culture) ? _cultures.Language[culture] : (byte)0;

    private int SettlementLimit()
        => Math.Min(_settlements.HighWater, _state.SettlementCapacity);

    /// <summary>Номер строки с максимумом голосов или -1, если голосов нет.</summary>
    private static int Best(int[] votes, int start, int length, out int top)
    {
        int best = -1;
        top = 0;

        for (int i = 0; i < length; i++)
        {
            int value = votes[start + i];
            if (value > top)
            {
                top = value;
                best = i;
            }
        }

        return best;
    }

    /// <summary>Догматы новой веры. Пустой набор запрещён: вера без учения ни на что не влияла бы.</summary>
    private static Dogma RollDogmas(Rng rng, int era)
    {
        Dogma dogmas = Dogma.None;
        if (rng.Chance(0.45f))
        {
            dogmas |= Dogma.War;
        }

        if (rng.Chance(0.4f))
        {
            dogmas |= Dogma.Trade;
        }

        if (rng.Chance(era >= 5 ? 0.55f : 0.35f))
        {
            dogmas |= Dogma.Knowledge;
        }

        if (rng.Chance(0.3f))
        {
            dogmas |= Dogma.Order;
        }

        return dogmas == Dogma.None ? Dogma.Order : dogmas;
    }

    /// <summary>Раскол меняет ровно один догмат: дочерняя вера узнаваема, но уже другая.</summary>
    private static Dogma Flip(Dogma dogmas, Rng rng)
    {
        int bit = 1 << rng.NextInt(4);
        Dogma result = (Dogma)(byte)((byte)dogmas ^ (byte)bit);
        return result == Dogma.None ? Dogma.Knowledge : result;
    }
}
