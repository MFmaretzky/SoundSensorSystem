using Iot.Device.Ads1115;
using System.Device.I2c;
using UnitsNet;

namespace Scsys {
  class Program {
    static void Main(string[] args) {
        /* Initialize I2C settings */
      int i = 0;
      int busId = 1;
      int deviceAddress = 0x49;
      var i2cSettings = new I2cConnectionSettings(busId, deviceAddress);
      var device = I2cDevice.Create(i2cSettings);

        /* Initialize ADS1115 */
      Ads1115 adc = new Ads1115(device, InputMultiplexer.AIN0, MeasuringRange.FS6144);
      Console.WriteLine("ADS1115 initialized successfully.");

        /* Initialise dictionary of lists to hold samples */
      Dictionary<InputMultiplexer, List<ElectricPotential>> samples = new() {
        { InputMultiplexer.AIN0, new List<ElectricPotential>() },
        { InputMultiplexer.AIN1, new List<ElectricPotential>() },
        { InputMultiplexer.AIN2, new List<ElectricPotential>() },
        { InputMultiplexer.AIN3, new List<ElectricPotential>() }
      };

      while (true) {
        Console.WriteLine($"Readout no.{++i}");
        CollectSamples(adc, samples);
        Console.WriteLine("");
        Thread.Sleep(2000);
      }
    }

    /// <summary>
    /// Reading given ADS1115 channel obtaining voltage sample
    /// </summary>
    /// <param name="adc"></param>
    /// <param name="channel"></param>
    /// <returns></returns>
    static ElectricPotential ReadAds1115Channel(Ads1115 adc, InputMultiplexer channel) {
      adc.InputMultiplexer = channel;
      short raw = adc.ReadRaw();
      return adc.RawToVoltage(raw);
    }

    /// <summary>
    /// Collects and process 10 consecutive samples and stores average & maximum voltage in dictionary  
    /// </summary>
    /// <param name="adc"></param>
    /// <param name="samples"></param>
    static void CollectSamples(Ads1115 adc, Dictionary<InputMultiplexer, List<ElectricPotential>> samples) {
      foreach (var channel in samples.Keys.ToList()) {
        samples[channel].Clear();
        for (int i = 0; i < 10; i++) {
          ElectricPotential voltage = ReadAds1115Channel(adc, channel);
          samples[channel].Add(voltage);
          Thread.Sleep(10);
        }
      }

      foreach (var channel in samples.Keys) {
        var channelSamples = samples[channel];
        ElectricPotential average = ElectricPotential.FromVolts(channelSamples.Average(v => v.Volts));
        ElectricPotential max = channelSamples.Max();
        Console.WriteLine($"Channel {channel}: Vavg = {average}, Vmax = {max}");
      }
    }
  }
}