using System.Threading.Tasks;

namespace Runtime.Dialogue.Plugins.Localization
{
    /// <summary>
    /// 直参照（インスペクターで登録されたアセット）をそのまま返す標準ローダー。
    /// （外部パッケージに依存せず即座に動作します）
    /// </summary>
    public class DirectReferenceLanguageLoader : ILanguageAssetLoader
    {
        public Task<LoadedLanguageAssets> LoadAssetsAsync(LanguageAssetConfig config)
        {
            if (config == null)
            {
                return Task.FromResult<LoadedLanguageAssets>(null);
            }

            var loaded = new LoadedLanguageAssets
            {
                LanguageCode = config.languageCode,
                TextTable = config.textTable,
                FontDatabase = config.fontDatabase
            };

            return Task.FromResult(loaded);
        }

        public void ReleaseAssets(LoadedLanguageAssets assets)
        {
            // 直参照のため解放処理は不要
        }
    }
}
