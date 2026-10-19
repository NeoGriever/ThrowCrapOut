using System.Globalization;

namespace ThrowCrapOut;

public enum Language { English, German, French, Japanese }

internal static class Text
{
    internal static readonly Dictionary<string, string[]> Strings = new()
    {
        ["Armoury"] = ["Armoury", "Arsenal", "Arsenal", "アーマリーチェスト"],
        ["Settings"] = ["Settings", "Einstellungen", "Paramètres", "設定"],
        ["Language"] = ["Language", "Sprache", "Langue", "言語"],
        ["ChatInfos"] = ["Chat messages", "Chat-Infos", "Messages de chat", "チャット通知"],
        ["Delay"] = ["Delay", "Verzögerung", "Délai", "待機時間"],
        ["Idle"] = ["Idle", "Bereit", "Prêt", "待機中"],
        ["Sort"] = ["Sort", "Sortieren", "Trier", "整頓"],
        ["Split"] = ["Split", "Aufteilen", "Séparer", "分離"],
        ["Clear"] = ["Clear", "Ausräumen", "Vider", "移動"],
        ["Random"] = ["Shuffle", "Mischen", "Mélanger", "シャッフル"],
        ["SortTooltip"] = ["Sort by item level, highest first", "Nach Gegenstandsstufe absteigend sortieren", "Trier par niveau d’objet décroissant", "アイテムレベルの高い順に整頓"],
        ["SplitTooltip"] = ["Sort by item level and move gear without a gearset to the last slots", "Nach Gegenstandsstufe sortieren und Ausrüstung ohne Gearset ans Ende verschieben", "Trier par niveau d’objet et placer les objets sans tenue à la fin", "アイテムレベル順に整頓し、未登録装備を末尾に配置"],
        ["ClearTooltip"] = ["Move gear without a gearset to inventory", "Ausrüstung ohne Gearset ins Inventar verschieben", "Déplacer les objets sans tenue vers l’inventaire", "未登録装備を所持品へ移動"],
        ["Stop"] = ["Stop", "Stopp", "Arrêter", "停止"],
        ["Gearset"] = ["Gearset", "Gearset", "Tenue", "ギアセット"],
        ["NoGearset"] = ["No gearset", "Ohne Gearset", "Sans tenue", "未登録"],
        ["Free"] = ["Free", "Frei", "Libre", "空き"],
        ["Unknown"] = ["Unknown", "Unbekannt", "Inconnu", "不明"],
        ["All"] = ["All", "Alle", "Toutes", "全クラス"],
        ["ArmoryMainHand"] = ["Main-Hand", "Haupthand", "Arme principale", "メインアーム"],
        ["ArmoryOffHand"] = ["Off-Hand", "Nebenhand", "Arme secondaire", "サブアーム"],
        ["ArmoryHead"] = ["Head", "Kopf", "Tête", "頭防具"],
        ["ArmoryEar"] = ["Earrings", "Ohrringe", "Boucles d’oreilles", "耳飾り"],
        ["ArmoryBody"] = ["Chest", "Rumpf", "Torse", "胴防具"],
        ["ArmoryNeck"] = ["Necklace", "Hals", "Collier", "首飾り"],
        ["ArmoryHands"] = ["Gloves", "Hände", "Mains", "手防具"],
        ["ArmoryWrist"] = ["Armband", "Handgelenke", "Bracelets", "腕輪"],
        ["ArmoryLegs"] = ["Legs", "Beine", "Jambes", "脚防具"],
        ["ArmoryRings"] = ["Rings", "Ringe", "Bagues", "指輪"],
        ["ArmoryFeets"] = ["Shoes", "Füße", "Pieds", "足防具"],
        ["ArmorySoulCrystal"] = ["Job-Crystals", "Jobkristalle", "Cristaux de job", "ソウルクリスタル"],
        ["Ready"] = ["Open /tco to view your armoury.", "Arsenal mit /tco öffnen.", "Ouvrez l’arsenal avec /tco.", "/tco でアーマリーチェストを表示します。"],
        ["LoggedOut"] = ["Not logged in.", "Nicht eingeloggt.", "Non connecté.", "ログインしていません。"],
        ["CharacterChanged"] = ["Character changed; refreshing.", "Charakter gewechselt; aktualisiere.", "Personnage changé ; actualisation.", "キャラクター変更。更新中。"],
        ["ScanUnavailable"] = ["Armoury unavailable; retrying automatically.", "Arsenal nicht verfügbar; erneuter Versuch folgt.", "Arsenal indisponible ; nouvelle tentative automatique.", "アーマリーチェストを取得できません。自動で再試行します。"],
        ["InventoryUnavailable"] = ["Inventory unavailable.", "Inventar nicht verfügbar.", "Inventaire indisponible.", "所持品を取得できません。"],
        ["NotLoaded"] = ["Not loaded", "Nicht geladen", "Non chargé", "未読み込み"],
        ["InvalidData"] = ["Invalid slot data", "Ungültige Slot-Daten", "Données invalides", "スロットデータが無効です"],
        ["UnreadableSlot"] = ["Unreadable slot", "Slot nicht lesbar", "Emplacement illisible", "読み取れないスロット"],
        ["LinkedItem"] = ["Linked item", "Verknüpfter Eintrag", "Objet lié", "参照アイテム"],
        ["UnknownItem"] = ["Unknown item {0}", "Unbekannter Gegenstand {0}", "Objet inconnu {0}", "不明なアイテム {0}"],
        ["GearsetUnavailable"] = ["Gearset check unavailable.", "Gearset-Prüfung nicht verfügbar.", "Vérification des tenues indisponible.", "ギアセットを確認できません。"],
        ["Busy"] = ["An action is already running.", "Es läuft bereits eine Aktion.", "Une action est déjà en cours.", "別の操作を実行中です。"],
        ["Scanning"] = ["{0}: refreshing armoury…", "{0}: Arsenal wird erfasst …", "{0} : actualisation de l’arsenal…", "{0}：アーマリーチェスト更新中…"],
        ["Stopping"] = ["Stopping after the current step.", "Abbruch nach dem laufenden Schritt.", "Arrêt après l’étape en cours.", "現在の処理が完了したら停止します。"],
        ["ActionResult"] = ["{0}: {1} ({2}/{3})", "{0}: {1} ({2}/{3})", "{0} : {1} ({2}/{3})", "{0}：{1}（{2}/{3}）"],
        ["Working"] = ["{0}: {1}/{2}", "{0}: {1}/{2}", "{0} : {1}/{2}", "{0}： {1}/{2}"],
        ["Progress"] = ["{0}: {1}/{2}", "{0}: {1}/{2}", "{0} : {1}/{2}", "{0}：{1}/{2}"],
        ["Incomplete"] = ["{0}: incomplete data.", "{0}: Daten unvollständig.", "{0} : données incomplètes.", "{0}：データが不完全です。"],
        ["OrderUnavailable"] = ["Slot order unavailable or sorting in progress.", "Slot-Reihenfolge nicht verfügbar oder Sortierung aktiv.", "Ordre des emplacements indisponible ou tri en cours.", "スロット順序を取得できないか、整頓中です。"],
        ["SwapFailed"] = ["Swap not confirmed. Stopped; check your armoury.", "Tausch nicht bestätigt. Gestoppt; bitte Arsenal prüfen.", "Échange non confirmé. Arrêt ; vérifiez l’arsenal.", "入れ替えを確認できません。停止しました。装備を確認してください。"],
        ["Canceled"] = ["Canceled.", "Abgebrochen.", "Annulé.", "中止しました。"],
        ["Complete"] = ["Complete.", "Abgeschlossen.", "Terminé.", "完了しました。"],
        ["InventoryBusy"] = ["Inventory busy. Try again shortly.", "Inventar beschäftigt. Bitte gleich erneut versuchen.", "Inventaire occupé. Réessayez dans un instant.", "所持品を処理中です。しばらくしてから再試行してください。"],
        ["Stale"] = ["Items changed. Restart the action.", "Gegenstände verändert. Aktion neu starten.", "Les objets ont changé. Relancez l’action.", "アイテムが変更されました。操作を再実行してください。"],
        ["GearsetChanged"] = ["Gearset assignment changed. Restart Split.", "Gearset-Zuordnung verändert. Aufteilen neu starten.", "Affectation de tenue modifiée. Relancez la séparation.", "ギアセット登録が変更されました。分離を再実行してください。"],
        ["Checking"] = ["Checking {0}…", "Prüfe {0} …", "Vérification de {0}…", "{0} を確認中…"],
        ["Moved"] = ["Moved {0} ({1} / IL: {2}) to Inventory", "{0} ({1} / IL: {2}) ins Inventar verschoben", "{0} ({1} / Niv. {2}) déplacé dans l’inventaire", "{0}（{1} / IL: {2}）を所持品に移動しました"],
        ["Level"] = ["IL: {0}", "IL: {0}", "Niv. {0}", "IL: {0}"],
        ["MoveFailed"] = ["Move not confirmed. Check inventory; see Dalamud log for details.", "Verschieben nicht bestätigt. Inventar prüfen; Details im Dalamud-Log.", "Déplacement non confirmé. Vérifiez l’inventaire et le journal Dalamud.", "移動を確認できません。所持品とDalamudログを確認してください。"],
        ["Disabled"] = ["Category disabled; nothing moved.", "Kategorie deaktiviert; nichts verschoben.", "Catégorie désactivée ; aucun déplacement.", "無効なカテゴリのため移動しませんでした。"],
        ["GearsetProtected"] = ["Item belongs to a gearset or could not be checked.", "Gegenstand gehört zu einem Gearset oder konnte nicht geprüft werden.", "Objet lié à une tenue ou vérification impossible.", "ギアセット登録済み、または確認できないアイテムです。"],
        ["InventoryFull"] = ["No free slot in the regular inventory.", "Kein freier Slot im normalen Inventar.", "Aucun emplacement libre dans l’inventaire normal.", "所持品に空きがありません。"],
        ["Waiting"] = ["Moving {0}…", "Verschiebe {0} …", "Déplacement de {0}…", "{0} を移動中…"],
        ["DisplayMismatch"] = ["{0} moved, but its display position differs from the target.", "{0} verschoben, aber die Anzeigeposition weicht vom Ziel ab.", "{0} déplacé, mais sa position affichée diffère de la cible.", "{0} は移動しましたが、表示位置が指定と異なります。"],
    };

    public static string Get(Language language, string key, params object[] args)
    {
        var index = Enum.IsDefined(language) ? (int)language : 0;
        var value = Strings.TryGetValue(key, out var translations) ? translations[index] : key;
        return args.Length == 0 ? value : string.Format(CultureInfo.InvariantCulture, value, args);
    }
}
