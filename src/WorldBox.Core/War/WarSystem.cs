using System.Numerics;
using WorldBox.Core.Economy;
using WorldBox.Core.Eras;
using WorldBox.Core.People;
using WorldBox.Core.Simulation;
using WorldBox.Core.Tribes;
using WorldBox.Core.World;

namespace WorldBox.Core.War;

/// <summary>
/// Война: кто с кем воюет, кто кого бьёт и чем это кончается для карты.
/// За один прогон система делает девять вещей: забывает войны исчезнувших народов;
/// осматривает полосу карты и считает, чья земля с чьей соприкасается; ищет столицы;
/// раз в несколько прогонов пересматривает отношения — сначала мир, потом объявления;
/// набирает отряды за казну; ведёт войска к чужим городам; сводит встретившихся в бою;
/// держит осады и берёт города; поднимает восстания на захваченной земле; платит войску
/// содержание и остужает усталость.
///
/// Случайность берётся только из <see cref="WorldState.Rng"/>: одинаковые партии дают
/// одинаковые войны. Аллокаций в тике нет — все буферы выделены в конструкторе.
/// Люди правятся одним проходом в конце прогона: смерть мирных и смена подданства
/// копятся в короткие списки, иначе каждая осада стоила бы обхода всего народа.
/// </summary>
public sealed class WarSystem : ISimulationSystem
{
    /// <summary>Раз во сколько тиков система просыпается.</summary>
    public const int TicksPerRun = 5;

    /// <summary>За сколько прогонов осматривается вся карта в поисках границ.</summary>
    public const int SweepParts = 8;

    /// <summary>Сколько цветов в палитре народов: новому народу бунта нужен свой.</summary>
    private const int PaletteColors = 12;

    /// <summary>Сколько правок людей копится за прогон: смертей мирных и смен подданства.</summary>
    private const int ChangeSlots = 32;

    private readonly WarTable _table;
    private readonly EraTable? _eras;
    private readonly Population _people;
    private readonly TribeStore _tribes;
    private readonly SettlementStore _settlements;
    private readonly Territory _territory;
    private readonly ArmyStore _armies;
    private readonly Diplomacy _diplomacy;
    private readonly TribeMarket? _market;

    private readonly int _capacity;
    private readonly int _width;
    private readonly int _height;
    private readonly int _rowsPerRun;

    private readonly int[] _contact;
    private readonly int[] _pendingContact;
    private readonly int[] _border;
    private readonly int[] _pendingBorder;
    private readonly int[] _capital;
    private readonly int[] _armyCount;
    private readonly int[] _armyMen;
    private readonly int[] _siegeMen;
    private readonly short[] _siegeTribe;

    private readonly int[] _lossX;
    private readonly int[] _lossY;
    private readonly int[] _lossRadius;
    private readonly int[] _lossCount;
    private readonly int[] _handX;
    private readonly int[] _handY;
    private readonly int[] _handRadius;
    private readonly short[] _handFrom;
    private readonly short[] _handTo;

    private int _lossLen;
    private int _handLen;
    private int _scanRow;
    private long _run;

    public WarSystem(
        WarTable table,
        EraTable? eras,
        Population people,
        TribeStore tribes,
        SettlementStore settlements,
        Territory territory,
        ArmyStore armies,
        Diplomacy diplomacy,
        TribeMarket? market = null)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(people);
        ArgumentNullException.ThrowIfNull(tribes);
        ArgumentNullException.ThrowIfNull(settlements);
        ArgumentNullException.ThrowIfNull(territory);
        ArgumentNullException.ThrowIfNull(armies);
        ArgumentNullException.ThrowIfNull(diplomacy);

        _table = table;
        _eras = eras;
        _people = people;
        _tribes = tribes;
        _settlements = settlements;
        _territory = territory;
        _armies = armies;
        _diplomacy = diplomacy;
        _market = market;

        _capacity = Math.Min(tribes.Capacity, diplomacy.Capacity);
        _width = territory.Width;
        _height = territory.Height;
        _rowsPerRun = Math.Max(1, _height / SweepParts);

        _contact = new int[_capacity * _capacity];
        _pendingContact = new int[_capacity * _capacity];
        _border = new int[_capacity];
        _pendingBorder = new int[_capacity];
        _capital = new int[_capacity];
        _armyCount = new int[_capacity];
        _armyMen = new int[_capacity];
        _siegeMen = new int[settlements.Capacity];
        _siegeTribe = new short[settlements.Capacity];

        _lossX = new int[ChangeSlots];
        _lossY = new int[ChangeSlots];
        _lossRadius = new int[ChangeSlots];
        _lossCount = new int[ChangeSlots];
        _handX = new int[ChangeSlots];
        _handY = new int[ChangeSlots];
        _handRadius = new int[ChangeSlots];
        _handFrom = new short[ChangeSlots];
        _handTo = new short[ChangeSlots];

        Array.Fill(_capital, -1);
    }

    public string Name => "Война";

    public int Interval => TicksPerRun;

    /// <summary>Номер прогона войны. По нему считаются перемирия и длина войн.</summary>
    public long Run => _run;

    /// <summary>Сколько войн идёт сейчас.</summary>
    public int ActiveWars { get; private set; }

    /// <summary>Сколько отрядов на карте сейчас.</summary>
    public int ArmyCount { get; private set; }

    /// <summary>Самое большое число отрядов за партию: по нему видно, хватает ли ёмкости.</summary>
    public int PeakArmies { get; private set; }

    /// <summary>Сколько боёв было в последний прогон.</summary>
    public int LastBattles { get; private set; }

    /// <summary>Сколько городов взято в последний прогон.</summary>
    public int LastCaptures { get; private set; }

    /// <summary>Сколько боёв было за партию.</summary>
    public int TotalBattles { get; private set; }

    /// <summary>Сколько городов взято за партию.</summary>
    public int TotalCaptures { get; private set; }

    /// <summary>Сколько восстаний было за партию.</summary>
    public int TotalRevolts { get; private set; }

    /// <summary>Сколько людей погибло на войне за партию: воины и мирные вместе.</summary>
    public int TotalDeaths { get; private set; }

    public void Tick(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);

        WorldMap? map = world.Map;
        if (map == null)
        {
            return;
        }

        _run++;
        LastBattles = 0;
        LastCaptures = 0;

        ForgetDeadTribes();
        ScanBorders();
        FindCapitals();
        CountArmies();

        if (_run % _table.War.CheckRuns == 0)
        {
            MakePeaces();
            DeclareWars(world);
        }

        Recruit();
        March(map);
        Battles(map);
        Sieges(world);
        Revolts(world);
        ApplyPeopleChanges();
        Upkeep();
        Exhale();

        ActiveWars = _diplomacy.WarCount;
        ArmyCount = _armies.Count;
        if (ArmyCount > PeakArmies)
        {
            PeakArmies = ArmyCount;
        }
    }

    /// <summary>Народ исчез: его войны и усталость закрываются, иначе слот достанется новому с чужой войной.</summary>
    private void ForgetDeadTribes()
    {
        for (int t = 1; t < _capacity; t++)
        {
            if (_tribes.Alive[t])
            {
                continue;
            }

            if (_diplomacy.Wars[t] > 0 || _diplomacy.Exhaustion[t] != 0f)
            {
                _diplomacy.Forget(t);
            }
        }
    }

    /// <summary>Полоса карты: считаем, чья земля с чьей соприкасается. Без соседства воевать не с кем.</summary>
    private void ScanBorders()
    {
        short[] owner = _territory.Owner;
        int endRow = Math.Min(_height, _scanRow + _rowsPerRun);

        for (int y = _scanRow; y < endRow; y++)
        {
            int row = y * _width;
            for (int x = 0; x < _width; x++)
            {
                int index = row + x;
                short mine = owner[index];
                if (mine == TribeStore.None || (uint)mine >= (uint)_capacity)
                {
                    continue;
                }

                // Смотрим только вправо и вниз: каждая пара тайлов считается один раз.
                if (x + 1 < _width)
                {
                    Touch(mine, owner[index + 1]);
                }

                if (y + 1 < _height)
                {
                    Touch(mine, owner[index + _width]);
                }
            }
        }

        _scanRow = endRow >= _height ? 0 : endRow;
        if (_scanRow == 0)
        {
            PublishBorders();
        }
    }

    private void Touch(short mine, short other)
    {
        if (other == TribeStore.None || other == mine || (uint)other >= (uint)_capacity)
        {
            return;
        }

        _pendingContact[(mine * _capacity) + other]++;
        _pendingContact[(other * _capacity) + mine]++;
        _pendingBorder[mine]++;
        _pendingBorder[other]++;
    }

    /// <summary>Итоги полного круга по карте становятся живыми данными.</summary>
    private void PublishBorders()
    {
        Array.Copy(_pendingContact, _contact, _contact.Length);
        Array.Clear(_pendingContact, 0, _pendingContact.Length);
        Array.Copy(_pendingBorder, _border, _border.Length);
        Array.Clear(_pendingBorder, 0, _pendingBorder.Length);
    }

    /// <summary>Столица народа — его самое людное поселение. К ней отходят разбитые и в ней набирают войско.</summary>
    private void FindCapitals()
    {
        Array.Fill(_capital, -1);

        int high = _settlements.HighWater;
        for (int i = 0; i < high; i++)
        {
            if (!_settlements.Alive[i])
            {
                continue;
            }

            short tribe = _settlements.Tribe[i];
            if ((uint)tribe >= (uint)_capacity)
            {
                continue;
            }

            int best = _capital[tribe];
            if (best < 0 || _settlements.People[i] > _settlements.People[best])
            {
                _capital[tribe] = i;
            }
        }
    }

    /// <summary>Сколько у кого отрядов и людей под ружьём. Заодно расходятся отряды исчезнувших народов.</summary>
    private void CountArmies()
    {
        Array.Clear(_armyCount, 0, _armyCount.Length);
        Array.Clear(_armyMen, 0, _armyMen.Length);

        int high = _armies.HighWater;
        for (int i = 0; i < high; i++)
        {
            if (!_armies.Alive[i])
            {
                continue;
            }

            short tribe = _armies.Tribe[i];
            if ((uint)tribe >= (uint)_capacity || !_tribes.Alive[tribe])
            {
                _armies.Disband(i);
                continue;
            }

            _armyCount[tribe]++;
            _armyMen[tribe] += _armies.Men[i];
        }
    }

    /// <summary>Кому пора мириться: усталым, разбитым и тем, чья война тянется слишком долго.</summary>
    private void MakePeaces()
    {
        WarCauseSettings cause = _table.War;

        for (int a = 1; a < _capacity; a++)
        {
            if (_diplomacy.Wars[a] == 0)
            {
                continue;
            }

            for (int b = a + 1; b < _capacity; b++)
            {
                if (!_diplomacy.IsAtWar(a, b))
                {
                    continue;
                }

                bool tooLong = _run - _diplomacy.SinceRun(a, b) >= cause.MaxWarRuns;
                bool tired = _diplomacy.Exhaustion[a] >= cause.PeaceExhaustion
                    || _diplomacy.Exhaustion[b] >= cause.PeaceExhaustion;
                bool nothingLeft = Beaten(a) || Beaten(b);

                if (tooLong || tired || nothingLeft)
                {
                    _diplomacy.MakePeace(a, b, _run + cause.TruceRuns);
                }
            }
        }
    }

    /// <summary>Стороне нечем и незачем воевать: ни города, ни отряда.</summary>
    private bool Beaten(int tribe)
        => !_tribes.Alive[tribe] || (_tribes.Settlements[tribe] <= 0 && _armyCount[tribe] == 0);

    /// <summary>Кто кому объявляет войну. Воюют только соседи: без общей границы войску идти некуда.</summary>
    private void DeclareWars(WorldState world)
    {
        WarCauseSettings cause = _table.War;
        int left = cause.DeclarationsPerCheck;
        if (left <= 0)
        {
            return;
        }

        for (int a = 1; a < _capacity && left > 0; a++)
        {
            // Разбитому народу войну не объявляют и сам он её не начинает: иначе в журнале
            // копятся пустые войны, которые замиряются в тот же прогон.
            if (!_tribes.Alive[a] || Beaten(a))
            {
                continue;
            }

            for (int b = a + 1; b < _capacity && left > 0; b++)
            {
                if (!_tribes.Alive[b] || _contact[(a * _capacity) + b] == 0 || Beaten(b))
                {
                    continue;
                }

                if (_diplomacy.StateOf(a, b) != Diplomacy.Peace || _diplomacy.TruceActive(a, b, _run))
                {
                    continue;
                }

                float wantA = Desire(a, b);
                float wantB = Desire(b, a);
                float chance = MathF.Min(cause.MaxChancePerCheck, MathF.Max(wantA, wantB));
                if (chance <= 0f || !world.Rng.Chance(chance))
                {
                    continue;
                }

                int aggressor = wantA >= wantB ? a : b;
                int victim = wantA >= wantB ? b : a;
                if (!_diplomacy.Declare(aggressor, victim, _run))
                {
                    continue;
                }

                left--;
                JoinAllies(world, aggressor, victim, cause.AllyJoin);
            }
        }
    }

    /// <summary>Насколько народу хочется чужой земли и чужого сырья.</summary>
    private float Desire(int a, int b)
    {
        WarCauseSettings cause = _table.War;
        if (_tribes.Era[a] < cause.MinEra || _tribes.Settlements[a] <= 0)
        {
            return 0f;
        }

        float weariness = cause.PeaceExhaustion > 0f ? _diplomacy.Exhaustion[a] / cause.PeaceExhaustion : 0f;
        if (weariness >= 1f)
        {
            return 0f;
        }

        // Доля своей границы, которая смотрит на этого соседа: дальние чужаки никого не злят.
        float share = _border[a] > 0 ? _contact[(a * _capacity) + b] / (float)_border[a] : 0f;
        float mine = _territory.Tiles[a];
        float theirs = _territory.Tiles[b];
        float land = theirs / (mine + theirs + 1f);

        float want = cause.Ambition
            + (cause.LandHunger * land * share)
            + (cause.ResourceGreed * Greed(a, b) * share);

        return want * (1f - weariness);
    }

    /// <summary>Чего не хватает нам, но есть у соседа.</summary>
    private float Greed(int a, int b)
    {
        if (_market == null || (uint)a >= (uint)_market.Capacity || (uint)b >= (uint)_market.Capacity)
        {
            return 0f;
        }

        int need = _market.Deficit[a];
        if (need == 0)
        {
            return 0f;
        }

        int both = need & _market.Surplus[b];
        return both == 0
            ? 0f
            : BitOperations.PopCount((uint)both) / (float)BitOperations.PopCount((uint)need);
    }

    /// <summary>Союзники иногда вступают в чужую войну.</summary>
    private void JoinAllies(WorldState world, int aggressor, int victim, float chance)
    {
        if (chance <= 0f)
        {
            return;
        }

        for (int c = 1; c < _capacity; c++)
        {
            if (c == aggressor || c == victim || !_tribes.Alive[c] || !_diplomacy.IsAlly(c, aggressor))
            {
                continue;
            }

            if (_diplomacy.TruceActive(c, victim, _run))
            {
                continue;
            }

            if (world.Rng.Chance(chance))
            {
                _diplomacy.Declare(c, victim, _run);
            }
        }
    }

    /// <summary>Набор войска. Отряд стоит денег: казна народа — потолок его военной силы.</summary>
    private void Recruit()
    {
        ArmySettings army = _table.Armies;
        int minEra = _table.War.MinEra;

        for (int t = 1; t < _capacity; t++)
        {
            if (!_armies.HasRoom)
            {
                return;
            }

            if (!_tribes.Alive[t] || _diplomacy.Wars[t] == 0 || _tribes.Era[t] < minEra)
            {
                continue;
            }

            // Сколько воинов народ вообще готов держать под ружьём.
            int want = (int)(_tribes.People[t] * army.MenPerThousandPeople / 1000f);
            if (want - _armyMen[t] < army.MenPerArmy)
            {
                continue;
            }

            float cost = army.MenPerArmy * army.GoldPerMan;
            if (_market != null && (uint)t < (uint)_market.Capacity)
            {
                if (_market.Wealth[t] < cost)
                {
                    continue;
                }

                _market.Wealth[t] -= cost;
            }

            int home = _capital[t];
            int x = home >= 0 ? _settlements.X[home] : _tribes.HomeX[t];
            int y = home >= 0 ? _settlements.Y[home] : _tribes.HomeY[t];

            if (_armies.Raise(x, y, (short)t, army.MenPerArmy, army.MoraleStart) >= 0)
            {
                _armyCount[t]++;
                _armyMen[t] += army.MenPerArmy;
            }
        }
    }

    /// <summary>Поход: снабжение, мораль и два шага к чужому городу.</summary>
    private void March(WorldMap map)
    {
        ArmySettings army = _table.Armies;
        int high = _armies.HighWater;

        for (int i = 0; i < high; i++)
        {
            if (!_armies.Alive[i])
            {
                continue;
            }

            short tribe = _armies.Tribe[i];
            short ground = _territory.OwnerAt(_armies.X[i], _armies.Y[i]);
            if (ground == tribe)
            {
                // Своя земля кормит и ободряет.
                _armies.Supply[i] = 1f;
                _armies.Morale[i] = MathF.Min(1f, _armies.Morale[i] + army.MoraleRecovery);
            }
            else if (ground != TribeStore.None && _diplomacy.IsAtWar(tribe, ground))
            {
                _armies.Supply[i] = MathF.Max(0f, _armies.Supply[i] - army.SupplyLossPerRun);
            }

            int target = _armies.Target[i];
            if (!ValidTarget(tribe, target))
            {
                target = FindTarget(tribe, _armies.X[i], _armies.Y[i]);
                _armies.Target[i] = target;
            }

            int toX;
            int toY;
            if (target >= 0)
            {
                toX = _settlements.X[target];
                toY = _settlements.Y[target];
            }
            else if (!FindEnemyArmy(tribe, i, out toX, out toY))
            {
                continue;
            }

            Step(map, i, toX, toY, army.MarchTilesPerRun);
        }
    }

    /// <summary>Цель годится, пока город жив и принадлежит врагу.</summary>
    private bool ValidTarget(short tribe, int target)
        => target >= 0
            && target < _settlements.Capacity
            && _settlements.Alive[target]
            && _diplomacy.IsAtWar(tribe, _settlements.Tribe[target]);

    /// <summary>Ближайший чужой город.</summary>
    private int FindTarget(short tribe, int x, int y)
    {
        int best = -1;
        int bestDistance = int.MaxValue;
        int high = _settlements.HighWater;

        for (int i = 0; i < high; i++)
        {
            if (!_settlements.Alive[i])
            {
                continue;
            }

            short owner = _settlements.Tribe[i];
            if (owner == tribe || !_diplomacy.IsAtWar(tribe, owner))
            {
                continue;
            }

            int dx = _settlements.X[i] - x;
            int dy = _settlements.Y[i] - y;
            int distance = (dx * dx) + (dy * dy);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    /// <summary>Ближайший вражеский отряд: если городов у врага нет, войско идёт на войско.</summary>
    private bool FindEnemyArmy(short tribe, int self, out int x, out int y)
    {
        x = 0;
        y = 0;
        int best = int.MaxValue;
        int high = _armies.HighWater;

        for (int i = 0; i < high; i++)
        {
            if (i == self || !_armies.Alive[i] || !_diplomacy.IsAtWar(tribe, _armies.Tribe[i]))
            {
                continue;
            }

            int dx = _armies.X[i] - _armies.X[self];
            int dy = _armies.Y[i] - _armies.Y[self];
            int distance = (dx * dx) + (dy * dy);
            if (distance < best)
            {
                best = distance;
                x = _armies.X[i];
                y = _armies.Y[i];
            }
        }

        return best != int.MaxValue;
    }

    /// <summary>Шаги к цели. Вплотную к цели отряд не входит: осада и бой идут с соседнего тайла.</summary>
    private void Step(WorldMap map, int index, int toX, int toY, int tiles)
    {
        for (int step = 0; step < tiles; step++)
        {
            int dx = toX - _armies.X[index];
            int dy = toY - _armies.Y[index];
            if (Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1)
            {
                return;
            }

            int stepX = Math.Sign(dx);
            int stepY = Math.Sign(dy);
            if (!TryMove(map, index, stepX, stepY)
                && !TryMove(map, index, stepX, 0)
                && !TryMove(map, index, 0, stepY))
            {
                return;
            }
        }
    }

    /// <summary>Переставляет отряд на клетку, если там суша.</summary>
    private bool TryMove(WorldMap map, int index, int dx, int dy)
    {
        if (dx == 0 && dy == 0)
        {
            return false;
        }

        int x = _armies.X[index] + dx;
        int y = _armies.Y[index] + dy;
        if (!map.InBounds(x, y))
        {
            return false;
        }

        int tile = map.Index(x, y);
        if (!map.IsLand(tile))
        {
            return false;
        }

        _armies.X[index] = x;
        _armies.Y[index] = y;
        return true;
    }

    /// <summary>Встретившиеся враги бьются сразу: соседние клетки — уже поле боя.</summary>
    private void Battles(WorldMap map)
    {
        BattleSettings battle = _table.Battle;
        ArmySettings army = _table.Armies;
        int high = _armies.HighWater;

        for (int a = 0; a < high; a++)
        {
            if (!_armies.Alive[a])
            {
                continue;
            }

            for (int b = a + 1; b < high; b++)
            {
                if (!_armies.Alive[a])
                {
                    break;
                }

                if (!_armies.Alive[b] || !_diplomacy.IsAtWar(_armies.Tribe[a], _armies.Tribe[b]))
                {
                    continue;
                }

                if (Math.Abs(_armies.X[a] - _armies.X[b]) > 1 || Math.Abs(_armies.Y[a] - _armies.Y[b]) > 1)
                {
                    continue;
                }

                Fight(map, a, b, battle, army);
            }
        }
    }

    /// <summary>
    /// Один бой. Сила отряда — число воинов, умноженное на эпоху, мораль, снабжение
    /// и домашнюю землю. Случайности в бою нет вовсе: исход читается с карты, а не с костей.
    /// </summary>
    private void Fight(WorldMap map, int a, int b, BattleSettings battle, ArmySettings army)
    {
        float strengthA = Strength(a, battle);
        float strengthB = Strength(b, battle);
        float total = strengthA + strengthB;
        if (total <= 0f)
        {
            return;
        }

        bool firstWins = strengthA >= strengthB;
        int winner = firstWins ? a : b;
        int loser = firstWins ? b : a;
        float winnerShare = (firstWins ? strengthA : strengthB) / total;
        float loserShare = 1f - winnerShare;

        int lossLoser = Losses(_armies.Men[loser], battle.LossShareLoser, winnerShare);
        int lossWinner = Losses(_armies.Men[winner], battle.LossShareWinner, loserShare);

        _armies.Men[loser] -= lossLoser;
        _armies.Men[winner] -= lossWinner;
        _armies.Morale[loser] = MathF.Max(0f, _armies.Morale[loser] - battle.MoraleHitLoser);
        _armies.Morale[winner] = MathF.Min(1f, _armies.Morale[winner] + battle.MoraleGainWinner);

        Tire(_armies.Tribe[loser], lossLoser);
        Tire(_armies.Tribe[winner], lossWinner);
        TotalDeaths += lossLoser + lossWinner;
        TotalBattles++;
        LastBattles++;

        // Рядом с полем боя гибнут и мирные: война выкашивает деревни.
        int civilians = (int)MathF.Round((lossLoser + lossWinner) * battle.CiviliansPerSoldier);
        QueueLoss(_armies.X[loser], _armies.Y[loser], battle.LossRadius, civilians);

        if (_armies.Men[loser] < army.DisbandMen)
        {
            _armies.Disband(loser);
            return;
        }

        if (_armies.Morale[loser] < battle.RetreatMorale)
        {
            Retreat(map, loser, winner, army.RetreatTiles);
        }
    }

    /// <summary>Потери тем больше, чем сильнее был противник.</summary>
    private static int Losses(int men, float share, float enemyShare)
    {
        int loss = (int)MathF.Ceiling(men * share * 2f * enemyShare);
        return Math.Clamp(loss, 0, men);
    }

    /// <summary>Разбитые отходят от победителя и ищут цель заново.</summary>
    private void Retreat(WorldMap map, int loser, int winner, int tiles)
    {
        int dx = Math.Sign(_armies.X[loser] - _armies.X[winner]);
        int dy = Math.Sign(_armies.Y[loser] - _armies.Y[winner]);
        if (dx == 0 && dy == 0)
        {
            dx = 1;
        }

        for (int step = 0; step < tiles; step++)
        {
            if (!TryMove(map, loser, dx, dy) && !TryMove(map, loser, dx, 0) && !TryMove(map, loser, 0, dy))
            {
                break;
            }
        }

        _armies.Target[loser] = ArmyStore.NoTarget;
    }

    private float Strength(int index, BattleSettings battle)
    {
        short tribe = _armies.Tribe[index];
        float power = 1f + ((EraPower(tribe) - 1f) * battle.EraPowerWeight);
        float morale = 1f + (_armies.Morale[index] * battle.MoraleWeight);
        float supply = MathF.Max(battle.SupplyFloor, _armies.Supply[index]);
        float ground = _territory.OwnerAt(_armies.X[index], _armies.Y[index]) == tribe
            ? 1f + battle.TerrainDefense
            : 1f;

        return _armies.Men[index] * power * morale * supply * ground;
    }

    /// <summary>Военная сила эпохи народа: копьё против танка.</summary>
    private float EraPower(short tribe)
    {
        if (_eras == null)
        {
            return 1f;
        }

        int era = Math.Clamp(_tribes.Era[tribe], 0, _eras.Count - 1);
        return _eras.MilitaryPower[era];
    }

    /// <summary>Осады: войско у стен давит город, ушло — осада спадает.</summary>
    private void Sieges(WorldState world)
    {
        SiegeSettings siege = _table.Siege;
        Array.Clear(_siegeMen, 0, _siegeMen.Length);
        Array.Clear(_siegeTribe, 0, _siegeTribe.Length);

        int highArmies = _armies.HighWater;
        for (int i = 0; i < highArmies; i++)
        {
            if (!_armies.Alive[i])
            {
                continue;
            }

            int target = _armies.Target[i];
            if (!ValidTarget(_armies.Tribe[i], target))
            {
                continue;
            }

            if (Math.Abs(_settlements.X[target] - _armies.X[i]) > 1
                || Math.Abs(_settlements.Y[target] - _armies.Y[i]) > 1)
            {
                continue;
            }

            _siegeMen[target] += _armies.Men[i];
            _siegeTribe[target] = _armies.Tribe[i];
        }

        int high = _settlements.HighWater;
        for (int i = 0; i < high; i++)
        {
            if (!_settlements.Alive[i])
            {
                continue;
            }

            int men = _siegeMen[i];
            if (men <= 0)
            {
                if (_settlements.Siege[i] > 0f)
                {
                    _settlements.Siege[i] = MathF.Max(0f, _settlements.Siege[i] - siege.ReliefPerRun);
                }

                continue;
            }

            // Стены тормозят подкоп, лишние люди сверх нормы его не ускоряют.
            float walls = 1f + (siege.WallsPerLevel * _settlements.Level[i]);
            float ready = MathF.Min(1f, men / (float)Math.Max(1, siege.MenForFullProgress));
            float progress = _settlements.Siege[i] + (siege.ProgressPerRun * ready / walls);

            bool starved = progress >= siege.StarveMinProgress
                && _settlements.Food[i] < _settlements.People[i] * siege.StarvePerPerson;

            if (progress >= 1f || starved)
            {
                Capture(world, i, _siegeTribe[i]);
                continue;
            }

            _settlements.Siege[i] = progress;
        }
    }

    /// <summary>Город взят: грабёж, новый хозяин, чужая земля вокруг и злоба горожан.</summary>
    private void Capture(WorldState world, int settlement, short winner)
    {
        SiegeSettings siege = _table.Siege;
        short old = _settlements.Tribe[settlement];
        if (winner == TribeStore.None || winner == old || (uint)winner >= (uint)_capacity)
        {
            return;
        }

        int x = _settlements.X[settlement];
        int y = _settlements.Y[settlement];

        if (_market != null && (uint)old < (uint)_market.Capacity && (uint)winner < (uint)_market.Capacity)
        {
            float loot = _market.Wealth[old] * siege.SackShare;
            _market.Wealth[old] -= loot;
            _market.Wealth[winner] += loot;
        }

        int killed = (int)MathF.Round(_settlements.People[settlement] * siege.SackShare);
        if (killed > 0)
        {
            _settlements.People[settlement] -= killed;
            QueueLoss(x, y, 1, killed);
        }

        _settlements.Tribe[settlement] = winner;
        _settlements.Siege[settlement] = 0f;
        _settlements.Food[settlement] = 0f;
        _settlements.Captured[settlement] = world.Tick;

        // Свой же город обратно встречают без злобы.
        _settlements.Unrest[settlement] = _settlements.Origin[settlement] == winner
            ? 0f
            : _table.Revolt.UnrestOnCapture;

        if (_tribes.Settlements[old] > 0)
        {
            _tribes.Settlements[old]--;
        }

        _tribes.Settlements[winner]++;

        int core = Math.Max((int)_settlements.Radius[settlement], 1);
        Annex(world, x, y, siege.AnnexRadius, core, old, winner);
        QueueHandover(x, y, siege.AnnexRadius, old, winner);

        TotalCaptures++;
        LastCaptures++;

        // Осаждавшие ищут новую цель, иначе будут стоять у своего же города.
        int highArmies = _armies.HighWater;
        for (int i = 0; i < highArmies; i++)
        {
            if (_armies.Alive[i] && _armies.Target[i] == settlement)
            {
                _armies.Target[i] = ArmyStore.NoTarget;
            }
        }
    }

    /// <summary>Земля вокруг города меняет хозяина: после осады карта другая.</summary>
    private void Annex(WorldState world, int centerX, int centerY, int radius, int core, short from, short to)
    {
        for (int y = centerY - radius; y <= centerY + radius; y++)
        {
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                if (!world.InBounds(x, y))
                {
                    continue;
                }

                int tile = world.Index(x, y);
                short owner = _territory.OwnerAt(tile);
                bool nearCity = Math.Abs(x - centerX) <= core && Math.Abs(y - centerY) <= core;
                if (owner == from || (nearCity && owner == TribeStore.None))
                {
                    _territory.Claim(tile, to);
                }
            }
        }
    }

    /// <summary>Восстания: захваченный город помнит старого хозяина.</summary>
    private void Revolts(WorldState world)
    {
        RevoltSettings revolt = _table.Revolt;
        int high = _settlements.HighWater;

        for (int i = 0; i < high; i++)
        {
            if (!_settlements.Alive[i] || _settlements.Unrest[i] <= 0f)
            {
                continue;
            }

            _settlements.Unrest[i] = MathF.Max(0f, _settlements.Unrest[i] - revolt.UnrestDecay);

            if (_settlements.Captured[i] <= 0L
                || _settlements.Unrest[i] < revolt.MinUnrest
                || !world.Rng.Chance(revolt.ChancePerRun))
            {
                continue;
            }

            Revolt(world, i, revolt);
        }
    }

    /// <summary>Бунт: город и земля вокруг уходят прежнему хозяину или новому свободному народу.</summary>
    private void Revolt(WorldState world, int settlement, RevoltSettings revolt)
    {
        short holder = _settlements.Tribe[settlement];
        short origin = _settlements.Origin[settlement];
        bool oldOwnerLives = origin != TribeStore.None
            && origin != holder
            && (uint)origin < (uint)_capacity
            && _tribes.Alive[origin];

        short rebel = TribeStore.None;
        if (!oldOwnerLives || world.Rng.Chance(revolt.FreeTribeChance))
        {
            rebel = NewTribe(world, settlement, holder);
        }

        if (rebel == TribeStore.None && oldOwnerLives)
        {
            rebel = origin;
        }

        if (rebel == TribeStore.None || rebel == holder)
        {
            return;
        }

        int x = _settlements.X[settlement];
        int y = _settlements.Y[settlement];
        int radius = Math.Max((int)_settlements.Radius[settlement], 1);

        _settlements.Tribe[settlement] = rebel;
        _settlements.Origin[settlement] = rebel;
        _settlements.Captured[settlement] = 0L;
        _settlements.Unrest[settlement] = 0f;
        _settlements.Siege[settlement] = 0f;

        if (_tribes.Settlements[holder] > 0)
        {
            _tribes.Settlements[holder]--;
        }

        _tribes.Settlements[rebel]++;

        int moved = _settlements.People[settlement];
        if (moved > 0)
        {
            _tribes.People[rebel] += moved;
            _tribes.People[holder] = Math.Max(0, _tribes.People[holder] - moved);
        }

        Annex(world, x, y, radius, radius, holder, rebel);
        QueueHandover(x, y, radius, holder, rebel);
        _diplomacy.Declare(rebel, holder, _run);
        TotalRevolts++;
    }

    /// <summary>Новый народ из бунта. Мест нет — бунт вернёт город прежнему хозяину.</summary>
    private short NewTribe(WorldState world, int settlement, short holder)
    {
        string name = TribeNames.Unique(world.Rng, _tribes);
        byte color = (byte)((_tribes.ColorIndex[holder] + 1) % PaletteColors);
        short rebel = _tribes.Create(name, color, _settlements.X[settlement], _settlements.Y[settlement]);
        if (rebel == TribeStore.None || (uint)rebel >= (uint)_capacity)
        {
            return TribeStore.None;
        }

        _tribes.Era[rebel] = _tribes.Era[holder];
        return rebel;
    }

    /// <summary>Копит смерти мирных: сами люди правятся одним проходом в конце прогона.</summary>
    private void QueueLoss(int x, int y, int radius, int count)
    {
        if (count <= 0 || _lossLen >= ChangeSlots)
        {
            return;
        }

        _lossX[_lossLen] = x;
        _lossY[_lossLen] = y;
        _lossRadius[_lossLen] = radius;
        _lossCount[_lossLen] = count;
        _lossLen++;
    }

    /// <summary>Копит смену подданства: жители взятой округи меняют народ.</summary>
    private void QueueHandover(int x, int y, int radius, short from, short to)
    {
        if (from == to || from == TribeStore.None || _handLen >= ChangeSlots)
        {
            return;
        }

        _handX[_handLen] = x;
        _handY[_handLen] = y;
        _handRadius[_handLen] = radius;
        _handFrom[_handLen] = from;
        _handTo[_handLen] = to;
        _handLen++;
    }

    /// <summary>Один проход по людям: сначала новое подданство, потом потери.</summary>
    private void ApplyPeopleChanges()
    {
        if (_lossLen == 0 && _handLen == 0)
        {
            return;
        }

        int high = _people.HighWater;
        for (int p = 0; p < high; p++)
        {
            if (!_people.Alive[p])
            {
                continue;
            }

            int x = _people.X[p];
            int y = _people.Y[p];

            for (int h = 0; h < _handLen; h++)
            {
                if (_people.Tribe[p] != _handFrom[h])
                {
                    continue;
                }

                if (Math.Abs(x - _handX[h]) > _handRadius[h] || Math.Abs(y - _handY[h]) > _handRadius[h])
                {
                    continue;
                }

                _people.Tribe[p] = _handTo[h];
                break;
            }

            for (int l = 0; l < _lossLen; l++)
            {
                if (_lossCount[l] <= 0)
                {
                    continue;
                }

                if (Math.Abs(x - _lossX[l]) > _lossRadius[l] || Math.Abs(y - _lossY[l]) > _lossRadius[l])
                {
                    continue;
                }

                _lossCount[l]--;
                _people.Kill(p);
                TotalDeaths++;
                break;
            }
        }

        _lossLen = 0;
        _handLen = 0;
    }

    /// <summary>Содержание войска. Кончилась война — отряды расходятся, кончилась казна — войско тает.</summary>
    private void Upkeep()
    {
        ArmySettings army = _table.Armies;
        int high = _armies.HighWater;

        for (int i = 0; i < high; i++)
        {
            if (!_armies.Alive[i])
            {
                continue;
            }

            short tribe = _armies.Tribe[i];
            if ((uint)tribe >= (uint)_capacity || !_tribes.Alive[tribe] || _diplomacy.Wars[tribe] == 0)
            {
                _armies.Disband(i);
                continue;
            }

            if (_market != null && (uint)tribe < (uint)_market.Capacity)
            {
                float cost = _armies.Men[i] * army.UpkeepPerMan;
                if (_market.Wealth[tribe] >= cost)
                {
                    _market.Wealth[tribe] -= cost;
                }
                else
                {
                    // На жалованье не хватило: воины разбегаются и голодают.
                    _market.Wealth[tribe] = 0f;
                    int lost = Math.Min(_armies.Men[i], (int)MathF.Ceiling(_armies.Men[i] * army.StarveLossShare));
                    _armies.Men[i] -= lost;
                    _armies.Morale[i] = MathF.Max(0f, _armies.Morale[i] - army.StarveMoraleDrop);
                    Tire(tribe, lost);
                    TotalDeaths += lost;
                }
            }

            if (_armies.Men[i] < army.DisbandMen)
            {
                _armies.Disband(i);
            }
        }
    }

    /// <summary>Каждый погибший воин копит усталость от войны.</summary>
    private void Tire(short tribe, int soldiers)
    {
        if (soldiers <= 0 || (uint)tribe >= (uint)_capacity)
        {
            return;
        }

        float grown = _diplomacy.Exhaustion[tribe] + (_table.War.ExhaustionPerSoldier * soldiers);
        _diplomacy.Exhaustion[tribe] = MathF.Min(1f, grown);
    }

    /// <summary>Усталость спадает сама: без этого мир стал бы навсегда мирным.</summary>
    private void Exhale()
    {
        float decay = _table.War.ExhaustionDecay;
        if (decay <= 0f)
        {
            return;
        }

        for (int t = 1; t < _capacity; t++)
        {
            if (_diplomacy.Exhaustion[t] > 0f)
            {
                _diplomacy.Exhaustion[t] = MathF.Max(0f, _diplomacy.Exhaustion[t] - decay);
            }
        }
    }
}
