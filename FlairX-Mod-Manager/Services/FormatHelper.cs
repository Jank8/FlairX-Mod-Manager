namespace FlairX_Mod_Manager.Services
{
    public static class FormatHelper
    {
        public static string FormatCount(int count)
        {
            if (count >= 1000000)
            {
                return $"{count / 1000000.0:F1}M";
            }

            if (count >= 1000)
            {
                return $"{count / 1000.0:F1}K";
            }

            return count.ToString();
        }
    }
}
