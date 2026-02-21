namespace ChineseCharacterIdentifier;

public static class ChinCharIdentifier
{
        private static readonly HashSet<char> TradChars;
        private static readonly HashSet<char> SimpChars;
        private static readonly HashSet<char> SharedChars;
        private static readonly HashSet<char> AllChars;

        static ChinCharIdentifier()
        {
            // 1. Load the base Traditional characters
            TradChars = new HashSet<char>(ChineseCharacterData.Traditional);
            
            // 2. Merge your exceptions into the Traditional set
            TradChars.UnionWith(ChineseCharacterData.TraditionalExceptions);

            // 3. Load the Simplified characters
            SimpChars = new HashSet<char>(ChineseCharacterData.Simplified);

            // 4. Calculate Shared characters (Intersection)
            // Any exception character that is also in SimpChars will naturally fall into here.
            SharedChars = new HashSet<char>(TradChars);
            SharedChars.IntersectWith(SimpChars);

            // 5. Calculate All characters (Union)
            AllChars = new HashSet<char>(TradChars);
            AllChars.UnionWith(SimpChars);
        }

        public static ChineseCharacterType Identify(string text)
        {
            if (string.IsNullOrEmpty(text)) 
                return ChineseCharacterType.None;

            // Filter out non-Chinese characters
            var filteredText = new HashSet<char>(text);
            filteredText.IntersectWith(AllChars);

            if (filteredText.Count == 0)
                return ChineseCharacterType.None;

            // If the filtered text only contains characters shared by both sets
            if (filteredText.IsSubsetOf(SharedChars))
                return ChineseCharacterType.Either;

            // If all Chinese characters are found in the traditional set
            if (filteredText.IsSubsetOf(TradChars))
                return ChineseCharacterType.Traditional;

            // If all Chinese characters are found in the simplified set
            if (filteredText.IsSubsetOf(SimpChars))
                return ChineseCharacterType.Simplified;

            // Check if it's a mix of strictly simplified and strictly traditional
            var diff = new HashSet<char>(filteredText);
            diff.ExceptWith(TradChars); // Remove all traditional chars
            
            // If what is left over belongs purely to the simplified set, then it contains both
            if (diff.IsSubsetOf(SimpChars))
                return ChineseCharacterType.Both;

            return ChineseCharacterType.None;
        }
}