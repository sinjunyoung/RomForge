namespace RomForge.Core.Models;

public enum PatchSourceKind { File, Zip }
public enum PatchFileKind   { File, Folder, Zip }
public enum PairStatus      { Matched, OrphanSource, OrphanPatch }

public class PatchPair
{
    public string  SourcePath  { get; set; } = string.Empty;
    public string  PatchPath   { get; set; } = string.Empty;
    public string  BaseName    { get; set; } = string.Empty;
    public PairStatus Status   { get; set; }
}
