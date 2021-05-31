using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace MigrasiLogee.Infrastructure
{
    public class CsvBooleanConverter : DefaultTypeConverter
    {
        public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
        {
            return bool.Parse(text);
        }

        public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
        {
            return value.ToString();
        }
    }
}
