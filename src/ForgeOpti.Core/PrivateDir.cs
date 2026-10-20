using System.Security.AccessControl;
using System.Security.Principal;

namespace ForgeOpti.Core;

/// <summary>Dossier réservé à SYSTEM et aux administrateurs (héritage coupé) : état, réglages, journal, staging des mises à jour.</summary>
public static class PrivateDir
{
    static readonly SecurityIdentifier System = new(WellKnownSidType.LocalSystemSid, null);
    static readonly SecurityIdentifier Admins = new(WellKnownSidType.BuiltinAdministratorsSid, null);

    /// <summary>Crée le dossier s'il manque, puis (ré)impose l'ACL admin/SYSTEM et reprend la propriété des fichiers qu'il contient déjà.</summary>
    public static string Ensure(string path)
    {
        var di = Directory.CreateDirectory(path);
        var sec = new DirectorySecurity();
        sec.SetAccessRuleProtection(true, false);
        foreach (var sid in new[] { System, Admins })
            sec.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        try { sec.SetOwner(Admins); } catch (InvalidOperationException) { /* non élevé : le propriétaire reste le créateur */ }
        di.SetAccessControl(sec);
        // Fichiers pré-créés par un utilisateur standard : on retire leurs ACL explicites pour qu'ils héritent de l'ACL du dossier.
        foreach (var f in di.EnumerateFiles("*", SearchOption.AllDirectories).Where(f => !f.Attributes.HasFlag(FileAttributes.ReparsePoint)))
        {
            var fs = new FileSecurity();
            try { fs.SetOwner(Admins); } catch (InvalidOperationException) { }
            fs.SetAccessRuleProtection(false, false);
            f.SetAccessControl(fs);
        }
        return path;
    }
}
