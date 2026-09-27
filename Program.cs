using System.Text;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;
using System.Text.Json;

namespace BleScanner;

public class BleDeviceNames
{
    // device name is the name of the device as advertised by the device itself (e.g. "GVH5100_1234")
    public string deviceName { get; set; }
    // Name is the user-friendly name of the device (e.g. "Front door")
    public string Name { get; set; }
}

internal static class Program
{
    private static readonly object ConsoleLock = new();

    private static List<BleDeviceNames> deviceNames = new List<BleDeviceNames>();

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        /*  Sample names.json file format:
            [
                {
                    "deviceName": "GVH5100_1234",
                    "Name": "Front door"
                },
            ] 
        */

        // Load device names from names.json file if it exists
        try
        {
            string filePath = "names.json";
            if (File.Exists(filePath))
            {
                string jsonString = File.ReadAllText(filePath);
                if (jsonString.Length > 0)
                    deviceNames.AddRange(JsonSerializer.Deserialize<List<BleDeviceNames>>(jsonString));
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Could not find names.json file at {filePath}. Using device names as advertised by the devices.");
                Console.ResetColor();
            }
        } 
        catch (Exception ex)
        {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Could not load user friendly device names from names.json: {ex.Message}");
                Console.ResetColor();
        }

        //
        var watcher = new BluetoothLEAdvertisementWatcher
        {
            // Active = also request scan response packets (device name, more data).
            // Passive = lower power, advertisement packets only.
            ScanningMode = BluetoothLEScanningMode.Passive
        };

        watcher.Received += (_, e) => OnAdvertisementReceived(e);
        watcher.Stopped += (_, e) =>
        {
            lock (ConsoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[watcher stopped] reason: {e.Error}");
                Console.ResetColor();
            }
        };

        watcher.Start();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true; // don't kill the process; let us clean up
            cts.Cancel();
        };

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (TaskCanceledException){}

        lock (ConsoleLock)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Shutting BLE watcher down.");
            Console.ResetColor();
        }
        watcher.Stop();

        return 0;
    }

    private static void OnAdvertisementReceived(
        BluetoothLEAdvertisementReceivedEventArgs e)
    {
        var address = e.BluetoothAddress;
        var name = string.IsNullOrWhiteSpace(e.Advertisement.LocalName)
            ? "(no name)"
            : e.Advertisement.LocalName;

        var manufacturerIds = e.Advertisement.ManufacturerData
            .Select(m => m.CompanyId)
            .ToArray();

        var serviceUuids = e.Advertisement.ServiceUuids
            .Select(g => g.ToString())
            .ToArray();

        var dataSections = e.Advertisement.DataSections
            .ToArray();


        var rssi = e.RawSignalStrengthInDBm;  // received signal strength indicator (dBm)

        var tempF = 0.0;
        var humidity = 0.0; 
        var battery = 0u;

        // we are only interested in the Govee H5100 devices 
        if (name.Contains("5100", StringComparison.OrdinalIgnoreCase) )
        {

            // Bytes   0   1  2   3       4   5    6      7
            // Data:   |NA  NA NA  NA|  
            // Temperature & Humidity:    |MSB     LSB|
            //   Note: encoded as Temperature * 1000 + Humidity
            //   Note: Temperature is in Celsius as an integer with one decimal place (e.g. 23.4°C is encoded as 23400)
            //   Note: Humidity is in percent as an integer with one decimal place (e.g. 45.6% is encoded as 456)
            // Battery:                                   |byte|  Note: battery is a percentage (0-100), not voltage.

            var slice = GetBytes(dataSections[3].Data).ToArray();  // data is in the 4th data section (index 3)
            uint tempAndHumidityBuffer = ((uint)slice[4] << 16) | ((uint)slice[5] << 8) | slice[6];  // get the 3 bytes of temperature and humidity data as a single uint
            var tempIsNegative = (tempAndHumidityBuffer & 0x800000) != 0;  // check if the temperature is negative (most significant bit of the 3 bytes is set)
            tempAndHumidityBuffer &= 0x7FFFFF;  // clear the negative bit so we can get the absolute value of the temperature
            uint iHumidity = tempAndHumidityBuffer % 1000;  // humidity is the remainder of data after dividing by 1000
            float fTempC = (tempAndHumidityBuffer- iHumidity) / 10000.0f; // remove humidity, divide by 1000 to get Celsius, then divide by 10 to get decimal place
            if (tempIsNegative)
            {
                fTempC = -fTempC;  // if the negative bit is set, make the temperature negative
            }

            tempF = (fTempC * 9.0f / 5.0f) + 32.0f; // convert to Fahrenheit
            humidity = iHumidity / 10.0f;  // divide by 10 to get decimal place
            battery = (uint)slice[7]; // battery level in percent

            // convert device name to user-friendly name if it exists in the names.json file, otherwise use the device name as advertised by the device
            string preferredDeviceName = deviceNames.FirstOrDefault(d => d.deviceName == name)?.Name ?? name;

            lock (ConsoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"{preferredDeviceName}: {tempF:F1}°F, {humidity:F1}%, Battery: {battery}%");
                Console.ResetColor();
            }
        }
    }

    private static byte[] GetBytes(Windows.Storage.Streams.IBuffer buffer)
    {
        var bytes = new byte[buffer.Length];
        using var reader = DataReader.FromBuffer(buffer);
        reader.ReadBytes(bytes);
        return bytes;
    }
}