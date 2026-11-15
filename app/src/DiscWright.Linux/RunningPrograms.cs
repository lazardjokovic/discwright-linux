using System.Diagnostics;

namespace DiscWright.Linux;

/// <summary>
/// The real way of running a program: a process, with both of its outputs read.
/// </summary>
public sealed class RunningPrograms : IRunPrograms
{
    /// <remarks>
    /// Looked for along PATH, the way a shell would, rather than by starting it
    /// to see what happens. Starting a program that is not there throws, and
    /// the difference between "not installed" and "installed and failed" is
    /// the difference between two sentences somebody has to act on.
    /// </remarks>
    public bool Has(string program)
    {
        string[] folders = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        string[] spellings = OperatingSystem.IsWindows() ? [program + ".exe", program] : [program];

        return folders.Any(folder => spellings.Any(name => File.Exists(Path.Combine(folder, name))));
    }

    public async Task<int> Run(
        string program, IReadOnlyList<string> arguments, Action<string> said, CancellationToken ct)
    {
        ProcessStartInfo how = new(program)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // One argument each, never one long string. A folder with a space or a
        // quote in its name is then a folder, not something to be escaped
        // correctly on every system this runs on.
        foreach (string argument in arguments)
        {
            how.ArgumentList.Add(argument);
        }

        using Process running = new() { StartInfo = how };

        // Both outputs are read, and read as they arrive. A program whose
        // output nobody reads fills its pipe and stops for ever, which the
        // disc's own menu learned from robocopy.
        //
        // One line at a time under a lock, because the two outputs arrive on
        // two threads and whoever is listening keeps a list.
        object oneAtATime = new();

        void Heard(object sender, DataReceivedEventArgs line)
        {
            if (line.Data is not null)
            {
                lock (oneAtATime)
                {
                    said(line.Data);
                }
            }
        }

        running.OutputDataReceived += Heard;
        running.ErrorDataReceived += Heard;

        running.Start();
        running.BeginOutputReadLine();
        running.BeginErrorReadLine();

        try
        {
            await running.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                running.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // It ended by itself between the cancelling and the stopping.
            }

            throw;
        }

        // The number can arrive before the last lines do. This waits for the
        // outputs to be read to their ends, so nothing said is lost.
        running.WaitForExit();

        return running.ExitCode;
    }
}
