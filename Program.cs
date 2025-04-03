// See https://aka.ms/new-console-template for more information
using AdvancedDataLineageAnalyzer;

Console.WriteLine("Hello, World!");

var engine = new SqlAnalysisEngine();
var reportGenerator = new ReportGenerator();

foreach (var file in Directory.GetFiles(@"D:\Project\Austin\TestSP\", "*.sql"))
{
    try
    {
        Console.WriteLine($"\nAnalyzing {Path.GetFileName(file)}...");
        var result = engine.AnalyzeSqlFile(file);
        var tableAnalysisResult = result.Item1; // Accessing the first item of the tuple
        var columnAnalysisResult = result.Item2; // Accessing the second item of the tuple
        reportGenerator.GenerateReport(tableAnalysisResult, columnAnalysisResult);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error analyzing {file}: {ex.Message}");
    }
}
