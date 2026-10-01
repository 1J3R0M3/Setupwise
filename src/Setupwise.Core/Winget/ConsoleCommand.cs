using System.Text;

namespace Setupwise.Core.Winget;

/// <summary>
/// Turns a line typed into the console into winget arguments. Each argument is passed to winget as is
/// (no shell), so only winget can be started and characters like "&amp;" or "|" have no special meaning.
/// </summary>
public static class ConsoleCommand
{
    /// <summary>
    /// Splits like a command line: spaces separate arguments, double quotes group them ("Visual Studio").
    /// A leading "winget" or "winget.exe" is dropped, so both "winget show x" and "show x" work.
    /// </summary>
    public static IReadOnlyList<string> Parse(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var args = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var hasArgument = false;

        foreach (var ch in line)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                hasArgument = true; // "" is an empty argument
            }
            else if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (hasArgument) args.Add(current.ToString());
                current.Clear();
                hasArgument = false;
            }
            else
            {
                current.Append(ch);
                hasArgument = true;
            }
        }
        if (hasArgument) args.Add(current.ToString());

        if (args.Count > 0 && (args[0].Equals("winget", StringComparison.OrdinalIgnoreCase)
                               || args[0].Equals("winget.exe", StringComparison.OrdinalIgnoreCase)))
            args.RemoveAt(0);
        return args;
    }
}
