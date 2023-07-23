


using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;

class Program
{
    private static readonly WebClient _client = new();
    private static IWebDriver _driver;

    private static readonly ProcessStartInfo _powerShellStartInfo = new ProcessStartInfo
    {
        FileName = "powershell.exe",
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        WindowStyle = ProcessWindowStyle.Hidden
    };

    private const string LICHESS_WEB = @"https://lichess.org/analysis";
    private const string DOWNLOAD_FOLDER = @"D:\Programmmieren\extract sprite links from lichess\sprites\";

    private static readonly List<string> _svgResults = new();


    static void Main(string[] args)
    {
        RunAsync();
    }

    private static void RunAsync()
    {
        try
        {
            Connect();

            Console.WriteLine("Select the target sprites in the lichess website.");
            Console.ReadLine();

            var pieces = FindPieceUrls();
            var downloads = DownloadAll(pieces);
            AwaitDownloads(downloads);

            GenerateAllPngs();
        }
        finally
        {
            _client.Dispose();
            _driver.Quit();
        }
        ///Console.ReadLine();
    }

    private static void GenerateAllPngs()
    {
        foreach (var piece in _svgResults)
        {
            string svgPiece = piece;
            string pngPiece = Path.Combine(Path.GetDirectoryName(svgPiece), Path.GetFileNameWithoutExtension(svgPiece) + ".png");

            string script = @"
                cd 'C:\Program Files\Inkscape\bin'
                .\inkscape.com -w 1024 -h 1024 " + "\"" + svgPiece + "\" -o \"" + pngPiece + "\"";

            RunPowerShellScriptAsync(script);
        }
    }

    private static async void RunPowerShellScriptAsync(string script)
    {
        using Process ps = new Process { StartInfo = _powerShellStartInfo };
        try
        {
            ps.Start();

            await ps.StandardInput.WriteLineAsync(script);
            ps.StandardInput.Close();

            string output = await ps.StandardOutput.ReadToEndAsync();
            string errors = await ps.StandardError.ReadToEndAsync();

            await ps.WaitForExitAsync();

            int exitCode = ps.ExitCode;

            Console.WriteLine("Output:");
            Console.WriteLine(output);
            Console.WriteLine("Errors:");
            Console.WriteLine(errors);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
        finally
        {
            ps.Close();
            ps.Dispose();
        }
    }

    static string? ExtractBase64Data(string input)
    {
        string pattern = @"data:image/svg\+xml;base64,([A-Za-z0-9+/=]+)";

        Match match = Regex.Match(input, pattern);

        if (match.Success && match.Groups.Count > 1)
        {
            string base64Data = match.Groups[1].Value;
            return base64Data;
        }

        return null; 
    }

    static async void DownloadSvg(string base64SvgData, string downloadPath)
    {
        try
        {
            byte[] svgData = Convert.FromBase64String(base64SvgData);
            await File.WriteAllBytesAsync(downloadPath, svgData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while downloading and saving the SVG file: {ex.Message} \n");
        }
    }

    static async void AwaitDownloads(List<Task> tasks)
    {
        await Task.WhenAll(tasks);
    }

    static List<Task> DownloadAll(Task<HashSet<(string url, string name)>> pieces)
    {
        var tasks = new List<Task>();
        foreach (var piece in pieces.Result)
        {
            Task task = GetDownload(piece);
            task.Start();
            tasks.Add(task);
        }
        return tasks;
    }

    static Task GetDownload((string url, string name) piece)
    {
        string outputFile = $"{DOWNLOAD_FOLDER}{piece.name}.svg";
        _svgResults.Add(outputFile);
        return new Task(() => DownloadSvg(piece.url, outputFile));
    }

    static void Connect()
    {
        var chrome_options = new ChromeOptions();
        _driver = new ChromeDriver(chrome_options);

        _driver.Navigate().GoToUrl(LICHESS_WEB);
    }

    static async Task<HashSet<(string Url, string PieceName)>> FindPieceUrls()
    {
        var pieces = _driver.FindElements(By.TagName("piece"));
        var results = new HashSet<(string Url, string PieceName)>();
        var urls = new HashSet<string>();
        var names = new HashSet<string>();

        foreach (var piece in pieces)
        {
            string name = piece.GetAttribute("class");
            if (!names.Contains(name))
            {
                string rawUrl = piece.GetCssValue("background-image");
                string? url = ExtractBase64Data(rawUrl);

                if (url != null)
                {
                    names.Add(name);
                    urls.Add(url);
                    results.Add((url, name));
                }
            }
        }
       
        return results;
    }


}