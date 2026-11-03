using System.Diagnostics;

namespace Zafiro.Avalonia.Mcp.Tool.Preview;

public interface IProcessRunner
{
    Task<ProcessRunResult> Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken);
}

public sealed record ProcessRunResult(int ExitCode, string StandardOutput, string StandardError)
{
    public static ProcessRunResult Success(string standardOutput) => new(0, standardOutput, string.Empty);
}

public sealed class DotnetProcessRunner : IProcessRunner
{
    public async Task<ProcessRunResult> Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            },
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        // Build servers started by this command would inherit the redirected pipes and keep them
        // open after it exits, so reading the output to the end would never finish.
        process.StartInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        process.StartInfo.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        process.StartInfo.Environment["UseSharedCompilation"] = "false";

        process.Start();

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch
        {
            TryKill(process);
            throw;
        }

        return new ProcessRunResult(
            process.ExitCode,
            await standardOutput,
            await standardError);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }
}
