using System;
using System.Threading.Tasks;

namespace FastBindings.Interfaces
{
    public interface IValueConverterBase
    {
    }

    public class ConverterArgs
    {
        public object[] Values { get; set; }
        public Type TargetType { get; set; }
        //   public CultureInfo Culture { get; set; }
        public ConverterArgs(object[] values, Type targetType) 
        {
            Values = values;
            TargetType = targetType;
        }
    }

    public class ConverterBackArgs
    {
        public object Value { get; set; }
     //   public Type[] TargetTypes { get; set; }
     //   public CultureInfo Culture { get; set; }
    }

    public interface IForwardValueConverter : IValueConverterBase
    {
        object Convert(string converterName, ConverterArgs args);
    }

    public interface IAsyncForwardValueConverter : IValueConverterBase
    {
        Task<object> AsyncConvert(string converterName, ConverterArgs args);
    }


    public interface IBackValueConverter : IValueConverterBase
    {
        object[] ConvertBack(string converterName, ConverterBackArgs args);
    }
}
