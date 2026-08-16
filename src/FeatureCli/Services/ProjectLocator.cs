using System.Xml.Linq;

namespace FeatureCli.Services;

public record ProjectInfo(string ProjectDirectory, string FeaturesDirectory, string RootNamespace, string CsprojPath);

public static class ProjectLocator
{
    public static ProjectInfo Locate(string? explicitPath = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return LocateExplicit(explicitPath);
        }

        var startDir = Directory.GetCurrentDirectory();
        if (!Directory.Exists(startDir))
        {
            throw new DirectoryNotFoundException($"El directorio actual no existe: {startDir}");
        }

        // 1. Direct check in startDir
        var directCsprojs = GetValidCsprojs(startDir);
        if (directCsprojs.Length > 0)
        {
            var featuresDir = Path.Combine(startDir, "Features");
            return CreateProjectInfo(startDir, featuresDir, directCsprojs[0]);
        }

        // 2. Search downward inside startDir (subdirectories up to depth 3)
        var childCandidates = FindCandidateProjects(startDir, maxDepth: 3);
        if (childCandidates.Count > 0)
        {
            var best = SelectBestCandidate(childCandidates);
            if (best != null)
            {
                return CreateProjectInfo(best.ProjectDir, best.FeaturesDir, best.CsprojPath);
            }
        }

        // 3. Search upward from startDir (subfolders moving up to solution/git boundary)
        var current = new DirectoryInfo(startDir).Parent;
        while (current != null)
        {
            // 3a. If current directory itself contains a valid csproj
            var parentCsprojs = GetValidCsprojs(current.FullName);
            if (parentCsprojs.Length > 0)
            {
                var featuresDir = Path.Combine(current.FullName, "Features");
                return CreateProjectInfo(current.FullName, featuresDir, parentCsprojs[0]);
            }

            // 3b. Check if current directory is a Solution or Git root boundary
            var isSolutionOrGitRoot = current.GetFiles("*.sln").Length > 0 ||
                                     current.GetFiles("*.slnx").Length > 0 ||
                                     Directory.Exists(Path.Combine(current.FullName, ".git"));

            if (isSolutionOrGitRoot)
            {
                // Search within this solution only
                var solutionCandidates = FindCandidateProjects(current.FullName, maxDepth: 3);
                if (solutionCandidates.Count > 0)
                {
                    var best = SelectBestCandidate(solutionCandidates);
                    if (best != null)
                    {
                        return CreateProjectInfo(best.ProjectDir, best.FeaturesDir, best.CsprojPath);
                    }
                }

                // Strictly stop here. Do NOT climb beyond the solution/repo root boundary!
                break;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException(
            "No se encontró un proyecto .NET válido en el directorio actual ni en la solución. " +
            "Asegúrate de ejecutar el comando dentro de un proyecto VSA o especifica la ruta con '--project-path'.");
    }

    private static ProjectInfo LocateExplicit(string explicitPath)
    {
        var fullPath = Path.GetFullPath(explicitPath);
        if (File.Exists(fullPath))
        {
            if (fullPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                var projectDir = Path.GetDirectoryName(fullPath)!;
                return CreateProjectInfo(projectDir, Path.Combine(projectDir, "Features"), fullPath);
            }
            fullPath = Path.GetDirectoryName(fullPath)!;
        }

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"El directorio especificado no existe: {explicitPath}");
        }

        var directCsprojs = GetValidCsprojs(fullPath);
        if (directCsprojs.Length > 0)
        {
            return CreateProjectInfo(fullPath, Path.Combine(fullPath, "Features"), directCsprojs[0]);
        }

        var candidates = FindCandidateProjects(fullPath, maxDepth: 3);
        if (candidates.Count > 0)
        {
            var best = SelectBestCandidate(candidates);
            if (best != null)
            {
                return CreateProjectInfo(best.ProjectDir, best.FeaturesDir, best.CsprojPath);
            }
        }

        throw new InvalidOperationException(
            $"No se encontró ningún proyecto .csproj válido en '{explicitPath}' ni en sus subdirectorios.");
    }

    private record CandidateProject(string ProjectDir, string FeaturesDir, string CsprojPath, bool HasFeaturesDir);

    private static List<CandidateProject> FindCandidateProjects(string rootDir, int maxDepth)
    {
        var list = new List<CandidateProject>();
        SearchCandidatesRecursive(rootDir, maxDepth, list);
        return list;
    }

    private static void SearchCandidatesRecursive(string dirPath, int maxDepth, List<CandidateProject> results)
    {
        if (maxDepth < 0 || !Directory.Exists(dirPath)) return;

        var dirName = Path.GetFileName(dirPath);
        if (dirName.StartsWith('.') ||
            dirName.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            dirName.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
            dirName.Equals("node_modules", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var csprojs = GetValidCsprojs(dirPath);
        if (csprojs.Length > 0)
        {
            var featuresDir = Path.Combine(dirPath, "Features");
            results.Add(new CandidateProject(dirPath, featuresDir, csprojs[0], Directory.Exists(featuresDir)));
        }

        try
        {
            foreach (var sub in Directory.GetDirectories(dirPath))
            {
                SearchCandidatesRecursive(sub, maxDepth - 1, results);
            }
        }
        catch
        {
            // Ignore access errors on forbidden folders
        }
    }

    private static CandidateProject? SelectBestCandidate(List<CandidateProject> candidates)
    {
        if (candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0];

        // Priority 1: Has 'Features' folder
        var withFeatures = candidates.Where(c => c.HasFeaturesDir).ToList();
        if (withFeatures.Count == 1) return withFeatures[0];
        if (withFeatures.Count > 1)
        {
            var preferredWithFeatures = withFeatures.FirstOrDefault(c => IsPreferredProjectName(c.CsprojPath));
            return preferredWithFeatures ?? withFeatures[0];
        }

        // Priority 2: In 'src' folder
        var inSrc = candidates.Where(c =>
            c.ProjectDir.Contains(Path.DirectorySeparatorChar + "src" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            c.ProjectDir.EndsWith(Path.DirectorySeparatorChar + "src", StringComparison.OrdinalIgnoreCase) ||
            c.ProjectDir.Contains(Path.AltDirectorySeparatorChar + "src" + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            c.ProjectDir.EndsWith(Path.AltDirectorySeparatorChar + "src", StringComparison.OrdinalIgnoreCase)).ToList();

        if (inSrc.Count == 1) return inSrc[0];
        if (inSrc.Count > 1)
        {
            var preferredInSrc = inSrc.FirstOrDefault(c => IsPreferredProjectName(c.CsprojPath));
            return preferredInSrc ?? inSrc[0];
        }

        // Priority 3: Preferred project name (Api, Web, App, etc.)
        var preferred = candidates.FirstOrDefault(c => IsPreferredProjectName(c.CsprojPath));
        return preferred ?? candidates[0];
    }

    private static bool IsPreferredProjectName(string csprojPath)
    {
        var name = Path.GetFileNameWithoutExtension(csprojPath);
        return name.EndsWith(".Api", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".Web", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".Server", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".App", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".Application", StringComparison.OrdinalIgnoreCase);
    }

    public static string[] GetValidCsprojs(string dirPath)
    {
        if (!Directory.Exists(dirPath)) return [];
        return Directory.GetFiles(dirPath, "*.csproj")
            .Where(f => !IsCliOrTestProject(Path.GetFileName(f)))
            .ToArray();
    }

    public static bool IsCliOrTestProject(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        return name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".Test", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".Cli", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("FeatureCli", StringComparison.OrdinalIgnoreCase);
    }

    private static ProjectInfo CreateProjectInfo(string projectDir, string featuresDir, string csprojPath)
    {
        var rootNamespace = Path.GetFileNameWithoutExtension(csprojPath);

        try
        {
            var doc = XDocument.Load(csprojPath);
            var nsElement = doc.Descendants("RootNamespace").FirstOrDefault();
            if (nsElement != null && !string.IsNullOrWhiteSpace(nsElement.Value))
            {
                rootNamespace = nsElement.Value.Trim();
            }
        }
        catch
        {
            // Fallback to csproj file name
        }

        return new ProjectInfo(projectDir, featuresDir, rootNamespace, csprojPath);
    }
}
