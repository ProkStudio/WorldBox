namespace WorldBox.Core.Tribes;

/// <summary>
/// Кто владеет тайлом. Ноль — ничья земля. Один массив на всю карту:
/// 512x512 это полмегабайта, зато проверка владельца стоит одно обращение в память.
/// Счётчик изменений нужен рисованию: пока он не вырос, перерисовывать границы незачем.
/// </summary>
public sealed class Territory
{
    private readonly int _width;
    private readonly int _height;

    public Territory(int width, int height, int tribeCapacity)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Размер карты должен быть положительным.");
        }

        if (tribeCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tribeCapacity), "Нужен хотя бы один слот народа.");
        }

        _width = width;
        _height = height;
        Owner = new short[width * height];
        Core = new byte[width * height];
        Tiles = new int[tribeCapacity];
    }

    public int Width => _width;

    public int Height => _height;

    public short[] Owner { get; }

    /// <summary>
    /// Ядро владений: тайл занят самим поселением. Сглаживание границ такие тайлы не отдаёт,
    /// иначе город на краю страны терял бы землю под собственными домами.
    /// </summary>
    public byte[] Core { get; }

    /// <summary>Сколько тайлов у каждого народа.</summary>
    public int[] Tiles { get; }

    /// <summary>Сколько тайлов занято хоть кем-то.</summary>
    public int Claimed { get; private set; }

    /// <summary>Растёт при любом изменении владельца.</summary>
    public int Version { get; private set; }

    public short OwnerAt(int index) => (uint)index < (uint)Owner.Length ? Owner[index] : TribeStore.None;

    public short OwnerAt(int x, int y)
    {
        return (uint)x < (uint)_width && (uint)y < (uint)_height ? Owner[(y * _width) + x] : TribeStore.None;
    }

    /// <summary>Ставит владельца тайлу. Возвращает true, если владелец сменился.</summary>
    public bool Claim(int index, short tribe) => Claim(index, tribe, false);

    /// <summary>
    /// Ставит владельца тайлу. Флаг core помечает землю под самим поселением:
    /// такой тайл не уходит при сглаживании границ. Возвращает true, если владелец сменился.
    /// </summary>
    public bool Claim(int index, short tribe, bool core)
    {
        if ((uint)index >= (uint)Owner.Length || (uint)tribe >= (uint)Tiles.Length)
        {
            return false;
        }

        short current = Owner[index];
        if (current == tribe)
        {
            // Владелец тот же, но пометку ядра обновить надо: поселение могло вырасти.
            if (core)
            {
                Core[index] = 1;
            }

            return false;
        }

        if (current != TribeStore.None)
        {
            Tiles[current]--;
        }

        if (tribe != TribeStore.None)
        {
            Tiles[tribe]++;
        }

        if (current == TribeStore.None)
        {
            Claimed++;
        }
        else if (tribe == TribeStore.None)
        {
            Claimed--;
        }

        Owner[index] = tribe;
        Core[index] = core ? (byte)1 : (byte)0;
        Version++;
        return true;
    }

    /// <summary>Отпускает землю народа вокруг точки. Чужие тайлы не трогает.</summary>
    public int Release(short tribe, int centerX, int centerY, int radius)
    {
        if (tribe == TribeStore.None)
        {
            return 0;
        }

        int minX = Math.Max(0, centerX - radius);
        int maxX = Math.Min(_width - 1, centerX + radius);
        int minY = Math.Max(0, centerY - radius);
        int maxY = Math.Min(_height - 1, centerY + radius);
        int cleared = 0;

        for (int y = minY; y <= maxY; y++)
        {
            int row = y * _width;
            for (int x = minX; x <= maxX; x++)
            {
                int index = row + x;
                if (Owner[index] == tribe && Claim(index, TribeStore.None))
                {
                    cleared++;
                }
            }
        }

        return cleared;
    }

    /// <summary>Отпускает всю землю народа. Зовётся редко: только когда народ исчез.</summary>
    public int ReleaseAll(short tribe)
    {
        if (tribe == TribeStore.None)
        {
            return 0;
        }

        int cleared = 0;
        for (int i = 0; i < Owner.Length; i++)
        {
            if (Owner[i] == tribe && Claim(i, TribeStore.None))
            {
                cleared++;
            }
        }

        return cleared;
    }

    public void Clear()
    {
        Array.Clear(Owner, 0, Owner.Length);
        Array.Clear(Core, 0, Core.Length);
        Array.Clear(Tiles, 0, Tiles.Length);
        Claimed = 0;
        Version++;
    }

    public ulong Checksum()
    {
        unchecked
        {
            ulong hash = 1469598103934665603UL;
            hash ^= (ulong)Claimed;
            hash *= 1099511628211UL;

            for (int i = 0; i < Owner.Length; i++)
            {
                short owner = Owner[i];
                if (owner == TribeStore.None)
                {
                    continue;
                }

                hash ^= (ulong)(uint)i;
                hash *= 1099511628211UL;
                hash ^= (ulong)(uint)owner;
                hash *= 1099511628211UL;
            }

            return hash;
        }
    }
}
