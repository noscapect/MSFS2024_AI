namespace Msfs2024Ai.Copilot.Controls;

internal sealed class AircraftControlDefinition
{
    public AircraftControlDefinition(
        string category,
        string preset,
        string calculatorCode,
        string source)
    {
        Category = category;
        Preset = preset;
        CalculatorCode = calculatorCode;
        Source = source;
    }

    public string Category { get; }
    public string Preset { get; }
    public string CalculatorCode { get; }
    public string Source { get; }
}
