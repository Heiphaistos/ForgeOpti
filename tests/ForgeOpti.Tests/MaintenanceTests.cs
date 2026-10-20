using ForgeOpti.Core;
using Xunit;

public class MaintenanceTests
{
    [Theory]
    [InlineData(@"C:\Program Files\ForgeOpti\ForgeOpti.exe", true)]
    [InlineData(@"C:\Program Files (x86)\ForgeOpti\ForgeOpti.exe", true)]
    [InlineData(@"C:\Program Files\..\Users\Momo\x.exe", false)]
    [InlineData(@"C:\Program FilesEvil\ForgeOpti.exe", false)]
    [InlineData(@"C:\Users\Momo\AppData\Local\ForgeOpti-Portable\0.5.0\ForgeOpti.exe", false)]
    public void Tache_seulement_pour_un_exe_sous_Program_Files(string exe, bool ok) => Assert.Equal(ok, Maintenance.IsProtectedPath(exe));
}
