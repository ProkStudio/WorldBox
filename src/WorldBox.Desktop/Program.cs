using System.Globalization;
using WorldBox.Desktop;

// Аргументы: --seed <число> --size <число>
// Пример: dotnet run --project src/WorldBox.Desktop -- --seed 42 --size 512
int seed = 20260911;
int size = 512;

for (int i = 0; i < args.Length - 1; i++)
{
    if (string.Equals(args[i], "--seed", StringComparison.Ordinal) &&
        int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedSeed))
    {
        seed = parsedSeed;
    }
    else if (string.Equals(args[i], "--size", StringComparison.Ordinal) &&
             int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedSize))
    {
        size = Math.Clamp(parsedSize, 64, 4096);
    }
}

using var game = new WorldBoxGame(seed, size);
game.Run();
