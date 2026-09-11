using System.Diagnostics;
using System.Globalization;
using WorldBox.Core;
using WorldBox.Core.Simulation;

// Консольный замер симуляции без окна.
// Пример: dotnet run -c Release --project src/WorldBox.Bench -- --ticks 5000 --size 512 --seed 1
int ticks = 5000;
int size = 512;
int seed = 20260911;

for (int i = 0; i < args.Length - 1; i++)
{
    if (!int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
    {
        continue;
    }

    switch (args[i])
    {
        case "--ticks":
            ticks = Math.Max(1, value);
            break;
        case "--size":
            size = Math.Clamp(value, 16, 4096);
            break;
        case "--seed":
            seed = value;
            break;
    }
}

var world = new WorldState(size, size, seed);
var loop = new SimulationLoop(world);

// Прогрев: первые тики всегда медленнее из-за JIT.
loop.RunTicks(Math.Min(200, ticks));
world.TickStats.Clear();

var watch = Stopwatch.StartNew();
loop.RunTicks(ticks);
watch.Stop();

double totalMs = watch.Elapsed.TotalMilliseconds;
double msPerTick = totalMs / ticks;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("Мир: {0}x{1}, сид {2}", size, size, seed);
Console.WriteLine("Тиков: {0}", ticks);
Console.WriteLine("Всего: {0:F1} мс", totalMs);
Console.WriteLine("На тик: {0:F4} мс (бюджет 6 мс)", msPerTick);
Console.WriteLine("Худший 1% тиков: {0:F4} мс", world.TickStats.Percentile(0.99));
Console.WriteLine("Тиков в секунду: {0:F0}", 1000.0 / Math.Max(msPerTick, 0.000001));
Console.WriteLine();
Console.WriteLine("Строка для docs/PERF.md:");
Console.WriteLine(
    "| {0:yyyy-MM-dd} | S0 | {1}x{1}, пустой мир | {2:F4} | {3:F4} | — |",
    DateTime.Now,
    size,
    msPerTick,
    world.TickStats.Percentile(0.99));

for (int i = 0; i < loop.SystemCount; i++)
{
    Console.WriteLine("  {0}: {1:F2} мс всего", loop.SystemName(i), loop.SystemTotalMs(i));
}
