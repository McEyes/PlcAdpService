using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jb.smartchangeover.Service.Domain.Shared
{
    public static class JsonExtension
    {
        public static string ToJSON(this object obj)
        {
            if (obj == null)
            {
                return null;
            }
            return JsonConvert.SerializeObject(obj);
        }

        public static string ToJSON(this object obj, bool isFormat = false, bool isIgnoreNull = false)
        {
            if (obj == null)
            {
                return null;
            }
            var jSetting = new JsonSerializerSettings();
            jSetting.NullValueHandling = NullValueHandling.Ignore;

            if (isFormat)
            {
                if (isIgnoreNull)
                {
                    return JsonConvert.SerializeObject(obj, Formatting.Indented, jSetting);
                }
                else
                {
                    return JsonConvert.SerializeObject(obj, Formatting.Indented);
                }

            }
            else
            {
                if (isIgnoreNull)
                {
                    return JsonConvert.SerializeObject(obj, jSetting);
                }
                else
                {
                    return JsonConvert.SerializeObject(obj);
                }
            }
        }

        public static string ToJSONIgnoreNullValue(this object obj)
        {
            if (obj == null)
            {
                return null;
            }
            var jSetting = new JsonSerializerSettings();
            jSetting.NullValueHandling = NullValueHandling.Ignore;
            return JsonConvert.SerializeObject(obj, jSetting);
        }

        public static T FromJSON<T>(this string input)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(input);
            }
            catch
            {
                return default(T);
            }
        }

        public static T GetValue<T>(string json, string key)
        {
            JObject jsonObject = JObject.Parse(json);
            T value = jsonObject[key].ToObject<T>();
            return value;
        }
    }
}
