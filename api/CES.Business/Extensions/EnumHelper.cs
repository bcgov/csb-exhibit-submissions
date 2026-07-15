using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CES.Business.Extensions
{
    public class EnumDescription
    {
        public int Id { get; set; }
        public required string Description { get; set; }
    }

    public class EnumHelper
    {
        public static IEnumerable<EnumDescription> GetEnumDescriptions<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T))
                       .Cast<T>()
                       .Select(e => new EnumDescription { Id = (int)(object)e, Description = GetEnumDescription(e) });
        }

        public static string GetEnumDescription<T>(T value) where T : Enum
        {
            // GetField always succeeds here: value.ToString() on an Enum yields one of that enum's own field names.
            var fieldInfo = value.GetType().GetField(value.ToString())!;

            var descriptionAttributes = (DescriptionAttribute[])fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);

            return descriptionAttributes.Length > 0 ? descriptionAttributes[0].Description : value.ToString();
        }
    }
}
