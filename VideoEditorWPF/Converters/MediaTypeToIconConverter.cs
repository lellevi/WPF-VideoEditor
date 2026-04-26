using System;
using System.Globalization;
using System.Windows.Data;
using VideoEditorWPF.Models;

namespace VideoEditorWPF.Converters
{
    public class MediaTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MediaType type)
            {
                return type == MediaType.Video ? "📹" : "🎵";
            }
            return "📹";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}