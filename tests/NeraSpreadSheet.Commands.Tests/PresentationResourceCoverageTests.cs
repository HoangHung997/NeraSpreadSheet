using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed partial class PresentationResourceCoverageTests
{
    [TestMethod]
    public void NativePresentationResourceCallsShouldExistInTheVietnameseFallbackCatalog()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "NeraSpreadSheet.Commands")))
            directory = directory.Parent;
        Assert.IsNotNull(directory, "Run this source coverage audit from a repository checkout.");
        var checkedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var platform in new[] { "Wpf", "WinForms", "Maui" })
        {
            foreach (var path in Directory.EnumerateFiles(Path.Combine(directory.FullName, "src", $"NeraSpreadSheet.{platform}"), "*.cs"))
            {
                var name = Path.GetFileName(path);
                if (!name.Contains("Ribbon", StringComparison.Ordinal) && !name.Contains("Filter", StringComparison.Ordinal) &&
                    !name.Contains("BarPresenter", StringComparison.Ordinal) && !name.Contains("TableHost", StringComparison.Ordinal)) continue;
                foreach (Match match in ResourceCall().Matches(File.ReadAllText(path)))
                {
                    var key = Regex.Unescape(match.Groups[1].Value);
                    Assert.IsTrue(PresentationLocalization.ContainsKey(key), $"Missing resource {key} in {name}.");
                    checkedKeys.Add(key);
                }
            }
        }
        Assert.IsGreaterThan(140, checkedKeys.Count, "The audit must cover the native presentation surface.");
    }

    [GeneratedRegex("Localization\\.(?:Get|Format)\\(\"((?:\\\\.|[^\"\\\\])*)\"")]
    private static partial Regex ResourceCall();
}
