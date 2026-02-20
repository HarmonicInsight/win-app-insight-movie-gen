# InsightMovie → InsightCast 実装計画

作成日: 2026年2月20日

---

## 現状分析（As-Is）

### 実装済み機能

| 機能 | 実装状況 | 対応ファイル |
|---|---|---|
| PPTX取込（ノート抽出＋画像エクスポート） | ✅ 実装済 | `Utils/PptxImporter.cs` |
| VOICEVOX音声合成（日本語のみ） | ✅ 実装済 | `VoiceVox/VoiceVoxClient.cs` |
| FFmpegによるMP4動画生成 | ✅ 実装済 | `Video/FFmpegWrapper.cs`, `Video/SceneGenerator.cs` |
| シーン編集（素材・ナレーション・字幕） | ✅ 実装済 | `ViewModels/MainWindowViewModel.cs` |
| 字幕スタイル（フォント・色・影・背景） | ✅ 実装済 | `Models/TextStyle.cs`, `Views/TextStyleDialog.xaml` |
| トランジション（フェード・ワイプ等） | ✅ 実装済 | `Models/Transition.cs`, `Video/VideoComposer.cs` |
| BGM（ボリューム・フェード・ダッキング） | ✅ 実装済 | `Models/BGMSettings.cs`, `Video/VideoComposer.cs` |
| プロジェクト保存/読込（JSON） | ✅ 実装済 | `Models/Project.cs` |
| ライセンス管理（Free/Trial/Std/Pro/Ent） | ✅ 実装済 | `Core/License.cs` |
| 音声キャッシュ | ✅ 実装済 | `VoiceVox/AudioCache.cs` |
| プレビュー再生 | ✅ 実装済 | `Views/PreviewPlayerDialog.xaml` |

### 技術スタック

- **フレームワーク**: WPF (.NET 8.0 Windows)
- **音声エンジン**: VOICEVOX（ローカルREST API）
- **動画処理**: FFmpeg（外部コマンド）
- **PPTX解析**: DocumentFormat.OpenXml 3.0.1
- **シリアライゼーション**: System.Text.Json 8.0.5

---

## ギャップ分析（insightcast.jp 対 現コードベース）

| # | insightcast.jp の提供機能 | 現在の実装 | ギャップ |
|---|---|---|---|
| 1 | 製品名「InsightCast」 | 「InsightMovie」のまま | **リネーム必要** |
| 2 | PDF取込 | なし | **新規実装** |
| 3 | テキストファイル取込 | なし | **新規実装** |
| 4 | 画像取込（JPG/PNG） | ✅ 手動選択で対応 | 一括取込UI改善 |
| 5 | 多言語ナレーション（EN/ZH/VI/PT等） | 日本語のみ（VOICEVOX） | **クラウドTTS連携** |
| 6 | 男声・女声選択 | VOICEVOXの話者で対応 | 多言語音声の男女選択追加 |
| 7 | 30分以内で動画完成 | ✅ ローカル処理で達成可能 | - |
| 8 | MP4ダウンロード | ✅ MP4エクスポート | - |
| 9 | Free: 月3本・透かし付き | Free版あるが月制限・透かしなし | **透かし・月次カウント** |
| 10 | Business: 30本・ロゴ挿入・透かしなし | なし | **ロゴ挿入機能** |
| 11 | Enterprise: 無制限・API・SSO | なし | **Phase 3以降** |
| 12 | API連携（LMS/SharePoint） | なし | **Phase 3以降** |
| 13 | 暗号化処理・自動削除 | なし | **セキュリティ強化** |
| 14 | 即座の修正・再生成 | ✅ シーン再編集で対応 | - |

---

## 実装フェーズ

### Phase 1: ブランドリネーム（InsightMovie → InsightCast）
**目標**: 全コードベースで「InsightMovie」を「InsightCast」にリネーム
**期間**: 1-2日
**リスク**: 低（検索・置換が主体）

#### 1-1. プロジェクト構造のリネーム

| 対象 | 変更前 | 変更後 |
|---|---|---|
| ソリューションファイル | `InsightMovie.sln` | `InsightCast.sln` |
| プロジェクトフォルダ | `InsightMovie/` | `InsightCast/` |
| csproj ファイル | `InsightMovie.csproj` | `InsightCast.csproj` |
| RootNamespace | `InsightMovie` | `InsightCast` |
| AssemblyName | `InsightMovie` | `InsightCast` |

#### 1-2. ソースコード内のリネーム

| 対象 | ファイル数 | 変更内容 |
|---|---|---|
| namespace宣言 | 全.csファイル（32ファイル） | `InsightMovie.*` → `InsightCast.*` |
| using文 | 全.csファイル | `using InsightMovie.*` → `using InsightCast.*` |
| XAML xmlns | 全.xamlファイル（8ファイル） | `clr-namespace:InsightMovie.*` → `clr-namespace:InsightCast.*` |
| x:Class属性 | 全.xamlファイル | `InsightMovie.Views.*` → `InsightCast.Views.*` |

#### 1-3. UIテキスト・ユーザー向け文字列

| ファイル | 変更箇所 |
|---|---|
| `MainWindowViewModel.cs:50` | `_windowTitle = "InsightMovie - 新規プロジェクト"` → `"InsightCast - 新規プロジェクト"` |
| `MainWindowViewModel.cs:965` | NewProject内の `WindowTitle` |
| `MainWindowViewModel.cs:985` | OpenProject内の `WindowTitle` |
| `MainWindowViewModel.cs:1030` | SaveProjectAs内の `WindowTitle` |
| `MainWindowViewModel.cs:847` | ExportVideo内のデフォルトファイル名 `"InsightMovie.mp4"` → `"InsightCast.mp4"` |
| `MainWindowViewModel.cs:1023` | SaveProjectAsの `"InsightMovie.json"` → `"InsightCast.json"` |
| `MainWindowViewModel.cs:1176` | ShowTutorial: `"InsightMovie チュートリアル"` → `"InsightCast チュートリアル"` |
| `MainWindowViewModel.cs:1220-1224` | ShowAbout: バージョン情報全体 |
| `MainWindow.xaml:91` | ヘッダーのタイトルテキスト `"InsightMovie"` → `"InsightCast"` |
| `MainWindow.xaml:235` | メニュー `"InsightMovieについて"` → `"InsightCastについて"` |

#### 1-4. 設定パス・キャッシュパス

| ファイル | 変更箇所 |
|---|---|
| `Core/Config.cs:12` | `"InsightMovie"` → `"InsightCast"` (AppData パス) |
| `ViewModels/MainWindowViewModel.cs:1066` | `"insightmovie_cache"` → `"insightcast_cache"` |
| `Services/ExportService.cs:45` | `"insightmovie_build"` → `"insightcast_build"` |

#### 1-5. ライセンスプロダクトコード

| ファイル | 変更箇所 | 注意 |
|---|---|---|
| `Core/License.cs:32` | `PRODUCT_CODE = "INMV"` → `"INCS"` | 既存キーの後方互換性を検討 |
| `Core/License.cs:37` | 正規表現に `INCS` を追加 | 旧コード`INMV`も受け入れ可能にする |
| `InsightMovie.csproj:13` | Description文字列 |
| `InsightMovie.csproj:14-15` | Company, Product |

#### 1-6. ビルド関連

| ファイル | 変更内容 |
|---|---|
| `build.ps1` | プロジェクトパス参照の更新 |
| `Installer/` | インストーラ設定の更新 |
| `.gitmodules` | パス参照の確認 |

---

### Phase 2: 入力形式の拡張
**目標**: PDF・テキストファイルの取込サポート
**期間**: 1-2週間
**依存**: Phase 1 完了後

#### 2-1. PDF取込機能

**方針**: PDFの各ページを画像に変換し、テキストを抽出してナレーションテキストに設定

**新規ファイル**: `Utils/PdfImporter.cs`

**実装内容**:
- NuGetパッケージ追加: `PdfPig`（テキスト抽出）+ FFmpegベースのPDF→画像変換（もしくは`SkiaSharp` + `PdfiumViewer`）
- PDFの各ページを PNG 画像として export
- 各ページのテキストを抽出し、Scene の NarrationText に設定
- PptxImporter と同じインターフェースパターン（SlideData 互換）

**UI変更**:
- `MainWindowViewModel.cs`: `ImportPdfCommand` 追加
- `MainWindow.xaml`: ヘッダーバーに「PDF取込」ボタン追加
- ファイル選択ダイアログのフィルタにPDF追加

**ライセンス**: PPTX取込と同じ制限（Trial/Pro以上）

#### 2-2. テキストファイル取込機能

**方針**: テキストファイルを段落単位でシーンに分割。各シーンは黒背景＋ナレーション。

**新規ファイル**: `Utils/TextImporter.cs`

**実装内容**:
- `.txt` ファイルを読み込み
- 空行区切りで段落を分割 → 各段落を1シーンとして追加
- ナレーションテキスト = 段落テキスト
- 字幕テキスト = 段落テキスト（オプション）
- 画像なし → 黒背景（既存機能で対応可能）

**UI変更**:
- `MainWindowViewModel.cs`: `ImportTextCommand` 追加
- `MainWindow.xaml`: メニューに「テキスト取込」追加

#### 2-3. 一括素材取込の改善

**方針**: 複数画像を一括選択してシーンに追加

**実装内容**:
- ファイル選択ダイアログで複数選択対応（既存のSelectMediaは単一選択）
- 複数画像 → 画像数分のシーンを自動追加
- ドラッグ＆ドロップ対応（MainWindow.xaml に AllowDrop）

---

### Phase 3: 多言語音声ナレーション
**目標**: 日本語以外の言語でのAI音声ナレーション
**期間**: 2-3週間
**依存**: Phase 1 完了後（Phase 2 と並行可能）

#### 3-1. TTS抽象化レイヤー

**方針**: 現在のVOICEVOX依存を抽象化し、複数のTTSバックエンドを切り替え可能にする

**新規ファイル**:

```
Services/
├── ITtsEngine.cs          # TTS抽象インターフェース
├── TtsEngineFactory.cs    # エンジンファクトリー
├── VoiceVoxTtsEngine.cs   # 既存VOICEVOX（ラッパー）
├── AzureTtsEngine.cs      # Azure Cognitive Services TTS
└── TtsVoiceInfo.cs        # 話者情報モデル
```

**ITtsEngine インターフェース**:
```csharp
public interface ITtsEngine
{
    string EngineName { get; }
    Task<List<TtsVoiceInfo>> GetVoicesAsync(string? languageFilter = null);
    Task<byte[]> SynthesizeAsync(string text, string voiceId, TtsOptions? options = null);
    Task<bool> CheckConnectionAsync();
}
```

**対応言語と推奨エンジン**:

| 言語 | コード | TTSエンジン | 備考 |
|---|---|---|---|
| 日本語 | ja-JP | VOICEVOX（既存） | ローカル、無料 |
| 英語 | en-US | Azure TTS | クラウド |
| 中国語 | zh-CN | Azure TTS | クラウド |
| ベトナム語 | vi-VN | Azure TTS | クラウド |
| ポルトガル語 | pt-BR | Azure TTS | クラウド |

#### 3-2. Azure TTS 連携

**NuGetパッケージ追加**: `Microsoft.CognitiveServices.Speech`

**設定**: APIキー・リージョンを `Config.cs` に追加
```csharp
public string? AzureTtsKey { get; set; }
public string? AzureTtsRegion { get; set; }
```

**UI変更**:
- 言語選択ドロップダウンの追加（プロジェクト全体 + シーン個別）
- 話者リストを言語でフィルタリング
- セットアップウィザードにAzure TTS設定画面を追加

#### 3-3. 音声キャッシュの拡張

**変更ファイル**: `VoiceVox/AudioCache.cs`

- キャッシュキーにエンジン名と言語コードを追加
- キャッシュパス: `{engine}/{lang}/{hash}.wav`

---

### Phase 4: プラン機能の実装
**目標**: insightcast.jp の3プラン（Free/Business/Enterprise）に合わせた機能制限
**期間**: 1-2週間
**依存**: Phase 1 完了後

#### 4-1. プラン体系の再構成

**変更ファイル**: `Core/License.cs`

現在の5段階（Free/Trial/Std/Pro/Ent）を insightcast.jp の3段階にマッピング:

| 現行プラン | 新プラン | 機能 |
|---|---|---|
| Free | Free | 月3本、透かし付き |
| Trial | （廃止→Free移行） | - |
| Std | Business | 月30本、5ユーザー、ロゴ挿入、透かしなし |
| Pro | Business | 同上 |
| Ent | Enterprise | 無制限、API連携、SSO |

#### 4-2. 透かし（ウォーターマーク）機能

**新規ファイル**: `Video/WatermarkService.cs`

**実装内容**:
- Free プランのエクスポート時、動画右下に「InsightCast Free」の半透明テキストを重ねる
- FFmpeg drawtext フィルタで実装（SceneGenerator.cs の字幕と同じパターン）
- ExportService.cs の Export メソッド内、最終結合後に適用

#### 4-3. ロゴ挿入機能

**新規ファイル**: `Video/LogoOverlayService.cs`

**実装内容**:
- Business プラン以上で利用可能
- ユーザーがロゴ画像（PNG）を指定
- 配置位置: 左上/右上/左下/右下 から選択
- FFmpeg overlay フィルタで実装
- Project モデルに `LogoSettings` を追加

#### 4-4. 月次生成カウント

**変更ファイル**: `Core/Config.cs`

**実装内容**:
- `Config.cs` に月次カウンター追加:
  - `ExportCount` (int): 当月のエクスポート数
  - `ExportCountMonth` (string): カウントの対象月 (YYYY-MM)
- ExportVideo 実行前にカウントチェック
- 月が変わればリセット
- Free: 3本/月、Business: 30本/月、Enterprise: 無制限

#### 4-5. Feature Matrix 更新

```csharp
private static readonly Dictionary<string, PlanCode[]> FEATURE_MATRIX = new()
{
    { "subtitle",       new[] { PlanCode.Business, PlanCode.Ent } },
    { "subtitle_style", new[] { PlanCode.Business, PlanCode.Ent } },
    { "transition",     new[] { PlanCode.Business, PlanCode.Ent } },
    { "pptx_import",    new[] { PlanCode.Business, PlanCode.Ent } },
    { "pdf_import",     new[] { PlanCode.Business, PlanCode.Ent } },
    { "multilang_tts",  new[] { PlanCode.Business, PlanCode.Ent } },
    { "logo_overlay",   new[] { PlanCode.Business, PlanCode.Ent } },
    { "no_watermark",   new[] { PlanCode.Business, PlanCode.Ent } },
    { "api_access",     new[] { PlanCode.Ent } },
    { "sso",            new[] { PlanCode.Ent } },
};
```

---

### Phase 5: セキュリティ・運用機能
**目標**: insightcast.jp の信頼性/セキュリティ機能の実装
**期間**: 1-2週間
**依存**: Phase 1-4

#### 5-1. 一時ファイルの自動削除

**変更ファイル**: `Services/ExportService.cs`, `Video/SceneGenerator.cs`

**実装内容**:
- エクスポート完了後、temp ディレクトリ配下の中間ファイルを確実に削除
- アプリ終了時にキャッシュディレクトリのクリーンアップ
- `App.xaml.cs` の OnExit で残存一時ファイルを削除

#### 5-2. プロジェクトファイルの暗号化（オプション）

**新規ファイル**: `Core/ProjectEncryption.cs`

**実装内容**:
- AES-256-GCM による Project JSON の暗号化/復号
- Enterprise プランのみ
- パスワードベース鍵導出（PBKDF2）

---

### Phase 6: 将来拡張（SaaS化・API化）
**目標**: Web版・API連携の基盤整備
**期間**: 要別途計画
**備考**: デスクトップアプリの成熟後に着手

- REST API サーバー（ASP.NET Core）の新規プロジェクト追加
- バッチ動画生成キュー
- LMS/SharePoint Webhook 連携
- SSO（SAML/OIDC）連携
- マルチテナント対応
- ユーザー管理・課金連携

---

## 実装優先順位

```
Phase 1 (ブランドリネーム)          ██████░░░░ 1-2日
    ↓
Phase 2 (PDF・テキスト取込)         ██████████ 1-2週 ─┐
Phase 3 (多言語TTS)                ██████████ 2-3週 ─┤ 並行可
Phase 4 (プラン機能)               ██████████ 1-2週 ─┘
    ↓
Phase 5 (セキュリティ)             ██████░░░░ 1-2週
    ↓
Phase 6 (SaaS化)                  ██████████████████ 要別途計画
```

**推奨開始順序**: Phase 1 → Phase 2 + Phase 4 並行 → Phase 3 → Phase 5

---

## ファイル変更一覧（Phase 1-5 合計）

### 変更ファイル

| ファイル | Phase | 変更種別 |
|---|---|---|
| `InsightMovie.csproj` → `InsightCast.csproj` | 1 | リネーム＋修正 |
| `InsightMovie.sln` → `InsightCast.sln` | 1 | リネーム＋修正 |
| 全 .cs ファイル（32ファイル） | 1 | namespace/using リネーム |
| 全 .xaml ファイル（8ファイル） | 1 | xmlns/x:Class リネーム |
| `Core/Config.cs` | 1,4 | パスリネーム＋月次カウンター |
| `Core/License.cs` | 1,4 | プロダクトコード＋プラン再構成 |
| `ViewModels/MainWindowViewModel.cs` | 1,2,3,4 | UI文字列＋新コマンド＋TTS統合 |
| `Views/MainWindow.xaml` | 1,2,3 | UIテキスト＋新ボタン＋言語選択 |
| `Services/ExportService.cs` | 1,4,5 | パスリネーム＋透かし＋自動削除 |
| `build.ps1` | 1 | パス参照更新 |

### 新規ファイル

| ファイル | Phase | 内容 |
|---|---|---|
| `Utils/PdfImporter.cs` | 2 | PDF取込 |
| `Utils/TextImporter.cs` | 2 | テキストファイル取込 |
| `Services/ITtsEngine.cs` | 3 | TTS抽象インターフェース |
| `Services/TtsEngineFactory.cs` | 3 | TTSエンジンファクトリー |
| `Services/VoiceVoxTtsEngine.cs` | 3 | VOICEVOX TTSアダプタ |
| `Services/AzureTtsEngine.cs` | 3 | Azure TTS連携 |
| `Services/TtsVoiceInfo.cs` | 3 | 話者情報モデル |
| `Video/WatermarkService.cs` | 4 | 透かし機能 |
| `Video/LogoOverlayService.cs` | 4 | ロゴ挿入機能 |
| `Models/LogoSettings.cs` | 4 | ロゴ設定モデル |
| `Core/ProjectEncryption.cs` | 5 | プロジェクト暗号化 |

### 新規NuGetパッケージ

| パッケージ | Phase | 用途 |
|---|---|---|
| `UglyToad.PdfPig` | 2 | PDFテキスト抽出 |
| `Microsoft.CognitiveServices.Speech` | 3 | Azure TTS |
