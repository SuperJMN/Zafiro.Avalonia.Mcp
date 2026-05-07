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
