namespace CostamaEditor.Data;

public static class ItemDbLineParser
{
    public const int FieldCount = 18;

    public static bool TryParse(string line, out string[] fields, out string[] scripts)
    {
        fields = new string[FieldCount];
        scripts = Array.Empty<string>();
        var fieldStart = 0;
        for (var i = 0; i < FieldCount; i++)
        {
            var separator = line.IndexOf(',', fieldStart);
            if (separator < 0)
                return false;

            fields[i] = line[fieldStart..separator];
            fieldStart = separator + 1;
        }

        scripts = SplitScripts(line[fieldStart..]);
        return true;
    }

    public static string[] SplitScripts(string tail)
    {
        if (string.IsNullOrWhiteSpace(tail))
            return Array.Empty<string>();

        var scripts = new List<string>();
        var start = 0;
        var curlyDepth = 0;
        var quote = '\0';
        var inBlockComment = false;
        var inLineComment = false;

        for (var i = 0; i < tail.Length; i++)
        {
            var current = tail[i];
            var next = i + 1 < tail.Length ? tail[i + 1] : '\0';

            if (inLineComment)
                break;
            if (inBlockComment)
            {
                if (current == '*' && next == '/')
                {
                    inBlockComment = false;
                    i++;
                }
                continue;
            }
            if (quote != '\0')
            {
                if (current == '\\')
                    i++;
                else if (current == quote)
                    quote = '\0';
                continue;
            }

            if (current == '/' && next == '*')
            {
                inBlockComment = true;
                i++;
            }
            else if (current == '/' && next == '/')
            {
                inLineComment = true;
            }
            // Auriga scripts use apostrophes as local-variable prefixes, e.g. '@r'.
            else if (current == '"')
            {
                quote = current;
            }
            else if (current == '{')
            {
                curlyDepth++;
            }
            else if (current == '}')
            {
                curlyDepth--;
            }
            else if (current == ',' && curlyDepth == 0)
            {
                scripts.Add(tail[start..i]);
                start = i + 1;
            }
        }

        scripts.Add(tail[start..]);
        return scripts.Select(script => script.Trim()).ToArray();
    }
}
