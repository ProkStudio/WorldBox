namespace WorldBox.Core.War;

/// <summary>
/// Отношения народов: мир, война, союз. Плюс две вещи, без которых войны шли бы вечно:
/// усталость от войны и перемирие после мира, пока которого второй раз воевать нельзя.
///
/// Матрица квадратная и симметричная: 64 народа — это всего пара килобайт, зато
/// вопрос «эти двое воюют?» стоит одно обращение в память. Аллокаций в тике нет.
/// </summary>
public sealed class Diplomacy
{
    /// <summary>Мир: отношений нет или они нейтральны.</summary>
    public const byte Peace = 0;

    /// <summary>Война.</summary>
    public const byte War = 1;

    /// <summary>Союз: такие друг другу войну не объявляют и вступают в чужие войны.</summary>
    public const byte Alliance = 2;

    private readonly byte[] _state;
    private readonly long[] _truceUntil;
    private readonly long[] _since;

    public Diplomacy(int capacity)
    {
        if (capacity < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Нужно хотя бы два слота народов.");
        }

        Capacity = capacity;
        _state = new byte[capacity * capacity];
        _truceUntil = new long[capacity * capacity];
        _since = new long[capacity * capacity];
        Wars = new int[capacity];
        Exhaustion = new float[capacity];
    }

    public int Capacity { get; }

    /// <summary>Сколько войн у каждого народа сейчас.</summary>
    public int[] Wars { get; }

    /// <summary>Усталость от войны от 0 до 1. Дошла до порога — народ идёт мириться.</summary>
    public float[] Exhaustion { get; }

    /// <summary>Сколько пар воюет сейчас.</summary>
    public int WarCount { get; private set; }

    /// <summary>Сколько войн объявлено за всю партию.</summary>
    public int DeclarationCount { get; private set; }

    /// <summary>Сколько миров заключено за всю партию.</summary>
    public int PeaceCount { get; private set; }

    /// <summary>Место пары в матрице.</summary>
    public int Pair(int a, int b) => (a * Capacity) + b;

    public byte StateOf(int a, int b) => Inside(a, b) ? _state[Pair(a, b)] : Peace;

    public bool IsAtWar(int a, int b) => StateOf(a, b) == War;

    public bool IsAlly(int a, int b) => StateOf(a, b) == Alliance;

    /// <summary>Воюет ли народ хоть с кем-то.</summary>
    public bool IsAtWarAny(int tribe) => (uint)tribe < (uint)Capacity && Wars[tribe] > 0;

    /// <summary>С какого прогона идёт эта война.</summary>
    public long SinceRun(int a, int b) => Inside(a, b) ? _since[Pair(a, b)] : 0L;

    /// <summary>Держится ли ещё перемирие между этими двумя.</summary>
    public bool TruceActive(int a, int b, long run) => Inside(a, b) && run < _truceUntil[Pair(a, b)];

    /// <summary>Объявляет войну. Возвращает false, если война уже идёт или пара неверная.</summary>
    public bool Declare(int a, int b, long run)
    {
        if (!Inside(a, b) || a == b || a == 0 || b == 0 || _state[Pair(a, b)] == War)
        {
            return false;
        }

        Set(a, b, War);
        _since[Pair(a, b)] = run;
        _since[Pair(b, a)] = run;
        Wars[a]++;
        Wars[b]++;
        WarCount++;
        DeclarationCount++;
        return true;
    }

    /// <summary>Заключает мир и ставит перемирие до заданного прогона.</summary>
    public bool MakePeace(int a, int b, long truceUntilRun)
    {
        if (!Inside(a, b) || _state[Pair(a, b)] != War)
        {
            return false;
        }

        Set(a, b, Peace);
        _truceUntil[Pair(a, b)] = truceUntilRun;
        _truceUntil[Pair(b, a)] = truceUntilRun;

        if (Wars[a] > 0)
        {
            Wars[a]--;
        }

        if (Wars[b] > 0)
        {
            Wars[b]--;
        }

        if (WarCount > 0)
        {
            WarCount--;
        }

        PeaceCount++;
        return true;
    }

    /// <summary>Заключает союз. Воюющие сначала должны помириться.</summary>
    public bool SetAlliance(int a, int b)
    {
        if (!Inside(a, b) || a == b || a == 0 || b == 0 || _state[Pair(a, b)] != Peace)
        {
            return false;
        }

        Set(a, b, Alliance);
        return true;
    }

    /// <summary>Народ исчез: все его войны и союзы закрываются, иначе слот достанется новому с чужими войнами.</summary>
    public void Forget(int tribe)
    {
        if ((uint)tribe >= (uint)Capacity)
        {
            return;
        }

        for (int other = 1; other < Capacity; other++)
        {
            if (other == tribe)
            {
                continue;
            }

            if (_state[Pair(tribe, other)] == War)
            {
                MakePeace(tribe, other, 0L);
            }

            Set(tribe, other, Peace);
            _truceUntil[Pair(tribe, other)] = 0L;
            _truceUntil[Pair(other, tribe)] = 0L;
        }

        Wars[tribe] = 0;
        Exhaustion[tribe] = 0f;
    }

    public void Clear()
    {
        Array.Clear(_state, 0, _state.Length);
        Array.Clear(_truceUntil, 0, _truceUntil.Length);
        Array.Clear(_since, 0, _since.Length);
        Array.Clear(Wars, 0, Wars.Length);
        Array.Clear(Exhaustion, 0, Exhaustion.Length);
        WarCount = 0;
        DeclarationCount = 0;
        PeaceCount = 0;
    }

    /// <summary>Грубая контрольная сумма для теста повторимости.</summary>
    public ulong Checksum()
    {
        unchecked
        {
            ulong hash = 1469598103934665603UL;
            hash = Mix(hash, (ulong)(uint)WarCount);
            hash = Mix(hash, (ulong)(uint)DeclarationCount);
            hash = Mix(hash, (ulong)(uint)PeaceCount);

            for (int a = 1; a < Capacity; a++)
            {
                hash = Mix(hash, (ulong)(long)MathF.Round(Exhaustion[a] * 1000f));

                for (int b = a + 1; b < Capacity; b++)
                {
                    byte state = _state[Pair(a, b)];
                    if (state == Peace && _truceUntil[Pair(a, b)] == 0L)
                    {
                        continue;
                    }

                    hash = Mix(hash, (ulong)(uint)a);
                    hash = Mix(hash, (ulong)(uint)b);
                    hash = Mix(hash, state);
                    hash = Mix(hash, (ulong)_truceUntil[Pair(a, b)]);
                }
            }

            return hash;
        }
    }

    private void Set(int a, int b, byte state)
    {
        _state[Pair(a, b)] = state;
        _state[Pair(b, a)] = state;
    }

    private bool Inside(int a, int b) => (uint)a < (uint)Capacity && (uint)b < (uint)Capacity;

    private static ulong Mix(ulong hash, ulong value)
    {
        unchecked
        {
            hash ^= value;
            hash *= 1099511628211UL;
            return hash;
        }
    }
}
