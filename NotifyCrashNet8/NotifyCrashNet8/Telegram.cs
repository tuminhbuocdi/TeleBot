using System.Net.Http;
using System.Threading.Tasks;

public static class Telegram
{
    static string botToken = "BOT_TOKEN";
    static string chatId = "CHAT_ID";

    public static async Task Send(string msg)
    {
        using HttpClient client = new HttpClient();
        string url =
            $"https://api.telegram.org/bot{botToken}/sendMessage?chat_id={chatId}&text={msg}";
        await client.GetAsync(url);
    }
}
