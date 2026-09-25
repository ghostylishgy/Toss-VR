# Toss · Meta VR Glasses 抛硬币应用 — 三方协作共享简报

> 版本：v0.3 ｜ 更新日期：2026-09-25 ｜ 维护：胖墩墩（Muse）
> 用途：GPT（产品/交互设计）、Gemini（工程实现）、Muse（Meta 生态事实核查）
> 协作的唯一事实来源。任何结论变更都先更新本文档，再同步给三方。
>
> v0.3 更新（2026-09-25 晚）：① 侃哥完成 Meta 开发者账号注册 ② Meta VR Start 申请**已获批**（当晚收到 Welcome 邮件）③ 中国确认在 Start 受支持国家名单内（申请表国家下拉框可选 China）④ Devpost 大赛报名完成（272 participants，截止 2026-11-19 04:00 GMT+8）。P0 非开发任务全部关闭。
>
> v0.2 更新（采纳 GPT 2026-09-25 核验反馈）：① Realistic Mode 拆分为 Level A（保底）/ Level B（实验性标志性手势）；② 比赛版 MVP 收缩为 polished vertical slice；③ 新增 Interaction Principle #1；④ VR Start/Devpost 资格列为 P0；⑤ FMM 未知项升级（官方口径 all future devices）。

---

## 1. 项目定位

**一句话**：Meta VR Glasses 上的"空间交互 Hello World"——用"抛硬币"这个全人类通用的动作，一次验证眼镜 App 的基础交互全链路。

- 产品目标表述：**不要做"一个运行在 VR Glasses 上的 Coin Toss App"，要做"现实世界里突然多出了一枚数字硬币"**。用户看到的仍然是自己的手、朋友、桌子、房间，唯一不存在于现实中的东西就是那枚硬币。
- 商业预期：Prototype + Competition Entry + Interaction Research + Portfolio。不提前追求商业化。
- 参赛目标：Meta VR Start 开发者大赛（$1M 奖金池，2026-09-24 ~ 11-18 投稿截止，12-11 公布）。Toss 属于官方明确扶持的 casual + 纯手势品类。

## 1.1 Toss Interaction Principle #1（正式原则）

> **Gaze indicates attention. Hands express intent.**
> 眼睛表示注意，手表示意图。

- 用户抬头追踪飞起的硬币时，gaze 不得触发任何状态改变（避免眼动误触，也符合真实世界行为）。
- 所有"决定性操作"（抓取、释放、确认）必须由手势表达。

## 1.2 Zero-UI 原则（比赛版）

- 不要首页、不要 Dashboard、不要复杂菜单、不要大面积 HUD、不要虚拟房间。
- 传统 App UI 能省则省，让硬币本身成为界面。

## 2. 目标硬件（已确认事实，来源：Meta 官方 2026-09-23 发布）

- 产品：Meta VR Glasses，2027 年春季发货，$1,299.99，首发美国
- 形态：眼镜本体约 100g（显示/摄像头/传感器/空间音频）＋ 外置计算 puck（Snapdragon Reality Elite / 电池 / 存储，光纤线缆连接，可放口袋），约 3 小时连续媒体播放，45W 快充
- 显示：micro-OLED "5K Infinite Display"，约 37 PPD；视场角约 70×66 度（比 Quest 3 的 110×96 窄）⚠️ UI 元素往中间收，别放边缘
- 输入：**眼睛瞄准（gaze）＋ 手势（hand）为核心输入**，出厂不带手柄（Touch Plus 另售）。官方原话：可用 eyes and natural hand gestures 操作系统，不需要控制器
- 显示：full-color passthrough（能看到真实房间/桌面）→ **硬币必须出现在真实世界里，而不是 VR 房间里**
- 系统：与 Quest 跑同一套 Horizon OS、同一套 SDK、同一个商店与支付体系

## 3. 开发环境（已确认，来源：developers.meta.com，文档 2026-09-01~18 已提前备好）

- SDK：**v207**（官方博客原话：VR Glasses 不到 2027 春季才发货，但 v207 SDK 现在就能下载开工）
- 专属文档页（真实存在）：`Support Meta VR Glasses`（Unity / Unreal 双路径）、`Get started with Meta VR Glasses`、`Test your app for Meta VR Glasses`、`What's new in Meta VR Glasses`
- 引擎：Unity 6000.0.66f2+，包 `com.meta.xr.sdk.core` + `com.meta.xr.sdk.interaction` + `com.meta.xr.sdk.interaction.ovr`（Interaction SDK OVR 路径）；备选 Unreal（Meta Oculus-VR fork，UE 5.0+）
- 构建：IL2CPP + ARM64；Hand Tracking Support 选 Hands Only；Eye Tracking permission 按需声明
- 无真机开发：Meta XR Simulator（Unity/Unreal，内置 VR Glasses profile，可用笔记本摄像头追踪手）、Meta Spatial Simulator、Immersive Web Emulator；Quest 3/3S 可做真机手势测试（非必须）
- 自检工具：Device Readiness Check（一键扫出项目里要为眼镜改的地方）
- 商店规则：**商店会优先把纯手势应用推给不买手柄的用户**——纯手势 = 更大的分发面

## 4. 待 Meta 确认的未知项（原型阶段按"假设成立"推进，写成可调参数）

| # | 未知项 | 现状 | 对 Toss 的影响 | 应对 |
|---|--------|------|---------------|------|
| ① | VR Glasses 是否开放完整 hand skeleton 数据 | Quest 上已开放；眼镜官方口径"同一套 SDK"，大概率开放 | 高（自定义拇指弹手势依赖它） | 手势识别阈值全部做成可调参数 |
| ② | Fast Motion Mode（60Hz 手部追踪）是否支持眼镜 | Quest 2/Pro/3 已支持；**官方文档原话 "FMM is supported on Quest 2, Quest Pro, Quest 3, and all future devices"**（2026-08-11 更新）→ 眼镜大概率支持，待真机/新版文档最终确认 | Level B 手势若数据不足则 fallback 到 Level A，项目不卡死 |
| ③ | Environment Depth / Spatial Anchors 是否全量支持 | Quest 3 已支持；眼镜未见 compatibility profile | 中（决定硬币能否"落到真实桌面" vs 只能悬浮） | Phase 1 允许悬浮结算，Phase 2 再做桌面落点 |

## 5. 核心交互设计（Toss 的"抛"动作）

### 5.1 Level A — Guaranteed Interaction（MVP 保底路径）

只用 VR Glasses 已确认支持的能力：Gaze + Pinch/Grab + Hand Movement + Release。

```
Pinch/Grab 拿起硬币 → 手向上挥动 → Release 松开
  → 物理抛掷（Interaction SDK 现成 grab/throw）
  → 硬币空间旋转（gaze 自然追踪，不触发状态）
  → 下落 → 手掌/Pinch 接住 → Heads/Tails 结算
```

即使完整 skeleton / FMM 最终不可用，Toss 依然成立。**比赛版 MVP 只做到这一层做到极致。**

### 5.2 Level B — Experimental Signature Gesture（实验性标志性手势）

我们真正想要的标志性动作：拇指托住硬币 → thumb flick 一弹 → 硬币飞起。

依赖：hand joint 数据质量、thumb pose、采样率、fast motion tracking、手势识别精度。
策略：**实验性质，非 MVP blocker**。Simulator/真机实验证明数据足够则启用，不足则自动 fallback 到 Level A。项目不因它卡死。

（原 v0.1 的 Casual Mode / Microgestures 思路并入此处作为备选简化触发方式。）

### 5.3 物理与呈现（比赛版做到极致的一枚硬币）

- Unity Rigidbody：真实比例硬币模型、重力、旋转角速度、空间位置
- 开奖仪式感：慢动作 + 空间音频（旋转声→落地脆响）→ 制造"再 Toss 一次"的欲望
- 真随机：系统随机数决定正反面
- 比赛版 KPI：**第一次 Toss 是否让人觉得"这东西就应该存在于眼镜上"**——功能数量不重要，核心 mechanic 的完成度最重要（官方建议：scope to one mechanic and finish it；tight vertical slice > broad prototype with gaps）

## 6. 功能规划

### 6.1 Competition Build（比赛版，11-18 投稿前只做这些）

> 原则：一枚做到极致的硬币。tight vertical slice > broad prototype with gaps。

- Level A 完整循环：拿起 → 抛 → 旋转 → 接住 → 结算，做到"第一次 Toss 就让人觉得它就该存在于眼镜上"
- **Decision Mode（轻量保留）**：如 "Pizza or Burgers?"，Heads=Pizza / Tails=Burgers，Toss 后直接出结果。作用是向评委说明 Toss 对应现实中的微型决策需求，不要扩展搜索/地图/Agent
- **极轻量成就（仅 3 个）**：第一次 Toss / 连续 Toss 10 次 / 连续同一面——只为增加一点 replay value，不做成系统
- Zero-UI（见 1.2）

### 6.2 Backlog（P2，比赛后再议——不削弱核心 mechanic 的完成度）

- 完整成就系统（反悔大师/果断帝/选择困难症晚期/墨菲附体/五五开）
- 决策日记、命运周报
- 派对模式（共享空间锚点、多人同看一枚硬币、喝酒游戏规则）
- 决定→行动闭环（抛完自动搜餐厅/建提醒）
- 掷骰子 / 剪刀石头布（同一技术栈的横向扩展）

## 7. 三 AI 分工

- **Muse（胖墩墩）**：Meta 生态事实核查。追踪 developers.meta.com 文档更新，验证 API 可用性，维护本简报。所有"待确认问题"由我负责查证后回写。**一级信源恒为 developers.meta.com 官方资料。**
- **GPT**：产品与交互设计。深化状态机、Decision Mode 文案、Level B 手势实验方案、交互细节。
- **Gemini**：工程实现。按以下顺序推进（只有前一级稳定才进入下一级）：
  - P0 Environment：Unity + Meta XR SDK + Interaction SDK + VR Glasses Profile + Simulator 跑通
  - P1 Coin：真实比例硬币模型、Rigidbody、重力、旋转、空间位置
  - P2 Guaranteed Toss：Pinch/Grab → upward movement → release → physics
  - P3 Result：Heads/Tails 判定 + 音效 + 最小反馈
  - P4 Catch：手掌/Pinch 接取逻辑
  - P5 Signature Gesture：实验 thumb flick（失败则 fallback 到 Level A，比赛 Build 不受影响）
  - 全程用 Device Readiness Check 做验收（查 controller 依赖、FOV、纯手柄输入等问题）
- 输出约定：每家输出分"结论"和"待确认问题"两部分；待确认问题汇总到我这里，我查证后更新到本文档第 4 节。

## 8. 里程碑

- [x] **P0 非开发任务（2026-09-25 已全部完成）**：Meta 开发者账号注册 ✅ ｜ Meta VR Start 申请已获批（当晚收到 Welcome 邮件）✅ ｜ Devpost 大赛报名完成 ✅（272 participants，投稿截止 2026-11-19 04:00 GMT+8）
- [ ] 2026-10-15：Simulator 跑通 Level A 全链路，并通过验收标准（见下）
- [ ] 2026-11-01：Competition Build 冻结（含 Decision Mode + 3 个轻量成就）
- [ ] 2026-11-18：Meta VR Start 大赛投稿截止
- [ ] 2026-12-11：大赛公布；同步评估是否启动 Backlog（6.2）
- [ ] 2027 春季：VR Glasses 发货后做真机适配（按届时官方 compatibility profile 调整第 4 节参数）

### Toss Core Loop 验收标准（2026-10-15）

连续完成 20 次 Toss：无明显误触、无需手柄、无需教程即可理解、硬币始终位于舒适 FOV、抛掷过程不丢失关键视觉信息、Heads/Tails 清晰可识别、每次操作能自然进入下一次 Toss。

## 9. 关键链接（大赛主信源为 Meta 官方博客，Road to VR 降为辅助）
- 大赛官方页面（主信源）：https://developers.meta.com/blog/meta-connect-2026-vr-start-developer-competition/
- 大赛官方规则（Devpost）：https://start-developer-competition-26.devpost.com/rules
- 开发者博客（Connect 2026 复盘）：https://developers.meta.com/blog/meta-connect-recap-start-building-the-future-of-vr/
- Unity 支持指南：https://developers.meta.com/horizon/documentation/unity/unity-support-meta-vr-glasses/
- Unreal 支持指南：https://developers.meta.com/horizon/documentation/unreal/unreal-support-meta-vr-glasses/
- 眼镜新特性：https://developers.meta.com/horizon/documentation/android-apps/whats-new-in-glasses/
- Hand Tracking 总览（含 FMM / WMM / Microgestures / Grab）：https://developers.meta.com/horizon/documentation/unity/unity-handtracking-overview/
- FMM 文档：https://developers.meta.com/horizon/documentation/unity/fast-motion-mode
- 大赛（Road to VR 报道，辅助）：https://roadtovr.com/meta-vr-glasses-dev-competition-1m-2026/

## 10. 参赛资格（2026-09-25 核查）

- 大赛官方条款（Devpost 2026）：排除地区仅为巴西、魁北克及受制裁地区（俄罗斯、朝鲜、伊朗、古巴、叙利亚等）。**中国不在排除名单内**。
- 参赛三条件：① 年满 18 岁 ② 拥有开启 Developer Access 的有效 Meta 账号 ③ 投稿时已是 Meta VR Start 会员。
- Start 会员要求"位于受支持国家"。**已验证（2026-09-25）：中国在受支持名单内**——申请表国家下拉框可选 China，且申请当晚即获批（Welcome to the Start program! 邮件）。
- 开发者账号注册必须本人操作（Meta 账号实名＋点同意 ToS）：① 注册/登录 Meta 账号 ② developers.meta.com 创建开发者组织 ③ 申请加入 Meta Horizon Start ④ Devpost 注册并按大赛指引投稿。—— 侃哥 2026-09-25 已全部走完。
- 备注：若获奖，奖金发放到中国的税务/汇款路径届时再按条款处理。
- 大赛官方规则：https://start-developer-competition-26.devpost.com/rules
