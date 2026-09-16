# Hello Stardew

一个《星露谷物语》(Stardew Valley) 的 SMAPI 模组，做两件事：

1. 将游戏内状态通过一个**本地只读 HTTP API** 暴露出来，供外部工具（如 MCP server、脚本、AI agent）查询。包括：
   - **日历**：日期、节日、生日、事件
   - **住所**：农夫 / 农场 / 配偶 / 宠物 / 孩子的名字
   - **关系**：与每个村民的好感度、心数、婚姻状态
   - **状态快照**：时间、地点、金钱、体力、生命、天气、技能、背包
   - **近期行为**：最近 3 个游戏日做过的事（对话、送礼、钓鱼、出货、升级、消费……）
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
  - [GET /household](#get-household)
  - [GET /relationship](#get-relationship)
  - [GET /state](#get-state)
  - [GET /activity/recent](#get-activityrecent)
  - [GET /events/recent](#get-eventsrecent)
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
| GET | `/state` | 无 | 玩家当前状态快照 |
| GET | `/activity/recent` | 无 | 最近 3 个游戏日的行为记录 |
| GET | `/events/recent` | `days`（可选） | 今天前后若干天的日历事件 |

查询参数取值：

- `season`：`spring` / `summer` / `fall` / `winter`，并接受别名 `autumn`（等同于 `fall`）。大小写不敏感。
- `day`：整数，范围 `1`–`28`。
- `includePast`：仅 `1` 或 `true`（大小写不敏感）为真，其余一律为假。
- `npc`：村民名字。内部名（`Haley`）和显示名都可以，大小写不敏感。不传则返回所有已认识的村民。
- `days`：整数，范围 `0`–`28`，默认 `3`。表示今天往前、往后各看多少天。

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
    "version": "1.3.0",
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
| 404 | `not_found` | 路径不存在 |
| 404 | `unknown_npc` | `/relationship` 指定了玩家尚未认识的村民 |
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
