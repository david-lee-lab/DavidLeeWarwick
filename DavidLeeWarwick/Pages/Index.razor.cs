using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Timers;
using System.Web;
using System.Xml.Linq;

namespace DavidLeeWarwick.Pages;

public partial class Index
{
    //Http
    static HttpClient httpClient = new HttpClient();

    //Race data
    string raceseries, racename, racetrack, racestate;
    DateTime starttime;
    TimeSpan duration, timeremaining;
    List<Driver> drivers;

    //Page data
    string error;
    DateTime polled, attempted;

    //Timer
    System.Timers.Timer timer;

    string PollingInfo
    {
        get
        {
            if (error != null) return $"Attempted at {attempted:HH:mm:ss:fff}: {error}";
            return $"Polled at {attempted:HH:mm:ss:fff}";
        }
    }
    string RaceStarttime => starttime.ToString("d MMM yyyy HH:mm");
    string Duration(TimeSpan span) => span.ToString();
    private async void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        await ReadData();
        await InvokeAsync(StateHasChanged);
        timer.Start();
    }
    async Task ReadData()
    {
        attempted = DateTime.UtcNow;
        error = null;
        try
        {
            using (HttpRequestMessage request = new(HttpMethod.Get, "http://dev-sample-api.tsl-timing.com/sample-data"))
            {
                request.Headers.Add("Accept", "application/json");
                using (HttpResponseMessage response = await Index.httpClient.SendAsync(request))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        error = $"Tsl-timing returned {response.StatusCode}";
                        return;
                    }
                    using (Stream streamResp = response.Content.ReadAsStream())
                    {
                        using (JsonDocument document = await JsonDocument.ParseAsync(streamResp))
                        {
                            UnpackData(document);
                        }
                    }
                }
            }
            polled = DateTime.UtcNow;
            error = null;
        }
        catch
        {
            error = $"Error connecting to tsl-timing.";
        }
    }
    TimeSpan GetDuration(JsonElement element, string propertyName)
    {
        string[] sa = element.GetProperty(propertyName).GetString().Split(':');
        return new TimeSpan(int.Parse(sa[0]), int.Parse(sa[1]), int.Parse(sa[2]));
    }
    void UnpackData(JsonDocument document)
    {
        drivers.Clear();
        JsonElement element;
        JsonElement root = document.RootElement;
        raceseries = root.GetProperty("series").GetString();
        drivers.Clear();
        racename = root.GetProperty("name").GetString();
        racetrack = root.GetProperty("track").GetString();
        racestate = root.GetProperty("state").GetString();
        starttime = root.GetProperty("startTime").GetDateTime();
        duration = GetDuration(root, "duration");
        timeremaining = GetDuration(root, "timeRemaining");
        foreach (JsonElement classificationElement in root.GetProperty("classification").EnumerateArray())
        {
            Driver driver = new Driver();
            driver.Name = classificationElement.GetProperty("name").GetString();
            driver.TeamName = classificationElement.GetProperty("teamName").GetString();
            if (classificationElement.TryGetProperty("fastestLapTime", out element))
            {
                if (element.ValueKind != JsonValueKind.Null && element.TryGetProperty("display", out element))
                    driver.FastestLap = element.GetString();
            }
            if (classificationElement.TryGetProperty("lastLapTime", out element))
            {
                if (element.ValueKind != JsonValueKind.Null && element.TryGetProperty("display", out element))
                    driver.LastLap = element.GetString();
            }
            driver.Position = classificationElement.GetProperty("position").GetInt32();
            driver.Laps = classificationElement.GetProperty("laps").GetInt32();
            if (classificationElement.TryGetProperty("currentLapSectorTimes", out element))
            {
                foreach (JsonProperty innerProperty in element.EnumerateObject())
                {
                    int sector;
                    if (int.TryParse(innerProperty.Name, out sector))
                    {
                        if (sector > 0 && sector <= 3)
                        {
                            if (innerProperty.Value.ValueKind != JsonValueKind.Null && innerProperty.Value.TryGetProperty("display", out element))
                                driver.Sectors[sector - 1] = element.GetString();
                        }
                    }
                }
            }
            drivers.Add(driver);
        }
    }
    async protected override Task OnInitializedAsync()
    {
        drivers = new(50);
        await ReadData();

        //Show static demo data from earlier
        if (polled == DateTime.MinValue)
        {
            using (JsonDocument jsonDocument= JsonDocument.Parse(DemoData.samplejson))
            {
                UnpackData(jsonDocument);
            }
            polled = DateTime.UtcNow;
        }

        //Timer
        timer = new System.Timers.Timer();
        timer.Interval = 1500;
        timer.Elapsed += Timer_Elapsed;
        timer.AutoReset = false;
        timer.Start();
    }
}
class Driver
{
    public string Name, TeamName, FastestLap, LastLap;
    public int Position, Laps;
    public string[] Sectors = new string[3];
}
