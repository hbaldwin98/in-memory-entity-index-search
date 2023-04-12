using System.Text;
using System.Text.Json;

namespace Indexer;

internal class JsonComparer
{

    List<Dictionary<string, Dictionary<string, Tuple<string, string>>>> Comparisons = new List<Dictionary<string, Dictionary<string, Tuple<string, string>>>>();
    public JsonComparer()
    {
    }

    public List<Dictionary<string, Dictionary<string, Tuple<string, string>>>> CompareFiles(string firstJson, string secondJson)
    {
        var jsonDocuments = GetDocuments(firstJson);
        var jsonDocuments2 = GetDocuments(secondJson);

        foreach (var jsonDocument in jsonDocuments)
        {
            foreach (var jsonDocument2 in jsonDocuments2)
            {
                var dict = FlattenJson(jsonDocument.RootElement);
                var dict2 = FlattenJson(jsonDocument2.RootElement);

                Comparisons.Add(CompareJson(dict, dict2));
            }
        }

        return Comparisons;
    }

    public List<JsonDocument> GetDocuments(string json)
    {
        var jsonDocuments = new List<JsonDocument>();
        using (var jsonDoc = JsonDocument.Parse(json))
        {
            var root = jsonDoc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in root.EnumerateArray())
                {
                    jsonDocuments.Add(JsonDocument.Parse(element.GetRawText()));
                }
            }
        }

        return jsonDocuments;
    }

    public Dictionary<string, List<string>> FlattenJson(JsonElement element, string prefix = "")
    {
        var dict = new Dictionary<string, List<string>>();

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                FlattenObject(element, prefix, dict);
                break;

            case JsonValueKind.Array:
                FlattenArray(element, prefix, dict);
                break;

            case JsonValueKind.String:
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Number:
                FlattenElement(element, prefix, dict);
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                break;
        }

        return dict;
    }

    private void FlattenElement(JsonElement element, string prefix, Dictionary<string, List<string>> dict)
    {
        var numberValue = element.ToString();

        if (!string.IsNullOrEmpty(numberValue))
        {
            var fieldName = prefix;

            if (!dict.ContainsKey(fieldName))
            {
                dict[fieldName] = new List<string>();
            }

            if (!dict[fieldName].Contains(numberValue))
            {
                dict[fieldName].Add(numberValue);
            }
        }
    }

    private void FlattenArray(JsonElement element, string prefix, Dictionary<string, List<string>> dict)
    {
        for (int i = 0; i < element.GetArrayLength(); i++)
        {
            var childDict = FlattenJson(element[i], prefix);

            foreach (var kvp in childDict)
            {
                if (!dict.ContainsKey(kvp.Key))
                {
                    dict[kvp.Key] = new List<string>();
                }

                dict[kvp.Key].AddRange(kvp.Value);
            }
        }
    }

    private void FlattenObject(JsonElement element, string prefix, Dictionary<string, List<string>> dict)
    {
        foreach (var objProperty in element.EnumerateObject())
        {
            var fieldName = objProperty.Name;

            if (!string.IsNullOrEmpty(prefix))
            {
                fieldName = $"{prefix}.{fieldName}";
            }

            var childDict = FlattenJson(objProperty.Value, fieldName);

            foreach (var kvp in childDict)
            {
                if (!dict.ContainsKey(kvp.Key))
                {
                    dict[kvp.Key] = new List<string>();
                }

                dict[kvp.Key].AddRange(kvp.Value);
            }
        }
    }

    private Dictionary<string, Dictionary<string, Tuple<string, string>>> CompareJson(Dictionary<string, List<string>> dict1, Dictionary<string, List<string>> dict2)
    {
        // Get the values for the "Id" field in both dictionaries
        var id1 = dict1.GetValueOrDefault("id")?.FirstOrDefault();
        var id2 = dict2.GetValueOrDefault("id")?.FirstOrDefault();

        if (id1 != null && id2 != null && id1 != id2)
        {
            throw new ArgumentException("Cannot compare dictionaries with different Ids.");
        }

        // Compare the values of the flattened dictionaries
        var differences = new Dictionary<string, Dictionary<string, Tuple<string, string>>>();
        foreach (var kvp in dict1)
        {
            var fieldName = kvp.Key;

            if (dict2.ContainsKey(fieldName))
            {
                var values1 = kvp.Value;
                var values2 = dict2[fieldName];

                if (!values1.SequenceEqual(values2))
                {
                    if (!differences.ContainsKey(id1))
                    {
                        differences[id1] = new Dictionary<string, Tuple<string, string>>();
                    }

                    differences[id1][fieldName] = new Tuple<string, string>(string.Join(",", values1), string.Join(",", values2));
                }
            }
            else
            {
                if (!differences.ContainsKey(id1))
                {
                    differences[id1] = new Dictionary<string, Tuple<string, string>>();
                }

                differences[id1][fieldName] = new Tuple<string, string>(string.Join(",", kvp.Value), null);
            }
        }

        foreach (var kvp in dict2)
        {
            var fieldName = kvp.Key;

            if (!dict1.ContainsKey(fieldName))
            {
                if (!differences.ContainsKey(id2))
                {
                    differences[id2] = new Dictionary<string, Tuple<string, string>>();
                }

                differences[id2][fieldName] = new Tuple<string, string>(null, string.Join(",", kvp.Value));
            }
        }

        return differences;
    }

    public string GenerateJsonOutput(List<Dictionary<string, Dictionary<string, Tuple<string, string>>>> differences)
    {
        var json = new StringBuilder();

        json.Append("[\n");

        for (int i = 0; i < differences.Count(); i++)
        {
            json.Append(GenerateJsonOutput(differences[i]));

            if (i < differences.Count() - 1)
            {
                json.Append(",\n");
            }
        }

        json.Append("\n]");

        return json.ToString();
    }

    public string GenerateJsonOutput(Dictionary<string, Dictionary<string, Tuple<string, string>>> differences)
    {
        var json = new StringBuilder();
        var options = new JsonSerializerOptions { WriteIndented = true };

        json.Append("{");

        foreach (var kvp in differences)
        {
            var id = kvp.Key;
            var idDifferences = kvp.Value;

            json.Append($"\"{id}\": {{");

            foreach (var diff in idDifferences)
            {
                var fieldName = diff.Key;
                var values = diff.Value;

                json.Append($"\"{fieldName}\": {{");

                if (values.Item1 != null)
                {
                    json.Append($"\"left\": \"{values.Item1}\",");
                }
                else
                {
                    json.Append("\"left\": null,");
                }

                if (values.Item2 != null)
                {
                    json.Append($"\"right\": \"{values.Item2}\"");
                }
                else
                {
                    json.Append("\"right\": null");
                }

                json.Append("},");
            }

            // Remove the trailing comma
            if (idDifferences.Any())
            {
                json.Remove(json.Length - 1, 1);
            }

            json.Append("},");
        }

        // Remove the trailing comma
        if (differences.Any())
        {
            json.Remove(json.Length - 1, 1);
        }

        json.Append("}");

        return JsonSerializer.Serialize(JsonDocument.Parse(json.ToString()).RootElement, options);
    }

    public override string ToString()
    {
        return GenerateJsonOutput(Comparisons);
    }
}
