using System.Text;
using System.Text.RegularExpressions;

public static class BoogiePrettyPrinter
{
    private static readonly Regex LabelLine =
        new(@"^\s*([A-Za-z_.$][A-Za-z0-9_.$]*)\s*:\s*$", RegexOptions.Compiled);

    public static string IndentBoogie(string code)
    {
        var lines = code.Replace("\r\n","\n").Replace("\r","\n").Split('\n');
        var sb = new StringBuilder(lines.Length * 64);
        int indent = 0;

        foreach (var raw in lines)
        {
            var line = raw.Trim();

            if (line.Length == 0) { sb.AppendLine(); continue; }

            // de-indent BEFORE printing if line starts with a closing brace
            if (line.StartsWith("}")) indent = Math.Max(0, indent - 1);

            // labels stay flush-left
            if (LabelLine.IsMatch(line))
            {
                sb.AppendLine(line);
                continue;
            }

            // write line with indent (4 spaces per level)
            sb.Append(' ', indent * 4).AppendLine(line);

            // increase indent AFTER printing if line opens a block
            if (line.EndsWith("{")) indent++;
        }

        return CollapseBlankLines(sb.ToString());
    }

    private static string CollapseBlankLines(string input)
    {
        var sb = new StringBuilder(input.Length);
        using var reader = new StringReader(input);
        string? l; bool prevBlank = false;
        while ((l = reader.ReadLine()) != null)
        {
            bool blank = l.Trim().Length == 0;
            if (blank && prevBlank) continue;
            sb.AppendLine(l);
            prevBlank = blank;
        }
        return sb.ToString();
    }
}
