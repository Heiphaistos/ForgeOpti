namespace ForgeOpti.Core;

public static class RestorePoint
{
    /// <summary>Crée un point de restauration système (admin requis). Lève si échec : l'appelant NE DOIT PAS optimiser sans.</summary>
    public static void Create(string label) => Engine.Ps($$"""
        $k='HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore'
        $o=(Get-ItemProperty $k -Name SystemRestorePointCreationFrequency -ErrorAction SilentlyContinue).SystemRestorePointCreationFrequency
        Set-ItemProperty $k SystemRestorePointCreationFrequency 0 -Type DWord
        try {
          Enable-ComputerRestore -Drive "$env:SystemDrive\" -ErrorAction SilentlyContinue
          Checkpoint-Computer -Description '{{label.Replace("'", "")}}' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop
        } finally {
          if ($null -eq $o) { Remove-ItemProperty $k SystemRestorePointCreationFrequency -ErrorAction SilentlyContinue }
          else { Set-ItemProperty $k SystemRestorePointCreationFrequency $o -Type DWord }
        }
        """);
}
