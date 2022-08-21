using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;

namespace XMLReverse.Lib
{
    public static class CsvTool
    {
        private static readonly Encoding Enc = Encoding.UTF8;

        public static void WriteToFile<T>(string file, IEnumerable<T> obj)
        {
            using var stream = File.Create(file);
            using var writer = new StreamWriter(stream, Enc);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csv.WriteRecords(obj);
            csv.Flush();
            Env.Execute(file);
        }

        public static void GenerateTable(string file,
            IDictionary<string, SortedDictionary<string, string>> paths, IDictionary<string, PathStat> stats)
        {
            var items = new List<IDictionary<string, object>>();
            const string id = "@XML";
            const string count = "@COUNT";
            const string sum = "@SUM";
            var allKeys = paths.Keys.Concat(stats.Keys).Distinct().ToArray();
            var allAttr = paths.SelectMany(p => p.Value.Keys).Distinct().ToArray();

            foreach (var key in allKeys)
            {
                if (!paths.TryGetValue(key, out var map))
                    continue;
                var label = string.Join(" → ", key.Split('/').Skip(1));
                if (string.IsNullOrWhiteSpace(label))
                    continue;
                IDictionary<string, object> tuple = new ExpandoObject();
                tuple[id] = label;
                foreach (var attr in allAttr)
                {
                    if (attr.StartsWith("{"))
                        continue;
                    map.TryGetValue(attr, out var raw);
                    var value = string.IsNullOrWhiteSpace(raw) ? 0 : 1;
                    var attrKey = attr.TrimEnd('_').Replace('_', '#');
                    tuple[attrKey] = value;
                }
                var itemSum = tuple.Sum(t => t.Value is int i ? i : 0);
                if (itemSum == 0)
                    continue;
                tuple[count] = itemSum;
                items.Add(tuple);
            }

            IDictionary<string, object> sumX = new ExpandoObject();
            var allSums = items.First().Keys.Skip(1);
            var sums = allSums.Select(s => (key: s, sum: items.Sum(l => (int)l[s])));
            sumX[id] = sum;
            foreach (var pair in sums)
                sumX[pair.key] = pair.sum;
            items.Add(sumX);

            var sortedBySum = sumX.Keys.Skip(1)
                .OrderByDescending(s => s == count ? 0 : (int)sumX[s])
                .ToArray();

            var records = new List<dynamic>();
            foreach (var item in items.OrderByDescending(i =>
                         i[id].Equals(sum) ? 0 : (int)i[count]))
            {
                IDictionary<string, object> tuple = new ExpandoObject();
                tuple[id] = item[id];
                foreach (var sortedKey in sortedBySum)
                    tuple[sortedKey] = item[sortedKey];
                records.Add(tuple);
            }

            WriteToFile(file, records);
        }
    }
}