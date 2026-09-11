namespace WorldBox.Core.Simulation;

/// <summary>
/// Одна система мира: население, экономика, война и так далее.
/// Порядок выполнения задаётся один раз в SimulationLoop и не меняется без причины.
/// </summary>
public interface ISimulationSystem
{
    /// <summary>Имя для профилировщика и отчётов замеров.</summary>
    string Name { get; }

    /// <summary>Раз во сколько тиков запускать. 1 — каждый тик, 5 — каждый пятый.</summary>
    int Interval => 1;

    void Tick(WorldState world);
}
