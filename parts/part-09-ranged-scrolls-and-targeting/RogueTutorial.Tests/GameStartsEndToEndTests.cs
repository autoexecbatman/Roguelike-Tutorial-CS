/*
 * End-to-end test: launches the real executable and checks it survives startup.
 *
 * This is the only level that exercises assembly loading, the MonoGame host and the
 * native SDL libraries - the exact things that were missing when the game built cleanly
 * and then threw FileNotFoundException at run time. Unit and integration tests cannot
 * see any of that.
 *
 * It opens a real window for a few seconds. Skip it with:
 *
 *     dotnet test --filter "Category!=EndToEnd"
 */

using System;
using System.Diagnostics;
using System.IO;
using Xunit;

[Trait("Category", "EndToEnd")]
public sealed class GameStartsEndToEndTests
{
    // How long the game must stay alive to count as having started successfully.
    private static readonly TimeSpan SurvivalWindow = TimeSpan.FromSeconds(6);

    /// <summary>
    /// Walks up from the test assembly to the repository root and returns the game
    /// executable built in the same configuration. Throws when it is not there, because
    /// a missing executable is a broken test setup rather than a failing game.
    /// </summary>
    private static string LocateGameExecutable()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);

        // Climb until the folder holding both projects is found.
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "RogueTutorial")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not find the RogueTutorial project folder above the test output.");
        }

        // Mirror the test assembly's configuration folder so Debug tests run the Debug game.
        string configuration = AppContext.BaseDirectory.Contains("Release", StringComparison.OrdinalIgnoreCase)
            ? "Release"
            : "Debug";

        string executablePath = Path.Combine(
            directory.FullName, "RogueTutorial", "bin", configuration, "net9.0", "RogueTutorial.exe");

        if (!File.Exists(executablePath))
        {
            throw new InvalidOperationException($"The game executable is missing: {executablePath}");
        }

        return executablePath;
    }

    [Fact]
    public void TheGameStaysAliveAfterStartup()
    {
        string executablePath = LocateGameExecutable();

        ProcessStartInfo startInfo = new ProcessStartInfo(executablePath)
        {
            // Run from the output folder so the native runtimes resolve as they do normally.
            WorkingDirectory = Path.GetDirectoryName(executablePath)!,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        using Process game = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The game process failed to start.");

        try
        {
            // WaitForExit returning true means it died inside the window, which is the failure.
            bool exitedEarly = game.WaitForExit(SurvivalWindow);

            if (exitedEarly)
            {
                string standardError = game.StandardError.ReadToEnd();
                Assert.Fail($"The game exited after {game.ExitCode} within {SurvivalWindow.TotalSeconds}s. Stderr:\n{standardError}");
            }
        }
        finally
        {
            // Always close the window, including when the assertion above failed.
            if (!game.HasExited)
            {
                game.Kill(entireProcessTree: true);
                game.WaitForExit(TimeSpan.FromSeconds(10));
            }
        }
    }
}
