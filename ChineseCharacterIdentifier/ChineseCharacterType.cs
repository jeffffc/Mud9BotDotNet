namespace ChineseCharacterIdentifier;

public enum ChineseCharacterType
{
    /// <summary>There are no recognized Chinese characters in the string.</summary>
    None,
    /// <summary>The string consists of traditional characters.</summary>
    Traditional,
    /// <summary>The string consists of simplified characters.</summary>
    Simplified,
    /// <summary>The string could be simplified or traditional -- the test is inconclusive.</summary>
    Either,
    /// <summary>The string consists of characters recognized solely as traditional characters and also consists of characters recognized solely as simplified characters.</summary>
    Both
}