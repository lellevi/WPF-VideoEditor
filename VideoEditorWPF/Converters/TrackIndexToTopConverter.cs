using System;
using System.Globalization;
using System.Windows.Data;

namespace VideoEditorWPF.Converters
{
    public class TrackIndexToTopConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int index = (int)value;
            return index * 70;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
// Конвертер для WPF, преобразует индекс трека (int) в позицию Y (Top) в пикселях.
// Каждый трек занимает 70px высоты (index * 70).
// Используется для вертикального позиционирования аудио/видео треков в timeline.
// ConvertBack не реализован (только одностороннее преобразование).
// TODO?