using RomForge.Core.Models.PSVita;
using System.IO;

namespace RomForge.Core.Services.PSVita
{
    public class NPSService
    {
        private static readonly Lazy<NPSService> _instance = new(() => new NPSService());

        public static NPSService Instance => _instance.Value;

        private bool _isInitialized = false;

        private NPSService() { }

        public void Initialize()
        {
            if (_isInitialized) 
                return;

            string appPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PSV_GAMES.tsv");
            string addContPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PSV_DLCS.tsv");

            NPSTsvParser.Instance.LoadData(appPath, addContPath);

            _isInitialized = true;
        }

        public NPSGroup? GetGroup(string? titleId)
        {
            Initialize();

            if (string.IsNullOrEmpty(titleId))
                return null;

            NPSTsvParser.Instance.TryGetGroup(titleId, out var group);

            return group;
        }
    }
}