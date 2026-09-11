namespace WorldBox.Core.World;

/// <summary>
/// Генератор мира. Шаги идут в том же порядке, что в docs/DESIGN.md:
/// высота, уровень моря, температура, влажность, реки, биомы, плодородие, ресурсы.
/// Один и тот же сид всегда даёт один и тот же мир.
/// </summary>
public static class WorldGenerator
{
    /// <summary>
    /// Где у мира полюс. Края карты срезаны в океан (см. BuildElevation), поэтому суша живёт
    /// только внутри этой доли высоты. Широта тянется именно до сюда.
    /// </summary>
    private const float PolarEdge = 0.82f;

    /// <summary>Сторона области, по которым раздаётся доля месторождений: руда есть в каждом краю мира.</summary>
    private const int Region = 64;

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
        AssignBiomes(map, settings);
        BuildFertility(map);
        PlaceResources(map, rng, settings);
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
            // Края карты всегда океан, поэтому суша живёт внутри полосы |широта| < PolarEdge.
            // Широту растягиваем на эту полосу: иначе у мира нет ни одного холодного берега,
            // вся суша оказывается тропиками и тундра с тайгой не появляются никогда.
            float latitude = Math.Clamp(MathF.Abs(((y * invHeight) * 2f) - 1f) / PolarEdge, 0f, 1f);
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
        float invHeight = 1f / Math.Max(1, map.Height - 1);
        float beltWidth = MathF.Max(settings.DryBeltWidth, 0.01f);

        // Ветер дует с запада: горы слева забирают дождь и сушат всё, что справа.
        for (int y = 0; y < map.Height; y++)
        {
            float latitude = Math.Clamp(MathF.Abs(((y * invHeight) * 2f) - 1f) / PolarEdge, 0f, 1f);

            // Пояс пустынь: на этой широте воздух опускается и дождя почти нет.
            float belt = (latitude - settings.DryBeltLatitude) / beltWidth;
            float dryBelt = 1f - (settings.DryBeltStrength * MathF.Exp(-belt * belt));

            float barrier = 0f;
            for (int x = 0; x < map.Width; x++)
            {
                int i = map.Index(x, y);
                float elevation = map.Elevation[i];
                barrier = MathF.Max(barrier * 0.985f, elevation);

                // Влага с моря падает нелинейно: середина материка суше берега заметно сильнее.
                float fromOcean = 1f - (Math.Min(distance[i], reach) / (float)reach);
                fromOcean *= fromOcean;
                float random = noise.Fbm(x * 0.016f, y * 0.016f, 5);
                float value = (fromOcean * 0.45f) + (random * 0.55f);

                float shadow = MathF.Max(0f, barrier - elevation);
                value -= shadow * settings.RainShadow;
                value *= dryBelt;

                // Холодный воздух держит меньше воды.
                value *= 0.70f + (0.30f * map.Temperature[i]);

                map.Moisture[i] = Math.Clamp(value, 0f, 1f);
            }
        }

        NormalizeMoisture(map);
    }

    /// <summary>
    /// Растягивает влажность суши на весь диапазон 0..1 по рангу.
    /// Шум сам по себе кучкуется около середины, и тогда вся суша превращается в один луг.
    /// Ранг сохраняет рисунок «где суше», но гарантирует, что сухие и мокрые пояса есть на любом сиде.
    /// </summary>
    private static void NormalizeMoisture(WorldMap map)
    {
        const int Buckets = 4096;
        var histogram = new int[Buckets];
        int land = 0;

        for (int i = 0; i < map.TileCount; i++)
        {
            if (map.Elevation[i] < map.SeaLevel)
            {
                continue;
            }

            histogram[Math.Clamp((int)(map.Moisture[i] * Buckets), 0, Buckets - 1)]++;
            land++;
        }

        if (land == 0)
        {
            return;
        }

        var rank = new float[Buckets];
        int accumulated = 0;
        for (int b = 0; b < Buckets; b++)
        {
            int here = histogram[b];
            rank[b] = (accumulated + (here * 0.5f)) / land;
            accumulated += here;
        }

        for (int i = 0; i < map.TileCount; i++)
        {
            int bucket = Math.Clamp((int)(map.Moisture[i] * Buckets), 0, Buckets - 1);
            map.Moisture[i] = Math.Clamp(rank[bucket], 0f, 1f);
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

    /// <summary>
    /// Где начинаются горы и вершины, считаем по доле суши, а не по абсолютной высоте:
    /// иначе на гористом сиде хребты съедают треть материка, а на ровном гор нет вообще.
    /// Нижние границы оставлены, чтобы равнина не объявляла себя хребтом.
    /// </summary>
    private static void PickMountainLevels(WorldMap map, WorldGenSettings settings, out float mountainAbove, out float peakAbove)
    {
        const int Buckets = 1024;
        var histogram = new int[Buckets];
        int land = 0;

        for (int i = 0; i < map.TileCount; i++)
        {
            float above = map.Elevation[i] - map.SeaLevel;
            if (above < 0f)
            {
                continue;
            }

            histogram[Math.Clamp((int)(above * Buckets), 0, Buckets - 1)]++;
            land++;
        }

        mountainAbove = 0.16f;
        peakAbove = 0.26f;
        if (land == 0)
        {
            return;
        }

        int mountainTarget = (int)(land * Math.Clamp(settings.MountainShare, 0.01f, 0.5f));
        int peakTarget = (int)(land * Math.Clamp(settings.PeakShare, 0.002f, 0.3f));

        int accumulated = 0;
        float mountainLevel = 0f;
        float peakLevel = 0f;
        bool peakFound = false;
        bool mountainFound = false;

        for (int b = Buckets - 1; b >= 0; b--)
        {
            accumulated += histogram[b];
            if (!peakFound && accumulated >= peakTarget)
            {
                peakLevel = b / (float)Buckets;
                peakFound = true;
            }

            if (accumulated >= mountainTarget)
            {
                mountainLevel = b / (float)Buckets;
                mountainFound = true;
                break;
            }
        }

        if (!mountainFound)
        {
            return;
        }

        mountainAbove = MathF.Max(mountainLevel, 0.10f);
        peakAbove = MathF.Max(peakFound ? peakLevel : mountainAbove + 0.10f, mountainAbove + 0.04f);
    }

    private static void AssignBiomes(WorldMap map, WorldGenSettings settings)
    {
        float sea = map.SeaLevel;
        PickMountainLevels(map, settings, out float mountainAbove, out float peakAbove);
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
                else if (above > peakAbove)
                {
                    biome = Biome.Peak;
                }
                else if (above > mountainAbove)
                {
                    biome = temperature < 0.14f ? Biome.Glacier : Biome.Mountain;
                }
                else if (above < 0.012f && TouchesOcean(map, x, y))
                {
                    biome = temperature < 0.16f ? Biome.Tundra : Biome.Beach;
                }
                else if (above < 0.035f && temperature > 0.25f
                    && (moisture > 0.86f || (moisture > 0.66f && map.Flow[i] >= map.RiverThreshold * 0.45f)))
                {
                    // Болото — это мокрая низина или разлив рядом с рекой, а не половина материка.
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

        if (temperature < 0.52f)
        {
            // Сухо и прохладно — это степь, а не пустыня: полоса шире, чем у жарких широт.
            if (moisture < 0.26f)
            {
                return Biome.Steppe;
            }

            if (moisture < 0.40f)
            {
                return Biome.Shrubland;
            }

            if (moisture < 0.62f)
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

    /// <summary>
    /// Раскладывает месторождения по доле суши, а не по абсолютному порогу шума.
    /// Порог отдавал редкую руду на волю случая: на карте 256 на 256 олова выходило
    /// семь тайлов, а урана ни одного, и все народы навсегда застревали перед бронзой.
    /// Доля гарантирует руду на любом сиде, а рисунок пятен по-прежнему задаёт шум.
    /// Идём от редкой руды к частой: редкая занимает подходящий тайл первой.
    /// </summary>
    private static void PlaceResources(WorldMap map, Rng rng, WorldGenSettings settings)
    {
        int land = 0;
        for (int i = 0; i < map.TileCount; i++)
        {
            map.ResourceAt[i] = (byte)ResourceKind.None;
            if (Biomes.IsLand((Biome)map.BiomeAt[i]))
            {
                land++;
            }
        }

        if (land == 0)
        {
            return;
        }

        // Генерация идёт один раз и вне тика, поэтому два буфера на всю сушу здесь уместны.
        var tiles = new int[land];
        var scores = new float[land];
        int minimum = Math.Max(0, settings.MinDeposits);

        Sprinkle(map, rng, tiles, scores, land, ResourceKind.Uranium, settings.UraniumShare, minimum, 0.05f);
        Sprinkle(map, rng, tiles, scores, land, ResourceKind.Oil, settings.OilShare, minimum, 0.04f);
        Sprinkle(map, rng, tiles, scores, land, ResourceKind.Tin, settings.TinShare, minimum, 0.06f);
        Sprinkle(map, rng, tiles, scores, land, ResourceKind.Saltpeter, settings.SaltpeterShare, minimum, 0.05f);
        Sprinkle(map, rng, tiles, scores, land, ResourceKind.Coal, settings.CoalShare, minimum, 0.045f);
        Sprinkle(map, rng, tiles, scores, land, ResourceKind.Iron, settings.IronShare, minimum, 0.05f);
        Sprinkle(map, rng, tiles, scores, land, ResourceKind.Copper, settings.CopperShare, minimum, 0.055f);
        Sprinkle(map, rng, tiles, scores, land, ResourceKind.Stone, settings.StoneShare, minimum, 0.07f);
        Sprinkle(map, rng, tiles, scores, land, ResourceKind.Wood, settings.WoodShare, minimum, 0.08f);
    }

    /// <summary>
    /// Кладёт один вид руды. Доля раздаётся не на всю карту сразу, а по областям
    /// Region на Region, и внутри области руда садится туда, где выше шум.
    /// Без областей верхушка шума собирала редкую руду в одно-два пятна на весь мир:
    /// нефть лежала в глуши, ни одного тайла во владениях, и мир вставал на Индустрии.
    /// Остаток от деления переносится в следующую область, поэтому итог равен доле.
    /// </summary>
    private static void Sprinkle(
        WorldMap map,
        Rng rng,
        int[] tiles,
        float[] scores,
        int land,
        ResourceKind kind,
        float share,
        int minimum,
        float frequency)
    {
        var noise = new Noise(rng.NextInt(int.MaxValue));

        int total = 0;
        for (int i = 0; i < map.TileCount; i++)
        {
            var free = (Biome)map.BiomeAt[i];
            if (map.ResourceAt[i] == (byte)ResourceKind.None
                && Biomes.IsLand(free)
                && Fits(kind, free, map.Elevation[i] - map.SeaLevel))
            {
                total++;
            }
        }

        if (total == 0)
        {
            return;
        }

        int target = (int)(land * Math.Clamp(share, 0f, 0.5f));
        target = Math.Clamp(target, Math.Min(minimum, total), total);
        if (target == 0)
        {
            return;
        }

        float ratio = target / (float)total;
        float carry = 0f;

        for (int blockY = 0; blockY < map.Height; blockY += Region)
        {
            for (int blockX = 0; blockX < map.Width; blockX += Region)
            {
                int endY = Math.Min(map.Height, blockY + Region);
                int endX = Math.Min(map.Width, blockX + Region);
                int count = 0;

                for (int y = blockY; y < endY; y++)
                {
                    for (int x = blockX; x < endX; x++)
                    {
                        int i = map.Index(x, y);
                        if (map.ResourceAt[i] != (byte)ResourceKind.None)
                        {
                            continue;
                        }

                        var biome = (Biome)map.BiomeAt[i];
                        if (!Biomes.IsLand(biome) || !Fits(kind, biome, map.Elevation[i] - map.SeaLevel))
                        {
                            continue;
                        }

                        tiles[count] = i;
                        scores[count] = noise.Fbm(x * frequency, y * frequency, 3);
                        count++;
                    }
                }

                if (count == 0)
                {
                    continue;
                }

                carry += count * ratio;
                int take = Math.Min((int)carry, count);
                if (take <= 0)
                {
                    continue;
                }

                carry -= take;

                // Сортировка по возрастанию шума: самые рудные тайлы оказываются в хвосте.
                Array.Sort(scores, tiles, 0, count);

                for (int n = 0; n < take; n++)
                {
                    map.ResourceAt[tiles[count - 1 - n]] = (byte)kind;
                }
            }
        }
    }

    /// <summary>Где руда вообще может лежать. Маски те же, что были у порогов шума.</summary>
    private static bool Fits(ResourceKind kind, Biome biome, float above)
    {
        bool mountains = biome == Biome.Mountain || biome == Biome.Peak;
        bool hills = above > 0.07f;
        bool forest = biome == Biome.TemperateForest || biome == Biome.Taiga || biome == Biome.Rainforest;
        bool dry = biome == Biome.Desert || biome == Biome.Savanna || biome == Biome.Steppe;

        return kind switch
        {
            ResourceKind.Uranium => mountains || dry,
            ResourceKind.Oil => above < 0.06f || biome == Biome.Marsh,
            ResourceKind.Tin => mountains,
            ResourceKind.Saltpeter => dry,
            ResourceKind.Coal => forest || biome == Biome.Marsh,
            ResourceKind.Iron => hills,
            ResourceKind.Copper => hills,
            ResourceKind.Stone => mountains || hills,
            ResourceKind.Wood => forest,
            _ => false,
        };
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        float t = Math.Clamp((value - edge0) / MathF.Max(edge1 - edge0, 0.0001f), 0f, 1f);
        return t * t * (3f - (2f * t));
    }
}
