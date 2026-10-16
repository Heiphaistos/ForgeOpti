using ForgeOpti.Core;

// Usage : ForgeOpti.VmTest.exe <rapport.json>   (à lancer en admin/SYSTEM dans une VM jetable UNIQUEMENT)
var report = args.Length > 0 ? args[0] : @"C:\ForgeOptiTest\report.json";
Directory.CreateDirectory(Path.GetDirectoryName(report)!);
var all = Catalog.LoadAll();
SelfTest.Kvp("FO_status", $"start total={all.Count}");
var n = 0;
var res = SelfTest.Run(all, report, line =>
{
    n++;
    File.AppendAllText(report + ".log", line + Environment.NewLine);
    if (n % 10 == 0) SelfTest.Kvp("FO_status", $"{n}/{all.Count}");
});
var bad = res.Where(r => r.Status != "OK").ToList();
SelfTest.Kvp("FO_status", $"DONE ok={res.Count - bad.Count} bad={bad.Count}");
File.WriteAllText(report + ".done", "done");
