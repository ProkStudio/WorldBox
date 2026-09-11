using WorldBox.Core.Eras;
using WorldBox.Core.Simulation;
using WorldBox.Core.World;

namespace WorldBox.Core.People;

/// <summary>
/// Жизнь людей за один тик: старение, еда и голод, переезд на соседний тайл, роды и смерть.
/// Никаких списков и LINQ: один проход по массивам, ноль аллокаций.
/// Все случайности берутся из единственного генератора мира, поэтому партия повторяема.
///
/// Настройки посчитаны на опорный пятилетний тик. Когда система эпох сжимает время,
/// шансы пересчитываются на новую длину тика: иначе при двадцати пяти годах в тике
/// люди умирали бы от старости быстрее, чем успевали рожать, и мир вымирал бы в палеолите.
///
/// Смерть у человека одна, поэтому шансы смерти и переезда пересчитываются по правилу
/// «хотя бы раз за тик». А детей за длинный тик бывает несколько, поэтому рождаемость — это
/// ожидаемое число детей (ставка умножается на длину тика), а не один шанс на тик.
/// Старый потолок «не больше одного ребёнка за тик» в палеолите давал на человека 0,8 ребёнка
/// за всю жизнь — меньше замещения, и мир вымирал за тридцать тиков.
/// </summary>
public sealed class PopulationSystem : ISimulationSystem
{
    /// <summary>Длина тика в годах, на которую посчитаны числа в PopulationSettings.</summary>
    public const float ReferenceYears = 5f;

    private static readonly int[] NeighborX = { 1, 1, 0, -1, -1, -1, 0, 1 };
    private static readonly int[] NeighborY = { 0, 1, 1, 1, 0, -1, -1, -1 };

    private readonly PopulationSettings _settings;
    private readonly TribeTech? _tech;

    public PopulationSystem(Population people, PopulationSettings? settings = null, TribeTech? tech = null)
    {
        ArgumentNullException.ThrowIfNull(people);
        People = people;
        _settings = settings ?? PopulationSettings.Default;
        _tech = tech;
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
        TribeTech? tech = _tech;
        int techCapacity = tech?.Capacity ?? 0;

        float years = world.YearsPerTick;
        float ratio = years > 0f ? years / ReferenceYears : 1f;

        // Пересчёт делается один раз на тик, а не на каждого жителя.
        float oldAgeChance = Compound(settings.OldAgeChance, ratio);
        float starveChance = Compound(settings.StarveChance, ratio);
        float moveChance = Compound(settings.MoveChance, ratio);

        // Рождаемость масштабируется линейно: за вдвое дольший тик рождается вдвое больше детей.
        float birthRate = settings.BirthChance * ratio;
        float hungerGain = settings.HungerGain * ratio;
        float foodFactor = settings.FoodFactor * ratio;

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
            float food = fertility * foodFactor / (1f + (density * settings.Crowding));

            // С эпохами один работник кормит больше: пашня, плуг, фабрики.
            if (tech != null)
            {
                int tribe = people.Tribe[i];
                if ((uint)tribe < (uint)techCapacity)
                {
                    food *= tech.FoodMultiplier[tribe];
                }
            }

            float hunger = people.Hunger[i] + hungerGain - food;
            if (hunger < 0f)
            {
                hunger = 0f;
            }
            else if (hunger > 1f)
            {
                hunger = 1f;
            }

            people.Hunger[i] = hunger;

            if (age > settings.MaxAge && rng.Chance(oldAgeChance))
            {
                people.Kill(i);
                deaths++;
                continue;
            }

            if (hunger >= 1f && rng.Chance(starveChance))
            {
                people.Kill(i);
                deaths++;
                continue;
            }

            if (rng.Chance(moveChance))
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
                && people.HasRoom)
            {
                // Целая часть — дети, которые родятся точно, дробная — один бросок кости.
                // Бросок делается всегда, чтобы поток случайных чисел не зависел от веток.
                float expected = birthRate * (1f - hunger);
                int children = (int)expected;
                if (rng.Chance(expected - children))
                {
                    children++;
                }

                if (children > settings.MaxBirthsPerTick)
                {
                    children = settings.MaxBirthsPerTick;
                }

                for (int child = 0; child < children; child++)
                {
                    if (!people.HasRoom || people.Spawn(x, y, people.Tribe[i], 0f) < 0)
                    {
                        break;
                    }

                    births++;
                }
            }
        }

        LastBirths = births;
        LastDeaths = deaths;
    }

    /// <summary>
    /// Переводит шанс с опорного тика на текущий по правилу «хотя бы раз за несколько лет».
    /// Рождаемость и смертность меняются вместе, поэтому баланс не рассыпается при сжатии времени.
    /// </summary>
    private static float Compound(float chance, float ratio)
    {
        if (chance <= 0f)
        {
            return 0f;
        }

        if (chance >= 1f)
        {
            return 1f;
        }

        if (ratio > 0.999f && ratio < 1.001f)
        {
            return chance;
        }

        return 1f - MathF.Pow(1f - chance, ratio);
    }
}
