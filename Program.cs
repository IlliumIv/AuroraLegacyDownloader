using System;
using System.Xml;

namespace AuroraLegacyDownloader;

internal class Program
{
    static readonly HttpClient? _client;

    static void Main(string[] args) => Download(args[0]);

    static void Download(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;

        Console.Write($"Download {url} ... ");

        var uri = new Uri(url);
        var text = GetFile(_client, uri);

        ParseFile(text, out var content);
        SaveFile(text, content.FileName, content.Path);

        Console.WriteLine("Done.");

        foreach (var u in content.urls)
        {
            Download(u);
        }
    }

    static void SaveFile(string text, string? fileName, string? path)
    {
        if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(path)) return;

        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, fileName), text);
    }

    static string GetFile(HttpClient? client, Uri uri)
    {
        client ??= new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, policyErrors) => true,
            SslProtocols = System.Security.Authentication.SslProtocols.Tls12,
        });

        var message = new HttpRequestMessage(HttpMethod.Get, uri);

        var response = client.Send(message);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("{0}\n\n\t{1}", response.StatusCode, response.Content.ReadAsStringAsync().Result);
        }

        return response.Content.ReadAsStringAsync().Result;
    }

    static bool ParseFile(string text, out (string? FileName, string? Path, HashSet<string> urls) content)
    {
        var xml = new XmlDocument();
        xml.LoadXml(text);
        var root = xml.DocumentElement;

        content = (null, null, []);

        if (root != null)
        {
            var path = root.ChildNodes.Cast<XmlNode>().First(n => n.Name == "info")
                .ChildNodes.Cast<XmlNode>().First(n => n.Name == "update")
                .ChildNodes.Cast<XmlNode>().First(n => n.Name == "file")
                .Attributes?.Cast<XmlAttribute>().First(a => a.Name == "url").Value;

            if (path != null)
            {
                path = new Uri(path).AbsolutePath[1..];

                var lastSlashIndex = path.LastIndexOf('/');

                content.Path = path[..lastSlashIndex];
                content.FileName = path[lastSlashIndex..][1..];
            }

            content.urls = root.ChildNodes.Cast<XmlNode>().FirstOrDefault(n => n.Name == "files")?
                .ChildNodes.Cast<XmlNode>()
                .Select(n => n.Attributes?.Cast<XmlAttribute>().First(a => a.Name == "url").Value ?? string.Empty).ToHashSet() ?? [];

            return true;
        }

        return false;
    }
}