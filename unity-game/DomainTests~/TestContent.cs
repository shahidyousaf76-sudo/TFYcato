using System.IO;
using Newtonsoft.Json;
using TFY.CatAdventure.Domain;

namespace TFY.CatAdventure.Tests
{
    internal static class TestContent
    {
        private static GameContent content;
        public static readonly string Root = Path.GetFullPath(Path.Combine(System.AppContext.BaseDirectory, "../../../../.."));

        /// <summary>The same JSON the game ships in Resources (exported from firebase/functions/src/content).</summary>
        public static GameContent Load()
        {
            if (content != null) return content;
            var path = Path.Combine(Root, "unity-game/Assets/Resources/default_content.json");
            content = JsonConvert.DeserializeObject<GameContent>(File.ReadAllText(path), new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
            return content;
        }
    }
}
