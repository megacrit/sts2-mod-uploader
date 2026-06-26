namespace ModUploader;

public class ModConfig
{
  public string? title;
  public string? description;
  public string? visibility;
  public string? changeNote;
  public List<string>? tags;
  public List<ulong>? dependencies;
  public string[]? contentDescriptors;
  public string? minBranch;
  public string? maxBranch;
}