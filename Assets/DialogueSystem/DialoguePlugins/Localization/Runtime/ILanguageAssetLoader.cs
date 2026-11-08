using System.Threading.Tasks;
using Runtime.Dialogue.Core;

namespace Runtime.Dialogue.Plugins.Localization
{
    public class LoadedLanguageAssets
    {
        public string LanguageCode { get; set; }
        public DialogueTextTable TextTable { get; set; }
        public DialogueFontDatabase FontDatabase { get; set; }
    }

    /// <summary>
    /// 言語切り替え時にテキスト辞書やフォントデータベースをロードするローダーのインターフェース。
    /// （直参照ローダーや Addressables ローダーを差し替えて使用可能）
    /// </summary>
    public interface ILanguageAssetLoader
    {
        Task<LoadedLanguageAssets> LoadAssetsAsync(LanguageAssetConfig config);
        void ReleaseAssets(LoadedLanguageAssets assets);
    }
}
