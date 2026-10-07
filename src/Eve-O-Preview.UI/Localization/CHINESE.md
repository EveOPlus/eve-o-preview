# Chinese translation guide

Apply this guide only when adding, reviewing or updating Chinese translations
(`zh-Hans` or `zh-Hant`). Read [catalog maintenance](README.md) for shared
formatting, runtime boundaries and validation requirements.

Use the Simplified Chinese wording contributed by Rangeen (冉吉) in `zh-Hans.json` as the
starting point for new Chinese UI text. Read the control and nearby descriptions
before choosing a term. These examples capture useful corrections, not a rule to
replace every occurrence of an English word. Traditional Chinese applies the same
contextual corrections with its own UI terminology; do not mechanically copy
Simplified Chinese. Native-speaker review is still needed for regional fluency
and specialized game vocabulary.
The language selector credits this assistance specifically to Simplified Chinese;
it does not imply that every entry or future addition has been reviewed by Rangeen.

| Context | Preferred example | Lesson for future translations |
| --- | --- | --- |
| Background client FPS | 后台客户端 FPS | Background means a nonforeground application, not scenery (背景). |
| Theme accent colour | 强调颜色 / 强调色 | Accent describes visual emphasis, not pronunciation (口音 / 重音). |
| Title text stroke | 描边 | Stroke is a drawn outline, not a medical condition (中风). |
| Live / still-image previews | 实时预览 / 静态预览 | Describe moving versus captured content, not broadcast or living things. |
| Combat damage / incoming DPS / outgoing DPS | 伤害 / 承受DPS / 输出DPS | Describe damage received or dealt, not damaged objects or generic data transfer. Repairs need their own received/sent context. |
| Alpha damage | 单次伤害; DPH in `DPS / alpha font` | Alpha means damage per hit here, not transparency or a release stage; retain familiar DPS/DPH abbreviations. |
| Faction and weapon choices | 势力; 艾玛金色; 加达里蓝色; 盖伦特绿色; 疾速炮; 立体炸弹 | Reuse the supplied EVE vocabulary instead of literal translations or phonetic guesses. These are display labels, not stored identifiers. |
| NPC | NPC | Keep a familiar game abbreviation; do not expand it into an unrelated organization. |
| System tray / account token | 系统托盘 / 令牌 | Resolve technical meanings from the feature, not dictionary alternatives such as 磁盘 or 信使. |
| Learn more / colour picker | 了解更多 / 打开 {0} 的颜色选择器 | Use familiar Chinese UI phrasing and natural argument order. |
| Character settings saved | 已为 {0} 保存角色设置 | Make completed status and the affected character explicit; preserve the argument. |
| Invalid profile name | 末尾空格 / 文件名非法字符 | Explain the actual restriction: trailing spaces and forbidden filename characters, not all characters. |
| Offline character settings | 可以对离线角色进行设置 | Explain what the user can do, rather than saying an offline character "works". |
| Shortcut hints | Ctrl+↑ / Ctrl+↓; Esc | Keep recognizable key names, arrows and bindings intact. |

Keep instructions aligned with the actual translated button/page label. Prefer
concise labels and clear action/status sentences; do not copy incidental spacing,
punctuation mistakes or awkward wording as a style rule. Preserve user names,
product names, literal examples, line breaks and all composite-format arguments.

## Traditional Chinese wording

Use `設定檔`, `用戶端`, `資料夾`, `儲存`, `套用`, `字型`, `圖示` and
`快捷鍵` consistently. Use `背景執行` for a background application; `背景`
remains appropriate for an actual visual background. Translate the meaning of
the supplied corrections while retaining regional terms such as `雷射`, `飛彈`
and `巡弋`, and Traditional Chinese weapon spelling such as `疾速砲`.

| Context | Traditional Chinese example |
| --- | --- |
| Theme accent / title outline | 強調色 / 描邊 |
| Live / captured preview | 即時預覽 / 靜態影像預覽 |
| Screenshot in progress | 正在擷取已開啟用戶端的靜態畫面… |
| Incoming / outgoing DPS | 承受 DPS / 輸出 DPS |
| Alpha / alpha font | 單次傷害 / DPS / DPH 字型 |
| Incoming / outgoing repairs | 接收 / 輸出的維修 |
| Damage scale | 傷害倍率（%）, a simulation multiplier rather than damage severity |
| System tray / account token | 系統匣 / 權杖 |
| Invalid profile name | 保留名稱、結尾空白或檔名禁用字元 |

Preserve literal names such as `Default`, `EVE-O Preview`, `Fenris Creations (FC)`
and `Aura Asuna`, including signatures and meaningful line breaks. Keep `NPC`,
`DPS`, `DPH`, `Ctrl+↑ / Ctrl+↓` and `Esc` recognizable. Refer to translated
actions by their exact labels, such as `結束`, `套用變更` and `儲存角色設定`.
The adaptation is not a separate contributor-reviewed Traditional Chinese
submission; keep the language credit's automated-translation notice.

## Validation

Merge contributions by exact English key against the current catalog rather than
replacing the file wholesale. Retain newer entries absent from an older submission.
If an English key changed only in punctuation (for example `Taking a still from an
open client…` versus `Taking a still from an open client...`), confirm the current
call site and transfer the translation to that exact key. Check duplicate keys,
coverage and format specifiers, then run the existing localization tests and UI
smoke checks. Inspect Chinese renders for clipping; automated checks alone do not
prove every sentence is idiomatic.
