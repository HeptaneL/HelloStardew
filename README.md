# Hello Stardew

一个《星露谷物语》(Stardew Valley) 的 SMAPI 模组，做两件事：

1. 将游戏内的日历数据（日期、节日、生日、事件）通过一个**本地只读 HTTP API** 暴露出来，供外部工具（如 MCP server、脚本、AI agent）查询。
2. 让你可以和配偶进行**自定义对话**：按住 `Alt` 点击配偶输入你想说的话，NPC 的回复会带上几条可选回复。

- **UniqueID**: `heptane.HelloStardew`
- **最低 SMAPI 版本**: `4.0.0`
- **默认地址**: `http://127.0.0.1:8788/`

> 服务端只读，所有接口都通过 HTTP GET 查询。路由本身不校验 HTTP 方法，但仍应仅用 GET。

## 目录

- [安装与运行](#安装与运行)
- [配置](#配置)
- [通用约定](#通用约定)
- [API 列表](#api-列表)
  - [GET /health](#get-health)
  - [GET /date](#get-date)
  - [GET /events/today](#get-eventstoday)
  - [GET /events/day](#get-eventsday)
  - [GET /birthdays/week](#get-birthdaysweek)
  - [GET /birthdays/day](#get-birthdaysday)
  - [GET /calendar](#get-calendar)
- [错误码](#错误码)
- [数据结构](#数据结构)

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
| `EnableSpouseConversation` | bool | `true` | 是否启用与配偶的 AI 对话。 |
| `InitiateTypedDialogueKey` | string | `LeftAlt` | 按住此键点击配偶可输入自己的话。取值同 SMAPI 的 `SButton`。 |
| `AgentEndpoint` | string | `http://127.0.0.1:8000/chat` | 生成台词的 agent 地址。 |
| `AgentTimeoutSeconds` | int | `30` | 单次请求超时秒数。超时后会显示一句兜底台词。 |
| `OfferTypedResponse` | bool | `true` | 选项里是否提供 `*Something else*`（自己打字）。 |

### 游戏内修改配置

安装 [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098)（可选依赖）后，上表所有选项都能在游戏内修改，无需手动编辑文件。

- 在标题界面或游戏内按 GMCM 的快捷键打开菜单，找到 **Hello Stardew**。
- **关闭配置菜单时改动即生效**，不需要重启游戏。
- 改 `BindAddress` / `Port` 会重新绑定监听端口；若新端口已被占用，会保留原有监听并在 SMAPI 控制台报错。

未安装 GMCM 时，手动编辑 `config.json` 后需重启游戏生效。

## 与配偶对话

需要先在游戏里结婚。之后：

1. **按住 `Alt`（可改）点击配偶**，弹出输入框，写下你想说的话。
2. 模组把这句发给 `AgentEndpoint`，拿到回复后当作配偶的台词显示。
3. 同一页会列出可选项：agent 生成的建议回复、`*Stay silent*`、以及 `*Something else*`。
4. 选任意一项会继续下一轮；选 `*Stay silent*` 结束对话。

按 `Enter` 发送，`Esc` 取消。输入框支持退格、方向键、`Home` / `End` / `Delete`。

### agent 返回格式

要让**建议回复**出现，agent 的 `message` 字段需要按这个格式返回：

```
- I had a long day at the farm today. How was yours?
% It was quiet without you.
% I'm exhausted too.
% Let's rest together.
```

- 第一个不以 `%` 开头的行作为配偶的台词（前导 `-` 会被去掉）
- 每个 `%` 开头的行作为一条建议回复

**如果 agent 只返回单行纯文本，功能不会失效**——只是没有建议回复，仍然会显示台词加 `*Stay silent*` / `*Something else*` 两个选项。

### 说明

- **不带历史**：每次只把当前这一句发给 agent，agent 来处理记忆和会话。
- 对话只对配偶生效，其他村民仍走原版对话。
- agent 不可达或超时 → 显示一句兜底台词并在 SMAPI 控制台记 Error 日志。
- 回复文本中的 `#`、`$`、`^`、`¦` 会被过滤，因为它们在原版对话脚本里是标记字符。

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
- **`date` 字段**：仅出现在列表类接口（`/events/*`、`/birthdays/*`、`/calendar`）中，值是**当前**游戏内日期，与查询的 season/day 无关。
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

查询参数取值：

- `season`：`spring` / `summer` / `fall` / `winter`，并接受别名 `autumn`（等同于 `fall`）。大小写不敏感。
- `day`：整数，范围 `1`–`28`。
- `includePast`：仅 `1` 或 `true`（大小写不敏感）为真，其余一律为假。

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
    "version": "1.2.0",
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
| 404 | `not_found` | 路径不存在 |
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
