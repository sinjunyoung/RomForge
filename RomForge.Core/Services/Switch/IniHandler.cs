using System.IO;
using System.Text;

namespace RomForge.Core.Services.Switch
{
    public class IniHandler(string filePath)
    {
        private readonly Dictionary<string, Dictionary<string, string>> _data = new(StringComparer.OrdinalIgnoreCase);

        private string _currentSection = "Default";

        public IniHandler() : this(string.Empty) { }

        public async Task LoadAsync()
        {
            if (!File.Exists(filePath))
                return;

            _currentSection = "Default";

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            string? line;

            while ((line = await reader.ReadLineAsync()) != null)
                ParseLine(line);
        }

        public void LoadFromBytes(byte[] data)
        {
            _currentSection = "Default";

            using var reader = new StreamReader(new MemoryStream(data, false));
            string? line;

            while ((line = reader.ReadLine()) != null)
                ParseLine(line);
        }

        public string GetValue(string section, string key, string defaultValue = "")
        {
            if (_data.TryGetValue(section, out var sectionData) && sectionData.TryGetValue(key, out string? value))
                return value;

            return defaultValue;
        }

        public void SetValue(string section, string key, string value)
        {
            if (!_data.ContainsKey(section))
                _data[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            _data[section][key] = value;
        }

        public byte[] ToBytes()
        {
            var sb = new StringBuilder();

            foreach (var section in _data)
            {
                sb.AppendLine($"[{section.Key}]");

                foreach (var kvp in section.Value)
                    sb.AppendLine($"{kvp.Key}=\"{kvp.Value}\"");

                sb.AppendLine();
            }

            return new UTF8Encoding(false).GetBytes(sb.ToString());
        }

        public async Task SaveAsync() => await File.WriteAllBytesAsync(filePath, ToBytes());

        private void ParseLine(string line)
        {
            string trimmed = line.Trim();

            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(';'))
                return;

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                _currentSection = trimmed[1..^1];

                if (!_data.ContainsKey(_currentSection))
                    _data[_currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                return;
            }

            var parts = trimmed.Split(['='], 2);

            if (parts.Length != 2)
                return;

            if (!_data.ContainsKey(_currentSection))
                _data[_currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            _data[_currentSection][parts[0].Trim()] = parts[1].Trim('\"');
        }
    }
}