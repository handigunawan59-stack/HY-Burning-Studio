using System.Reflection;
var type = Assembly.GetExecutingAssembly().GetType("HYBurningStudio.Services.WriteSpeed") ?? throw new Exception("Write speed selection is missing");
var normalize = type.GetMethod("Normalize")!;
var result = (int[])normalize.Invoke(null, [new long[] { 1200, -1, 600, 1200, 0, 4294967295L }])!;
if (!result.SequenceEqual(new[] { 600, 1200 })) throw new Exception("Only distinct positive supported speeds must be offered in order");
var validate = type.GetMethod("Validate")!;
validate.Invoke(null, [null, result]);
validate.Invoke(null, [600, result]);
try { validate.Invoke(null, [800, result]); throw new Exception("Unsupported speed must be rejected"); }
catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException) { }
Console.WriteLine("Write speed normalization and validation passed.");
