using System.Text.Json;
using NLog;
using WebSocketTunnel.DTO;

namespace WebSocketTunnel;

public static class ConfigService
{
    private static Logger _logger = LogManager.GetCurrentClassLogger();

    public static Config ReadConfig()
    {
        if (File.Exists(Consts.ConfigFileName))
        {
            try
            {
                var configString = File.ReadAllText(Consts.ConfigFileName);
                return JsonSerializer.Deserialize<Config>(configString);
            }
            catch (Exception e)
            {
                _logger.Error(e);
            }
        }
        string error = "Config file missing";
        _logger.Warn(error);
        throw new AggregateException(error);
    }

    public static void SaveConfig(Config config)
    {
        var configString = JsonSerializer.Serialize(config);
        File.WriteAllText(Consts.ConfigFileName, configString);
    }
}