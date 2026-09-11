using WorldBox.Core.Simulation;
using WorldBox.Core.World;

namespace WorldBox.Core.People;

/// <summary>
/// Жизнь людей за один тик: старение, еда и голод, переезд на соседний тайл, роды и смерть.
/// Никаких списков и LINQ: один проход по массивам, ноль аллокаций.
/// Все случайности берутся из единственного генератора мира, поэтому партия повторяема.
/// </summary>
public sealed class PopulationSystem : ISimulationSystem
{
    private static readonly int[] NeighborX = { 1, 1, 0, -1, -1, -1, 0, 1 };
    private static readonly int[] NeighborY = { 0, 1, 1, 1, 0, -1, -1, -1 };

    private readonly PopulationSettings _settings;

    public PopulationSystem(Population people, PopulationSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(people);
        People = people;
        _settings = settings ?? PopulationSettings.Default;
    }

    public Population People { get; }

    public string Name => "Жители";

    public int LastBirths { get; private set; }

    public int LastDeaths { get; private set; }

    public void Tick(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);

        WorldMap? map = world.Map;
        if (map == null)
        {
            return;
        }

        Population people = People;
        PopulationSettings settings = _settings;
        Rng rng = world.Rng;

        float years = world.YearsPerTick;
        int width = map.Width;
        int height = map.Height;

        // Граница берётся до цикла: рождённые в этом тике ждут следующего.
        int high = people.HighWater;
        int births = 0;
        int deaths = 0;

        for (int i = 0; i < high; i++)
        {
            if (!people.Alive[i])
            {
                continue;
            }

            int x = people.X[i];
            int y = people.Y[i];
            int index = (y * width) + x;

            float age = people.Age[i] + years;
            people.Age[i] = age;

            float fertility = map.Fertility[index];
            int density = people.DensityAt(index);
            float food = fertility * settings.FoodFactor / (1f + (density * settings.Crowding));

            float hunger = people.Hunger[i] + settings.HungerGain - food;
            if (hunger < 0f)
            {
                hunger = 0f;
            }
            else if (hunger > 1f)
            {
                hunger = 1f;
            }

            people.Hunger[i] = hunger;

            if (age > settings.MaxAge && rng.Chance(settings.OldAgeChance))
            {
                people.Kill(i);
                deaths++;
                continue;
            }

            if (hunger >= 1f && rng.Chance(settings.StarveChance))
            {
                people.Kill(i);
                deaths++;
                continue;
            }

            if (rng.Chance(settings.MoveChance))
            {
                int side = rng.NextInt(8);
                int nx = x + NeighborX[side];
                int ny = y + NeighborY[side];

                if ((uint)nx < (uint)width && (uint)ny < (uint)height)
                {
                    int neighbor = (ny * width) + nx;
                    if (map.IsLand(neighbor))
                    {
                        int neighborDensity = people.DensityAt(neighbor);
                        float neighborFertility = map.Fertility[neighbor];

                        // Идут туда, где сытнее или свободнее. Голодные идут куда угодно.
                        if (neighborFertility > fertility
                            || neighborDensity < density
                            || hunger > settings.DesperateHunger)
                        {
                            people.Move(i, nx, ny);
                            x = nx;
                            y = ny;
                        }
                    }
                }
            }

            if (age >= settings.AdultAge
                && age <= settings.LastFertileAge
                && hunger < settings.BirthHungerLimit
                && people.HasRoom
                && rng.Chance(settings.BirthChance * (1f - hunger)))
            {
                if (people.Spawn(x, y, people.Tribe[i], 0f) >= 0)
                {
                    births++;
                }
            }
        }

        LastBirths = births;
        LastDeaths = deaths;
    }
}
