using System.Reflection;

var type = Assembly.GetExecutingAssembly().GetType("HYBurningStudio.Services.AudioDiscDuration");
if (type is null) throw new Exception("Audio disc duration calculation is missing");
var method = type.GetMethod("EstimateSeconds") ?? throw new Exception("Audio duration API is missing");
static double Calculate(MethodInfo method, params double[] seconds) =>
    (double)method.Invoke(null, [seconds.Select(TimeSpan.FromSeconds).ToArray()])!;
if (Calculate(method) != 0) throw new Exception("An empty project occupies no audio time");
if (Calculate(method, 60, 90) != 152) throw new Exception("Two tracks must include one two-second gap");
if (Calculate(method, 1, 4.1) != 11) throw new Exception("Tracks must account for minimum length and conservative rounding");
Console.WriteLine("Audio disc estimates include gaps, minimum track length, and rounding.");
