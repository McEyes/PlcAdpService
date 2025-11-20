using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EquimentAdapter.General.Commons
{
    public static class CommonExtension
    {
        public static List<byte[]> GetFieldByteValues<T>(this T obj)
        {
            var list = new List<byte[]>();
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            var fields = typeof(T).GetProperties();
            foreach (var field in fields)
            {
                list.Add(Encoding.ASCII.GetBytes(field.GetValue(obj) + ""));
            }
            return list;
        }
        public static T SetFieldValues<T>(this T obj, string formatData) where T : new()
        {
            if (obj == null) new T();
            var bodyData = formatData;
            var crStr = Encoding.ASCII.GetString(cr);
            var tabStr = Encoding.ASCII.GetString(tab);
            var index = formatData.IndexOf(crStr);
            var numList = new List<string[]>();
            if (index > 4)
            {
                bodyData = bodyData.Substring(0, index);
                var subData = bodyData.Substring(index);
                var items = subData.Split(new string[] { crStr }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var item in items)
                {
                    var sfileds = item.Split(new string[] { tabStr }, StringSplitOptions.RemoveEmptyEntries);
                    numList.Add(sfileds);
                }
            }

            var values = bodyData.Split(new string[] { tabStr }, StringSplitOptions.RemoveEmptyEntries);
            return obj.SetFieldValues<T>(values, numList);
        }

        public static T SetFieldValues<T>(this T obj, string[] values, List<string[]> subValues = null) where T : new()
        {
            if (obj == null) new T();
            var fields = typeof(T).GetProperties();
            var index = 0;
            var len = fields.Length-1;
            var crStr = Encoding.ASCII.GetString(cr);
            foreach (var field in fields)
            {
                if (field.PropertyType.IsGenericType)
                {
                    if (subValues != null && subValues.Count > 0)
                    {
                        var list = Array.CreateInstance(field.PropertyType, subValues.Count);
                        var i = 0;
                        foreach (var sValues in subValues)
                        {
                            var subObj = Activator.CreateInstance(field.PropertyType.GetGenericTypeDefinition());
                            subObj.SetFieldValues(sValues);
                            list.SetValue(subObj, i++);
                        }
                        field.SetValue(obj, list);
                    }
                }
                else if(index<values.Length)
                {
                    field.SetValue(obj, values[index]);
                    index++;
                }
            }
            return obj;
        }

        private static byte[] tab = new byte[] { 9 };//ASCII值：9	控制值：HT，也就是tab
        private static byte[] cr = new byte[] { 13 };//ASCII值：13	控制值：CR
        public static byte[] GetFieldByteValues<T>(this T obj, byte[] tab)
        {
            var list = new List<byte>();
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            var fields = typeof(T).GetProperties();
            var i = 0;

            foreach (var field in fields)
            {
                if (++i < fields.Count())
                {
                    list.AddRange(Encoding.ASCII.GetBytes(field.GetValue(obj) + ""));
                    list.AddRange(tab);
                }
                else if (field.PropertyType.IsGenericType && field.Name == "NumList")
                {
                    var datas = (List<EventAck>)(field.GetValue(obj));
                    if (datas != null)
                    {
                        list.AddRange(Encoding.ASCII.GetBytes(datas.Count.ToString()));
                        list.AddRange(cr);
                        foreach (var item in datas)
                        {
                            list.AddRange(Encoding.ASCII.GetBytes(item.EventName));
                            list.AddRange(cr);
                            list.AddRange(Encoding.ASCII.GetBytes(item.Ack));
                            list.AddRange(cr);
                        }
                    }
                }
                else
                {
                    list.AddRange(Encoding.ASCII.GetBytes(field.GetValue(obj) + ""));
                }
            }
            return list.ToArray();
        }

        public class EventAck
        {
            /// <summary>
            /// 事件名称（EventName）：指定要验证的事件。如果为空，则所有事件均为有效事件.
            /// 具体事件查找：3. Event Message List
            /// </summary>
            public string EventName { get; set; }
            /// <summary>
            /// ACK事件名称（ACK）：指定有效事件的回复（ACK）。0表示无ACK，1表示有ACK
            /// </summary>
            public string Ack { get; set; } = "1";
        }

        public static byte[] ToBytes(this List<string> data, byte[] tab)
        {
            var list = new List<byte>();
            foreach (var field in data)
            {
                list.AddRange(BytesCombine(Encoding.ASCII.GetBytes(field), tab));
            }
            return list.ToArray();
        }

        public static byte[] BytesCombine(this List<byte[]> bytes, byte[] tab)
        {
            var list = new List<byte>();
            var i = 1;
            foreach (var item in bytes)
            {
                if (++i == bytes.Count)
                    list.AddRange(item);
                else
                    list.AddRange(BytesCombine(item, tab));
            }
            return list.ToArray();
        }
        public static byte[] BytesCombine(this byte[] pBytes, params byte[][] comBytes)
        {
            var data = comBytes.SelectMany(bytes => bytes).ToList();
            data.InsertRange(0, pBytes);
            return data.ToArray();
        }

        public static List<string> GetFieldValues<T>(this T obj)
        {
            var list = new List<string>();
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            var fields = typeof(T).GetFields();
            foreach (var field in fields)
            {
                list.Add(field.GetValue(obj) + "");
            }
            return list;
        }
        /// <summary>
        /// ASCII的byte值转成byte
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static byte[] IntToBytes(this int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return bytes;
        }
        public static byte[] ToBytes(this int value)
        {
            return Encoding.ASCII.GetBytes(value.ToString());
        }
    }
}
