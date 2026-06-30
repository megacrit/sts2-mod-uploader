using System.Text.Json.Serialization;

namespace ModUploader;

[JsonSourceGenerationOptions(WriteIndented = true, IncludeFields = true)]
[JsonSerializable(typeof(ModConfig))]
[JsonSerializable(typeof(ModLocalization))]
internal partial class SourceGenerationContext : JsonSerializerContext { }