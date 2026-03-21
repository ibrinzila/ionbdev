namespace Water.Eval;

/// <summary>
/// A single evaluation test case.
/// </summary>
public class EvalCase
{
    public string Name { get; set; }
    public Dictionary<string, object?> Input { get; set; }
    public Dictionary<string, object?> Expected { get; set; }
    public Dictionary<string, object?> Metadata { get; set; } = new();

    public EvalCase(string name, Dictionary<string, object?> input, Dictionary<string, object?> expected)
    {
        Name = name;
        Input = input;
        Expected = expected;
    }
}

/// <summary>
/// Base interface for evaluators.
/// </summary>
public interface IEvaluator
{
    string Name { get; }
    EvalResult Evaluate(Dictionary<string, object?> actual, Dictionary<string, object?> expected);
}

/// <summary>
/// Result of a single evaluation.
/// </summary>
public class EvalResult
{
    public bool Passed { get; set; }
    public double Score { get; set; }
    public string Reason { get; set; } = "";
}

/// <summary>
/// Exact match evaluator.
/// </summary>
public class ExactMatch : IEvaluator
{
    public string Name => "exact_match";
    public string Field { get; }

    public ExactMatch(string field = "response")
    {
        Field = field;
    }

    public EvalResult Evaluate(Dictionary<string, object?> actual, Dictionary<string, object?> expected)
    {
        var actualVal = actual.TryGetValue(Field, out var a) ? a?.ToString() : null;
        var expectedVal = expected.TryGetValue(Field, out var e) ? e?.ToString() : null;
        var passed = actualVal == expectedVal;
        return new EvalResult { Passed = passed, Score = passed ? 1.0 : 0.0, Reason = passed ? "Match" : $"Expected '{expectedVal}' but got '{actualVal}'" };
    }
}

/// <summary>
/// Contains match evaluator.
/// </summary>
public class ContainsMatch : IEvaluator
{
    public string Name => "contains_match";
    public string Field { get; }

    public ContainsMatch(string field = "response")
    {
        Field = field;
    }

    public EvalResult Evaluate(Dictionary<string, object?> actual, Dictionary<string, object?> expected)
    {
        var actualVal = actual.TryGetValue(Field, out var a) ? a?.ToString() ?? "" : "";
        var expectedVal = expected.TryGetValue(Field, out var e) ? e?.ToString() ?? "" : "";
        var passed = actualVal.Contains(expectedVal, StringComparison.OrdinalIgnoreCase);
        return new EvalResult { Passed = passed, Score = passed ? 1.0 : 0.0, Reason = passed ? "Contains match" : $"'{actualVal}' does not contain '{expectedVal}'" };
    }
}

/// <summary>
/// Report from running an evaluation suite.
/// </summary>
public class EvalReport
{
    public int TotalCases { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public double AverageScore { get; set; }
    public List<Dictionary<string, object?>> Results { get; set; } = new();
}

/// <summary>
/// Evaluation suite for testing flows.
/// </summary>
public class EvalSuite
{
    public string Name { get; set; }
    public List<EvalCase> Cases { get; } = new();
    public List<IEvaluator> Evaluators { get; } = new();

    public EvalSuite(string name)
    {
        Name = name;
    }

    public EvalSuite AddCase(EvalCase evalCase)
    {
        Cases.Add(evalCase);
        return this;
    }

    public EvalSuite AddEvaluator(IEvaluator evaluator)
    {
        Evaluators.Add(evaluator);
        return this;
    }

    public async Task<EvalReport> RunAsync(Func<Dictionary<string, object?>, Task<Dictionary<string, object?>>> flowRunner)
    {
        var report = new EvalReport { TotalCases = Cases.Count };
        var scores = new List<double>();

        foreach (var evalCase in Cases)
        {
            var actual = await flowRunner(evalCase.Input);
            var caseResults = new Dictionary<string, object?> { ["case"] = evalCase.Name };
            var casePassed = true;

            foreach (var evaluator in Evaluators)
            {
                var result = evaluator.Evaluate(actual, evalCase.Expected);
                caseResults[$"{evaluator.Name}_passed"] = result.Passed;
                caseResults[$"{evaluator.Name}_score"] = result.Score;
                caseResults[$"{evaluator.Name}_reason"] = result.Reason;
                scores.Add(result.Score);
                if (!result.Passed) casePassed = false;
            }

            if (casePassed) report.Passed++;
            else report.Failed++;

            report.Results.Add(caseResults);
        }

        report.AverageScore = scores.Count > 0 ? scores.Average() : 0;
        return report;
    }
}
