using Iot.Device.Ads1115;
using System.Device.I2c;
using UnitsNet;
using Newtonsoft.Json;
using Microsoft.Azure.Devices.Client;
using System.Text;

namespace Scsys
{
  class Program
  {
    static int i = 0;
    public class Readout
    {
      public short RawValue { get; set; }
      public double Voltage { get; set; }
      public double DbValue { get; set; }
      public string? Time { get; set; }
      public string? Date { get; set; }
    }

    // Change the return type of Main to Task
    static async Task Main(string[] args)
    {
      /* Initialize connection to IoT hub */
      DeviceClient deviceClient;
      string iotHubUri = "IoTSoundSensorHub.azure-devices.net";
      string deviceKey = "hTMcf2QVF7cncI+d5e+J6rsSi/W9gibdVAIoTPc0MVc=";
      deviceClient = DeviceClient.Create(iotHubUri, new DeviceAuthenticationWithRegistrySymmetricKey("morkiraspi", deviceKey), TransportType.Mqtt);

      /* Initialize I2C settings */
      int busId = 1;
      int deviceAddress = 0x49;
      var i2cSettings = new I2cConnectionSettings(busId, deviceAddress);
      var device = I2cDevice.Create(i2cSettings);

      /* Initialize ADS1115 */
      Ads1115 adc = new Ads1115(device, InputMultiplexer.AIN0, MeasuringRange.FS6144);
      Console.WriteLine("ADS1115 initialized successfully.");

      /* Initialise dictionary of lists to hold samples */
      Dictionary<InputMultiplexer, List<Readout>> samples = new() {
        { InputMultiplexer.AIN0, new List<Readout>() },
        { InputMultiplexer.AIN1, new List<Readout>() },
        { InputMultiplexer.AIN2, new List<Readout>() },
        { InputMultiplexer.AIN3, new List<Readout>() }
      };


      while (true)
      {
        Console.WriteLine($"Readout no.{++i}");
        CollectSamples(adc, samples);

        /* Convert the dictionary to JSON and print */
        string serializedData = JsonConvert.SerializeObject(samples, Formatting.Indented);
        var serializedEncodedData = new Message(Encoding.ASCII.GetBytes(serializedData));
        Console.WriteLine($"{serializedData}");

        await deviceClient.SendEventAsync(serializedEncodedData); // Await the async call

        Thread.Sleep(2000);
      }
    }

    static ElectricPotential ReadAds1115Channel(Ads1115 adc, InputMultiplexer channel)
    {
      adc.InputMultiplexer = channel;
      short raw = adc.ReadRaw();
      return adc.RawToVoltage(raw);
    }

    static void CollectSamples(Ads1115 adc, Dictionary<InputMultiplexer, List<Readout>> samples)
    {
      foreach (var channel in samples.Keys.ToList())
      {
        samples[channel].Clear();
        for (int i = 0; i < 2; i++)
        {
          ElectricPotential voltage = ReadAds1115Channel(adc, channel);
          short rawValue = adc.ReadRaw();
          double dbValue = voltage.Volts * 50;

          /* Create a Readout object and add it to the list */
          Readout readout = new Readout
          {
            RawValue = rawValue,
            Voltage = voltage.Volts,
            DbValue = dbValue,
            Time = DateTime.Now.ToString("HH:mm:ss"),
            Date = DateTime.Now.ToString("yyyy-MM-dd")
          };
          samples[channel].Add(readout);

          Thread.Sleep(10);
        }
      }

      foreach (var channel in samples.Keys)
      {
        var channelSamples = samples[channel];
        ElectricPotential average = ElectricPotential.FromVolts(channelSamples.Average(v => v.Voltage));
        ElectricPotential max = channelSamples.Max(v => ElectricPotential.FromVolts(v.Voltage));
      }
    }
  }
}
