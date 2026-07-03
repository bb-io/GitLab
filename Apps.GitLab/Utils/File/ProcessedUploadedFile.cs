using Blackbird.Filters.Transformations;

namespace Apps.GitLab.Utils.File;

public record ProcessedUploadedFile(byte[] FileBytes, Transformation? Transformation);