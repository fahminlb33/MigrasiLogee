namespace MigrasiLogee.Models
{
    public record ServiceUptimeRecord(bool UseHttps, string HostName, string Path, string ProjectName, string IngressName);
}
