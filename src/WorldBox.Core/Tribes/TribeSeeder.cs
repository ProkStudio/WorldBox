using WorldBox.Core.People;
using WorldBox.Core.World;

namespace WorldBox.Core.Tribes;

/// <summary>
/// Старт партии: несколько очагов на плодородной суше, вокруг каждого свой народ,
/// его люди и первая стоянка. Точки берутся из генератора мира, поэтому один сид
/// всегда даёт одну и ту же расстановку.
/// </summary>
public static class TribeSeeder
{
    public const int DefaultTribes = 7;

    private const int SpotAttempts = 8192;
    private const float MinSpotFertility = 0.32f;
    private const int SpreadRadius = 4;
    private const int PlaceAttempts = 48;
    private const int ColorCount = 12;
    private const float MaxStartAge = 30f;

    public static int Seed(
        WorldState world,
        Population people,
        TribeStore tribes,
        SettlementStore settlements,
        Territory territory,
        int tribeCount,
        int peoplePerTribe)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(people);
        ArgumentNullException.ThrowIfNull(tribes);
        ArgumentNullException.ThrowIfNull(settlements);
        ArgumentNullException.ThrowIfNull(territory);

        WorldMap map = world.Map ?? throw new InvalidOperationException("У мира ещё нет карты: сначала SetMap.");
        if (tribeCount <= 0 || peoplePerTribe <= 0)
        {
            return 0;
        }

        Rng rng = world.Rng;
        int minDistance = Math.Max(10, Math.Min(map.Width, map.Height) / 8);
        int[] spotX = new int[tribeCount];
        int[] spotY = new int[tribeCount];
        int created = 0;

        for (int t = 0; t < tribeCount; t++)
        {
            if (!TryFindSpot(map, rng, spotX, spotY, created, minDistance, out int x, out int y))
            {
                break;
            }

            short tribe = tribes.Create(TribeNames.Unique(rng, tribes), (byte)(created % ColorCount), x, y);
            if (tribe == TribeStore.None)
            {
                break;
            }

            spotX[created] = x;
            spotY[created] = y;
            created++;

            int born = Spread(map, people, rng, x, y, tribe, peoplePerTribe);
            tribes.People[tribe] = born;

            int slot = settlements.Found(x, y, tribe, TribeNames.Place(rng), world.Tick);
            if (slot < 0)
            {
                continue;
            }

            byte radius = TribeSettings.Default.CampRadius;
            settlements.Radius[slot] = radius;
            settlements.People[slot] = born;
            tribes.Settlements[tribe] = 1;
            ClaimCircle(map, territory, x, y, radius, tribe);
        }

        return created;
    }

    private static bool TryFindSpot(
        WorldMap map,
        Rng rng,
        int[] spotX,
        int[] spotY,
        int used,
        int minDistance,
        out int x,
        out int y)
    {
        int width = map.Width;
        int height = map.Height;
        int limit = minDistance * minDistance;

        for (int attempt = 0; attempt < SpotAttempts; attempt++)
        {
            int cx = rng.NextInt(width);
            int cy = rng.NextInt(height);
            int index = (cy * width) + cx;

            if (!map.IsLand(index) || map.Fertility[index] < MinSpotFertility)
            {
                continue;
            }

            bool tooClose = false;
            for (int i = 0; i < used; i++)
            {
                int dx = spotX[i] - cx;
                int dy = spotY[i] - cy;
                if ((dx * dx) + (dy * dy) < limit)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
            {
                continue;
            }

            x = cx;
            y = cy;
            return true;
        }

        x = 0;
        y = 0;
        return false;
    }

    private static int Spread(WorldMap map, Population people, Rng rng, int centerX, int centerY, short tribe, int count)
    {
        int width = map.Width;
        int height = map.Height;
        int born = 0;

        for (int n = 0; n < count; n++)
        {
            for (int attempt = 0; attempt < PlaceAttempts; attempt++)
            {
                int x = centerX + rng.NextInt(-SpreadRadius, SpreadRadius + 1);
                int y = centerY + rng.NextInt(-SpreadRadius, SpreadRadius + 1);

                if ((uint)x >= (uint)width || (uint)y >= (uint)height || !map.IsLand((y * width) + x))
                {
                    continue;
                }

                if (people.Spawn(x, y, tribe, rng.Range(0f, MaxStartAge)) >= 0)
                {
                    born++;
                }

                break;
            }
        }

        return born;
    }

    private static void ClaimCircle(WorldMap map, Territory territory, int centerX, int centerY, int radius, short tribe)
    {
        int limit = radius * radius;
        int minX = Math.Max(0, centerX - radius);
        int maxX = Math.Min(map.Width - 1, centerX + radius);
        int minY = Math.Max(0, centerY - radius);
        int maxY = Math.Min(map.Height - 1, centerY + radius);

        for (int y = minY; y <= maxY; y++)
        {
            int dy = y - centerY;
            int row = y * map.Width;

            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - centerX;
                if ((dx * dx) + (dy * dy) > limit)
                {
                    continue;
                }

                int index = row + x;
                if (map.IsLand(index) && territory.OwnerAt(index) == TribeStore.None)
                {
                    territory.Claim(index, tribe);
                }
            }
        }
    }
}
