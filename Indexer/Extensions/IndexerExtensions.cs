using CsvHelper;
using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Indexer.Extensions
{
    public static class IndexerExtensions
    {
        public static IEnumerable<T> GetMatches<T>(this IIndexer<T> indexer, string value, string field)
            where T : Base
        {
            if (indexer == null)
            {
                throw new ArgumentNullException(nameof(indexer));
            }

            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(value));
            }

            if (string.IsNullOrEmpty(field))
            {
                throw new ArgumentException("Field cannot be null or empty.", nameof(field));
            }

            if (!indexer.GetIndex().TryGetValue(value, out var index))
            {
                return Enumerable.Empty<T>();
            }

            if (!index.TryGetValue(field, out var matches))
            {
                return Enumerable.Empty<T>();
            }

            return matches;
        }

        public static void ExportToCsv<T>(this IIndexer<T> indexer, string filePath) where T : Base
        {
            var index = indexer.GetIndex();

            var headerFields = index.Values.SelectMany(d => d.Keys).Distinct().OrderBy(k => k).ToList();

            var rows = GenerateRows(index);

            WriteCsv(headerFields, rows).SaveStreamToFile(filePath);
        }

        private static Dictionary<string, Dictionary<string, string>> GenerateRows<T>(Dictionary<string, Dictionary<string, HashSet<T>>> index) where T : Base
        {
            var rows = new Dictionary<string, Dictionary<string, string>>();
            foreach (var (key, values) in index)
            {
                foreach (var entity in values.Values.SelectMany(s => s).Distinct())
                {
                    var entityId = entity.Id;
                    if (!rows.ContainsKey(entityId))
                    {
                        rows[entityId] = new Dictionary<string, string>();
                    }
                    var rowValues = rows[entityId];
                    foreach (var (headerField, fieldValues) in values)
                    {
                        if (fieldValues.Contains(entity))
                        {
                            if (!rowValues.ContainsKey(headerField))
                            {
                                rowValues[headerField] = key;
                            }
                            else
                            {
                                rowValues[headerField] += ";" + key;
                            }
                        }
                    }
                }
            }
            return rows;
        }

        public static void SaveStreamToFile(this MemoryStream memoryStream, string filePath)
        {
            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                memoryStream.WriteTo(fileStream);
            }
        }

        public static MemoryStream WriteCsv(List<string> headerFields, Dictionary<string, Dictionary<string, string>> rows)
        {
            using (var memoryStream = new MemoryStream())
            using (var writer = new StreamWriter(memoryStream))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                // Write the header row
                foreach (var headerField in headerFields)
                {
                    csv.WriteField(headerField);
                }
                csv.NextRecord();

                // Write the data rows
                foreach (var (entityId, rowValues) in rows)
                {
                    foreach (var headerField in headerFields)
                    {
                        if (rowValues.TryGetValue(headerField, out var value))
                        {
                            csv.WriteField(value);
                        }
                        else
                        {
                            csv.WriteField(string.Empty);
                        }
                    }
                    csv.NextRecord();
                }

                writer.Flush();

                memoryStream.Position = 0;
                return new MemoryStream(memoryStream.GetBuffer(), 0, (int)memoryStream.Length, writable: true, publiclyVisible: false);
            }
        }
    }
}