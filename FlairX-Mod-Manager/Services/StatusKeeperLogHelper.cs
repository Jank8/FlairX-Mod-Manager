namespace FlairX_Mod_Manager.Services
{
    public static class StatusKeeperLogHelper
    {
        public static string GetLogPath() => PathManager.GetSettingsPath("StatusKeeper.log");

        public static void InitFileLogging(string logPath) => Logger.InitStatusKeeperLog();
    }
}
