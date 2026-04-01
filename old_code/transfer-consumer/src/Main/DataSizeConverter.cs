using System.Globalization;

namespace Main;

public class DataSizeConverter
{
   // canot be less than zero
   public static string ToDataSize(long input)
   {
      bool isMinus = input < 0;
      string[] sizes = { "B", "KB", "MB", "GB", "TB", "PB" };
      double len = Math.Abs(input);

      int order = 0;

      while (len >= 1024 && order < sizes.Length - 1)
      {
         order++;
         len = len / 1024;
      }

      var dataSize = string.Format(CultureInfo.InvariantCulture, "{0:0.##} {1}", len, sizes[order]);
      return isMinus ? "-" + dataSize : dataSize;

   }
   //
   // public static string ToDataSize2(long input)
   // {
   //    if (input < 0)
   //    {
   //       throw new ApplicationException($"Cannot convert to data size from value less then zero ({input})");
   //    }
   //
   //    string[] unitNames = {  "PB","TB" , "GB", "MB", "KB", "B"};
   //    long[] volumesInBytes = { 1125899906842624, 1099511627776, 1073741824, 1048576, 1024, 1};
   //
   //    long len = 0;
   //    long remainder = 0;
   //    var index = 0;
   //    for (int i = 0; i < unitNames.Length; i++)
   //    {
   //       index = i;
   //       long divisor = volumesInBytes[i];
   //       len = input / divisor;
   //       if (len > 0)
   //       {
   //           remainder = (long)(input % divisor);
   //           break;
   //       }
   //    }
   //
   //    if (remainder == 0)
   //    {
   //       Console.WriteLine(index);
   //       Console.WriteLine(unitNames[index]);
   //       return string.Format(CultureInfo.InvariantCulture, "{0} {1}", len, unitNames[index]);
   //    }
   //    else
   //    {
   //       return string.Format(CultureInfo.InvariantCulture, "{0}.{1} {2}", len, remainder, unitNames[index]);;
   //    }
   //
   //
   //
   // }

   public static long FromHumanReadableToBytes(string input)
   {
      string[] sizes = { "B", "KB", "MB", "GB", "TB", "PB" };
      string[] parts = input.Split(' ');

      if (parts.Length != 2)
      {
         throw new ArgumentException("Invalid input format");
      }

      double value = double.Parse(parts[0], CultureInfo.InvariantCulture);
      string unit = parts[1];

      int order = Array.IndexOf(sizes, unit);
      if (order < 0)
      {
         throw new ArgumentException("Invalid unit");
      }

      return (long)(value * Math.Pow(1024, order));
   }

}
