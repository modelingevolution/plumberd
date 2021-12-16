namespace Checkers.Common.Controls
{
   
    public interface IDataContextControl { }
    public interface IDataContextControl<T> : IDataContextControl
    {
        public T DataContext { get; set; }
    }

    
    public static class StringExtensions
    {
        public static int LastNonWhiteSpaceIndex(this string text, int start = 0)
        {
            for (int i = text.Length - 1; i >= start; i--)
            {
                if (!char.IsWhiteSpace(text[i]))
                    return i;
            }

            return -1;
        }

        public static string ToNaturalNumber(this int tmp)
        {
            if (tmp > 0) return tmp.ToString();
            else return "-";
        }
        
        public static string TrimStartWhiteSpace(this string text)
        {
            for (int i = 0; i < text.Length; i++)
                if (!char.IsWhiteSpace(text[i]))
                    return text.Substring(i);

            return string.Empty;
        }
        public static int SkipWhiteSpace(this string text, int startIndex)
        {
            for (int i = startIndex; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i]))
                    startIndex += 1;
                else break;
            }

            return startIndex;
        }
    }
}