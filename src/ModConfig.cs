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

  // Primary language of the title/description above (Steam API language code, e.g. "english").
  // Defaults to "english" when omitted.
  public string? language;
  // Localized title/description for additional languages. Each entry is submitted as its own
  // metadata-only item update so Steam stores a per-language title/description.
  public List<ModLocalization>? localizations;
}

public class ModLocalization
{
  public string? language;     // Steam API language code, e.g. "koreana", "schinese"
  public string? title;
  public string? description;
}