using System;
using System.Globalization;
using System.Windows.Data;

namespace Hermes.Helpers
{
    /// <summary>
    /// 바인딩된 값이 null이면 false를, null이 아니면 true를 반환하는 컨버터입니다.
    /// </summary>
    public class NullToFalseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
