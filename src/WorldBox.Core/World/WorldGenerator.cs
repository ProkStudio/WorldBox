namespace WorldBox.Core.World;

/// <summary>
/// Генератор мира. Шаги идут в том же порядке, что в docs/DESIGN.md:
/// высота, уровень моря, температура, влажность, реки, биомы, плодородие, ресурсы.
/// Один и тот же сид всегда даёт один и тот же мир.
/// </summary>
public static class WorldGenerator
{
    private static readonly int[] NeighborX = { 1, 1, 0, -1, -1, -1, 0, 1 };
    private static readonly int[] NeighborY = { 0, 1, 1, 1, 0, -1, -1, -1 };

    public static WorldMap Generate(int width, int height, int seed)
        => Generate(width, height, seed, WorldGenSettings.Default);

    public static WorldMap Generate(int width, int height, int seed, WorldGenSettings settings)
    {
        var map = new WorldMap(width, height) { Seed = seed };
        var rng = new Rng(seed);

        BuildElevation(map, rng, settings);
        PickSeaLevel(map, settings);
        BuildTemperature(map, rng);
        BuildMoisture(map, rng, settings);
        BuildRivers(map, settings);
        AssignBiomes(map);
        BuildFertility(map);
        PlaceResources(map, rng);
        map.Recount();
        return map;
    }

    private static void BuildElevation(WorldMap map, Rng rng, WorldGenSettings settings)
    {
        var continents = new Noise(rng.NextInt(int.MaxValue));
        var detail = new Noise(rng.NextInt(int.MaxValue));
        var ridges = new Noise(rng.NextInt(int.MaxValue));
        var warp = new Noise(rng.NextInt(int.MaxValue));

        float scale = settings.ContinentScale;
        float invWidth = 1f / Math.Max(1, map.Width - 1);
        float invHeight = 1f / Math.Max(1, map.Height - 1);

        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                float fx = x * scale;
                float fy = y * scale;

                // Лёгкое искривление координат делает берега изрезанными, а не круглыми.
                float wx = (warp.Fbm(fx * 1.7f, fy * 1.7f, 3) - 0.5f) * 1.4f;
                float wy = (warp.Fbm((fx * 1.7f) + 31.7f, (fy * 1.7f) - 12.3f, 3) - 0.5f) * 1.4f;

                float baseHeight = continents.Fbm(fx + wx, fy + wy, 6);
                float fine = detail.Fbm(x * 0.03f, y * 0.03f, 4);
                float ridge = ridges.Ridged(x * 0.012f, y * 0.012f, 5);

                float value = (baseHeight * 0.70f) + (fine * 0.12f) + (ridge * settings.MountainStrength * baseHeight);

                // Края карты всегда океан: игрок не видит обрезанных материков.
                float nx = MathF.Abs(((x * invWidth) * 2f) - 1f);
                float ny = MathF.Abs(((y * invHeight) * 2f) - 1f);
                float edge = MathF.Max(nx, ny);
                float falloff = 1f - SmoothStep(0.72f, 0.99f, edge);

                map.Elevation[map.Index(x, y)] = Math.Clamp(value * falloff, 0f, 1f);
            }
        }
    }

    private static void PickSeaLevel(WorldMap map, WorldGenSettings settings)
    {
        const int Buckets = 2048;
        var histogram = new int[Buckets];
        for (int i = 0; i < map.TileCount; i++)
        {
            int bucket = Math.Clamp((int)(map.Elevation[i] * Buckets), 0, Buckets - 1);
            histogram[bucket]++;
        }

        int target = (int)(map.TileCount * Math.Clamp(settings.LandFraction, 0.05f, 0.9f));
        int accumulated = 0;
        int index = Buckets - 1;
        for (; index > 0; index--)
        {
            accumulated += histogram[index];
            if (accumulated >= target)
            {
                break;
            }
        }

        map.SeaLevel = index / (float)Buckets;
    }

    private static void BuildTemperature(WorldMap map, Rng rng)
    {
        var noise = new Noise(rng.NextInt(int.MaxValue));
        float invHeight = 1f / Math.Max(1, map.Height - 1);

        for (int y = 0; y < map.Height; y++)
        {
            float latitude = MathF.Abs(((y * invHeight) * 2f) - 1f);
            float bandTemperature = 1f - (latitude * latitude * 0.85f) - (latitude * 0.15f);

            for (int x = 0; x < map.Width; x++)
            {
                int i = map.Index(x, y);
                float value = bandTemperature + ((noise.Fbm(x * 0.010f, y * 0.010f, 4) - 0.5f) * 0.16f);

                float aboveSea = map.Elevation[i] - map.SeaLevel;
                if (aboveSea > 0f)
                {
                    value -= aboveSea * 1.35f;
                }

                map.Temperature[i] = Math.Clamp(value, 0f, 1f);
            }
        }
    }

    private static void BuildMoisture(WorldMap map, Rng rng, WorldGenSettings settings)
    {
        var noise = new Noise(rng.NextInt(int.MaxValue));
        int[] distance = DistanceToWater(map);
        int reach = Math.Max(1, settings.OceanMoistureReach);

        // Ветер дует с запада: горы слева забирают дождь и сушат всё, что справа.
        for (int y = 0; y < map.Height; y++)
        {
            float barrier = 0f;
            for (int x = 0; x < map.Width; x++)
            {
                int i = map.Index(x, y);
                float elevation = map.Elevation[i];
                barrier = MathF.Max(barrier * 0.985f, elevation);

                float fromOcean = 1f - (Math.Min(distance[i], reach) / (float)reach);
                float random = noise.Fbm(x * 0.016f, y * 0.016f, 5);
                float value = (fromOcean * 0.52f) + (random * 0.48f);

                float shadow = MathF.Max(0f, barrier - elevation);
                value -= shadow * settings.RainShadow;

                // Холодный воздух держит меньше воды.
                value *= 0.55f + (0.45f * map.Temperature[i]);

                map.Moisture[i] = Math.Clamp(value, 0f, 1f);
            }
        }
    }

    private static int[] DistanceToWater(WorldMap map)
    {
        var distance = new int[map.TileCount];
        var queue = new int[map.TileCount];
        int head = 0;
        int tail = 0;

        for (int i = 0; i < map.TileCount; i++)
        {
            if (map.Elevation[i] < map.SeaLevel)
            {
                distance[i] = 0;
                queue[tail++] = i;
            }
            else
            {
                distance[i] = int.MaxValue;
            }
        }

        while (head < tail)
        {
            int current = queue[head++];
            int cx = current % map.Width;
            int cy = current / map.Width;
            int next = distance[current] + 1;

            for (int k = 0; k < 8; k += 2)
            {
                int nx = cx + NeighborX[k];
                int ny = cy + NeighborY[k];
                if (!map.InBounds(nx, ny))
                {
                    continue;
                }

                int neighbor = map.Index(nx, ny);
                if (distance[neighbor] > next)
                {
                    distance[neighbor] = next;
                    queue[tail++] = neighbor;
                }
            }
        }

        return distance;
    }

    private static void BuildRivers(WorldMap map, WorldGenSettings settings)
    {
        int landCount = 0;
        for (int i = 0; i < map.TileCount; i++)
        {
            map.FlowDirection[i] = 255;
            map.Flow[i] = 0f;
            if (map.Elevation[i] >= map.SeaLevel)
            {
                landCount++;
            }
        }

        if (landCount == 0)
        {
            map.RiverThreshold = float.MaxValue;
            return;
        }

        var order = new int[landCount];
        var keys = new float[landCount];
        int cursor = 0;
        for (int i = 0; i < map.TileCount; i++)
        {
            if (map.Elevation[i] >= map.SeaLevel)
            {
                order[cursor] = i;
                keys[cursor] = -map.Elevation[i];
                map.Flow[i] = 0.15f + (map.Moisture[i] * 0.85f);
                cursor++;
            }
        }

        // Сверху вниз: каждый тайл отдаёт воду самому низкому соседу.
        Array.Sort(keys, order);

        for (int n = 0; n < order.Length; n++)
        {
            int current = order[n];
            int cx = current % map.Width;
            int cy = current / map.Width;
            float currentHeight = map.Elevation[current];

            int bestIndex = -1;
            int bestDirection = 255;
            float bestHeight = currentHeight;

            for (int k = 0; k < 8; k++)
            {
                int nx = cx + NeighborX[k];
                int ny = cy + NeighborY[k];
                if (!map.InBounds(nx, ny))
                {
                    continue;
                }

                int neighbor = map.Index(nx, ny);
                float neighborHeight = map.Elevation[neighbor];
                if (neighborHeight < bestHeight)
                {
                    bestHeight = neighborHeight;
                    bestIndex = neighbor;
                    bestDirection = k;
                }
            }

            if (bestIndex >= 0)
            {
                map.FlowDirection[current] = (byte)bestDirection;
                map.Flow[bestIndex] += map.Flow[current];
            }
        }

        var flows = new float[landCount];
        for (int n = 0; n < order.Length; n++)
        {
            flows[n] = map.Flow[order[n]];
        }

        Array.Sort(flows);
        int riverIndex = (int)((1f - Math.Clamp(settings.RiverFraction, 0.001f, 0.2f)) * (flows.Length - 1));
        map.RiverThreshold = MathF.Max(flows[riverIndex], 6f);
    }

    private static void AssignBiomes(WorldMap map)
    {
        float sea = map.SeaLevel;
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                int i = map.Index(x, y);
                float elevation = map.Elevation[i];

                if (elevation < sea)
                {
                    float depth = sea - elevation;
                    Biome water = depth > 0.10f ? Biome.DeepOcean : depth > 0.025f ? Biome.Ocean : Biome.Coast;
                    map.BiomeAt[i] = (byte)water;
                    continue;
                }

                float above = elevation - sea;
                float temperature = map.Temperature[i];
                float moisture = map.Moisture[i];
                bool hasRiver = map.Flow[i] >= map.RiverThreshold;
                bool isPit = map.FlowDirection[i] == 255;

                Biome biome;
                if (isPit && map.Flow[i] >= map.RiverThreshold * 1.5f)
                {
                    biome = Biome.Lake;
                }
                else if (hasRiver)
                {
                    biome = Biome.River;
                }
                else if (above > 0.26f)
                {
                    biome = Biome.Peak;
                }
                else if (above > 0.16f)
                {
                    biome = temperature < 0.14f ? Biome.Glacier : Biome.Mountain;
                }
                else if (above < 0.012f && TouchesOcean(map, x, y))
                {
                    biome = temperature < 0.16f ? Biome.Tundra : Biome.Beach;
                }
                else if (moisture > 0.72f && above < 0.05f && temperature > 0.25f)
                {
                    biome = Biome.Marsh;
                }
                else
                {
                    biome = Whittaker(temperature, moisture);
                }

                map.BiomeAt[i] = (byte)biome;
            }
        }
    }

    /// <summary>Таблица «тепло против влаги»: чем теплее и влажнее, тем гуще растительность.</summary>
    private static Biome Whittaker(float temperature, float moisture)
    {
        if (temperature < 0.13f)
        {
            return moisture < 0.30f ? Biome.Tundra : Biome.Glacier;
        }

        if (temperature < 0.30f)
        {
            return moisture < 0.26f ? Biome.Tundra : Biome.Taiga;
        }

        if (temperature < 0.50f)
        {
            if (moisture < 0.18f)
            {
                return Biome.Steppe;
            }

            if (moisture < 0.34f)
            {
                return Biome.Shrubland;
            }

            if (moisture < 0.58f)
            {
                return Biome.Grassland;
            }

            return Biome.TemperateForest;
        }

        if (temperature < 0.72f)
        {
            if (moisture < 0.16f)
            {
                return Biome.Desert;
            }

            if (moisture < 0.32f)
            {
                return Biome.Shrubland;
            }

            if (moisture < 0.52f)
            {
                return Biome.Grassland;
            }

            return Biome.TemperateForest;
        }

        if (moisture < 0.18f)
        {
            return Biome.Desert;
        }

        if (moisture < 0.42f)
        {
            return Biome.Savanna;
        }

        if (moisture < 0.60f)
        {
            return Biome.Grassland;
        }

        return Biome.Rainforest;
    }

    private static bool TouchesOcean(WorldMap map, int x, int y)
    {
        for (int k = 0; k < 8; k++)
        {
            int nx = x + NeighborX[k];
            int ny = y + NeighborY[k];
            if (map.InBounds(nx, ny) && map.Elevation[map.Index(nx, ny)] < map.SeaLevel)
            {
                return true;
            }
        }

        return false;
    }

    private static void BuildFertility(WorldMap map)
    {
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                int i = map.Index(x, y);
                var biome = (Biome)map.BiomeAt[i];
                if (Biomes.IsWater(biome))
                {
                    map.Fertility[i] = 0f;
                    continue;
                }

                float value = Biomes.Info(biome).Fertility;
                value *= 0.65f + (0.35f * map.Moisture[i]);

                // Пойма: рядом с рекой или озером земля всегда богаче.
                if (NextToFreshWater(map, x, y))
                {
                    value += 0.22f;
                }

                // В мороз растёт плохо.
                value *= 0.45f + (0.55f * Math.Clamp(map.Temperature[i] * 1.4f, 0f, 1f));

                map.Fertility[i] = Math.Clamp(value, 0f, 1f);
            }
        }
    }

    private static bool NextToFreshWater(WorldMap map, int x, int y)
    {
        for (int k = 0; k < 8; k++)
        {
            int nx = x + NeighborX[k];
            int ny = y + NeighborY[k];
            if (!map.InBounds(nx, ny))
            {
                continue;
            }

            var biome = (Biome)map.BiomeAt[map.Index(nx, ny)];
            if (biome == Biome.River || biome == Biome.Lake)
            {
                return true;
            }
        }

        return false;
    }

    private static void PlaceResources(WorldMap map, Rng rng)
    {
        var wood = new Noise(rng.NextInt(int.MaxValue));
        var stone = new Noise(rng.NextInt(int.MaxValue));
        var copper = new Noise(rng.NextInt(int.MaxValue));
        var tin = new Noise(rng.NextInt(int.MaxValue));
        var iron = new Noise(rng.NextInt(int.MaxValue));
        var coal = new Noise(rng.NextInt(int.MaxValue));
        var saltpeter = new Noise(rng.NextInt(int.MaxValue));
        var oil = new Noise(rng.NextInt(int.MaxValue));
        var uranium = new Noise(rng.NextInt(int.MaxValue));

        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                int i = map.Index(x, y);
                var biome = (Biome)map.BiomeAt[i];
                float above = map.Elevation[i] - map.SeaLevel;
                bool mountains = biome == Biome.Mountain || biome == Biome.Peak;
                bool hills = above > 0.07f;
                bool forest = biome == Biome.TemperateForest || biome == Biome.Taiga || biome == Biome.Rainforest;
                bool dry = biome == Biome.Desert || biome == Biome.Savanna || biome == Biome.Steppe;
                bool lowland = above < 0.06f && Biomes.IsLand(biome);

                // От редких к частым: редкий ресурс не должен затираться частым.
                ResourceKind kind = ResourceKind.None;
                if ((mountains || dry) && uranium.Fbm(x * 0.05f, y * 0.05f, 3) > 0.885f)
                {
                    kind = ResourceKind.Uranium;
                }
                else if ((lowland || biome == Biome.Marsh || biome == Biome.Coast) && oil.Fbm(x * 0.04f, y * 0.04f, 3) > 0.855f)
                {
                    kind = ResourceKind.Oil;
                }
                else if (mountains && tin.Fbm(x * 0.06f, y * 0.06f, 3) > 0.825f)
                {
                    kind = ResourceKind.Tin;
                }
                else if (dry && saltpeter.Fbm(x * 0.05f, y * 0.05f, 3) > 0.815f)
                {
                    kind = ResourceKind.Saltpeter;
                }
                else if ((forest || biome == Biome.Marsh) && coal.Fbm(x * 0.045f, y * 0.045f, 3) > 0.795f)
                {
                    kind = ResourceKind.Coal;
                }
                else if (hills && iron.Fbm(x * 0.05f, y * 0.05f, 3) > 0.755f)
                {
                    kind = ResourceKind.Iron;
                }
                else if (hills && copper.Fbm(x * 0.055f, y * 0.055f, 3) > 0.735f)
                {
                    kind = ResourceKind.Copper;
                }
                else if ((mountains || hills) && stone.Fbm(x * 0.07f, y * 0.07f, 3) > 0.62f)
                {
                    kind = ResourceKind.Stone;
                }
                else if (forest && wood.Fbm(x * 0.08f, y * 0.08f, 3) > 0.55f)
                {
                    kind = ResourceKind.Wood;
                }

                map.ResourceAt[i] = (byte)kind;
            }
        }
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        float t = Math.Clamp((value - edge0) / MathF.Max(edge1 - edge0, 0.0001f), 0f, 1f);
        return t * t * (3f - (2f * t));
    }
}
