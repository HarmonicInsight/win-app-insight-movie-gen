# InsightMovie JSON生成プロンプト例

以下のプロンプトをAI（ChatGPT、Claude等）に渡すと、InsightMovieにインポート可能なJSONが生成されます。

---

## プロンプト

```
あなたは動画シナリオライターです。
以下のテーマで、ナレーション動画用のシーン構成JSONを作成してください。

【テーマ】新製品「SmartPen」の紹介動画（3分程度）

【出力フォーマット】
以下のJSON形式で出力してください。他の文章は不要です。

{
  "scenes": [
    {
      "mediaPath": null,
      "narrationText": "ナレーションとして読み上げるテキスト",
      "subtitleText": "画面に表示する短い字幕",
      "durationMode": "auto",
      "transitionType": "fade",
      "transitionDuration": 0.5
    }
  ]
}

【ルール】
- scenesは5〜15個程度
- narrationTextは1シーン30〜80文字程度（読み上げ用なので自然な日本語）
- subtitleTextは15文字以内（画面表示用の要約）
- mediaPathは null にしてください（後で画像を設定します）
- durationModeは基本 "auto"（ナレーション長に合わせる）
- transitionTypeは "none", "fade", "dissolve", "wipeLeft", "wipeRight", "slideLeft", "slideRight", "zoomIn" のいずれか
- transitionDurationは 0.2〜2.0 秒
- 最初のシーンのtransitionTypeは "none" にしてください
```

---

## AIからの出力例

```json
{
  "scenes": [
    {
      "mediaPath": null,
      "narrationText": "皆さんこんにちは。本日は革新的な新製品、SmartPenをご紹介します。",
      "subtitleText": "SmartPenのご紹介",
      "durationMode": "auto",
      "transitionType": "none",
      "transitionDuration": 0.5
    },
    {
      "mediaPath": null,
      "narrationText": "SmartPenは、手書きの文字をリアルタイムでデジタルテキストに変換する、次世代のスマートペンです。",
      "subtitleText": "リアルタイム変換",
      "durationMode": "auto",
      "transitionType": "fade",
      "transitionDuration": 0.5
    },
    {
      "mediaPath": null,
      "narrationText": "100カ国語以上の言語に対応し、書いた瞬間に翻訳結果をスマートフォンで確認できます。",
      "subtitleText": "100カ国語対応",
      "durationMode": "auto",
      "transitionType": "dissolve",
      "transitionDuration": 0.5
    },
    {
      "mediaPath": null,
      "narrationText": "バッテリーは1回の充電で最大2週間持続。毎日の仕事や学習を途切れることなくサポートします。",
      "subtitleText": "2週間バッテリー",
      "durationMode": "auto",
      "transitionType": "fade",
      "transitionDuration": 0.5
    },
    {
      "mediaPath": null,
      "narrationText": "SmartPenで、あなたの手書きをもっとスマートに。詳しくは公式サイトをご覧ください。",
      "subtitleText": "詳しくは公式サイトへ",
      "durationMode": "auto",
      "transitionType": "fade",
      "transitionDuration": 1.0
    }
  ]
}
```

---

## 使い方

1. 上記のプロンプトをAIに投げる（テーマ部分を変更）
2. 出力されたJSONをファイルに保存（例: `my_video.json`）
3. InsightMovieで「JSON取込」ボタンをクリック
4. JSONファイルを選択 → シーンが自動生成される
5. 必要に応じて画像を設定し、動画を書き出す
