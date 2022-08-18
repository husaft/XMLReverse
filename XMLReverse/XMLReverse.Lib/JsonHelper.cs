using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace XMLReverse.Lib
{
    public static class JsonHelper
    {
        private static readonly Encoding Enc = Encoding.UTF8;

        private static readonly JsonSerializerSettings Cfg = new()
        {
            Formatting = Formatting.Indented
        };

        public static void WriteToFile(string path, object obj)
        {
            var file = ToJsonPath(path);
            var json = JsonConvert.SerializeObject(obj, Cfg);
            File.WriteAllText(file, json, Enc);
        }

        public static T ReadFromFile<T>(string path) where T : new()
        {
            var file = ToJsonPath(path);
            if (!File.Exists(file))
                return new T();
            var json = File.ReadAllText(file, Enc);
            return JsonConvert.DeserializeObject<T>(json, Cfg);
        }

        private static string ToJsonPath(string path) => $"{path}.json";
    }
}