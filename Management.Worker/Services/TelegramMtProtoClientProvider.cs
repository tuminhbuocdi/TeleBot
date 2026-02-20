using Microsoft.Extensions.Options;
using WTelegram;

namespace Management.Worker.Services;

public sealed class TelegramMtProtoClientProvider
{
    private readonly TelegramMtProtoOptions _opts;
    private readonly ILogger<TelegramMtProtoClientProvider> _logger;

    public TelegramMtProtoClientProvider(IOptions<TelegramMtProtoOptions> opts, ILogger<TelegramMtProtoClientProvider> logger)
    {
        _opts = opts.Value;
        _logger = logger;
    }

    public Client CreateClient()
    {
        var apiHash = (_opts.ApiHash ?? string.Empty).Trim().Replace("\r", "")
    .Replace("\n", "");

        if (string.IsNullOrWhiteSpace(apiHash))
        {
            throw new InvalidOperationException("TelegramMtProto:ApiHash is required");
        }

        var sessionPath = _opts.SessionPath;
        if (string.IsNullOrWhiteSpace(sessionPath))
        {
            sessionPath = "data/wtelegram.session";
        }

        if (!Path.IsPathRooted(sessionPath))
        {
            sessionPath = Path.Combine(AppContext.BaseDirectory, sessionPath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath) ?? AppContext.BaseDirectory);
        _logger.LogInformation("WTelegram session file: {SessionPath}", sessionPath);

        string Config(string what)
        {
            return what switch
            {
                "api_id" => _opts.ApiId.ToString(),
                "api_hash" => apiHash,
                "phone_number" => _opts.PhoneNumber,
                "password" => _opts.TwoFactorPassword,
                "session_pathname" => sessionPath,

                // WTelegram will ask for this interactively on first login
                "verification_code" => ReadCodeFromConsole(),

                _ => null
            };
        }

        Client CreateWithSessionStream()
        {
            //var fs = new FileStream(sessionPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return new Client(Config);
        }

        try
        {
            return CreateWithSessionStream();
        }
        catch (WTException ex) when (ex.Message.Contains("reading session file", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (File.Exists(sessionPath))
                {
                    _logger.LogWarning(ex, "Failed to read WTelegram session file. Deleting session file and retrying: {SessionPath}", sessionPath);
                    File.Delete(sessionPath);
                }
            }
            catch (Exception deleteEx)
            {
                _logger.LogWarning(deleteEx, "Failed to delete session file: {SessionPath}", sessionPath);
            }

            return CreateWithSessionStream();
        }

        string ReadCodeFromConsole()
        {
            _logger.LogWarning("WTelegram requires login code (OTP). Please input verification_code in console.");
            Console.Write("verification_code: ");
            return Console.ReadLine() ?? string.Empty;
        }
    }
}
