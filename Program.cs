using Iot.Device.Ads1115;
using System.Device.I2c;
using UnitsNet;
using Newtonsoft.Json;
using Microsoft.Azure.Devices.Client;
using System.Text;
using MathNet.Numerics.IntegralTransforms;
using System.Numerics;

namespace Scsys
{
  class Program
  {
    static int i = 0;

    public class Readout
    {
      public double Voltage { get; set; }
      public double DbValue { get; set; }
      public string? Time { get; set; }
      public string? Date { get; set; }
      public List<double>? Frequencies { get; set; }
    }

    static async Task Main()
    {
        /* Initialize connection to IoT hub */
      DeviceClient deviceClient;
      string iotHubUri = "SoundHub.azure-devices.net";
      string deviceKey = "V1ARX+ydYUrQlDcGJbUjekS90Z97l16aBAIoTNntwL0=";
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
        { InputMultiplexer.AIN3, new List<Readout>() }
      };

      while (true)
      {
        Console.WriteLine($"Readout no.{++i}");
        CollectSamples(adc, samples);

          /* Convert the dictionary to JSON and print */
        string serializedData = JsonConvert.SerializeObject(samples, Formatting.Indented);
        var serializedEncodedData = new Message(Encoding.ASCII.GetBytes(serializedData));
        await deviceClient.SendEventAsync(serializedEncodedData);

        Console.WriteLine($"{serializedData}");
        Thread.Sleep(10);
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
        List<double> voltages = new List<double>();

        for (int i = 0; i < 10; i++)
        {
          ElectricPotential voltage = ReadAds1115Channel(adc, channel);
          short rawValue = adc.ReadRaw();
          double dbValue = voltage.Volts * 50;
          voltages.Add(voltage.Volts);

          Readout readout = new Readout
          {
            Voltage = voltage.Volts,
            DbValue = dbValue,
            Time = DateTime.Now.ToString("HH:mm:ss.f"),
            Date = DateTime.Now.ToString("yyyy-MM-dd"),
            Frequencies = null
          };
          samples[channel].Add(readout);
          Thread.Sleep(2);
        }

        //var fftResult = PerformFFT(voltages.ToArray());

        //foreach (var readout in samples[channel])
        //{
          //readout.Frequencies = fftResult.ToList();
        //}
      }
    }

    static double[] PerformFFT(double[] data)
    {
      int n = data.Length;
      Complex[] fftData = new Complex[n];

      for (int i = 0; i < n; i++)
      {
        fftData[i] = new Complex(data[i], 0);
      }
      Fourier.Forward(fftData, FourierOptions.Matlab);

      double[] frequencies = new double[n];
      for (int i = 0; i < n; i++)
      {
        frequencies[i] = fftData[i].Magnitude;
      }
      return frequencies;
    }
  }
}
