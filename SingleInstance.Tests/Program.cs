using System.Reflection;

var type = Assembly.GetExecutingAssembly().GetType("HYBurningStudio.SingleInstance");
if (type is null) throw new Exception("Single instance coordinator is missing");
var name = "HYBurningStudio.Test." + Guid.NewGuid().ToString("N");
var activated = new ManualResetEventSlim();
using var first = (IDisposable)Activator.CreateInstance(type, name, (Action)(() => activated.Set()))!;
if (!(bool)type.GetProperty("IsPrimary")!.GetValue(first)!) throw new Exception("First instance must be primary");
using var second = (IDisposable)Activator.CreateInstance(type, name, (Action)(() => { }))!;
if ((bool)type.GetProperty("IsPrimary")!.GetValue(second)!) throw new Exception("Second instance must not be primary");
type.GetMethod("ActivatePrimary")!.Invoke(second, null);
if (!activated.Wait(TimeSpan.FromSeconds(3))) throw new Exception("Second instance did not activate the first");
Console.WriteLine("Single-instance activation passed.");
