using WorldBox.Core.World;

namespace WorldBox.Core.People;

/// <summary>
/// Первое расселение. Люди появляются только на суше и тем охотнее, чем плодороднее тайл,
/// поэтому старт получается в поймах и на лугах, а не в пустыне и не на льду.
/// Использует единственный генератор случайных чисел мира, значит результат повторяем по сиду.
/// </summary>
public static class PopulationSeeder
{
    private const float MinFertility = 0.25f;
    private const float MaxStartAge = 35f;
    private const int AttemptsPerPerson = 64;

    public static int Seed(WorldState world, Population people, int count)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(people);

        WorldMap? map = world.Map;
        if (map == null || count <= 0)
        {
            return 0;
        }

        Rng rng = world.Rng;
        int placed = 0;
        int attempts = 0;
        int maxAttempts = count * AttemptsPerPerson;

        while (placed < count && attempts < maxAttempts && people.HasRoom)
        {
            attempts++;

            int x = rng.NextInt(map.Width);
            int y = rng.NextInt(map.Height);
            int index = (y * map.Width) + x;

            if (!map.IsLand(index))
            {
                continue;
            }

            float fertility = map.Fertility[index];
            if (fertility < MinFertility || !rng.Chance(fertility))
            {
                continue;
            }

            float age = rng.NextFloat() * MaxStartAge;
            if (people.Spawn(x, y, 0, age) >= 0)
            {
                placed++;
            }
        }

        return placed;
    }
}
