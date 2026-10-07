using System;

namespace MadeYellow.WAVES.Surfaces
{
    /// <summary>Case-insensitive name match. A keyword matches when the name contains it. A * stands for any text.</summary>
    public static class SurfaceNameMatch
    {
        /// <summary>True when <paramref name="name"/> matches any pattern in <paramref name="keywords"/>.</summary>
        public static bool Any(string name, string[] keywords)
        {
            if (string.IsNullOrEmpty(name) || keywords == null)
                return false;

            for (int i = 0; i < keywords.Length; i++)
            {
                if (IsMatch(name, keywords[i]))
                    return true;
            }

            return false;
        }

        /// <summary>True when <paramref name="name"/> matches <paramref name="keyword"/>. Comparison ignores case.</summary>
        public static bool IsMatch(string name, string keyword)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(keyword))
                return false;

            if (keyword.IndexOf('*') < 0)
                return name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;

            int textIndex = 0;
            int patternIndex = 0;
            int star = -1;
            int mark = 0;
            while (textIndex < name.Length)
            {
                if (patternIndex < keyword.Length && keyword[patternIndex] == '*')
                {
                    star = patternIndex++;
                    mark = textIndex;
                }
                else if (patternIndex < keyword.Length && Same(name[textIndex], keyword[patternIndex]))
                {
                    textIndex++;
                    patternIndex++;
                }
                else if (star >= 0)
                {
                    patternIndex = star + 1;
                    textIndex = ++mark;
                }
                else
                {
                    return false;
                }
            }

            while (patternIndex < keyword.Length && keyword[patternIndex] == '*')
                patternIndex++;

            return patternIndex == keyword.Length;
        }

        static bool Same(char left, char right)
        {
            return char.ToUpperInvariant(left) == char.ToUpperInvariant(right);
        }
    }
}
