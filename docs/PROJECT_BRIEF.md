# Toss · Meta VR Glasses 抛硬币应用 — 三方协作共享简报

> 版本：v0.6 ｜ 更新日期：2026-09-25 ｜ 维护：GPT（唯一规则写入者）
> 用途：侃哥（总协调/拍板）、Muse（前沿事实核查 + 创意提案）、GPT（架构研判 + 规则维护 + Gemini 提示词）、Gemini（工程实现）
> **本文件 `docs/PROJECT_BRIEF.md` 是项目唯一事实源（SSOT）。规则、边界、已确认事实与正式决策，以仓库版本为准。**
>
> v0.6 更新（2026-09-25 晚，GPT 合并）：① 正式确立仓库单一规则写入治理：GPT 为 `PROJECT_BRIEF.md` 唯一写入者，Muse 只读并以文字 diff/核查意见反馈，Gemini 仅负责代码实现与代码推送；② 官方比赛截止与项目内部提交目标分离；③ Level A 改为“Confirmed-Capability Path”，避免把交互体验写成硬件保证；④ 明确 RNG 决定结果、Physics 负责呈现的确定性契约；⑤ 新增 Zero-UI ≠ Zero-Feedback；⑥ 新增 Decision Log。
>
> v0.5 更新（2026-09-25 晚，侃哥定）：仓库单一写入原则——GPT 为唯一规则写入人，Muse 只读不写，修改以文字 diff 经侃哥/GPT 写入；Gemini 负责工程代码，不修改项目规则。
>
> v0.4 更新（2026-09-25 晚，侃哥拍板三 AI 协作模型）：① Muse=前沿（Meta 政策/事实）+ 创意总监（产品思路设计）② GPT=开发思路研判 + 给 Gemini 的提示词拟定 + 规则维护 ③ Gemini=最终代码执行 ④ 侃哥=总协调 + 拍板。新增协作工作流与防传话失真规则。
>
> v0.3 更新（2026-09-25 晚）：① 侃哥完成 Meta 开发者账号注册 ② Meta VR Start 申请**已获批**（当晚收到 Welcome 邮件）③ 中国确认在 Start 受支持国家名单内（申请表国家下拉框可选 China）④ Devpost 大赛报名完成（272 participants，官方截止 2026-11-19 04:00 GMT+8）。P0 非开发任务全部关闭。
>
> v0.2 更新（采纳 GPT 2026-09-25 核验反馈）：① Realistic Mode 拆分为 Level A（保底）/ Level B（实验性标志性手势）；② 比赛版 MVP 收缩为 polished vertical slice；③ 新增 Interaction Principle #1；④ VR Start/Devpost 资格列为 P0；⑤ FMM 未知项升级（官方口径 all future devices）。

---

## 1. 项目定位

**一句话**：Meta VR Glasses 上的“空间交互 Hello World”——用“抛硬币”这个全人类通用的动作，一次验证眼镜 App 的基础交互全链路。

- 产品目标表述：**不要做“一个运行在 VR Glasses 上的 Coin Toss App”，要做“现实世界里突然多出了一枚数字硬币”**。用户看到的仍然是自己的手、朋友、桌子、房间，唯一不存在于现实中的东西就是那枚硬币。
- 商业预期：Prototype + Competition Entry + Interaction Research + Portfolio。不提前追求商业化。
- 参赛目标：Meta VR Start Developer Competition（$1M 奖金池，2026-09-24 开始；**官方截止：2026-11-19 04:00 GMT+8**；项目内部提交目标：**2026-11-18**；约 2026-12-11 公布）。
- Toss 属于 casual + hands-first / controller-free 的原生眼镜交互探索方向。

### 1.1 Toss Interaction Principle #1（正式原则）

> **Gaze indicates attention. Hands express intent.**  
> 眼睛表示注意，手表示意图。

- 用户抬头追踪飞起的硬币时，gaze 不得触发任何状态改变（避免眼动误触，也符合真实世界行为）。
- 所有“决定性操作”（抓取、释放、确认）必须由手势表达。
- Gaze 可以用于可视性、注视研究和舒适度分析，但不作为 Toss 核心状态机的隐式确认输入。

### 1.2 Zero-UI 原则（比赛版）

- 不要首页、不要 Dashboard、不要复杂菜单、不要大面积 HUD、不要虚拟房间。
- 传统 App UI 能省则省，让硬币本身成为界面。
- **Zero-UI ≠ Zero-Feedback。** 必须让用户知道“是否已抓住 / 是否已释放 / 结果是否已确定 / 是否可以再次 Toss”。
- 反馈优先级：**硬币自身状态变化 > 空间音频 > 极短暂空间文字 > 传统 UI**。

---

## 2. 目标硬件（已确认事实，来源：Meta 官方 2026-09-23 发布）

- 产品：Meta VR Glasses，2027 年春季发货，$1,299.99，首发美国。
- 形态：眼镜本体约 100g（显示/摄像头/传感器/空间音频）＋ 外置计算 puck（Snapdragon Reality Elite / 电池 / 存储，光纤线缆连接，可放口袋），约 3 小时连续媒体播放，45W 快充。
- 显示：micro-OLED “5K Infinite Display”，约 37 PPD；视场角约 70×66 度（比 Quest 3 的 110×96 窄）⚠️ 关键视觉信息向舒适中央视野收敛，避免依赖边缘 HUD。
- 输入：**眼睛瞄准（gaze）＋ 手势（hand）为核心输入**，出厂不带手柄（Touch Plus 另售）。官方口径：可用 eyes and natural hand gestures 操作系统，不需要控制器。
- 显示：full-color passthrough（能看到真实房间/桌面）→ **硬币必须出现在现实环境中，而不是 VR 房间里。**
- 系统：与 Quest 跑同一套 Horizon OS、同一套 SDK、同一个商店与支付体系。

---

## 3. 开发环境（已确认，来源：developers.meta.com）

- SDK：**v207**（官方博客明确：VR Glasses 2027 春季才发货，但 v207 SDK 现在即可下载并开始 build/test）。
- 专属文档页：`Support Meta VR Glasses`（Unity / Unreal 双路径）、`Get started with Meta VR Glasses`、`Test your app for Meta VR Glasses`、`What's new in Meta VR Glasses`。
- 主引擎：**Unity 6000.0.66f2+**。
- Unity 包：`com.meta.xr.sdk.core` + `com.meta.xr.sdk.interaction` + `com.meta.xr.sdk.interaction.ovr`（Interaction SDK OVR 路径）。
- 备选：Unreal（Meta Oculus-VR fork，UE 5.0+），比赛版暂不采用。
- 构建：IL2CPP + ARM64；Hand Tracking Support 选 Hands Only；Eye Tracking permission 按需声明。
- 无真机开发：Meta XR Simulator（Unity/Unreal，内置 VR Glasses profile，可用笔记本摄像头追踪手）、Meta Spatial Simulator、Immersive Web Emulator；Quest 3/3S 可做真机手势测试，但不是启动项目的前置条件。
- 自检工具：Device Readiness Check（检查 controller 依赖、FOV、controller-only input 等问题，并输出 readiness report）。
- 分发规则：对没有手柄的 VR Glasses 用户，hands-compatible content 会优先被展示；因此 Toss 比赛版坚持 hands-first / controller-free。

---

## 4. 待 Meta 确认的未知项

> 原则：未知项不得偷偷变成“已确认能力”。原型阶段允许以可替换、可降级、可配置方式推进。

| # | 未知项 | 当前状态 | 对 Toss 的影响 | 应对 |
|---|---|---|---|---|
| ① | VR Glasses 是否开放完整 hand skeleton / joint 数据 | Quest 上已开放；VR Glasses 官方口径“同一套 SDK”，但仍需目标 profile/实测确认 | 高：Level B thumb flick 依赖 | 手势识别阈值全部参数化；Level B 非 MVP blocker |
| ② | Fast Motion Mode（60Hz 手部追踪）是否支持 VR Glasses | Quest 2/Pro/3 已支持；官方文档写 “FMM is supported on Quest 2, Quest Pro, Quest 3, and all future devices”（2026-08-11 更新）→ 高概率支持，仍待 VR Glasses 明确文档/实测确认 | 中：影响快速 thumb flick 稳定性 | Level B 数据不足即 fallback Level A |
| ③ | Environment Depth / Spatial Anchors 是否全量支持 VR Glasses | Quest 3 已支持；VR Glasses 尚需目标 profile/兼容性表确认 | 中：影响硬币能否可信落到真实桌面 | Competition Build 不依赖真实桌面碰撞；后续再扩展 |

---

## 5. 核心交互设计

### 5.1 Level A — Confirmed-Capability Path（MVP 保底路径）

> 这里的“Confirmed-Capability”指**所依赖的基础输入能力已被官方确认**，不是说完整的 Toss UX 已被硬件保证。

使用 VR Glasses 已确认的 eyes/hands 基础能力：Gaze + Pinch/Grab + Hand Movement + Release。

```
Pinch/Grab 拿起硬币
  → 手向上挥动
  → Release 松开
  → Physics Toss
  → 硬币空间旋转（gaze 自然追踪，不触发状态）
  → 下落
  → Catch / resolve
  → Heads / Tails 结算
```

- “Catch”属于我们的应用交互实现，需要在 Simulator/真机上验证可用性；若手掌接取不稳定，可退化为 pinch catch、自动结算或其他不破坏核心体验的方式。
- 即使完整 skeleton / FMM 最终不可用，Toss 仍成立。
- **Competition Build 先把 Level A 做到稳定、自然、可重复。**

### 5.2 Level B — Experimental Signature Gesture（实验性标志性手势）

目标动作：**拇指托住硬币 → thumb flick 一弹 → 硬币飞起。**

依赖：

- hand joint / thumb pose 数据质量
- 采样率
- fast motion tracking
- 自定义手势识别精度
- Simulator 与未来真机之间的差异

策略：

- **实验性质，非 MVP blocker。**
- Simulator / Quest / VR Glasses 实验证明数据足够才启用。
- 不足则自动 fallback 到 Level A。
- 原 v0.1 Casual Mode / Microgestures 保留为备选简化触发方式，不单独扩张成一套模式系统。

### 5.3 物理、随机与呈现

- Unity Rigidbody：真实比例硬币模型、重力、旋转角速度、空间位置。
- 开奖仪式感：空间音频（旋转声 / 接触声 / 结果声）+ 克制的时间节奏变化，制造“再 Toss 一次”的欲望。
- **结果确定性契约：RNG 决定结果，Physics 负责呈现。**
  - 每次 Toss 在进入有效抛掷状态时生成唯一随机结果：Heads 或 Tails。
  - 物理轨迹、旋转和动画必须最终**收敛到已生成结果**。
  - 不允许“Rigidbody 自然落成 Heads，但逻辑层随机判定为 Tails”这种视觉与数据冲突。
  - 若未来改为纯物理解算决定结果，必须作为正式 Decision 变更写入本文件。
- 比赛版 KPI：**第一次 Toss 是否让人觉得“这东西就应该存在于眼镜上”**。功能数量不重要，核心 mechanic 完成度最重要。

---

## 6. 功能规划

### 6.1 Competition Build（项目内部 2026-11-18 前提交）

> 原则：**一枚做到极致的硬币。** Tight vertical slice > broad prototype with gaps.

必须完成：

- Level A 完整核心循环：拿起 → 抛 → 旋转 → 接住/结算 → Heads/Tails → 自然进入下一次 Toss。
- Zero-UI + 明确反馈。
- Hands-only / no controller dependency。
- 舒适 FOV 与可读结果。

允许保留但不得拖慢核心 mechanic：

- **Decision Mode（轻量）**：如 “Pizza or Burgers?”，Heads=Pizza / Tails=Burgers。作用仅是证明 Toss 对应现实中的微型决策需求。
- **极轻量成就（最多 3 个）**：第一次 Toss / 连续 Toss 10 次 / 连续同一面。若影响核心完成度，可从比赛版删除。

明确不进入比赛主线：

- 搜索 / 地图 / Agent
- 餐厅推荐
- 完整日记/周报
- 多人同步
- 大量成就系统
- 骰子 / 剪刀石头布扩展

### 6.2 Backlog（比赛后再议）

- 完整成就系统（反悔大师 / 果断帝 / 选择困难症晚期 / 墨菲附体 / 五五开）。
- 决策日记、命运周报。
- 派对模式（共享空间锚点、多人同看一枚硬币）。
- 决定→行动闭环（抛完自动搜餐厅 / 建提醒）。
- 掷骰子 / 剪刀石头布等同技术栈 Benchmark。

---

## 7. 四方协作与仓库治理

### 7.1 角色定位

- **侃哥 — 总协调 + 拍板**
  - 定方向、排优先级、裁决争议。
  - 规则变更最终以侃哥确认后由 GPT 写入 SSOT 为准。

- **Muse（胖墩墩）— 前沿事实核查 + 创意总监**
  - 跟踪 Meta 官方政策、文档、API 和生态变化。
  - 提出产品/交互创意。
  - 两顶帽子必须分开：已核实内容标“事实”；未核实产品构想标“提案”。
  - **只读仓库规则文件，不直接写入 `docs/PROJECT_BRIEF.md`。**
  - 修改建议以文字 diff / 核查意见交给侃哥或 GPT。

- **GPT — 架构研判 + 规则唯一写入者 + Gemini 提示词拟定**
  - 维护 `docs/PROJECT_BRIEF.md`，是规则与产品边界的唯一仓库写入者。
  - 对 Muse 的事实/创意反馈做一致性检查和工程边界判断。
  - 将已经拍板的方案拆成给 Gemini 的结构化工程提示词。
  - Gemini 提示词必须引用 SSOT 章节编号，不得从聊天记忆自行发明需求。

- **Gemini — 工程实现**
  - 负责 Unity 工程、代码、调试、测试和**工程代码推送**。
  - **不得自行修改项目规则、需求边界或 `PROJECT_BRIEF.md`。**
  - 拥有“实现真相”话语权：SDK/代码现实与简报冲突时必须打回，不硬写凑合。
  - 实现问题反馈 GPT；Meta 事实问题反馈 Muse 核查。

### 7.2 协作工作流

```
侃哥定方向 / 拍板
  → Muse 提供 Meta 事实核查 + 产品/交互提案（事实与提案分开）
  → GPT 做架构/可行性研判
  → GPT 将已确认结论写入 PROJECT_BRIEF.md
  → GPT 按章节编号生成 Gemini 工程提示词
  → Gemini 实现、测试、推送代码
  → 工程现实冲突：Gemini → GPT
  → Meta 事实疑问：GPT / Gemini → Muse
  → Muse 返回核查意见（只读，不写仓库）
  → GPT 整理规则 diff → 侃哥拍板 → GPT 写入 SSOT
```

### 7.3 防传话失真规则

1. `docs/PROJECT_BRIEF.md` 是唯一事实源；聊天、Muse 本地 `brief.md`、Gemini 实现笔记都不能覆盖它。
2. **PROJECT_BRIEF.md 只有 GPT 写入。**
3. Gemini 只修改工程代码/工程配置；不得顺手“修需求”。
4. Muse 只读仓库，输出事实核查、提案或文字 diff；不直接 commit 规则。
5. GPT 的工程提示词必须引用简报章节编号，例如“按 §5.1 实现 P2”。
6. Gemini 若发现提示词不可实现，必须说明具体 SDK/API/代码原因并打回。
7. AI 之间有分歧，不私下折中；记录各自依据，由侃哥裁决。
8. 新事实若改变产品边界，必须先更新 SSOT，再继续相关代码。
9. 工程代码已经证明 SSOT 某项错误时，优先相信可复现的工程事实，并触发规则修订。

### 7.4 Gemini 工程推进顺序

只有前一级达到验收条件才进入下一级：

- **P0 Environment**：Unity + Meta XR SDK + Interaction SDK + VR Glasses Profile + Simulator 跑通。
- **P1 Coin**：真实比例硬币模型、Rigidbody、重力、旋转、空间位置。
- **P2 Guaranteed Toss**：Pinch/Grab → upward movement → release → physics。
- **P3 Result**：Heads/Tails RNG 契约 + 视觉/音效结果反馈。
- **P4 Catch**：手掌/Pinch 接取逻辑；不稳定则按 §5.1 降级。
- **P5 Signature Gesture**：实验 thumb flick；失败 fallback Level A，不影响比赛 Build。

全程使用 Device Readiness Check 做兼容性验收。

---

## 8. 里程碑

- [x] **P0 非开发任务（2026-09-25 已全部完成）**：Meta 开发者账号注册 ✅ ｜ Meta VR Start 申请已获批 ✅ ｜ Devpost 大赛报名完成 ✅（报名时显示 272 participants）
- [ ] **2026-10-15**：Simulator 跑通 Level A 全链路，并通过 Toss Core Loop 验收。
- [ ] **2026-11-01**：Competition Build 冻结候选版；Decision Mode / 轻量成就只有在不损害核心 mechanic 时保留。
- [ ] **2026-11-18**：项目内部投稿完成目标（预留缓冲）。
- [ ] **2026-11-19 04:00 GMT+8**：Devpost 官方投稿截止。
- [ ] **约 2026-12-11**：比赛结果公布；评估是否启动 Backlog。
- [ ] **2027 春季**：VR Glasses 真机到位后按当时 compatibility profile 进行最终适配。

### 8.1 Toss Core Loop 验收标准（2026-10-15）

连续完成 20 次 Toss：

- 无明显误触。
- 无需手柄。
- 不依赖教程即可理解基本操作。
- 硬币核心运动始终保持在舒适可追踪 FOV。
- 抛掷过程不丢失关键视觉信息。
- Heads / Tails 清晰可识别。
- 逻辑结果与视觉最终朝向一致。
- 每次操作可以自然进入下一次 Toss。
- 如果 Catch 不稳定，必须有可重复的降级结算路径，而不是卡死流程。

---

## 9. 关键链接

一级信源优先使用 Meta 官方；第三方报道仅作辅助。

- 大赛官方页面：https://developers.meta.com/blog/meta-connect-2026-vr-start-developer-competition/
- 大赛官方规则（Devpost）：https://start-developer-competition-26.devpost.com/rules
- Connect 2026 开发者复盘：https://developers.meta.com/blog/meta-connect-recap-start-building-the-future-of-vr/
- Unity 支持指南：https://developers.meta.com/horizon/documentation/unity/unity-support-meta-vr-glasses/
- Unreal 支持指南：https://developers.meta.com/horizon/documentation/unreal/unreal-support-meta-vr-glasses/
- VR Glasses 新特性：https://developers.meta.com/horizon/documentation/android-apps/whats-new-in-glasses/
- Hand Tracking 总览：https://developers.meta.com/horizon/documentation/unity/unity-handtracking-overview/
- Fast Motion Mode：https://developers.meta.com/horizon/documentation/unity/fast-motion-mode
- Road to VR 比赛报道（辅助）：https://roadtovr.com/meta-vr-glasses-dev-competition-1m-2026/

---

## 10. 参赛资格（2026-09-25 已完成核查）

- 大赛官方条款排除地区包括巴西、魁北克及受制裁地区；中国不在已核查排除名单内。
- 参赛基本条件：年满 18 岁、有效 Meta Developer Access、投稿时为 Meta VR Start 会员。
- 中国已实际验证可选为 Start 申请国家；侃哥于 2026-09-25 申请并于当晚收到 Start Welcome 邮件。
- 侃哥已完成：Meta 开发者账号、开发者组织、VR Start、Devpost 大赛报名。
- 获奖后的税务/跨境汇款路径不影响当前开发；如获奖再按届时条款处理。

---

## 11. Decision Log

> 只记录会改变项目方向、工程边界或比赛策略的正式决定。详细讨论留在聊天/Issue，不在这里堆叠。

| ID | 日期 | 决策 | 状态 |
|---|---|---|---|
| D-001 | 2026-09-25 | Competition Build 采用 **Unity + Meta XR Interaction SDK** 为主路线，不以 WebXR 作为核心实现路径 | Active |
| D-002 | 2026-09-25 | Level B thumb flick 为实验性 Signature Gesture，**不是 MVP blocker** | Active |
| D-003 | 2026-09-25 | Toss 比赛版坚持 hands-first / controller-free | Active |
| D-004 | 2026-09-25 | **RNG 决定 Heads/Tails，Physics 负责呈现并收敛到结果** | Active |
| D-005 | 2026-09-25 | 比赛版采用 **polished vertical slice**，优先一枚硬币，不扩张为平台 | Active |
| D-006 | 2026-09-25 | `PROJECT_BRIEF.md` 的唯一规则写入者为 GPT；Muse 只读反馈，Gemini 只写工程代码，侃哥最终拍板 | Active |
| D-007 | 2026-09-25 | 官方截止为 2026-11-19 04:00 GMT+8；内部提交目标定为 2026-11-18，预留缓冲 | Active |

---

## 12. 当前下一步

**当前阶段：P0 Environment。**

下一份给 Gemini 的正式工程任务必须只覆盖：

- Unity 指定版本
- Meta XR SDK / Interaction SDK 安装与版本确认
- VR Glasses Device Profile
- Meta XR Simulator 可启动
- Hands-only / Look-and-Pinch 基础输入可观察
- Device Readiness Check 可运行
- 最小测试场景 build/run

**P0 不做硬币、不做物理、不做成就、不做 Decision Mode。**

P0 验收完成后，由 GPT 根据 §7.4 生成 P1 Coin 工程提示词。
