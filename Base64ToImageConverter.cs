using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace ChatApplication
{
    // Converter som tar en Base64-sträng och omvandlar den till en BitmapImage.
    public class Base64ToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
       {
           if (value is string base64String && !string.IsNullOrEmpty(base64String))
           {
               try
               {
                   byte[] imageBytes = System.Convert.FromBase64String(base64String);
                   BitmapImage bitmap = new BitmapImage();
                   bitmap.BeginInit();
                   bitmap.StreamSource = new MemoryStream(imageBytes);
                   bitmap.CacheOption = BitmapCacheOption.OnLoad;
                   bitmap.EndInit();
                   return bitmap;
               }
               catch
               {
                   return null;
               }
          }
           return null;
       }
       public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
       {
            throw new NotImplementedException();
        }
    }
}
