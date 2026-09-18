# Hello Stardew

一个《星露谷物语》(Stardew Valley) 的 SMAPI 模组，做两件事：

1. 将游戏内状态通过一个**本地只读 HTTP API** 暴露出来，供外部工具（如 MCP server、脚本、AI agent）查询。包括：
   - **日历**：日期、节日、生日、事件
   - **住所**：农夫 / 农场 / 配偶 / 宠物 / 孩子的名字
   - **关系**：与每个村民的好感度、心数、婚姻状态
   - **位置**：村民现在在哪、正在去哪，以及接下来要去哪
   - **礼物**：每个村民的礼物喜好，以及结合背包与箱子给出的送礼推荐
   - **状态快照**：时间、地点、金钱、体力、生命、天气、技能、背包
   - **近期行为**：最近 3 个游戏日做过的事（对话、送礼、钓鱼、出货、升级、消费……）
2. 让你可以和村民进行**自定义对话**：按住 `Alt` 点击村民输入你想说的话，NPC 的回复会带上几条可选回复。

- **UniqueID**: `heptane.HelloStardew`
- **最低 SMAPI 版本**: `4.0.0`
- **默认地址**: `http://127.0.0.1:8788/`

> 服务端只读，所有接口都通过 HTTP GET 查询。路由本身不校验 HTTP 方法，但仍应仅用 GET。

## 目录

- [安装与运行](#安装与运行)
- [配置](#配置)
- [与村民对话](#与村民对话)
- [语言与本地化](#语言与本地化)
- [通用约定](#通用约定)
- [API 列表](#api-列表)
  - [GET /health](#get-health)
  - [GET /date](#get-date)
  - [GET /events/today](#get-eventstoday)
  - [GET /events/day](#get-eventsday)
  - [GET /birthdays/week](#get-birthdaysweek)
  - [GET /birthdays/day](#get-birthdaysday)
  - [GET /calendar](#get-calendar)
  - [GET /household](#get-household)
  - [GET /relationship](#get-relationship)
  - [GET /npc/location](#get-npclocation)
  - [GET /state](#get-state)
  - [GET /activity/recent](#get-activityrecent)
  - [GET /events/recent](#get-eventsrecent)
  - [GET /gift/tastes](#get-gifttastes)
  - [GET /gift/suggest](#get-giftsuggest)
- [错误码](#错误码)
- [数据结构](#数据结构)
- [近期行为是怎么记录的](#近期行为是怎么记录的)

## 安装与运行

1. 构建模组并放入 `Mods/HelloStardew` 目录（需包含 `manifest.json` 与 `HelloStardew.dll`）。
2. 启动游戏并载入存档。模组会在 SMAPI 控制台打印：

   ```
   Calendar API ready. Try: curl http://127.0.0.1:8788/health
   ```

3. 验证服务是否可用：

   ```bash
   curl http://127.0.0.1:8788/health
   ```

> 只要模组已加载，HTTP 服务就会启动，**不要求**已载入存档；但除 `/health` 外的接口在未载入存档时会返回 `503 no_save_loaded`。

## 配置

配置文件为 `Mods/HelloStardew/config.json`，首次运行时由 SMAPI 自动生成，默认内容：

```json
{
  "BindAddress": "127.0.0.1",
  "Port": 8788,
  "EnableSpouseConversation": true,
  "InitiateTypedDialogueKey": "LeftAlt",
  "AgentEndpoint": "http://127.0.0.1:8000/chat",
  "AgentTimeoutSeconds": 30,
  "OfferTypedResponse": true
}
```

| 字段 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `BindAddress` | string | `127.0.0.1` | 监听地址。除非清楚风险，否则保持 localhost。 |
| `Port` | int | `8788` | 监听端口。 |
| `EnableSpouseConversation` | bool | `true` | 是否启用与村民的 AI 对话（配偶包含在内）。 |
| `InitiateTypedDialogueKey` | string | `LeftAlt` | 按住此键点击村民可输入自己的话。取值同 SMAPI 的 `SButton`。 |
| `AgentEndpoint` | string | `http://127.0.0.1:8000/chat` | 生成台词的 agent 地址。 |
| `AgentTimeoutSeconds` | int | `30` | 单次请求超时秒数。超时后会显示一句兜底台词。 |
| `OfferTypedResponse` | bool | `true` | 选项里是否提供 `*Something else*`（自己打字）。 |

> `EnableSpouseConversation` 是整套对话功能的开关，**覆盖所有村民**，不限于配偶。字段名沿用 "spouse" 是为了不破坏已经存在的 `config.json`（改字段名会让玩家的设置被静默重置为默认值）。

### 游戏内修改配置

安装 [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098)（可选依赖）后，上表所有选项都能在游戏内修改，无需手动编辑文件。

- 在标题界面或游戏内按 GMCM 的快捷键打开菜单，找到 **Hello Stardew**。
- **关闭配置菜单时改动即生效**，不需要重启游戏。
- 改 `BindAddress` / `Port` 会重新绑定监听端口；若新端口已被占用，会保留原有监听并在 SMAPI 控制台报错。

未安装 GMCM 时，手动编辑 `config.json` 后需重启游戏生效。

## 与村民对话

配偶和普通村民走的是**同一套逻辑**：没有结婚要求，任何村民都能聊。

1. **按住 `Alt`（可改）点击村民**，弹出输入框，写下你想说的话。
2. 模组把这句发给 `AgentEndpoint`，拿到回复后当作该村民的台词显示。
3. 台词说完**之后**，同一个对话框的最后一页列出可选项：agent 生成的建议回复、`*Stay silent*`、以及 `*Something else*`。
4. 选任意一项会继续下一轮；选 `*Stay silent*` 结束对话。

台词过长时会被拆成多页，按 `Enter` / 点击继续翻页。**可选项只出现在最后一页**，不会在村民还没说完时就冒出来。

按 `Enter` 发送，`Esc` 取消。输入框支持退格、方向键、`Home` / `End` / `Delete`。

### 会话与 thread_id

模组自己不保存对话历史，而是给 agent 一个 `thread_id`，由 agent 按这个 id 记住上下文。

请求体：

```json
{
  "thread_id": "haley-20260916143012-a1b2c3",
  "character": "Haley",
  "message": "Where should we go? I was thinking the beach.",
  "language": "zh",
  "is_spouse": true
}
```

`thread_id` 的生命周期：

- **开始一次聊天**（按住 `Alt` 点击村民）→ 生成一个新的 `thread_id`。
- **同一场聊天的后续每一轮**（点建议回复、点 `*Something else*` 再输入）→ 一直复用同一个 `thread_id`。
- **聊天结束**（选 `*Stay silent*`，或者中途走开、下次重新点击村民）→ 丢弃这个 `thread_id`。下一次聊天会拿到全新的 id，因此不会有上一场的记忆。

id 形如 `haley-20260916143012-a1b2c3`：角色名 + 起始时间 + 随机后缀。带时间戳方便在 agent 日志里分辨，随机后缀保证同一秒内开的两场聊天也不会撞号。

> `thread_id` 是**必填**字段，agent 端缺少它会返回 `422`。`CyberJu` 目前不使用记忆，模组仍然会传一个（每次读档后重新生成）。

### `is_spouse` 字段

`is_spouse` 表示这次说话的 NPC 是不是玩家的配偶（同居的 Krobus 也算）。agent 用它来选人格，不必先去 mod 的 HTTP API 查一次玩家状态。`CyberJu` 固定为 `false`。

这是一个**判断结果，不是限制**：`character` 是哪个角色完全由 mod 侧决定，`is_spouse` 只说明这个人的身份。模组不因为它是 `false` 就换个做法。

### 语言与 language 字段

`language` 取**游戏当前语言**（`LocalizedContentManager.CurrentLanguageCode`，如 `en` / `zh` / `ja`），每次请求时读取，因此不需要在模组侧做任何配置，也不会与游戏支持的语言列表脱节。

agent 的语言支持：

| `language` | 行为 |
| --- | --- |
| `zh` | 玩家输入先翻成英文再交给 agent；agent 的回复翻回中文后显示 |
| 其他（含 `en`） | 原样交给 agent，回复按英文显示 |

中文是在 agent 的边界上翻译的，agent 内部的 prompt、角色文档、对话记忆**全部保持英文**，所以每个角色只需要一份文档，一场在对话中途切换语言的聊天也不会让记忆变得前后不一致。

**翻译不会破坏返回格式**：`- ` 与 `% ` 是逐行翻译后由代码重新拼回去的，标记、顺序、行数都与英文原回复一一对应，因此建议回复照常出现。

### agent 返回格式

要让**建议回复**出现，agent 的 `message` 字段需要按这个格式返回：

```
- I had a long day at the farm today. How was yours?
% It was quiet without you.
% I'm exhausted too.
% Let's rest together.
```

- 第一个不以 `%` 开头的行作为村民的台词（前导 `-` 会被去掉）
- 每个 `%` 开头的行作为一条建议回复

**如果 agent 只返回单行纯文本，功能不会失效**——只是没有建议回复，仍然会显示台词加 `*Stay silent*` / `*Something else*` 两个选项。

### 说明

- **不带历史**：每次只把当前这一句加上 `thread_id` 发给 agent，历史存在 agent 那边。
- 对话对**所有村民**生效，不限于配偶。这次说话的是不是配偶，由模组通过请求里的 `is_spouse` 告诉 agent；agent 拿它选人格。
- agent 不可达或超时 → 显示一句兜底台词并在 SMAPI 控制台记 Error 日志。
- 回复文本中的 `#`、`$`、`^`、`¦`、`{`、`}` 会被过滤，因为它们在原版对话脚本里是标记字符（`{` 是翻页标记，模组自己用它来分页）。

## 语言与本地化

模组自己的界面文案走 SMAPI 标准的多语言机制，跟随**游戏语言设置**自动切换，模组侧不需要任何配置：

```text
i18n/
├── default.json   # 英文（兜底）
└── zh.json        # 中文
```

新增一门语言 = 新增一个 `i18n/<语言代码>.json`（代码与游戏一致，如 `ja` / `de`），**代码不用改**。找不到对应文件时回退到 `default.json`。

**会被翻译的文案**：

| 位置 | 例子 |
| --- | --- |
| 村民对话 | 输入框标题、`{{npc}} is thinking...`、`*Stay silent*`、`*Something else*` |
| CyberJu | `cj` 命令说明、`CyberJu is thinking...`、连不上 agent 时的兜底台词 |
| 输入框 | `Press Enter to send, or Escape to cancel.` |
| 配置菜单 | GMCM 里的全部分组标题、选项名与提示 |

文案键与对应文本都在 `i18n/*.json`，加文案时**两个文件都要加**（`default.json` 是兜底层，缺键会直接显示键名）。

**故意不翻译的**：

- **SMAPI 控制台日志**：给调试的人看，统一一种语言更好检索。
- **HTTP API 的错误信息**与 **`/activity/recent` 的 `text` 字段**：这些是喂给 agent 的，而 agent 内部一律用英文（见[语言与 language 字段](#语言与-language-字段)），翻译反而会打断这条链路。
- **`manifest.json`**：SMAPI 不本地化清单，安装界面固定显示英文。

> 对话里**角色说的话**不在这套文案里，那是 agent 按请求的 `language` 字段返回的，由 agent 负责翻译。

## 通用约定

- **响应格式**：`application/json; charset=utf-8`，字段名为 **camelCase**，`null` 字段会被省略，输出为紧凑格式（无缩进）。
- **统一外层结构**：

  ```json
  {
    "ok": true,
    "data": { },
    "date": { },
    "error": { "code": "...", "message": "..." }
  }
  ```

  - 成功：`ok: true`，携带 `data`；列表类接口额外携带 `date`（当前游戏内日期）。
  - 失败：`ok: false`，携带 `error`，无 `data`/`date`。
- **`date` 字段**：仅出现在列表类接口（`/events/*`、`/birthdays/*`、`/calendar`、`/relationship`、`/activity/recent`）中，值是**当前**游戏内日期，与查询的 season/day 无关。`/health`、`/date`、`/household`、`/state` 不携带该字段。
- **路径大小写不敏感**，且会忽略结尾斜杠（`/Health/` 等价于 `/health`）。
- **数据读取在主线程执行**，超时 5 秒；超时返回 `503 game_busy`。

## API 列表

| 方法 | 路径 | 查询参数 | 说明 |
| --- | --- | --- | --- |
| GET | `/` 或 `/health` | 无 | 健康检查 |
| GET | `/date` | 无 | 当前游戏内日期 |
| GET | `/events/today` | 无 | 今天的全部事件 |
| GET | `/events/day` | `season`, `day`（必填） | 指定日期的全部事件 |
| GET | `/birthdays/week` | `includePast`（可选） | 本周生日 |
| GET | `/birthdays/day` | `season`, `day`（必填） | 指定日期的生日 |
| GET | `/calendar` | `season`（可选） | 整季（28 天）日历 |
| GET | `/household` | 无 | 农夫 / 农场 / 配偶 / 宠物 / 孩子 |
| GET | `/relationship` | `npc`（可选） | 与村民的关系 |
| GET | `/npc/location` | `npc`（必填） | 村民现在在哪、接下来去哪 |
| GET | `/state` | 无 | 玩家当前状态快照 |
| GET | `/activity/recent` | 无 | 最近 3 个游戏日的行为记录 |
| GET | `/events/recent` | `days`（可选） | 今天前后若干天的日历事件 |
| GET | `/gift/tastes` | `npc`（可选） | 村民的礼物喜好 |
| GET | `/gift/suggest` | `npc`（必填）、`limit`（可选） | 送礼推荐 |

查询参数取值：

- `season`：`spring` / `summer` / `fall` / `winter`，并接受别名 `autumn`（等同于 `fall`）。大小写不敏感。
- `day`：整数，范围 `1`–`28`。
- `includePast`：仅 `1` 或 `true`（大小写不敏感）为真，其余一律为假。
- `npc`：村民名字。内部名（`Haley`）和显示名都可以，大小写不敏感。`/relationship` 与 `/gift/tastes` 不传则返回全部；`/gift/suggest` 与 `/npc/location` 必传。`/npc/location` 还要求该村民当前确实在世界里。
- `days`：整数，范围 `0`–`28`，默认 `3`。表示今天往前、往后各看多少天。
- `limit`：整数，范围 `1`–`50`，默认 `10`。`/gift/suggest` 最多返回多少条推荐。

---

### GET /health

健康检查，**无需载入存档**。响应不含 `date` 字段。

```bash
curl http://127.0.0.1:8788/health
```

```json
{
  "ok": true,
  "data": {
    "status": "ok",
    "mod": "HelloStardew",
    "version": "1.4.0",
    "saveLoaded": true
  }
}
```

---

### GET /date

获取当前游戏内日期及其所在周的划分。

```bash
curl http://127.0.0.1:8788/date
```

```json
{
  "ok": true,
  "data": {
    "year": 1,
    "season": "spring",
    "seasonName": "Spring",
    "dayOfMonth": 8,
    "dayOfWeek": "Mon",
    "weekIndex": 1,
    "weekStart": 8,
    "weekEnd": 14,
    "totalDays": 8
  }
}
```

> `weekIndex` 为 0 基、范围 0–3；`weekStart`/`weekEnd` 为该周在季节内的首尾日（1/7、8/14、15/21、22/28）。

---

### GET /events/today

获取今天的全部事件（节日、被动节日、钓鱼大赛、书商、生日）。

```bash
curl http://127.0.0.1:8788/events/today
```

```json
{
  "ok": true,
  "data": [
    { "type": "festival", "id": "spring13", "displayName": "Egg Festival", "locked": false }
  ],
  "date": {
    "year": 1, "season": "spring", "seasonName": "Spring", "dayOfMonth": 13,
    "dayOfWeek": "Sat", "weekIndex": 1, "weekStart": 8, "weekEnd": 14, "totalDays": 13
  }
}
```

---

### GET /events/day

获取指定季节某一天的全部事件。

**参数**

| 参数 | 必填 | 说明 |
| --- | --- | --- |
| `season` | 是 | `spring` / `summer` / `fall` / `winter`（或 `autumn`） |
| `day` | 是 | 1–28 |

```bash
curl "http://127.0.0.1:8788/events/day?season=spring&day=13"
```

```json
{
  "ok": true,
  "data": [
    { "type": "festival", "id": "spring13", "displayName": "Egg Festival", "locked": false },
    { "type": "birthday", "id": "Abigail", "displayName": "Abigail", "locked": false }
  ],
  "date": { "...DateInfo..." }
}
```

> `id` 对于节日为 `季节+日`（如 `spring13`）；对于生日为 NPC 内部名。无法解析名称时会直接回退为 id。

---

### GET /birthdays/week

获取本周生日。默认只返回**今天及本周剩余天数**，不包含本周已过去的日子。

**参数**

| 参数 | 必填 | 默认 | 说明 |
| --- | --- | --- | --- |
| `includePast` | 否 | `false` | 为 `1`/`true` 时包含本周已过去的日子 |

```bash
curl "http://127.0.0.1:8788/birthdays/week"
curl "http://127.0.0.1:8788/birthdays/week?includePast=true"
```

```json
{
  "ok": true,
  "data": [
    {
      "npcName": "Abigail",
      "displayName": "Abigail",
      "season": "spring",
      "day": 13,
      "dayOfWeek": "Sat",
      "daysUntil": 5,
      "isToday": false
    }
  ],
  "date": { "...DateInfo..." }
}
```

---

### GET /birthdays/day

获取指定季节某一天的生日。

**参数**

| 参数 | 必填 | 说明 |
| --- | --- | --- |
| `season` | 是 | `spring` / `summer` / `fall` / `winter`（或 `autumn`） |
| `day` | 是 | 1–28 |

```bash
curl "http://127.0.0.1:8788/birthdays/day?season=fall&day=13"
```

```json
{
  "ok": true,
  "data": [
    {
      "npcName": "Abigail",
      "displayName": "Abigail",
      "season": "fall",
      "day": 13,
      "dayOfWeek": "Sat",
      "daysUntil": -10,
      "isToday": false
    }
  ],
  "date": { "...DateInfo..." }
}
```

> `daysUntil` 可能为负数（表示该生日在本季已过去）。

---

### GET /calendar

获取某一季完整的 28 天日历，等价于游戏内日历菜单。**不会**跳过已过去的日子。

**参数**

| 参数 | 必填 | 默认 | 说明 |
| --- | --- | --- | --- |
| `season` | 否 | 当前季节 | `spring` / `summer` / `fall` / `winter`（或 `autumn`） |

```bash
curl "http://127.0.0.1:8788/calendar?season=summer"
```

```json
{
  "ok": true,
  "data": [
    { "day": 1, "dayOfWeek": "Mon", "isToday": false, "isPast": false, "events": [] },
    {
      "day": 11, "dayOfWeek": "Thu", "isToday": false, "isPast": false,
      "events": [
        { "type": "birthday", "id": "Maru", "displayName": "Maru", "locked": false }
      ]
    }
  ],
  "date": { "...DateInfo..." }
}
```

> `data` 固定包含 28 个元素。`isToday` / `isPast` 仅在查询当前季节时有意义，否则均为 `false`。

---

### GET /household

获取农夫、农场、配偶、宠物与孩子的名字。**不携带 `date` 字段**（这些信息与日期无关）。

```bash
curl http://127.0.0.1:8788/household
```

```json
{
  "ok": true,
  "data": {
    "farmerName": "Cyber",
    "farmName": "Sunny Farm",
    "spouseName": "Haley",
    "spouseDisplayName": "Haley",
    "daysMarried": 123,
    "petName": "Rex",
    "petType": "dog",
    "children": [
      { "name": "Luna", "displayName": "Luna", "gender": "female", "ageState": 3, "ageName": "toddler", "daysOld": 61 }
    ]
  }
}
```

> `spouseName` / `spouseDisplayName` / `daysMarried` 在未婚且无室友时为 `null` / `null` / `0`。
> `petName` 是玩家给宠物起的名字，`petType` 是品种（`cat` / `dog`），两者不同。
> `children` 在没有孩子时为空数组。

---

### GET /relationship

查询玩家与村民的关系。

**参数**

| 参数 | 必填 | 说明 |
| --- | --- | --- |
| `npc` | 否 | 村民名字，内部名或显示名均可，大小写不敏感。不传则返回全部 |

**查询单个村民**——`data` 是一个对象：

```bash
curl "http://127.0.0.1:8788/relationship?npc=Haley"
```

```json
{
  "ok": true,
  "data": {
    "npc": "Haley",
    "displayName": "Haley",
    "relationship": "married",
    "friendshipPoints": 2450,
    "hearts": 9,
    "maxHearts": 14,
    "daysMarried": 123,
    "talkedToToday": true,
    "giftsThisWeek": 2,
    "birthdaySeason": "spring",
    "birthdayDay": 14
  },
  "date": { "...DateInfo..." }
}
```

**查询全部村民**——`data` 是一个数组，按好感度从高到低排序：

```bash
curl http://127.0.0.1:8788/relationship
```

```json
{
  "ok": true,
  "data": [
    { "npc": "Haley", "displayName": "Haley", "relationship": "married", "friendshipPoints": 2450, "hearts": 9, "maxHearts": 14, "...": "..." },
    { "npc": "Abigail", "displayName": "Abigail", "relationship": "friendly", "friendshipPoints": 1200, "hearts": 4, "maxHearts": 8, "...": "..." }
  ],
  "date": { "...DateInfo..." }
}
```

> **注意两种形态**：带 `npc` 返回对象，不带返回数组。`npc` 不存在时返回 `404 unknown_npc`。
>
> `hearts` 用的是游戏自身的算法：`friendshipPoints / 250`（向下取整）。`maxHearts` 取决于该村民是否可攻略以及当前关系：
>
> | 情况 | `maxHearts` |
> | --- | --- |
> | 不可攻略的普通村民 | 10 |
> | 可攻略但尚未交往 | 8 |
> | 交往 / 订婚中 | 10 |
> | 配偶 | 14 |
>
> 所以「9 心 / 14 心」的配偶比「9 心 / 10 心」的普通朋友还有更多相处空间。
>
> `daysUntilWedding` 只在订婚状态下出现（其他情况该字段被省略）。

---

### GET /npc/location

某个村民现在在哪、接下来会去哪。

数据有两块，**它们可能不一致**：

- **计划**：`NPC.Schedule`，是游戏当天按季节 / 星期 / 天气 / 婚后状态从 `Characters/schedules/<Name>` 解析好的结果，条件已经替你判断完了。
  它的 key 是**出发时刻**（不是到达时刻），和 `Game1.timeOfDay` 同一域（`930` = 9:30）。
  「现在应该在哪」= 所有不超过当前时刻的 key 里最大的那一条。
- **现实**：NPC 的实际坐标。节日 / 事件、剧情都会临时覆盖日程，所以**两者冲突时以实际坐标为准**。

**参数**

| 参数 | 必填 | 说明 |
| --- | --- | --- |
| `npc` | 是 | 村民名字（内部名或显示名，大小写不敏感）。必须是当前已出现在世界里的村民 |

```bash
curl "http://127.0.0.1:8788/npc/location?npc=Haley"
```

```json
{
  "ok": true,
  "data": {
    "npc": "Haley",
    "displayName": "Haley",
    "now": { "time": 1050, "name": "10:50 AM" },
    "current": {
      "location": "Town",
      "locationName": "Pelican Town",
      "tile": { "x": 42, "y": 71 },
      "isMoving": true,
      "isTravelling": true,
      "travellingTo": "Town",
      "travellingToName": "Pelican Town",
      "hasReachedTarget": false,
      "isInEvent": false
    },
    "home": { "location": "HaleyHouse", "locationName": "Haley's House", "tile": { "x": 4, "y": 10 } },
    "scheduleKey": "spring",
    "currentTarget": {
      "time": { "time": 1000, "name": "10:00 AM" },
      "destination": { "location": "Town", "locationName": "Pelican Town", "tile": { "x": 82, "y": 30 } },
      "facingDirection": 2,
      "facing": "down",
      "endOfRouteBehavior": "square_5_3",
      "endOfRouteMessage": null
    },
    "nextTarget": {
      "time": { "time": 1300, "name": "1:00 PM" },
      "destination": { "location": "Beach", "locationName": "The Beach", "tile": { "x": 53, "y": 19 } },
      "facingDirection": 2,
      "facing": "down",
      "endOfRouteBehavior": null,
      "endOfRouteMessage": null
    }
  },
  "date": { "...DateInfo..." }
}
```

> **怎么读这份数据**
>
> | 想知道 | 看哪 |
> | --- | --- |
> | 现在在哪 | `current.location` / `current.locationName` + `current.tile` |
> | 正在路上吗 | `current.isTravelling`，终点是 `current.travellingTo` |
> | 计划在哪 | `currentTarget.destination`；为空说明今天的日程还没开始 |
> | 待会儿去哪 | `nextTarget` |
> | 今天为什么是这个日程 | `scheduleKey`，如 `spring` / `rain` / `marriage` |
> | 找不到人怎么办 | `home`，日程开始前通常在家 |
>
> **注意事项**
>
> - `currentTarget.time` 是**出发时间**。到点后还在走路，跨图时游戏常直接 warp，所以到达会有几分钟延迟。
> - `hasReachedTarget` 是精确比较（图和格子都相等）。走在半路时为 `false` 是正常的。
> - `isInEvent` 为 `true` 时（节日 / 事件）日程不可信，只能看实际坐标。
> - `currentTarget` / `nextTarget` 为 `null` 有两种原因：日程为空（清晨、换天尚未加载），或今天的日程已经全部走完。
> - 时间不要自己做时钟换算，整数比较即可；要展示用 `now.name` / `time.name`，它们已经是本地化过的。
> - 地点给的是**内部名**，`locationName` 才是可读名（来自 `Data/Locations` 的 `DisplayName`）。
> - 多实例 NPC（如小屋住户）用 `NameOrUniqueName` 区分，所以 `current.location` 可能是 `Cabin1234` 这种带后缀的名字，此时 `locationName` 会退回内部名。

---

### GET /state

玩家当前的状态快照。**不携带 `date` 字段**——日期时间信息已包含在 `data` 内。

```bash
curl http://127.0.0.1:8788/state
```

```json
{
  "ok": true,
  "data": {
    "year": 2,
    "season": "spring",
    "dayOfMonth": 8,
    "dayOfWeek": "Mon",
    "totalDays": 120,
    "timeOfDay": 1210,
    "timeOfDayName": "12:10 PM",
    "timeOfDayPhase": "afternoon",
    "locationName": "Farm",
    "locationDisplayName": "Farm",
    "locationIsOutdoors": true,
    "locationIsFarm": true,
    "isFestivalDay": false,
    "money": 15420,
    "stamina": 214.0,
    "maxStamina": 270,
    "health": 100,
    "maxHealth": 100,
    "weather": "rain",
    "skills": [
      { "name": "farming", "level": 6, "experience": 15800 },
      { "name": "fishing", "level": 3, "experience": 4200 },
      { "name": "foraging", "level": 4, "experience": 7100 },
      { "name": "mining", "level": 5, "experience": 11300 },
      { "name": "combat", "level": 2, "experience": 2600 },
      { "name": "luck", "level": 0, "experience": 0 }
    ],
    "inventory": {
      "slotsUsed": 7,
      "slotsTotal": 36,
      "totalItems": 143,
      "items": [
        { "name": "Parsnip", "qualifiedItemId": "(O)24", "stack": 12 }
      ]
    },
    "currentToolName": "Axe",
    "currentItemName": "Axe"
  }
}
```

> `timeOfDay` 是游戏的整数时钟（`1210` = 12:10），`timeOfDayName` 是本地化后的显示文本。
> `timeOfDayPhase` 是便于 agent 判断的粗粒度分档：`morning`（<12:00）/ `afternoon`（<17:00）/ `evening`（<20:00）/ `night`。
> `weather` 取值：`sunny` / `wind` / `rain` / `storm` / `snow` / `green_rain`。
> `stamina` 是浮点数，游戏内部即如此存储。
> 技能等级读取的是**不含增益**的裸等级，因此临时食物增益不会影响数值。
> `locationIsOutdoors` / `locationIsFarm` 便于 agent 判断能否放置物品、是否需要带工具。
> 未手持工具或物品时，`currentToolName` / `currentItemName` 会被省略。

---

### GET /activity/recent

玩家最近 **3 个游戏日**做过的行为，按时间从早到晚排列。携带 `date` 字段。

```bash
curl http://127.0.0.1:8788/activity/recent
```

```json
{
  "ok": true,
  "data": [
    { "type": "location", "text": "Went to Farm", "amount": 1, "year": 2, "season": "spring", "dayOfMonth": 8, "totalDays": 120, "timeOfDay": 610 },
    { "type": "fish", "text": "Caught 3 fish", "amount": 3, "year": 2, "season": "spring", "dayOfMonth": 8, "totalDays": 120, "timeOfDay": 720 },
    { "type": "talk", "text": "Talked to Abigail", "amount": 1, "npc": "Abigail", "year": 2, "season": "spring", "dayOfMonth": 8, "totalDays": 120, "timeOfDay": 1140 },
    { "type": "gift", "text": "Gave Abigail a gift", "amount": 1, "npc": "Abigail", "year": 2, "season": "spring", "dayOfMonth": 8, "totalDays": 120, "timeOfDay": 1150 },
    { "type": "money", "text": "Earned 850g", "amount": 850, "year": 2, "season": "spring", "dayOfMonth": 8, "totalDays": 120, "timeOfDay": 1600 }
  ],
  "date": { "...DateInfo..." }
}
```

`type` 取值：

| `type` | 含义 | `amount` |
| --- | --- | --- |
| `talk` | 和某村民说话 | 固定 1 |
| `gift` | 送礼给某村民 | 送出的数量 |
| `fish` | 钓鱼 | 钓到的数量 |
| `ship` | 出货（把东西放进出货箱） | 件数 |
| `combat` | 击败怪物 | 数量 |
| `forage` | 采集 | 数量 |
| `craft` | 制作 | 数量 |
| `cook` | 烹饪 | 数量 |
| `geode` | 敲开晶球 | 数量 |
| `quest` | 完成委托 | 数量 |
| `levelup` | 技能升级 | 升到的等级 |
| `money` | 赚钱或花钱 | 金额（正数） |
| `location` | 移动到新地点 | 固定 1 |

> `npc` 字段只在 `talk` 和 `gift` 上出现。
> 只有金额变动达到 100g 才记录 `money`，避免日常买卖淹没日志。
> 快速连续移动（例如连续下矿层）会合并成一条同一 10 分钟内的记录。
> 日志上限 300 条，超出后丢弃最早的记录。

---

### GET /events/recent

今天前后若干天的日历事件，按日期从早到晚排列，等价于把 `/events/day` 在一定范围内展开。携带 `date` 字段。

**参数**

| 参数 | 必填 | 默认 | 说明 |
| --- | --- | --- | --- |
| `days` | 否 | `3` | 今天往前、往后各看多少天，范围 `0`–`28` |

```bash
curl "http://127.0.0.1:8788/events/recent?days=3"
```

```json
{
  "ok": true,
  "data": [
    { "type": "birthday", "id": "Abigail", "displayName": "Abigail", "locked": false, "season": "spring", "dayOfMonth": 6, "year": 2, "daysOffset": -2, "isPast": true },
    { "type": "festival", "id": "spring13", "displayName": "Egg Festival", "locked": false, "season": "spring", "dayOfMonth": 13, "year": 2, "daysOffset": 5, "isPast": false }
  ],
  "date": { "...DateInfo..." }
}
```

> `daysOffset` 是相对今天的天数：负数表示已过去，`0` 表示今天，正数表示还没到。
> 跨季节、跨年时会自动换算 `season` 与 `year`。
> 书商日期只在查询当前季节时才出现，因为游戏的种子数据只覆盖当前季节。

---

### GET /gift/tastes

读取游戏的 `Data/NPCGiftTastes` 资产：每个村民最爱 / 喜欢 / 不喜欢 / 最恨 / 普通各是什么，外加所有村民共用的 `Universal_*` 列表。

**参数**

| 参数 | 必填 | 说明 |
| --- | --- | --- |
| `npc` | 否 | 村民名字（内部名或显示名，大小写不敏感）。不传则返回全部村民 |

```bash
curl "http://127.0.0.1:8788/gift/tastes?npc=Abigail"
```

```json
{
  "ok": true,
  "data": {
    "npc": "Abigail",
    "displayName": "Abigail",
    "tastes": {
      "love": [
        { "id": "(O)66", "name": "Amethyst", "kind": "item" },
        { "id": "-4", "name": "Fish", "kind": "category" },
        { "id": "category_fish", "name": "category_fish", "kind": "context_tag" }
      ],
      "like": [ "..." ],
      "dislike": [ "..." ],
      "hate": [ "..." ],
      "neutral": [ "..." ]
    }
  },
  "date": { "...DateInfo..." }
}
```

不传 `npc` 时返回整个目录：

```json
{
  "ok": true,
  "data": {
    "universal": {
      "love": [ "..." ], "like": [ "..." ], "dislike": [ "..." ], "hate": [ "..." ], "neutral": [ "..." ]
    },
    "villagers": [
      { "npc": "Abigail", "displayName": "Abigail", "tastes": { "...GiftTasteSetInfo..." } }
    ]
  },
  "date": { "...DateInfo..." }
}
```

> **条目有三种 `kind`**，因为游戏把三类东西混在同一个空格分隔的字段里，三者匹配方式完全不同：
>
> | `kind` | `id` 示例 | `name` 示例 | 含义 |
> | --- | --- | --- | --- |
> | `item` | `(O)66` | `Amethyst` | 具体物品。`id` 是解析后的**合格物品 ID** |
> | `category` | `-4` | `Fish` | 一整个物品分类，游戏拿 `Object.Category` 的负数去匹配 |
> | `context_tag` | `category_fish` | `category_fish` | 上下文标签，匹配所有带该标签的物品（`fish_river`、`fish_ocean` 等） |
>
> **`universal` 是所有村民共用的兜底规则**（游戏里的 `Universal_Love` / `Universal_Like` / `Universal_Dislike` / `Universal_Hate` / `Universal_Neutral`）。
> 它和村民自己的列表是**分开**的两套数据：游戏会先查 universal，再查该村民自己的条目，最后才落到「可食用但为负价值 → 讨厌」「价格低于 20 → 不喜欢」这类默认规则上。
> 因此 `love` 列表里没有某样东西，不代表送它就一定没有加成。
>
> 数据来自 `Data/NPCGiftTastes`，与当前日期无关；但**仍需载入存档**，因为显示名要查 `Data/Characters`。

---

### GET /gift/suggest

从**玩家背包**和**世界上所有箱子**里挑出该送什么，用游戏自己的 `NPC.getGiftTasteForThisItem` 逐件打分，换算成预计好感收益后排序。

**参数**

| 参数 | 必填 | 默认 | 说明 |
| --- | --- | --- | --- |
| `npc` | 是 | — | 村民名字（内部名或显示名，大小写不敏感） |
| `limit` | 否 | `10` | `suggestions` 与 `avoid` 各自返回的条数上限，范围 `1`–`50` |

```bash
curl "http://127.0.0.1:8788/gift/suggest?npc=Abigail&limit=3"
```

```json
{
  "ok": true,
  "data": {
    "npc": "Abigail",
    "displayName": "Abigail",
    "limits": {
      "giftsToday": 0,
      "giftsThisWeek": 1,
      "maxGiftsPerWeek": 2,
      "canGiveToday": true,
      "weeklyLimitReached": false,
      "isBirthday": false,
      "friendshipMultiplier": 1
    },
    "suggestions": [
      {
        "name": "Amethyst",
        "qualifiedItemId": "(O)66",
        "taste": "love",
        "tasteLevel": 0,
        "quality": 2,
        "qualityName": "gold",
        "points": 100,
        "totalCount": 3,
        "sources": [
          { "kind": "inventory", "location": "Farmhouse", "stack": 1 },
          { "kind": "chest", "location": "Farmhouse", "stack": 2 }
        ]
      }
    ],
    "avoid": [
      {
        "name": "Clay",
        "qualifiedItemId": "(O)330",
        "taste": "hate",
        "tasteLevel": 6,
        "quality": 0,
        "qualityName": "normal",
        "points": -40,
        "totalCount": 12,
        "sources": [ { "kind": "chest", "location": "Cabin", "stack": 12 } ]
      }
    ]
  },
  "date": { "...DateInfo..." }
}
```

> `points` 是**一次**送礼的好感收益，比照 `NPC.receiveGift` 的算法：`基础分 × 品质系数 × 日期系数`。
>
> | `taste` | `tasteLevel` | 基础好感 |
> | --- | --- | --- |
> | `love` | 0 | +80 |
> | `like` | 2 | +45 |
> | `dislike` | 4 | −20 |
> | `hate` | 6 | −40 |
> | `stardrop_tea` | 7 | +250（上限 +750，且不吃品质系数） |
>
> 品质系数：普通 ×1、银 ×1.1、金 ×1.25、铱 ×1.5（星之果实茶除外）。
> 日期系数：对方生日 ×8，已婚配偶再 ÷2，其余 ×1 —— 也就是 `friendshipMultiplier` 字段，`isBirthday` 是同一件事的布尔形式。
>
> `suggestions` 只收录 `love` / `like` / `stardrop_tea`，`avoid` 只收录 `dislike` / `hate`，两者都按 `points` 排序（`avoid` 最差的在前）。
> 同一件物品按「物品 + 品质」合并：`totalCount` 是各处加起来的数量，`sources` 按地点汇总，告诉你它在哪、那里一共有几个。
> 箱子会递归进农舍、小屋、畜棚等建筑的内部，去重后一起扫描。

> **额度**：`giftsToday` 是今天已送次数（普通礼物每天 1 次），`giftsThisWeek` 是本周已送次数（上限 `maxGiftsPerWeek` = 2）。
> `blockedReason` 给出拒收原因：`daily_limit`（今天已送过）、`weekly_limit`（本周额度用尽）、`divorced`（离过婚，对方拒收）、`cannot_receive_gifts`（该角色本来就不收礼）；为 `null` 时 `canGiveToday` 为 `true`。
> 对方的**生日**、**配偶**、**孩子**身份以及**星之果实茶**会绕过每周上限检查，所以 `weeklyLimitReached` 为 `true` 时生日礼物依然有效。
> 这些限制只描述普通礼物；`limits` 不因背包里有没有星之果实茶而变化。

## 错误码

失败响应示例：

```json
{
  "ok": false,
  "error": {
    "code": "invalid_day",
    "message": "Query parameter 'day' must be an integer between 1 and 28."
  }
}
```

| HTTP 状态 | `error.code` | 触发条件 |
| --- | --- | --- |
| 400 | `missing_season` | `/events/day`、`/birthdays/day` 缺少 `season` |
| 400 | `invalid_season` | `season` 不是 spring/summer/fall/winter（或 autumn） |
| 400 | `invalid_day` | `day` 缺失或不是 1–28 的整数 |
| 400 | `invalid_days` | `/events/recent` 的 `days` 不是 0–28 的整数 |
| 400 | `missing_npc` | `/gift/suggest`、`/npc/location` 缺少 `npc` |
| 400 | `invalid_limit` | `/gift/suggest` 的 `limit` 不是 1–50 的整数 |
| 404 | `not_found` | 路径不存在 |
| 404 | `unknown_npc` | `/relationship` 指定了玩家尚未认识的村民；`/gift/tastes`、`/gift/suggest` 指定了礼物数据里没有的名字；`/npc/location` 指定了一个不存在的名字 |
| 404 | `npc_unavailable` | 该村民存在于游戏数据里，但当前不在世界中（例如第一年的 Kent），因此读不到日程。`/gift/suggest` 与 `/npc/location` 都可能返回 |
| 500 | `internal_error` | 未处理的服务端异常 |
| 503 | `no_save_loaded` | 尚未载入存档 |
| 503 | `game_busy` | 主线程调用超过 5 秒超时 |

## 数据结构

### DateInfo

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `year` | int | 游戏年份 |
| `season` | string | 季节键：`spring` / `summer` / `fall` / `winter` |
| `seasonName` | string | 本地化季节名，如 `Spring` |
| `dayOfMonth` | int | 季节内日期，1–28 |
| `dayOfWeek` | string | 星期简称，如 `Mon` |
| `weekIndex` | int | 季节内第几周，0–3 |
| `weekStart` | int | 所在周首日，1/8/15/22 |
| `weekEnd` | int | 所在周末日，7/14/21/28 |
| `totalDays` | int | 存档累计天数 |

### BirthdayInfo

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `npcName` | string | NPC 内部名 |
| `displayName` | string | 显示名 |
| `season` | string | 生日所在季节键 |
| `day` | int | 生日日期，1–28 |
| `dayOfWeek` | string | 星期简称 |
| `daysUntil` | int | 距今天的天数，已过为负 |
| `isToday` | bool | 是否就是今天 |

### CalendarEventInfo

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `type` | string | `festival` / `passive_festival` / `fishing_derby` / `bookseller` / `birthday` |
| `id` | string | 事件 ID（节日为 `季节+日`，生日为 NPC 名） |
| `displayName` | string | 显示名；条件未满足的被动节日为 `???` |
| `locked` | bool | 事件存在但条件未满足（游戏内显示为 `???`） |

### DayCalendarInfo

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `day` | int | 日期，1–28 |
| `dayOfWeek` | string | 星期简称 |
| `isToday` | bool | 是否为今天（仅当前季节） |
| `isPast` | bool | 是否已过去（仅当前季节） |
| `events` | CalendarEventInfo[] | 该日全部事件 |

### HouseholdInfo

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `farmerName` | string | 农夫名字 |
| `farmName` | string | 农场名字 |
| `spouseName` | string? | 配偶内部名，无配偶时为 `null` |
| `spouseDisplayName` | string? | 配偶显示名 |
| `daysMarried` | int | 结婚天数，未婚为 `0` |
| `petName` | string? | 玩家给宠物起的名字 |
| `petType` | string? | 宠物品种，如 `cat` / `dog` |
| `children` | ChildInfo[] | 孩子列表，无孩子时为空数组 |

### ChildInfo

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `name` | string | 内部名 |
| `displayName` | string | 显示名 |
| `gender` | string | `male` / `female` |
| `ageState` | int | 成长阶段：0 新生儿 / 1 婴儿 / 2 爬行 / 3 学步 |
| `ageName` | string | 上述阶段的英文名 |
| `daysOld` | int | 出生天数 |

### RelationshipInfo

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `npc` | string | 村民内部名 |
| `displayName` | string | 显示名 |
| `relationship` | string | `friendly` / `dating` / `engaged` / `married` / `roommate` / `divorced` |
| `friendshipPoints` | int | 好感度点数 |
| `hearts` | int | `friendshipPoints / 250` |
| `maxHearts` | int | 该村民的心数上限（8 / 10 / 14，见上文说明） |
| `daysMarried` | int | 结婚天数 |
| `daysUntilWedding` | int? | 距婚礼天数，仅订婚时出现 |
| `talkedToToday` | bool | 今天是否已交谈 |
| `giftsThisWeek` | int | 本周送礼次数 |
| `birthdaySeason` | string? | 生日季节键，无生日时为 `null` |
| `birthdayDay` | int? | 生日日期，无生日时为 `null` |

### PlayerStateInfo

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `year` / `season` / `dayOfMonth` / `dayOfWeek` / `totalDays` | — | 同 DateInfo |
| `timeOfDay` | int | 游戏整数时钟，`1210` = 12:10 |
| `timeOfDayName` | string | 本地化时间文本 |
| `timeOfDayPhase` | string | `morning` / `afternoon` / `evening` / `night` |
| `locationName` | string | 地点内部名 |
| `locationDisplayName` | string | 地点显示名 |
| `locationIsOutdoors` | bool | 是否为室外 |
| `locationIsFarm` | bool | 是否为农场 |
| `isFestivalDay` | bool | 今天是否为节日 |
| `money` | int | 当前金钱 |
| `stamina` | float | 剩余体力 |
| `maxStamina` | int | 体力上限 |
| `health` | int | 当前生命 |
| `maxHealth` | int | 生命上限 |
| `weather` | string | `sunny` / `wind` / `rain` / `storm` / `snow` / `green_rain` |
| `skills` | SkillInfo[] | 六项技能，固定 6 个元素 |
| `inventory` | InventoryInfo | 背包摘要 |
| `currentToolName` | string? | 手持工具名 |
| `currentItemName` | string? | 手持物品名 |

### SkillInfo

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `name` | string | `farming` / `fishing` / `foraging` / `mining` / `combat` / `luck` |
| `level` | int | 不含增益的裸等级 |
| `experience` | int | 该技能累计经验 |

### InventoryInfo

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `slotsUsed` | int | 已占用格数 |
| `slotsTotal` | int | 背包总格数 |
| `totalItems` | int | 所有物品的堆叠总数 |
| `items` | ItemStackInfo[] | 每格内容，跳过空格 |

`ItemStackInfo`：`name`（显示名）、`qualifiedItemId`（如 `(O)24`）、`stack`（堆叠数）。

### ActivityEntry

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `type` | string | 行为类型，取值见 `/activity/recent` 的表 |
| `text` | string | 可读描述，如 `Gave Abigail a gift` |
| `amount` | int | 数量或金额，至少为 1 |
| `npc` | string? | 涉及的村民，仅 `talk` / `gift` 有 |
| `year` / `season` / `dayOfMonth` / `totalDays` / `timeOfDay` | — | 发生时间 |

### RecentEventInfo

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `type` | string | 同 CalendarEventInfo |
| `id` | string | 同 CalendarEventInfo |
| `displayName` | string | 同 CalendarEventInfo |
| `locked` | bool | 同 CalendarEventInfo |
| `season` | string | 事件所在季节键 |
| `dayOfMonth` | int | 事件日期 |
| `year` | int | 事件所在年份 |
| `daysOffset` | int | 相对今天的天数，负数为已过去 |
| `isPast` | bool | 是否已过去 |

### TileInfo

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `x` / `y` | int | 地块坐标，不是像素坐标 |

### ClockInfo

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `time` | int | 游戏时钟值，和 `Game1.timeOfDay` 同域（`930` = 9:30） |
| `name` | string | 本地化后的时间文本，如 `9:30 AM` |

### LocationRefInfo

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `location` | string | 地图内部名，如 `HaleyHouse` |
| `locationName` | string | 地图显示名，如 `Haley's House` |
| `tile` | TileInfo | 在该地图内的地块坐标 |

### NpcPresenceInfo

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `location` | string | 实际所在图的内部名（`NameOrUniqueName`） |
| `locationName` | string | 实际所在图的显示名 |
| `tile` | TileInfo | 实际所在的地块坐标 |
| `isMoving` | bool | 是否在移动（含路径走完但仍在插值的情况） |
| `isTravelling` | bool | 是否正在走一段日程路线 |
| `travellingTo` | string? | 这一段路线的终点图内部名，仅在赶路时有值 |
| `travellingToName` | string? | `travellingTo` 的显示名 |
| `hasReachedTarget` | bool | 是否已站在 `currentTarget` 的目标格子上（精确比较） |
| `isInEvent` | bool | 是否有节日 / 事件正在覆盖日程 |

### ScheduleStopInfo

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `time` | ClockInfo | **出发**时间，不是到达时间 |
| `destination` | LocationRefInfo | 这一段的终点图与格子 |
| `facingDirection` | int | 到达后朝向：0 上 / 1 右 / 2 下 / 3 左 |
| `facing` | string | `up` / `right` / `down` / `left` |
| `endOfRouteBehavior` | string? | 到达后的脚本行为，如 `sleep`、`square_5_3` |
| `endOfRouteMessage` | string? | 到达后要说的台词 key |

### NpcLocationInfo

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `npc` | string | 游戏内部名 |
| `displayName` | string | 显示名 |
| `now` | ClockInfo | 计算这份回答时的游戏内时间 |
| `current` | NpcPresenceInfo | 实际位置 |
| `home` | LocationRefInfo? | 村民自己的家；数据里没有时省略 |
| `scheduleKey` | string? | 今天选中的日程键，如 `spring` / `rain` / `marriage` |
| `currentTarget` | ScheduleStopInfo? | 现在应该生效的日程；日程尚未开始或已走完时该字段被省略 |
| `nextTarget` | ScheduleStopInfo? | 下一个日程点 |

### GiftEntryInfo

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | string | 物品的合格 ID、负数分类号，或上下文标签原文 |
| `name` | string | 物品显示名、分类名；上下文标签没有名字时就是标签本身 |
| `kind` | string | `item` / `category` / `context_tag` |

### GiftTasteSetInfo

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `love` / `like` / `dislike` / `hate` / `neutral` | GiftEntryInfo[] | 五档各自的内容，可能为空数组 |

### VillagerGiftTastesInfo

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `npc` | string | 游戏内部名，例如 `Abigail` |
| `displayName` | string | 显示名，来自 `Data/Characters` |
| `tastes` | GiftTasteSetInfo | 该村民的五档喜好 |

### GiftTasteCatalogInfo

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `universal` | GiftTasteSetInfo | 所有村民共用的兜底列表 |
| `villagers` | VillagerGiftTastesInfo[] | 每个村民一份，按显示名排序 |

### GiftItemSourceInfo

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `kind` | string | `inventory`（背包）或 `chest`（箱子） |
| `location` | string | 箱子所在地点；背包则是农夫当前位置 |
| `stack` | int | 该地点这一处一共有多少个（同地点的多个箱子已合并） |

### GiftOptionInfo

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `name` | string | 物品显示名 |
| `qualifiedItemId` | string | 物品的合格 ID |
| `taste` | string | `love` / `like` / `dislike` / `hate` / `stardrop_tea` |
| `tasteLevel` | int | 游戏自己的档位常量 |
| `quality` | int | 0 普通 / 1 银 / 2 金 / 4 铱 |
| `qualityName` | string | `normal` / `silver` / `gold` / `iridium` |
| `points` | int | 一次送礼的预计好感收益（含全部系数） |
| `totalCount` | int | 各处加起来的持有数量 |
| `sources` | GiftItemSourceInfo[] | 分别放在哪里、各有多少 |

### GiftLimitInfo

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `giftsToday` | int | 今天已送次数 |
| `giftsThisWeek` | int | 本周已送次数 |
| `maxGiftsPerWeek` | int | 每周上限，游戏固定为 2 |
| `canGiveToday` | bool | 现在送礼是否还会被接受 |
| `weeklyLimitReached` | bool | 本周普通礼物额度是否已用尽 |
| `isBirthday` | bool | 今天是否是对方生日 |
| `friendshipMultiplier` | float | 好感倍率：生日 8、配偶再减半、其余 1 |
| `blockedReason` | string? | 拒收原因，可接受时为 `null` |

### GiftAdviceInfo

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `npc` | string | 游戏内部名 |
| `displayName` | string | 显示名 |
| `limits` | GiftLimitInfo | 额度与倍率 |
| `suggestions` | GiftOptionInfo[] | 值得送的（`love` / `like` / `stardrop_tea`），`points` 高的在前 |
| `avoid` | GiftOptionInfo[] | 别送的（`dislike` / `hate`），`points` 低的在前 |

## 近期行为是怎么记录的

`/activity/recent` 的数据不是游戏自带的，而是本模组自己记录的。理解其原理有助于判断它能答什么、不能答什么。

**做法**：在每次 `UpdateTicked` 中按约 1 秒的间隔，读取一组「开销极小」的游戏数值（统计计数器、金钱、技能等级、每个村民的 `TalkedToToday` / `GiftsToday`、当前地点），与上一次采样做差，把出现的增量记成一条行为。

**为什么不用 Harmony 补丁或逐个行为的事件**：存档里的计数器和友情标记本身就已经反映了「发生了什么」，做差既不需要介入游戏代码路径，也不容易被其它改动了行为实现方式的模组破坏。

**局限性**：

- 只在游戏运行时记录。模组安装之前的历史无法追溯。
- 采样间隔约 1 秒，同一秒内合并发生的同类行为会被合并成一条（`amount` 累加）。
- 只有下面这些维度会被记录：钓鱼、出货、战斗、采集、制作、烹饪、开晶球、完成委托、技能升级、金钱变动（≥100g）、与村民交谈 / 送礼、地点移动。诸如「锄地」「浇水」「砍树」目前**不在**记录范围内。
- 记录保存在存档中（键名 `helloStardewActivity`），随存档走。多人游戏中只有主机记录。
- 保留最近 3 个游戏日、最多 300 条，超出即丢弃最早的记录。

> 记录完全在游戏主线程上进行，即使内部出错也只会记一条警告日志并让该次采样作废，不会影响游戏或 HTTP 接口。
