using System.Text.RegularExpressions;

namespace Gatosyocora.MeshDeleterWithTexture.Utilities
{
    public static class StringUtility
    {
        /// <summary>
        /// 最後にキーワードを追加する（重複なし）
        /// </summary>
        /// <param name="target"></param>
        /// <param name="keyword"></param>
        /// <returns></returns>
        public static string AddKeywordToEnd(string target, string keyword)
        {
            var normalString = Regex.Replace(target, keyword + ".*", string.Empty);
            return normalString + keyword;
        }
    }
}
