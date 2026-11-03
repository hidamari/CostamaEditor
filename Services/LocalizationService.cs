using System.Globalization;
using System.Windows;

namespace CostamaEditor.Services;

public static class LocalizationService
{
    private sealed record Text(string Chinese, string English, string Japanese);

    private static readonly IReadOnlyDictionary<string, Text> Texts = new Dictionary<string, Text>(StringComparer.Ordinal)
    {
        ["Language.Label"] = new("語言", "Language", "言語"),
        ["Main.Subtitle"] = new("Auriga 道具目錄 · ItemInfo · GRF 圖示來源檢視", "Auriga item catalog · ItemInfo · GRF icon viewer", "Auriga アイテム一覧 · ItemInfo · GRF アイコンビューア"),
        ["Main.Reload"] = new("重新載入來源", "Reload sources", "ソースを再読み込み"),
        ["Main.Db.Title"] = new("AURIGA ITEM DB", "AURIGA ITEM DB", "AURIGA ITEM DB"),
        ["Main.Db.Priority"] = new("後載入的定義覆蓋前者", "Later definitions override earlier ones", "後から読み込んだ定義が優先されます"),
        ["Main.ItemInfo.Title"] = new("ITEMINFO", "ITEMINFO", "ITEMINFO"),
        ["Main.Grf.Title"] = new("GRF 圖示來源", "GRF icon sources", "GRF アイコンソース"),
        ["Main.Source.Priority"] = new("上方檔案優先", "Files above have priority", "上のファイルが優先"),
        ["Source.Add"] = new("＋ 加入", "＋ Add", "＋ 追加"),
        ["Source.Remove"] = new("移除", "Remove", "削除"),
        ["Source.MoveUp"] = new("提高優先順序", "Move up in priority", "優先順位を上げる"),
        ["Source.MoveDown"] = new("降低優先順序", "Move down in priority", "優先順位を下げる"),
        ["Main.Catalog.Title"] = new("道具目錄", "Item catalog", "アイテム一覧"),
        ["Main.Catalog.Subtitle"] = new("以 Auriga item DB 為伺服器道具來源", "Auriga item DB is the source of server items", "サーバーアイテムは Auriga item DB から読み込みます"),
        ["Main.Search.Tooltip"] = new("搜尋 ID、英文名稱、日文名稱或 ItemInfo resource name", "Search by ID, English name, Japanese name, or ItemInfo resource name", "ID、英語名、日本語名、ItemInfo resource name で検索"),
        ["Main.NewItem"] = new("新增道具", "New item", "アイテムを追加"),
        ["Main.EditItem"] = new("編輯選取項目", "Edit selected item", "選択したアイテムを編集"),
        ["Main.Costama"] = new("Costama 獎池", "Costama pool", "コスたま抽選設定"),
        ["Grid.Id"] = new("ID", "ID", "ID"),
        ["Grid.Name"] = new("道具名稱", "Item name", "アイテム名"),
        ["Grid.Category"] = new("分類", "Category", "分類"),
        ["Grid.JapaneseName"] = new("日文名稱", "Japanese name", "日本語名"),
        ["Grid.Type"] = new("類型", "Type", "タイプ"),
        ["Grid.DefinitionSource"] = new("有效定義來源", "Effective definition source", "有効な定義元"),
        ["Grid.ResourceName"] = new("Resource Name", "Resource name", "Resource name"),
        ["Main.IconPlaceholder"] = new("圖示", "ICON", "アイコン"),
        ["Main.IdLabel"] = new("ID", "ID", "ID"),
        ["Main.CategoryLabel"] = new("分類：", "Category: ", "分類："),
        ["Main.TypeLabel"] = new("類型：", "Type: ", "タイプ："),
        ["Main.ResourceLabel"] = new("Resource：", "Resource: ", "Resource: "),
        ["Main.SourceTooltip"] = new("有效定義來源", "Effective definition source", "有効な定義元"),
        ["Main.HistoryTooltip"] = new("ID 重複定義的來源歷程", "History of duplicate ID definitions", "ID 重複定義の履歴"),
        ["Main.HistoryNone"] = new("無重複 ID 定義", "No duplicate ID definitions", "重複する ID 定義はありません"),
        ["Main.HistorySome"] = new("曾由以下來源定義：{0}", "Previously defined by: {0}", "以前の定義元：{0}"),
        ["Main.Status.Ready"] = new("來源已就緒；按「重新載入來源」建立道具目錄。", "Sources are ready. Select Reload sources to build the item catalog.", "ソースの準備ができました。「ソースを再読み込み」を押して一覧を作成してください。"),
        ["Main.Status.Loading"] = new("正在讀取 item DB、ItemInfo 與 GRF…", "Loading item DB, ItemInfo, and GRF…", "item DB、ItemInfo、GRF を読み込み中…"),
        ["Main.Status.Loaded"] = new("目錄已載入：{0} 個 item DB、{1} 個 ItemInfo、{2} 個 GRF。", "Catalog loaded: {0} item DB, {1} ItemInfo, {2} GRF source(s).", "一覧を読み込みました：item DB {0} 件、ItemInfo {1} 件、GRF {2} 件。"),
        ["Main.Status.LoadFailed"] = new("載入失敗：{0}", "Load failed: {0}", "読み込みに失敗しました：{0}"),
        ["Main.Status.LanguageChanged"] = new("語系已切換；設定已保存。", "Language changed; preference saved.", "言語を切り替え、設定を保存しました。"),
        ["Main.Status.LanguageSaveFailed"] = new("語系已切換，但設定未儲存：{0}", "Language changed, but the preference was not saved: {0}", "言語を切り替えましたが、設定を保存できませんでした：{0}"),
        ["Main.Status.SourceChanged"] = new("來源順序已更新；按「重新載入來源」套用。", "Source order updated. Select Reload sources to apply it.", "ソース順を更新しました。「ソースを再読み込み」を押して適用してください。"),
        ["Main.Status.SourceSaveFailed"] = new("來源順序已更新，但設定未儲存：{0}", "Source order updated, but settings were not saved: {0}", "ソース順を更新しましたが、設定を保存できませんでした：{0}"),
        ["Main.Status.LoadedSaveFailed"] = new("目錄已載入：{0} 個 item DB、{1} 個 ItemInfo、{2} 個 GRF。設定未儲存：{3}", "Catalog loaded: {0} item DB, {1} ItemInfo, {2} GRF source(s). Settings were not saved: {3}", "一覧を読み込みました：item DB {0} 件、ItemInfo {1} 件、GRF {2} 件。設定を保存できませんでした：{3}"),
        ["Main.Results.NotLoaded"] = new("尚未載入", "Not loaded", "未読み込み"),
        ["Main.Results.Loading"] = new("載入中", "Loading", "読み込み中"),
        ["Main.Results.LoadFailed"] = new("載入失敗", "Load failed", "読み込み失敗"),
        ["Main.Results.Count"] = new("{0:N0} 筆道具", "{0:N0} items", "{0:N0} 件のアイテム"),
        ["Main.Results.Visible"] = new("顯示 {0:N0} / {1:N0} 筆", "Showing {0:N0} / {1:N0}", "{0:N0} / {1:N0} 件を表示"),
        ["Main.Preview.Select"] = new("選取道具後顯示 ItemInfo 與 GRF 圖示來源。", "Select an item to view its ItemInfo and GRF icon source.", "アイテムを選択すると ItemInfo と GRF アイコンの情報を表示します。"),
        ["Main.Preview.NoResource"] = new("ItemInfo 找不到此 ID 的 identifiedResourceName。", "ItemInfo has no identifiedResourceName for this ID.", "この ID の identifiedResourceName が ItemInfo にありません。"),
        ["Main.Preview.NoGrf"] = new("ItemInfo：{0}（{1}）；尚未載入 GRF。", "ItemInfo: {0} ({1}); no GRF is loaded.", "ItemInfo：{0}（{1}）；GRF が読み込まれていません。"),
        ["Main.Preview.Searching"] = new("正在查找 {0}.bmp…", "Looking for {0}.bmp…", "{0}.bmp を検索中…"),
        ["Main.Preview.IconError"] = new("ItemInfo：{0}（{1}）；{2}", "ItemInfo: {0} ({1}); {2}", "ItemInfo：{0}（{1}）；{2}"),
        ["Main.Preview.IconFound"] = new("ItemInfo：{0}　·　圖示：{1}\\{2}", "ItemInfo: {0}　·　Icon: {1}\\{2}", "ItemInfo：{0}　·　アイコン：{1}\\{2}"),
        ["Main.Preview.DisplayFailed"] = new("找到圖檔，但 WPF 無法顯示：{0}", "Image found, but WPF could not display it: {0}", "画像は見つかりましたが、WPF で表示できません：{0}"),
        ["Main.Dialog.Reload.Title"] = new("需要重新載入", "Reload required", "再読み込みが必要です"),
        ["Main.Dialog.Reload.Grf"] = new("請先重新載入來源，讓道具目錄與圖示來源保持一致。", "Reload sources first so the item catalog and icon sources are in sync.", "アイテム一覧とアイコンソースを同期するため、先にソースを再読み込みしてください。"),
        ["Main.Dialog.Reload.Catalog"] = new("來源清單已變更或尚未載入。請先按「重新載入來源」更新目錄，再編輯或新增道具。", "Sources changed or are not loaded. Select Reload sources before editing or adding an item.", "ソース一覧が変更されたか、未読み込みです。アイテムの編集・追加前に「ソースを再読み込み」を押してください。"),
        ["Main.Dialog.NoTarget.Title"] = new("沒有寫入目標", "No write target", "書き込み先がありません"),
        ["Main.Dialog.NoTarget"] = new("請先加入至少一個 item DB 寫入目標。", "Add at least one item DB as a write target first.", "書き込み先の item DB を 1 つ以上追加してください。"),
        ["Main.FileFilter.All"] = new("所有檔案", "All files", "すべてのファイル"),
        ["Main.FileFilter.ItemDb"] = new("Auriga item DB", "Auriga item DB", "Auriga item DB"),
        ["Main.FileFilter.ItemInfo"] = new("ItemInfo", "ItemInfo", "ItemInfo"),
        ["Main.FileFilter.Grf"] = new("GRF 封存檔", "GRF archive", "GRF アーカイブ"),
        ["Pool.Title"] = new("Costama 獎池", "Costama pool", "コスたま抽選設定"),
        ["Pool.Intro"] = new("獎品只從 Auriga item DB 目錄選取；輸入整數權重，總和需為上限值。", "Choose prizes from the Auriga item DB catalog. Enter integer weights that add up to the upper limit.", "賞品は Auriga item DB から選択します。整数の重みを入力し、合計を上限値に合わせてください。"),
        ["Pool.Candidates.Title"] = new("道具目錄", "Item catalog", "アイテム一覧"),
        ["Pool.Candidates.Hint"] = new("依 ID 或 Jname 搜尋；Shift/Ctrl 多選可一次加入", "Search by ID or Jname; use Shift/Ctrl to select multiple items", "ID または Jname で検索。Shift/Ctrl で複数選択できます"),
        ["Pool.Candidates.SearchTooltip"] = new("使用半形空白分隔多個搜尋詞；符合任一詞的道具會顯示。", "Separate search terms with spaces; items matching any term are shown.", "半角スペースで検索語を区切ると、いずれかに一致するアイテムを表示します。"),
        ["Pool.Candidates.Count"] = new("顯示 {0:N0} / {1:N0} 個可加入道具", "Showing {0:N0} / {1:N0} available items", "追加可能なアイテム {0:N0} / {1:N0} 件を表示"),
        ["Pool.Candidates.Add"] = new("加入選取項目 →", "Add selected →", "選択項目を追加 →"),
        ["Pool.Rewards.Title"] = new("獎池內容", "Pool contents", "抽選内容"),
        ["Pool.Rewards.Hint"] = new("可多選後按 Delete 刪除；權重與數量可直接輸入", "Select rows and press Delete to remove; edit weights and quantities directly", "複数選択して Delete で削除できます。重みと数量は直接入力できます"),
        ["Pool.Target"] = new("寫入目標", "Write target", "書き込み先"),
        ["Pool.MaxValue"] = new("抽選上限", "Roll range", "抽選上限"),
        ["Pool.MaxValue.Tooltip"] = new("輸入 1–16,777,216 的正整數；獎品權重總和需等於此值。", "Enter a whole number from 1 to 16,777,216; prize weights must add up to this value.", "1～16,777,216 の整数を入力します。賞品の重みの合計をこの値に合わせてください。"),
        ["Pool.TotalWeight"] = new("總和 {0} / {1}", "Total {0} / {1}", "合計 {0} / {1}"),
        ["Pool.Icon"] = new("圖示", "Icon", "アイコン"),
        ["Pool.Prize"] = new("獎品", "Prize", "賞品"),
        ["Pool.Weight"] = new("權重", "Weight", "重み"),
        ["Pool.Quantity"] = new("數量", "Quantity", "数量"),
        ["Pool.Weight.Tooltip"] = new("輸入正整數權重；所有獎品權重總和必須等於抽選上限。", "Enter a positive integer weight; all prize weights must add up to the roll range.", "正の整数の重みを入力します。すべての賞品の重みの合計を抽選上限に合わせてください。"),
        ["Pool.Quantity.Tooltip"] = new("輸入正整數數量", "Enter a positive whole-number quantity", "正の整数を入力"),
        ["Pool.Remove"] = new("移除選取", "Remove selected", "選択項目を削除"),
        ["Pool.Script.Title"] = new("Auriga UseScript 預覽", "Auriga UseScript preview", "Auriga UseScript プレビュー"),
        ["Pool.Script.Hint"] = new("rand(1,上限) · 依獎池順序累積整數權重", "rand(1,range) · cumulative integer weights follow pool order", "rand(1,上限) · 抽選順に整数の重みを累積"),
        ["Pool.Apply"] = new("預覽寫入並編輯", "Preview and edit write", "書き込みをプレビューして編集"),
        ["Pool.Close"] = new("關閉", "Close", "閉じる"),
        ["Pool.MainSummary"] = new("{0} · ID {1} · 有效來源：{2}", "{0} · ID {1} · Source: {2}", "{0} · ID {1} · 有効な定義元：{2}"),
        ["Pool.MissingItem"] = new("未載入的道具 (ID {0})", "Unloaded item (ID {0})", "未読み込みのアイテム (ID {0})"),
        ["Pool.Status.Initial"] = new("輸入整數權重；獎池可清空，或所有權重總和等於抽選上限。", "Enter integer weights; the pool can be empty or the total must equal the roll range.", "整数の重みを入力します。抽選内容は空、または合計を抽選上限に合わせてください。"),
        ["Pool.Status.EmptyPreview"] = new("有效獎池的 UseScript 會顯示在此處。", "A valid pool's UseScript appears here.", "有効な抽選設定の UseScript をここに表示します。"),
        ["Pool.Status.InvalidScript"] = new("此 Costama 的 UseScript 格式無法拆解為獎池，為避免覆寫原腳本，請先用「編輯選取項目」處理。", "This Costama's UseScript format cannot be parsed. To protect the original script, edit it with Edit selected item first.", "この Costama の UseScript 形式を解析できません。元のスクリプトを保護するため、先に「選択したアイテムを編集」で編集してください。"),
        ["Pool.Status.LoadedExisting"] = new("已從既有 UseScript 載入 {0} 項獎品；可直接修改權重與數量。", "Loaded {0} prizes from the existing UseScript. Edit weights and quantities directly.", "既存の UseScript から賞品を {0} 件読み込みました。重みと数量を直接編集できます。"),
        ["Pool.Status.SelectCandidate"] = new("請先在左側選取一項或多項伺服器道具。", "Select one or more server items on the left first.", "左側でサーバーアイテムを 1 つ以上選択してください。"),
        ["Pool.Status.MaxItems"] = new("獎池最多 100 項獎品；請減少這次選取數量。", "A pool can contain at most 100 prizes. Select fewer items.", "賞品は最大 100 件です。選択数を減らしてください。"),
        ["Pool.Status.InvalidMaxValue"] = new("抽選上限需為 1–16,777,216 的正整數。", "The roll range must be a whole number from 1 to 16,777,216.", "抽選上限は 1～16,777,216 の整数にしてください。"),
        ["Pool.Status.MaxBelowItemCount"] = new("抽選上限小於已選獎品數；請提高上限。", "The roll range is lower than the number of selected prizes. Increase it.", "抽選上限が選択した賞品数より小さいため、上限を増やしてください。"),
        ["Pool.Status.Added"] = new("已加入 {0} 項獎品；請確認權重與數量。", "Added {0} prizes. Check their weights and quantities.", "賞品を {0} 件追加しました。重みと数量を確認してください。"),
        ["Pool.Status.Removed"] = new("已移除 {0} 項獎品，並按原比例重分配權重至抽選上限。", "Removed {0} prizes and redistributed the remaining weights proportionally to the roll range.", "賞品を {0} 件削除し、残りの重みを元の比率に応じて抽選上限まで再配分しました。"),
        ["Pool.Status.EmptyAfterRemoval"] = new("獎池已清空；預覽寫入時會清除 UseScript。", "The pool is empty; previewing the write will clear UseScript.", "抽選内容は空です。書き込みをプレビューすると UseScript を消去します。"),
        ["Pool.Status.EmptyPool"] = new("目前沒有獎品；可以直接預覽並清除 UseScript。", "There are no prizes; preview the write to clear UseScript.", "賞品はありません。書き込みをプレビューして UseScript を消去できます。"),
        ["Pool.Validation.Empty"] = new("獎池已清空；總和為 0，儲存時會清除 UseScript。", "Pool is empty with total 0; saving will clear UseScript.", "抽選内容は空で合計 0 です。保存すると UseScript を消去します。"),
        ["Pool.Validation.BadMaxValue"] = new("抽選上限需為 1–16,777,216 的正整數。", "The roll range must be a whole number from 1 to 16,777,216.", "抽選上限は 1～16,777,216 の整数にしてください。"),
        ["Pool.Validation.BadWeight"] = new("總和 {0} / {1} · 權重需為 1 到上限的正整數。", "Total {0} / {1} · weights must be positive integers no greater than the range.", "合計 {0} / {1} · 重みは上限以下の正の整数にしてください。"),
        ["Pool.Validation.BadQuantity"] = new("總和 {0} / {1} · 數量需為正整數。", "Total {0} / {1} · quantities must be positive integers.", "合計 {0} / {1} · 数量は正の整数にしてください。"),
        ["Pool.Validation.NotMax"] = new("總和 {0} / 上限 {1} · 有獎品時總和必須等於上限。", "Total {0} / range {1} · with prizes, the total must equal the range.", "合計 {0} / 上限 {1} · 賞品がある場合、合計を上限に合わせてください。"),
        ["Pool.Validation.Valid"] = new("總和 {0} / 上限 {1} · {2} 項獎品 · 數量有效。", "Total {0} / range {1} · {2} prizes · quantities are valid.", "合計 {0} / 上限 {1} · 賞品 {2} 件 · 数量は有効です。"),
        ["Pool.Preview.Fix"] = new("請先修正抽選上限、權重與數量欄位。", "Fix the roll range, weights, and quantities first.", "抽選上限、重み、数量を先に修正してください。"),
        ["Pool.Preview.EmptyPool"] = new("UseScript 將保持空白。", "UseScript will be left empty.", "UseScript は空欄になります。"),
        ["Pool.Preview.NeedMax"] = new("權重總和等於抽選上限後會產生 UseScript。", "The UseScript is generated when the weight total equals the roll range.", "重みの合計が抽選上限になると UseScript を生成します。"),
        ["Pool.Icon.NoResource"] = new("ItemInfo 找不到 identifiedResourceName。", "ItemInfo has no identifiedResourceName.", "identifiedResourceName が ItemInfo にありません。"),
        ["Pool.Icon.NotFound"] = new("GRF 圖示不存在。", "Icon not found in GRF.", "GRF にアイコンがありません。"),
        ["Pool.Icon.NotLoaded"] = new("尚未載入圖示。", "Icon not loaded yet.", "アイコンはまだ読み込まれていません。"),
        ["Pool.Icon.DisplayFailed"] = new("圖檔已找到但無法顯示：{0}", "Image found but could not be displayed: {0}", "画像は見つかりましたが表示できません：{0}"),
        ["Pool.Status.TargetRequired"] = new("請指定 UseScript 寫入目標 item DB。", "Select an item DB target for the UseScript.", "UseScript の書き込み先 item DB を選択してください。"),
        ["Editor.Subtitle"] = new("編輯 Auriga 18 欄位資料與 Use／Equip／Unequip scripts。儲存前必須先產生預覽。", "Edit Auriga's 18 item fields and Use/Equip/Unequip scripts. Preview changes before saving.", "Auriga の 18 項目と Use／Equip／Unequip スクリプトを編集します。保存前にプレビューしてください。"),
        ["Editor.Target"] = new("寫入目標 item DB", "Target item DB", "書き込み先 item DB"),
        ["Editor.Fields"] = new("資料欄位", "Item fields", "アイテム項目"),
        ["Editor.Scripts"] = new("Scripts", "Scripts", "スクリプト"),
        ["Editor.Preview"] = new("變更預覽", "Change preview", "変更プレビュー"),
        ["Editor.Preview.Unpreviewed"] = new("尚未預覽", "Not previewed", "未プレビュー"),
        ["Editor.Cancel"] = new("取消", "Cancel", "キャンセル"),
        ["Editor.Save"] = new("備份並儲存", "Back up and save", "バックアップして保存"),
        ["Editor.GeneratePreview"] = new("產生預覽", "Generate preview", "プレビューを作成"),
        ["Editor.ConflictCheck"] = new("我已確認這筆 ID 會形成覆寫定義", "I confirm this ID will create an overriding definition", "この ID が上書き定義になることを確認しました"),
        ["Editor.Field.Id"] = new("ID *", "ID *", "ID *"),
        ["Editor.Field.Name"] = new("Name / Category *", "Name / Category *", "Name / Category *"),
        ["Editor.Field.Jname"] = new("Jname *", "Jname *", "Jname *"),
        ["Editor.Field.Type"] = new("Type *", "Type *", "Type *"),
        ["Editor.Field.Price"] = new("Price", "Price", "価格"),
        ["Editor.Field.Sell"] = new("Sell", "Sell", "売値"),
        ["Editor.Field.Weight"] = new("Weight", "Weight", "重量"),
        ["Editor.Field.ATK"] = new("ATK", "ATK", "ATK"),
        ["Editor.Field.DEF"] = new("DEF", "DEF", "DEF"),
        ["Editor.Field.Range"] = new("Range", "Range", "射程"),
        ["Editor.Field.Slot"] = new("Slot", "Slot", "スロット"),
        ["Editor.Field.Job"] = new("Job", "Job", "職業"),
        ["Editor.Field.Gender"] = new("Gender", "Gender", "性別"),
        ["Editor.Field.Loc"] = new("Loc", "Loc", "装備位置"),
        ["Editor.Field.WLv"] = new("wLV", "wLV", "wLV"),
        ["Editor.Field.ELv"] = new("eLV", "eLV", "eLV"),
        ["Editor.Field.View"] = new("View", "View", "View"),
        ["Editor.Field.Refine"] = new("Refine", "Refine", "精錬"),
        ["Editor.Status.Initial"] = new("必填欄位：ID、Name、Jname、Type。新增道具的 ID 由使用者指定。", "Required fields: ID, Name, Jname, and Type. Specify the ID for a new item.", "必須項目：ID、Name、Jname、Type。新規アイテムの ID は自分で指定してください。"),
        ["Editor.Title.New"] = new("新增道具", "New item", "アイテムを追加"),
        ["Editor.Title.Edit"] = new("編輯道具 · {0} · ID {1}", "Edit item · {0} · ID {1}", "アイテムを編集 · {0} · ID {1}"),
        ["Editor.Preview.Initial"] = new("修改欄位後按「產生預覽」。此步驟不會寫入檔案。", "Edit the fields and select Generate preview. This step does not write to disk.", "項目を編集して「プレビューを作成」を押してください。この操作ではファイルに書き込みません。"),
        ["Editor.Preview.Conflict"] = new("ID {0} 已由 {1} 定義；此寫入會新增或取代一筆覆寫定義。", "ID {0} is already defined by {1}; this will add or replace an overriding definition.", "ID {0} は {1} で定義済みです。この書き込みは上書き定義を追加または置換します。"),
        ["Editor.Status.TargetRequired"] = new("請先指定寫入目標 item DB。", "Select a target item DB first.", "書き込み先 item DB を指定してください。"),
        ["Editor.Status.OrderBlocked"] = new("目標順序較早，後續定義會蓋過這筆變更。", "The target loads earlier, so a later definition would override this change.", "書き込み先の読み込み順が早いため、後続の定義に上書きされます。"),
        ["Editor.Status.OrderBlockedDetail"] = new("請改選有效定義來源，或調整 item DB 優先順序後重新載入。", "Choose the effective definition source or change item DB priority and reload.", "有効な定義元を選ぶか、item DB の優先順位を変更して再読み込みしてください。"),
        ["Editor.Status.Conflict"] = new("ID {0} 已由 {1} 定義；此寫入會新增或取代一筆覆寫定義。", "ID {0} is already defined by {1}; this will add or replace an overriding definition.", "ID {0} は {1} で定義済みです。この書き込みは上書き定義を追加または置換します。"),
        ["Editor.Status.ConflictDetail"] = new("確認覆寫來源後，勾選下方核取方塊以啟用儲存。", "Confirm the override source and check the box below to enable saving.", "上書き元を確認し、下のチェックボックスを選択すると保存できます。"),
        ["Editor.Status.PreviewReady"] = new("預覽完成；原檔尚未修改。", "Preview ready; the original file has not been changed.", "プレビューができました。元ファイルはまだ変更されていません。"),
        ["Editor.Status.PreviewReadyDetail"] = new("確認目標檔案與新行後，可備份並儲存。", "Review the target file and new line, then back up and save.", "対象ファイルと新しい行を確認してから、バックアップして保存してください。"),
        ["Editor.Status.PreviewFailed"] = new("預覽失敗", "Preview failed", "プレビューに失敗しました"),
        ["Editor.Status.FieldsChanged"] = new("欄位已變更，請重新產生預覽。", "Fields changed. Generate the preview again.", "項目が変更されました。もう一度プレビューしてください。"),
        ["Editor.Status.Saved"] = new("已儲存；備份：{0}", "Saved; backup: {0}", "保存しました。バックアップ：{0}"),
        ["Editor.Message.Saved"] = new("資料已寫入。\n備份檔：{0}", "Data was written.\nBackup: {0}", "データを書き込みました。\nバックアップ：{0}"),
        ["Editor.Message.SavedTitle"] = new("儲存完成", "Save complete", "保存完了"),
        ["Editor.Status.SaveFailed"] = new("儲存失敗", "Save failed", "保存に失敗しました"),
        ["Editor.Preview.Target"] = new("目標：{0}", "Target: {0}", "対象：{0}"),
        ["Editor.Preview.Operation"] = new("操作：{0}", "Operation: {0}", "操作：{0}"),
        ["Editor.Preview.Add"] = new("新增定義", "Add definition", "定義を追加"),
        ["Editor.Preview.Replace"] = new("取代目標檔案中的既有定義", "Replace existing definition in target file", "対象ファイルの既存定義を置換"),
        ["Editor.Preview.Id"] = new("ID：{0}", "ID: {0}", "ID：{0}"),
        ["Editor.Preview.NoOriginal"] = new("（目標檔案沒有此 ID，會在檔尾新增）", "(ID not found in target; it will be appended)", "（対象ファイルに ID がないため、末尾に追加します）"),
        ["Editor.Preview.Original"] = new("原始行：", "Original line:", "元の行："),
        ["Editor.Preview.New"] = new("新行：", "New line:", "新しい行："),
        ["Editor.Operation.Add"] = new("新增定義", "Add definition", "定義を追加"),
        ["Editor.Operation.Replace"] = new("取代目標檔案中的既有定義", "Replace existing definition in target file", "対象ファイルの既存定義を置換"),
        ["Writer.FieldCount"] = new("item DB 必須有 {0} 個欄位。", "item DB must contain {0} fields.", "item DB には項目が {0} 個必要です。"),
        ["Writer.InvalidId"] = new("道具 ID 必須是正整數。", "Item ID must be a positive integer.", "アイテム ID は正の整数にしてください。"),
        ["Writer.DuplicateId"] = new("目標檔案有 {0} 筆 ID {1}；為避免覆寫錯誤，請先整理重複定義。", "The target has {0} definitions for ID {1}. Resolve duplicates before writing.", "対象ファイルに ID {1} の定義が {0} 件あります。重複を整理してから書き込んでください。"),
        ["Writer.LineTooLong"] = new("這一行超過 Auriga item DB 讀取緩衝區的 4095 bytes 限制。", "This line exceeds Auriga item DB's 4095-byte read buffer limit.", "この行は Auriga item DB の読み込みバッファ上限 4095 bytes を超えています。"),
        ["Writer.NullField"] = new("欄位 {0} 不可為 null；未填欄位請使用空字串。", "Field {0} cannot be null; use an empty string for a blank field.", "項目 {0} は null にできません。空欄には空文字列を指定してください。"),
        ["Writer.InvalidField"] = new("欄位 {0} 不能包含逗號或換行。", "Field {0} cannot contain commas or line breaks.", "項目 {0} にカンマや改行を含めることはできません。"),
        ["Writer.EmptyName"] = new("道具名稱不可空白。", "Item name cannot be blank.", "アイテム名を空欄にはできません。"),
        ["Writer.EmptyJname"] = new("日文名稱不可空白。", "Jname cannot be blank.", "Jname を空欄にはできません。"),
        ["Writer.InvalidType"] = new("Type 必須是整數。", "Type must be an integer.", "Type は整数にしてください。"),
        ["Writer.ScriptLine"] = new("Auriga item DB 的腳本必須留在同一行。", "Auriga item DB scripts must stay on one line.", "Auriga item DB のスクリプトは 1 行に収めてください。"),
        ["Writer.ScriptBraces"] = new("第 {0} 個腳本必須以 {{ 開始並以 }} 結束。", "Script {0} must start with {{ and end with }}.", "スクリプト {0} は {{ で始まり }} で終わる必要があります。"),
        ["Writer.PreviewChanged"] = new("預覽後目標檔案已變更；請重新載入並產生新的預覽。", "The target file changed after preview. Reload it and generate a new preview.", "プレビュー後に対象ファイルが変更されました。再読み込みしてプレビューを作り直してください。"),
        ["Writer.BackupChanged"] = new("備份建立期間目標檔案發生變化；沒有套用預覽內容，備份已保留供檢查。", "The target changed while the backup was being created. The preview was not applied; the backup was kept for inspection.", "バックアップ作成中に対象ファイルが変更されました。プレビューは適用されず、確認用にバックアップを残しました。"),
        ["Writer.SaveChanged"] = new("儲存前目標檔案再次變更；沒有套用預覽內容，備份已保留供檢查。", "The target changed again before saving. The preview was not applied; the backup was kept for inspection.", "保存前に対象ファイルが再度変更されました。プレビューは適用されず、確認用にバックアップを残しました。"),
        ["Writer.TargetMissing"] = new("目標 item DB 不存在。", "The target item DB does not exist.", "対象の item DB がありません。"),
        ["Generator.Empty"] = new("獎池至少需要一項獎品。", "Add at least one prize to the pool.", "賞品を 1 件以上追加してください。"),
        ["Generator.InvalidId"] = new("獎品 ID 必須是正整數。", "Prize ID must be a positive integer.", "賞品 ID は正の整数にしてください。"),
        ["Generator.InvalidQuantity"] = new("獎品數量必須是正整數。", "Prize quantity must be a positive integer.", "賞品の数量は正の整数にしてください。"),
        ["Generator.InvalidWeight"] = new("每項權重必須是正整數，且不超過抽選上限。", "Each weight must be a positive integer no greater than the roll range.", "各重みは抽選上限以下の正の整数にしてください。"),
        ["Generator.TotalWeight"] = new("權重總和必須等於抽選上限。", "The weight total must equal the roll range.", "重みの合計を抽選上限に合わせてください。"),
        ["Icon.PaletteIndexInvalid"] = new("BMP 像素索引超出調色盤範圍。", "BMP pixel index is outside the palette range.", "BMP のピクセルインデックスがパレット範囲外です。"),
        ["Icon.TooLarge"] = new("圖示宣告大小超過 16 MiB 限制。", "Icon exceeds the 16 MiB size limit.", "アイコンの宣言サイズが 16 MiB の上限を超えています。"),
        ["Icon.NotBmp"] = new("解壓結果不是 BMP 檔。", "Decompressed data is not a BMP image.", "展開したデータは BMP 画像ではありません。"),
        ["Icon.NoData"] = new("GRF 元件沒有回傳圖檔資料。", "GRF component returned no image data.", "GRF コンポーネントから画像データが返されませんでした。"),
        ["Source.FileNotFound"] = new("找不到檔案", "File not found", "ファイルが見つかりません"),
        ["Source.NotLoaded"] = new("未載入", "Not loaded", "未読み込み"),
        ["Source.ReadFailed"] = new("讀取失敗", "Read failed", "読み込みに失敗しました"),
        ["Source.OpenFailed"] = new("開啟失敗", "Open failed", "開けませんでした"),
        ["Source.ItemDb.Loaded"] = new("載入 {0:N0} 筆", "Loaded {0:N0} records", "{0:N0} 件を読み込みました"),
        ["Source.ItemDb.LoadedSkipped"] = new("載入 {0:N0} 筆，略過 {1:N0} 行", "Loaded {0:N0} records; skipped {1:N0} lines", "{0:N0} 件を読み込み、{1:N0} 行をスキップしました"),
        ["Source.ItemDb.InvalidRows"] = new("{0:N0} 行欄位或 ID 格式不符", "{0:N0} lines have invalid fields or IDs", "{0:N0} 行の項目または ID 形式が不正です"),
        ["Source.ItemInfo.Unsupported"] = new("不支援的二進位 LUB", "Unsupported binary LUB", "未対応のバイナリ LUB"),
        ["Source.ItemInfo.UnsupportedDetail"] = new("目前只解析文字型 Lua／LUB；原檔未修改。", "Only text Lua/LUB is parsed; the original file was not changed.", "現在はテキスト形式の Lua／LUB のみ解析します。元ファイルは変更していません。"),
        ["Source.ItemInfo.Found"] = new("找到 {0:N0} 個 resource name", "Found {0:N0} resource names", "resource name を {0:N0} 件見つけました"),
        ["Source.ItemInfo.NotFound"] = new("找不到文字 ItemInfo 資料", "No text ItemInfo data found", "テキスト形式の ItemInfo データが見つかりません"),
        ["Source.ItemInfo.FoundDetail"] = new("優先序由上到下；重複 ID 採第一個檔案。", "Files are checked from top to bottom; the first duplicate ID wins.", "上から順に優先し、重複 ID は最初のファイルを採用します。"),
        ["Source.ItemInfo.NotFoundDetail"] = new("沒有找到 [ID] = { ... identifiedResourceName ... } 項目；二進位 LUB 不會被默默忽略。", "No [ID] = { ... identifiedResourceName ... } entries found; binary LUB files are not silently skipped.", "[ID] = { ... identifiedResourceName ... } がありません。バイナリ LUB は無視されません。"),
        ["Source.Grf.MissingComponent"] = new("缺少 GRF 讀取元件", "GRF reader component missing", "GRF 読み込みコンポーネントがありません"),
        ["Source.Grf.InstallHint"] = new("請將 GRF Editor 安裝在 Program Files 或 Program Files (x86) 的預設資料夾，再重新載入。", "Install GRF Editor in its default Program Files folder, then reload sources.", "GRF Editor を既定の Program Files フォルダーにインストールしてから再読み込みしてください。"),
        ["Source.Grf.Opened"] = new("已開啟", "Opened", "開きました"),
        ["Source.Grf.OpenedWarning"] = new("已開啟，有加密警告", "Opened with encryption warnings", "暗号化の警告がありますが開きました"),
        ["Source.Grf.OpenedError"] = new("已開啟，有讀取錯誤", "Opened with read errors", "読み込みエラーがありますが開きました"),
        ["Source.Grf.Indexed"] = new("索引 {0:N0} 個 item BMP。", "Indexed {0:N0} item BMPs.", "item BMP を {0:N0} 件索引しました。"),
        ["Source.Grf.IndexedWarning"] = new("索引 {0:N0} 個 item BMP。{1}", "Indexed {0:N0} item BMPs. {1}", "item BMP を {0:N0} 件索引しました。{1}"),
        ["Source.Grf.IndexedError"] = new("索引 {0:N0} 個 item BMP。GRF 元件回報讀取錯誤。", "Indexed {0:N0} item BMPs. The GRF component reported read errors.", "item BMP を {0:N0} 件索引しました。GRF コンポーネントが読み込みエラーを報告しました。"),
        ["Source.Grf.IndexedWithWarning"] = new("索引 {0:N0} 個 item BMP。GRF 元件回報警告。", "Indexed {0:N0} item BMPs. The GRF component reported a warning.", "item BMP を {0:N0} 件索引しました。GRF コンポーネントが警告を報告しました。"),
        ["Source.Grf.ComponentError"] = new("GRF 元件回報讀取錯誤。", "The GRF component reported read errors.", "GRF コンポーネントが読み込みエラーを報告しました。"),
        ["Source.Grf.ComponentWarning"] = new("GRF 元件回報警告。", "The GRF component reported a warning.", "GRF コンポーネントが警告を報告しました。"),
        ["Source.Grf.ComponentLoadFailed"] = new("GRF 元件載入失敗", "Failed to load GRF component", "GRF コンポーネントを読み込めませんでした"),
        ["Source.Grf.FileTableMissing"] = new("GRF 元件沒有提供 FileTable。", "The GRF component did not provide a FileTable.", "GRF コンポーネントに FileTable がありません。"),
        ["Source.Grf.EntriesMissing"] = new("GRF 元件沒有提供檔案清單。", "The GRF component did not provide a file list.", "GRF コンポーネントにファイル一覧がありません。"),
    };

    private static string _language = "zh-Hant";
    private static ResourceDictionary? _activeDictionary;

    public static string CurrentLanguage => _language;

    public static string Get(string key, params object?[] arguments)
    {
        if (!Texts.TryGetValue(key, out var text))
            return key;

        var template = _language switch
        {
            "en" => text.English,
            "ja" => text.Japanese,
            _ => text.Chinese,
        };
        return arguments.Length == 0 ? template : string.Format(CultureInfo.CurrentCulture, template, arguments);
    }

    public static void SetLanguage(string? language)
    {
        _language = language switch
        {
            "en" => "en",
            "ja" => "ja",
            _ => "zh-Hant",
        };

        var culture = _language switch
        {
            "en" => CultureInfo.GetCultureInfo("en-US"),
            "ja" => CultureInfo.GetCultureInfo("ja-JP"),
            _ => CultureInfo.GetCultureInfo("zh-TW"),
        };
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        var resources = Application.Current?.Resources;
        if (resources is not null)
        {
            if (_activeDictionary is not null)
                resources.MergedDictionaries.Remove(_activeDictionary);

            var dictionary = new ResourceDictionary();
            foreach (var (key, text) in Texts)
            {
                dictionary[$"Ui.{key}"] = _language switch
                {
                    "en" => text.English,
                    "ja" => text.Japanese,
                    _ => text.Chinese,
                };
            }
            resources.MergedDictionaries.Add(dictionary);
            _activeDictionary = dictionary;
        }
    }
}
