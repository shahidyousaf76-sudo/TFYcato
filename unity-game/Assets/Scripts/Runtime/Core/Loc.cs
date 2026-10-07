using System.Collections.Generic;

namespace TFY.CatAdventure
{
    /// <summary>
    /// Tiny localisation table. English and Danish are complete; other languages fall back to English per key.
    /// Add a language by adding a dictionary here (and its code to LANGUAGES in the server settings validation).
    /// </summary>
    public static class Loc
    {
        public static string Language { get; set; } = "en";
        public static readonly string[] Languages = { "en", "da" };
        public static string LanguageName(string code) => code == "da" ? "Dansk" : "English";

        public static string T(string key, params object[] args)
        {
            string s = null;
            if (Tables.TryGetValue(Language, out var table)) table.TryGetValue(key, out s);
            if (s == null) En.TryGetValue(key, out s);
            s = s ?? key;
            return args.Length > 0 ? string.Format(s, args) : s;
        }

        private static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            ["title"] = "TFY CAT ADVENTURE",
            ["home.hi"] = "Hi, {0}!", ["home.play"] = "PLAY", ["home.shop"] = "CAT SHOP", ["home.levels"] = "LEVELS",
            ["home.achievements"] = "ACHIEVEMENTS", ["home.rewards"] = "REWARDS", ["home.settings"] = "SETTINGS",
            ["welcome.new"] = "Hi, {0}! Let's run!", ["welcome.back"] = "Welcome back, {0}!",
            ["loading"] = "Loading…", ["back"] = "BACK", ["ok"] = "OK", ["yes"] = "YES", ["no"] = "NO", ["close"] = "CLOSE",
            ["levels.title"] = "LEVELS", ["levels.locked"] = "Finish level {0} to unlock this one!", ["levels.comingSoon"] = "Coming soon!",
            ["levels.level"] = "Level {0}",
            ["shop.title"] = "CAT SHOP", ["shop.buy"] = "UNLOCK", ["shop.equip"] = "WEAR", ["shop.equipped"] = "WEARING", ["shop.remove"] = "TAKE OFF",
            ["shop.select"] = "CHOOSE", ["shop.selected"] = "CHOSEN", ["shop.needLevel"] = "Finish level {0} first", ["shop.reward"] = "Special reward",
            ["shop.offline"] = "The Cat Shop needs internet. Try again soon!", ["shop.notEnough"] = "Not enough coins yet — keep running!",
            ["shop.notEnoughGems"] = "Not enough gems yet — look for gems in levels!", ["shop.unlocked"] = "Unlocked: {0}!",
            ["shop.cat.CATS"] = "CATS", ["shop.cat.HATS"] = "HATS", ["shop.cat.GLASSES"] = "GLASSES", ["shop.cat.SHOES"] = "SHOES",
            ["shop.cat.BACKPACKS"] = "BACKPACKS", ["shop.cat.CAPES_SCARVES"] = "CAPES", ["shop.cat.TRAILS"] = "TRAILS",
            ["achievements.title"] = "ACHIEVEMENTS", ["achievements.done"] = "Done!",
            ["rewards.title"] = "REWARDS", ["rewards.daily"] = "Daily Treats", ["rewards.day"] = "Day {0}", ["rewards.claim"] = "OPEN",
            ["rewards.claimed"] = "See you tomorrow!", ["rewards.boxes"] = "Mystery Boxes: {0}", ["rewards.openBox"] = "OPEN BOX",
            ["rewards.noBoxes"] = "Find mystery boxes in levels!", ["rewards.got"] = "You got {0}!", ["rewards.offline"] = "Rewards need internet. Try again soon!",
            ["rewards.later"] = "Come back a little later!", ["rewards.off"] = "Daily treats are resting today.",
            ["settings.title"] = "SETTINGS", ["settings.music"] = "Music", ["settings.sound"] = "Sound", ["settings.vibration"] = "Vibration",
            ["settings.graphics"] = "Graphics", ["settings.language"] = "Language", ["settings.help"] = "HOW TO PLAY", ["settings.parents"] = "FOR PARENTS",
            ["settings.reduceMotion"] = "Calm camera", ["settings.buttons"] = "On-screen buttons",
            ["settings.speed"] = "Game speed", ["speed.slow"] = "Slow", ["speed.normal"] = "Normal", ["speed.fast"] = "Fast",
            ["graphics.auto"] = "Auto", ["graphics.low"] = "Low", ["graphics.medium"] = "Medium", ["graphics.high"] = "High", ["graphics.ultra"] = "Ultra",
            ["help.text"] = "TAP — jump\nDOUBLE TAP — jump higher\nSWIPE UP — long jump\nSWIPE LEFT / RIGHT — change lane\nSWIPE DOWN — slide\n\nFollow the coins: they show where to jump!\nFlags save your progress.",
            ["parents.title"] = "FOR PARENTS", ["parents.gate"] = "Grown-ups only: what is {0} × {1}?",
            ["parents.privacy"] = "TFY Cat Adventure does not ask for names, emails, phone numbers, birthdays or photos. Players get a random name like Kitty4821. There is no chat. Game progress is stored with an anonymous account. Analytics are anonymous and used only to make levels fair and fix crashes.",
            ["parents.delete"] = "DELETE GAME DATA", ["parents.confirm"] = "Delete all progress, coins and cats on this device and online?", ["parents.deleted"] = "All game data was deleted.",
            ["parents.deleteOffline"] = "Deleting online data needs internet. Try again soon.",
            ["pause.title"] = "PAUSED", ["pause.resume"] = "RESUME", ["pause.restart"] = "RESTART", ["pause.home"] = "HOME",
            ["complete.title"] = "LEVEL COMPLETE!", ["complete.next"] = "NEXT LEVEL", ["complete.replay"] = "REPLAY", ["complete.home"] = "HOME",
            ["complete.score"] = "Score", ["complete.best"] = "Best", ["complete.newBest"] = "NEW BEST!", ["complete.unlocked"] = "Level {0} unlocked!",
            ["complete.offline"] = "Saved on this phone — it will sync when you're online.", ["complete.bonus"] = "Bonus", ["complete.box"] = "Mystery box found!",
            ["complete.achievement"] = "Achievement: {0}!",
            ["fail.title"] = "Oops! Try again!", ["fail.checkpoint"] = "FROM FLAG", ["fail.restart"] = "START OVER", ["fail.home"] = "HOME",
            ["tut.tap"] = "TAP TO JUMP", ["tut.double"] = "DOUBLE TAP TO JUMP HIGHER", ["tut.left"] = "SWIPE LEFT", ["tut.right"] = "SWIPE RIGHT",
            ["tut.down"] = "SWIPE DOWN TO SLIDE", ["tut.long"] = "JUMP AT THE ARROWS!", ["tut.great"] = "Great!", ["tut.rocks"] = "Shaking rocks! Change lane!",
            ["tut.critter"] = "Watch it move… then go!", ["tut.jumpOrSlide"] = "JUMP OVER OR SLIDE UNDER",
            ["checkpoint"] = "Checkpoint!", ["shield.used"] = "Shield saved you!",
            ["error.offline"] = "Couldn't connect right now. You can keep playing offline.",
            ["error.save"] = "Couldn't save right now — we'll try again.", ["error.generic"] = "Oops! Something went wrong. Let's try again.",
            ["error.asset"] = "Some pictures are still loading — using simple ones for now.", ["error.auth"] = "Couldn't sign in right now — you can keep playing offline.",
            ["error.corrupt"] = "We fixed your save file!", ["maintenance"] = "{0}",
        };

        private static readonly Dictionary<string, string> Da = new Dictionary<string, string>
        {
            ["title"] = "TFY KATTEEVENTYR",
            ["home.hi"] = "Hej, {0}!", ["home.play"] = "SPIL", ["home.shop"] = "KATTEBUTIK", ["home.levels"] = "BANER",
            ["home.achievements"] = "PRÆSTATIONER", ["home.rewards"] = "BELØNNINGER", ["home.settings"] = "INDSTILLINGER",
            ["welcome.new"] = "Hej, {0}! Lad os løbe!", ["welcome.back"] = "Velkommen tilbage, {0}!",
            ["loading"] = "Indlæser…", ["back"] = "TILBAGE", ["yes"] = "JA", ["no"] = "NEJ", ["close"] = "LUK",
            ["levels.title"] = "BANER", ["levels.locked"] = "Klar bane {0} for at låse op!", ["levels.comingSoon"] = "Kommer snart!", ["levels.level"] = "Bane {0}",
            ["shop.title"] = "KATTEBUTIK", ["shop.buy"] = "LÅS OP", ["shop.equip"] = "TAG PÅ", ["shop.equipped"] = "PÅ", ["shop.remove"] = "TAG AF",
            ["shop.select"] = "VÆLG", ["shop.selected"] = "VALGT", ["shop.needLevel"] = "Klar bane {0} først", ["shop.reward"] = "Særlig belønning",
            ["shop.offline"] = "Kattebutikken kræver internet. Prøv igen snart!", ["shop.notEnough"] = "Ikke nok mønter endnu — løb videre!",
            ["shop.notEnoughGems"] = "Ikke nok ædelsten endnu!", ["shop.unlocked"] = "Låst op: {0}!",
            ["shop.cat.CATS"] = "KATTE", ["shop.cat.HATS"] = "HATTE", ["shop.cat.GLASSES"] = "BRILLER", ["shop.cat.SHOES"] = "SKO",
            ["shop.cat.BACKPACKS"] = "RYGSÆKKE", ["shop.cat.CAPES_SCARVES"] = "KAPPER", ["shop.cat.TRAILS"] = "SPOR",
            ["achievements.title"] = "PRÆSTATIONER", ["achievements.done"] = "Klaret!",
            ["rewards.title"] = "BELØNNINGER", ["rewards.daily"] = "Dagens godbid", ["rewards.day"] = "Dag {0}", ["rewards.claim"] = "ÅBN",
            ["rewards.claimed"] = "Vi ses i morgen!", ["rewards.boxes"] = "Mystiske kasser: {0}", ["rewards.openBox"] = "ÅBN KASSE",
            ["rewards.noBoxes"] = "Find mystiske kasser på banerne!", ["rewards.got"] = "Du fik {0}!", ["rewards.offline"] = "Belønninger kræver internet.",
            ["settings.title"] = "INDSTILLINGER", ["settings.music"] = "Musik", ["settings.sound"] = "Lyd", ["settings.vibration"] = "Vibration",
            ["settings.graphics"] = "Grafik", ["settings.language"] = "Sprog", ["settings.help"] = "SÅDAN SPILLER DU", ["settings.parents"] = "FOR FORÆLDRE",
            ["settings.reduceMotion"] = "Rolig kamera", ["settings.buttons"] = "Knapper på skærmen",
            ["settings.speed"] = "Spilhastighed", ["speed.slow"] = "Langsom", ["speed.normal"] = "Normal", ["speed.fast"] = "Hurtig",
            ["graphics.auto"] = "Auto", ["graphics.low"] = "Lav", ["graphics.medium"] = "Mellem", ["graphics.high"] = "Høj", ["graphics.ultra"] = "Ultra",
            ["help.text"] = "TRYK — hop\nDOBBELTTRYK — hop højere\nSTRYG OP — langt hop\nSTRYG VENSTRE / HØJRE — skift bane\nSTRYG NED — glid\n\nFølg mønterne: de viser hvor du skal hoppe!\nFlag gemmer dit spil.",
            ["pause.title"] = "PAUSE", ["pause.resume"] = "FORTSÆT", ["pause.restart"] = "START FORFRA", ["pause.home"] = "HJEM",
            ["complete.title"] = "BANE KLARET!", ["complete.next"] = "NÆSTE BANE", ["complete.replay"] = "IGEN", ["complete.home"] = "HJEM",
            ["complete.score"] = "Point", ["complete.best"] = "Bedste", ["complete.newBest"] = "NY REKORD!", ["complete.unlocked"] = "Bane {0} er låst op!",
            ["fail.title"] = "Ups! Prøv igen!", ["fail.checkpoint"] = "FRA FLAGET", ["fail.restart"] = "FORFRA", ["fail.home"] = "HJEM",
            ["tut.tap"] = "TRYK FOR AT HOPPE", ["tut.double"] = "DOBBELTTRYK FOR AT HOPPE HØJERE", ["tut.left"] = "STRYG TIL VENSTRE", ["tut.right"] = "STRYG TIL HØJRE",
            ["tut.down"] = "STRYG NED FOR AT GLIDE", ["tut.long"] = "HOP VED PILENE!", ["tut.great"] = "Flot!", ["checkpoint"] = "Checkpoint!",
            ["error.offline"] = "Kunne ikke forbinde lige nu. Du kan spille videre offline.",
        };

        private static readonly Dictionary<string, Dictionary<string, string>> Tables = new Dictionary<string, Dictionary<string, string>> { ["en"] = En, ["da"] = Da };
    }
}
