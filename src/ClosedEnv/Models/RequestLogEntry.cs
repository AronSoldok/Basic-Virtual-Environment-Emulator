using System.Text.Json.Serialization;

namespace ClosedEnv.Models;

public sealed class RequestLogEntry
{
    public DateTime Time { get; set; } = DateTime.Now;
    public string Method { get; set; } = "";
    public string Host { get; set; } = "";
    public string Url { get; set; } = "";
    public bool Allowed { get; set; }
    public string Headers { get; set; } = "";
    public string BodyPreview { get; set; } = "";

    [JsonIgnore]
    public string TimeText => Time.ToString("HH:mm:ss.fff");

    [JsonIgnore]
    public string StatusText => Allowed ? "пропущен" : "отрезан";

    [JsonIgnore]
    public string ShortUrl
    {
        get
        {
            if (string.IsNullOrEmpty(Url) || Url.Length <= 96)
            {
                return Url;
            }

            return Url[..96] + "…";
        }
    }
}
