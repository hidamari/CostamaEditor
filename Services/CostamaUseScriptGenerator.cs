using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CostamaEditor.Services;

namespace CostamaEditor.Services;

public sealed record CostamaReward(int ItemId, int Weight, int Quantity);

public static class CostamaUseScriptGenerator
{
    public const int DefaultRandomRange = 100;
    public const int MaximumRandomRange = 16_777_216;

    private static readonly Regex SingleRewardPattern = new(
        @"^\s*\{\s*getitem\s+(?<itemId>\d+)\s*,\s*(?<quantity>\d+)\s*;\s*\}\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex RandomScriptPattern = new(
        @"^\s*\{\s*set\s*'@r\s*,\s*rand\s*\(\s*1\s*,\s*(?<range>\d+)\s*\)\s*;\s*(?<body>.*)\s*\}\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex RandomBranchPattern = new(
        @"\G\s*if\s*\(\s*'@r\s*<=\s*(?<threshold>\d+)\s*\)\s*\{\s*getitem\s+(?<itemId>\d+)\s*,\s*(?<quantity>\d+)\s*;\s*\}\s*else\s*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex FinalRewardPattern = new(
        @"\G\s*\{\s*getitem\s+(?<itemId>\d+)\s*,\s*(?<quantity>\d+)\s*;\s*\}\s*\z",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex BlockCommentPattern = new(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);

    public static string Generate(IReadOnlyList<CostamaReward> rewards, int randomRange)
    {
        if (randomRange is < 1 or > MaximumRandomRange)
            throw new ArgumentOutOfRangeException(nameof(randomRange));
        if (rewards.Count == 0)
            throw new ArgumentException(LocalizationService.Get("Generator.Empty"), nameof(rewards));
        if (rewards.Any(reward => reward.ItemId <= 0))
            throw new ArgumentException(LocalizationService.Get("Generator.InvalidId"), nameof(rewards));
        if (rewards.Any(reward => reward.Quantity <= 0))
            throw new ArgumentException(LocalizationService.Get("Generator.InvalidQuantity"), nameof(rewards));
        if (rewards.Any(reward => reward.Weight is < 1 || reward.Weight > randomRange))
            throw new ArgumentException(LocalizationService.Get("Generator.InvalidWeight"), nameof(rewards));
        if (rewards.Sum(reward => (long)reward.Weight) != randomRange)
            throw new ArgumentException(LocalizationService.Get("Generator.TotalWeight"), nameof(rewards));

        if (rewards.Count == 1)
        {
            var only = rewards[0];
            return $"{{ getitem {only.ItemId.ToString(CultureInfo.InvariantCulture)},{only.Quantity.ToString(CultureInfo.InvariantCulture)}; }}";
        }

        var script = new StringBuilder($"{{ set '@r, rand(1,{randomRange.ToString(CultureInfo.InvariantCulture)}); ");
        var cumulative = 0;
        for (var index = 0; index < rewards.Count - 1; index++)
        {
            var reward = rewards[index];
            cumulative += reward.Weight;
            script.Append($"if ('@r <= {cumulative.ToString(CultureInfo.InvariantCulture)}) {{ getitem {reward.ItemId.ToString(CultureInfo.InvariantCulture)},{reward.Quantity.ToString(CultureInfo.InvariantCulture)}; }} else ");
        }

        var finalReward = rewards[^1];
        script.Append($"{{ getitem {finalReward.ItemId.ToString(CultureInfo.InvariantCulture)},{finalReward.Quantity.ToString(CultureInfo.InvariantCulture)}; }} }}");
        return script.ToString();
    }

    public static bool TryParse(string script, out int randomRange, out IReadOnlyList<CostamaReward> rewards)
    {
        randomRange = DefaultRandomRange;
        rewards = Array.Empty<CostamaReward>();
        if (string.IsNullOrWhiteSpace(script) || string.Equals(script.Trim(), "{}", StringComparison.Ordinal))
            return true;

        var normalized = BlockCommentPattern.Replace(script, string.Empty);
        var singleReward = SingleRewardPattern.Match(normalized);
        if (singleReward.Success && TryReadReward(singleReward, DefaultRandomRange, out var onlyReward))
        {
            rewards = new[] { onlyReward };
            return true;
        }

        var randomScript = RandomScriptPattern.Match(normalized);
        if (!randomScript.Success)
            return false;

        if (!int.TryParse(randomScript.Groups["range"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out randomRange) ||
            randomRange is < 1 or > MaximumRandomRange)
            return false;

        var body = randomScript.Groups["body"].Value;
        var parsedRewards = new List<CostamaReward>();
        var position = 0;
        var previousThreshold = 0;
        while (true)
        {
            var branch = RandomBranchPattern.Match(body, position);
            if (!branch.Success)
                break;

            if (!int.TryParse(branch.Groups["threshold"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var threshold) ||
                threshold <= previousThreshold || threshold >= randomRange ||
                !TryReadReward(branch, threshold - previousThreshold, out var reward))
            {
                return false;
            }

            parsedRewards.Add(reward);
            previousThreshold = threshold;
            position += branch.Length;
        }

        var finalReward = FinalRewardPattern.Match(body, position);
        if (parsedRewards.Count == 0 || previousThreshold >= randomRange || !finalReward.Success ||
            !TryReadReward(finalReward, randomRange - previousThreshold, out var lastReward))
        {
            return false;
        }

        parsedRewards.Add(lastReward);
        rewards = parsedRewards;
        return true;
    }

    private static bool TryReadReward(Match match, int weight, out CostamaReward reward)
    {
        reward = default!;
        if (weight is < 1 or > MaximumRandomRange ||
            !int.TryParse(match.Groups["itemId"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var itemId) || itemId <= 0 ||
            !int.TryParse(match.Groups["quantity"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var quantity) || quantity <= 0)
        {
            return false;
        }

        reward = new CostamaReward(itemId, weight, quantity);
        return true;
    }
}
