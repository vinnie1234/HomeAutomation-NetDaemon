using System.ComponentModel.DataAnnotations;

namespace Automation.Configuration;

/// <summary>
/// The WhatsApp "lid" identifiers of Vincent and Carleen, so apps (and <see cref="Models.Persons.VincentModel"/> /
/// <see cref="Models.Persons.CarleenModel"/>) can recognize messages from them without hardcoding the IDs.
/// </summary>
public class WhatsAppConfig
{
    public string Vincent { get; set; } = "";
    public string Carleen { get; set; } = "";

    /// <summary>
    /// The URL the WhatsApp add-on can reach NetDaemon's own small web server on (e.g. "http://192.168.50.189:8080"),
    /// used to serve a generated voice note for a spoken reply: the add-on's own conversion of an arbitrary
    /// audio file (e.g. an MP3) into a WhatsApp-playable voice note turned out to not reliably work on mobile,
    /// so NetDaemon encodes a proper Ogg Opus file itself and serves it directly instead.
    /// </summary>
    [Url]
    public string NetDaemonBaseUrl { get; set; } = "";
}
