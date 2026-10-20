using System.Security.AccessControl;
using System.Security.Principal;
using ForgeOpti.Core;
using Xunit;

public class PrivateDirTests
{
    [Fact]
    public void Ensure_ne_laisse_que_SYSTEM_et_Administrateurs()
    {
        var d = Path.Combine(Path.GetTempPath(), "fo-priv-" + Guid.NewGuid().ToString("N"));
        PrivateDir.Ensure(d);
        try
        {
            var sec = new DirectoryInfo(d).GetAccessControl();
            Assert.True(sec.AreAccessRulesProtected);
            var sids = sec.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().Select(r => r.IdentityReference.Value).ToHashSet();
            Assert.Equal(new[] { "S-1-5-18", "S-1-5-32-544" }.ToHashSet(), sids);
        }
        finally
        {
            var s = new DirectorySecurity(); s.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(d).SetAccessControl(s); Directory.Delete(d, true);
        }
    }
}
