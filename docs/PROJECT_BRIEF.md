# Toss · Meta VR Glasses 抛硬币应用 — 四方协作共享简报

> 版本：v0.13 ｜ 更新日期：2026-09-26 ｜ 维护：GPT（唯一规则写入者）
> 用途：侃哥（总协调/拍板）、Muse（前沿事实核查 + 创意提案）、GPT（架构研判 + 规则维护 + Gemini 提示词）、Gemini（工程实现）
> **本文件 `docs/PROJECT_BRIEF.md` 是项目唯一事实源（SSOT）。规则、边界、已确认事实与正式决策，以仓库版本为准。**
>
> v0.13 更新（2026-09-26，P0 运行前最后收口）：① Unity Personal 已激活，项目完成首次完整初始化与编译，退出码 0；② Meta XR Core / Interaction / Interaction OVR v207、OpenXR 1.18.0、URP 17.0.4 已实际解析并缓存，不再只是 manifest 声明；③ Unity Hub 已识别 `G:\Dev\Toss-VR`，Editor 版本锁定 6000.0.66f2；④ **P0 仍未 PASS**，剩余唯一阶段为 Editor Play Mode + Meta XR Simulator 的运行态验证：VR Glasses profile、Hands Only、Gaze、Pinch、Look-and-Pinch、Device Readiness Check。
>
> v0.12 更新（2026-09-26，P0 安装阶段复核）：① `download.unity3d.com` 精确 SDK DNS 例外已生效，Unity 6000.0.66f2 与 Android toolchain 已完成 G 盘部署，普通机场开发流量约 4.18GB，Webshare 0 泄漏；② standalone Meta XR Simulator v207 已部署并配置为 OpenXR runtime；③ Unity 项目骨架、Android 构建参数与 Meta/OpenXR package manifest 已就位；④ **P0 尚未 PASS**：当前唯一人工前置为 Unity Personal 许可证激活，且 Meta XR package 实际解析、Editor Play Mode、VR Glasses profile、Gaze/Pinch/Look-and-Pinch、Device Readiness Check 仍需运行态验证；⑤ “文件已写入/manifest 已配置”不得等同于运行验证通过。
>
> v0.11 更新（2026-09-26，Unity CDN 例外策略拍板）：① 实机确认大陆 DIRECT 访问 `download.unity3d.com` 会被 302 到缺文件的 `download.unitychina.cn` 并 404；② 允许仅对 `download.unity3d.com` 添加更高优先级 `SDK DNS` 例外，绕过大陆镜像；③ 其他 Unity Package/Android 下载继续 DIRECT，Meta 继续 SDK DNS，Webshare 继续禁止开发下载；④ 本次 Unity Editor + Unity 自托管 Support 包的普通机场流量预算按约 10GB 可接受控制。
>
> v0.10 更新（2026-09-25 晚，Network Gate 实机审计）：① 当前 Clash Verge Rev 使用 Rule + TUN，所有未显式直连流量由 TUN 接管；② Unity 官方下载域名与 `dl.google.com` 当前误走 `SDK DNS` 普通机场，违反大文件 DIRECT 策略；③ 物理直连 TCP 已验证可用，问题仅在规则优先级；④ Webshare 固定住宅代理与开发域名完全隔离；⑤ P0 必须先加入 Unity / Android 下载显式 DIRECT 规则并复核，Network Gate PASS 后方可下载。
>
> v0.9 更新（2026-09-25 晚，P0 Network Gate）：① 开发下载分流正式定案：Unity/Android/UPM 大文件一律 DIRECT；Meta 开发者站点/SDK/Simulator 如直连失败则走普通机场节点；Webshare 固定住宅 IP 禁止用于开发下载；② 在任何大型下载前必须先审计 Clash/TUN、系统代理、WinHTTP、代理环境变量和实际路由；③ 无法确认路由时不得开始安装。
>
> v0.8 更新（2026-09-25 晚，P0 官方文档复核）：① Unity 6000.0.66f2+ 安装必须包含 Android Build Support、Android SDK & NDK Tools、OpenJDK；② OpenXR Plugin 锁定 1.17.0+；③ Meta XR Simulator v207 改为 standalone Windows runtime，旧 Unity Simulator package 不得作为新项目安装路径；④ P0 明确要求切换 Meta VR Glasses profile 后退出并重新进入 Play mode；⑤ P0 只验证环境和 Look-and-Pinch，不引入业务代码。
>
> v0.7 更新（2026-09-25 晚，合并“Toss 交互方案评审”）：① 正式加入核心交互状态机 `Spawn → Ready → Grabbed → Armed → Flight → Catch/Recovery → Reveal → Ready`；② 明确 Catch 是奖励而不是流程门槛，漏接必须 graceful recovery / auto-settle；③ 新增 Comfort Envelope，约束硬币高度、速度、角速度、生成/回收位置与舒适 FOV；④ 将状态机与舒适区作为后续 Gemini P2/P4 的工程约束。
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
- 主引擎：**Unity 6000.0.66f2+**。P0 为保证复现优先安装 **6000.0.66f2**，除非该版本在 Unity Hub 不可取得或 Meta 官方后续明确要求更高版本。
- Unity Hub 模块：**Android Build Support + Android SDK & NDK Tools + OpenJDK**（Meta 官方 Unity 前置要求）。
- Unity 包：`com.meta.xr.sdk.core` + `com.meta.xr.sdk.interaction` + `com.meta.xr.sdk.interaction.ovr`（Interaction SDK OVR 路径）。
- Unity OpenXR Plugin：**`com.unity.xr.openxr` 1.17.0+**。
- 备选：Unreal（Meta Oculus-VR fork，UE 5.0+），比赛版暂不采用。
- 构建：IL2CPP + ARM64；Hand Tracking Support 选 Hands Only；Eye Tracking permission 按需声明。
- 无真机开发：**Meta XR Simulator v207 standalone runtime**（Windows 独立安装，内置 VR Glasses profile；v207 支持 Look and Pinch 与 camera-driven hand tracking）。旧 `com.meta.xr.simulator` Unity package 已 deprecated，P0 不安装旧包。Quest 3/3S 可做真机手势测试，但不是启动项目的前置条件。
- Simulator 切换到 **Meta VR Glasses** profile 后，必须退出并重新进入 Unity Play mode，使 OpenXR instance 重新绑定目标 profile；默认 profile 为 Quest 3，不能只看“Simulator 能启动”就视为 P0 通过。
- 自检工具：Device Readiness Check（检查 controller 依赖、FOV、controller-only input 等问题，并输出 readiness report）。
- 分发规则：对没有手柄的 VR Glasses 用户，hands-compatible content 会优先被展示；因此 Toss 比赛版坚持 hands-first / controller-free。

### 3.1 P0 Network Gate（安装前强制检查）

开发下载采用以下固定策略：

- Unity Hub / Unity Editor / Android SDK / NDK / OpenJDK / Unity Package Manager：**DIRECT**。
- Unity Asset Store / Meta XR SDK：优先 DIRECT；若实际不可达或明显异常，再切普通机场代理。
- Meta Developer Center / Meta XR Simulator：允许在直连不可用时走**普通机场代理**。
- **Webshare 固定住宅 IP 禁止用于开发下载、SDK 下载、Simulator 下载和 Unity 大文件。**

在任何大型下载开始前，必须完成网络审计：

1. 检查 Clash 当前模式、TUN 是否启用、规则模式是否生效。
2. 检查 Windows 系统代理与 WinHTTP 代理。
3. 检查 `HTTP_PROXY` / `HTTPS_PROXY` / `ALL_PROXY` 等环境变量。
4. 验证 Unity / Google Android / Unity Package CDN 的实际路由为 DIRECT。
5. 验证 Meta 相关域名若需代理，实际走普通机场节点而非固定住宅 IP。
6. 无法确认实际路由时，**停止下载并返回审计结果，不得凭猜测继续。**

#### 当前实机审计结果（2026-09-25）

- Clash Verge Rev：Rule 模式，TUN 开启；系统代理关闭，但 TUN 接管大部分应用流量。
- Unity 域名当前无显式 DIRECT，最终命中 `MATCH,SDK DNS`，会消耗普通机场流量。
- `dl.google.com` 当前被 `DOMAIN-KEYWORD,google,SDK DNS` 提前命中，也会消耗普通机场流量。
- 物理网卡对 Unity CDN / Google Android 下载端点的 TCP 443 直连已实测成功，因此无需为这些大文件使用代理。
- Meta 开发者与 Oculus/CDN 当前走普通 `SDK DNS`，可接受；未命中 Webshare。
- Webshare 固定住宅节点仅绑定 `AI-Fixed-IP` 特定规则，不存在 Unity / Meta 开发下载泄漏。

Network Gate 基础修正规则（必须置于通用 Google / MATCH 规则之前）：

```yaml
- DOMAIN-SUFFIX,unity.com,DIRECT
- DOMAIN-SUFFIX,unity3d.com,DIRECT
- DOMAIN-SUFFIX,unitychina.cn,DIRECT
- DOMAIN,dl.google.com,DIRECT
```

实机安装阶段进一步确认：大陆 DIRECT 访问 `download.unity3d.com` 会被 Unity CDN 302 到 `download.unitychina.cn`，而目标 Unity 6 文件在国内镜像缺失并返回 404。为此，允许新增一个**更高优先级的精确例外**：

```yaml
- DOMAIN,download.unity3d.com,SDK DNS
```

该例外必须排在 `DOMAIN-SUFFIX,unity3d.com,DIRECT` 之前。这样只让 Unity Editor / Unity 自托管 Support 包走普通机场；`packages.unity.com`、`download.packages.unity.com`、`cdn.packages.unity.com` 以及 `dl.google.com` 继续 DIRECT。

本次普通机场流量预算：**约 10GB 可接受**。若实际明显超出预算，应暂停并复核连接去向。

应用规则后必须重新验证实际命中与小请求出口；未验证通过不得继续大型下载。

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

### 5.4 核心交互状态机（正式）

> 状态机是 Toss 核心循环的唯一行为骨架。Gemini 后续 P2/P4 实现必须围绕这些状态与合法转移展开，不允许用零散布尔值拼出另一套隐式流程。

```
Spawn
  → Ready
  → Grabbed
  → Armed
  → Flight
  → Catch / Recovery
  → Reveal
  → Ready
```

#### Spawn
- 创建/回收一枚可交互硬币到用户舒适操作区。
- 初始位置必须落在 Comfort Envelope 内，不贴脸、不贴视野边缘、不要求用户大幅低头或扭头寻找。
- Spawn 完成后进入 Ready。

#### Ready
- 硬币可被看见、可被手势拿起。
- Gaze 仅表示 attention，不触发抓取或状态改变。
- 合法转移：用户完成有效 Pinch/Grab → Grabbed。

#### Grabbed
- 硬币跟随手部交互。
- 系统持续观察手部运动，但尚未视为一次有效抛掷。
- 若用户直接松手且未达到有效 Toss 条件，可安全回到 Ready/Recovery，不产生一次正式结果。
- 当向上运动达到有效抛掷判定条件时 → Armed。

#### Armed
- 表示“这已经是一枚准备被真正抛出的硬币”。
- 这是 RNG 结果生成前后的工程边界：**进入有效 Toss 后，每次循环只允许生成一个 Heads/Tails 结果。**
- 用户 Release → Flight。
- 阈值必须参数化，具体数值由 Simulator/真机调优，不在 SSOT 硬编码。

#### Flight
- 硬币按物理轨迹上升、旋转、下落。
- 用户 gaze 可以自然追踪，但不得改变状态。
- 飞行必须受 Comfort Envelope 约束，避免关键阶段飞出舒适 FOV。
- 进入可接取阶段后 → Catch attempt；若无法可靠接取或即将离开安全区 → Recovery。

#### Catch / Recovery
- **Catch 是奖励，不是门槛。**
- 成功 Catch：允许在手中完成 Reveal，获得更强“真实硬币”仪式感。
- 漏接/没伸手/追踪失败：必须 graceful recovery / auto-settle，系统仍然完整结算该次 Toss。
- 不允许因为没有接住而卡死、丢失结果、要求重来。
- Recovery 可将硬币收束到安全可见位置，再进入 Reveal。

#### Reveal
- 展示已确定的 Heads / Tails。
- 逻辑结果必须与最终视觉朝向一致（遵循 §5.3 RNG/Physics 契约）。
- 反馈以硬币自身、空间音效和极短暂 HEADS/TAILS 为主。
- Reveal 完成后，自动回到 Ready，允许用户自然开始下一次 Toss。

### 5.5 Comfort Envelope（舒适交互包络）

> 目标不是“物理上能飞多高”，而是“用户能否自然地用眼睛追、用手接，而不需要追着虚拟物体找”。

Competition Build 必须建立一组**可调参数**，至少包括：

- 硬币 Spawn 距离 / 高度范围。
- 最大抛掷高度。
- 最大水平偏移。
- 最大线速度。
- 最大角速度 / 旋转表现上限。
- Flight 期间允许的舒适 FOV 区域。
- Recovery 触发边界。
- Reveal 安全位置。

原则：

1. **视觉连续性优先于纯物理自由度。** 必要时允许对轨迹、速度或旋转做温和约束。
2. 硬币不得因为用户一次过猛手势就飞出可追踪区域。
3. 接近 Comfort Envelope 边界时允许系统提前介入 Recovery。
4. 所有阈值必须参数化，先在 Simulator 调整，未来 VR Glasses 真机再重新标定。
5. Comfort Envelope 不得通过大面积 HUD 提醒用户；用户应通过硬币运动本身自然感知边界。

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

- **P0 Environment**：Unity 6000.0.66f2（含 Android Build Support / SDK&NDK / OpenJDK）+ Meta XR SDK v207 + Interaction SDK + OpenXR 1.17.0+ + standalone Meta XR Simulator v207 + VR Glasses Profile + Look-and-Pinch 跑通。
- **P1 Coin**：真实比例硬币模型、Rigidbody、重力、旋转、空间位置。
- **P2 Guaranteed Toss**：按 §5.4 实现 `Ready → Grabbed → Armed → Flight`，并满足 §5.5 Comfort Envelope。
- **P3 Result**：Heads/Tails RNG 契约 + 视觉/音效结果反馈。
- **P4 Catch**：按 §5.4 实现 `Catch / Recovery → Reveal → Ready`；Catch 不稳定时必须 graceful recovery / auto-settle，不得阻塞核心循环。
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
| D-008 | 2026-09-25 | **Catch 是奖励，不是完成一次 Toss 的门槛**；漏接必须 graceful recovery / auto-settle | Active |
| D-009 | 2026-09-25 | 核心状态机固定为 `Spawn → Ready → Grabbed → Armed → Flight → Catch/Recovery → Reveal → Ready` | Active |
| D-010 | 2026-09-25 | Toss 必须受参数化 **Comfort Envelope** 约束；视觉连续性与舒适 FOV 优先于无限制物理自由度 | Active |
| D-011 | 2026-09-25 | P0 使用 **standalone Meta XR Simulator v207**；不安装 deprecated 的旧 Unity Simulator package | Active |
| D-012 | 2026-09-25 | P0 锁定 Unity 6000.0.66f2 + Android modules，并要求 OpenXR Plugin 1.17.0+ | Active |
| D-013 | 2026-09-25 | P0 下载网络策略：Unity/Android/UPM 全部 DIRECT；Meta 必要时普通机场代理；Webshare 固定住宅 IP 禁止开发下载 | Active |
| D-014 | 2026-09-25 | 大型下载前必须通过 Network Gate；无法确认实际路由时不得开始安装 | Active |
| D-015 | 2026-09-25 | 实机审计确认 Unity/Android 直连可用；为 `unity.com` / `unity3d.com` / `unitychina.cn` / `dl.google.com` 添加显式 DIRECT 规则后再放行 P0 下载 | Active |
| D-016 | 2026-09-25 | Meta 开发域名允许继续走普通 `SDK DNS`；Webshare 固定住宅代理继续禁止开发下载 | Active |
| D-017 | 2026-09-26 | 因 Unity 中国镜像缺失目标文件，允许**仅 `download.unity3d.com`** 走 `SDK DNS`；其余 Unity/Android 下载继续 DIRECT | Active |
| D-018 | 2026-09-26 | 本次 Unity Editor + Unity 自托管 Support 包普通机场流量预算约 10GB，可接受；明显超预算则暂停复核 | Active |
| D-019 | 2026-09-26 | **P0 PASS 必须以 Unity Editor 运行态验证为准**；文件存在、manifest 写入、配置值落盘只能算“configured”，不能替代 package resolve / Play Mode / input / readiness 实测 | Active |

---

## 12. 当前下一步

**当前阶段：P0 Environment — Runtime Validation Only。**

### 12.1 已完成

- P0 Network Gate：**PASS**。
- Unity **6000.0.66f2** 已部署到 G 盘。
- Android Build Support / SDK / NDK / OpenJDK 已部署到 G 盘。
- `download.unity3d.com` 精确走普通 `SDK DNS`；其他 Unity Package / `dl.google.com` 保持 DIRECT。
- 本轮普通机场开发流量约 **4.18GB**；Webshare 开发流量 **0**。
- Unity Personal 许可证：**已激活**。
- `G:\Dev\Toss-VR` 已完成 Unity 首次完整初始化与编译，退出码 0。
- Meta XR package 已实际解析：`com.meta.xr.sdk.core 207.0.0`、`com.meta.xr.sdk.interaction 207.0.0`、`com.meta.xr.sdk.interaction.ovr 207.0.0`。
- OpenXR 已实际解析：`com.unity.xr.openxr 1.18.0`。
- URP 已实际解析：`17.0.4`。
- standalone Meta XR Simulator **v207** 已部署并配置为系统 OpenXR runtime。
- Unity Hub 已识别 Toss-VR 项目与 6000.0.66f2 Editor。
- P0 validation scene / diagnostics scripts 已存在。
- Android IL2CPP / ARM64 / Min SDK 32 等配置已落盘。

### 12.2 剩余唯一 P0 阶段：运行态验证

Gemini 必须在 Unity Editor 实际运行并取得证据：

- 打开 `G:\Dev\Toss-VR`，确认 Console 无阻塞性 compile/package error。
- 进入 `P0_EnvironmentValidation`。
- Editor Play Mode 可正常进入。
- standalone Meta XR Simulator v207 正常连接当前 Play session。
- Simulator 当前 profile = **Meta VR Glasses**；如有切换，切换后退出并重新进入 Play Mode。
- Hands Only 配置实际生效。
- Gaze 在运行态可观察。
- Pinch 在运行态可观察。
- Look-and-Pinch 对最小测试对象产生真实响应。
- Device Readiness Check 实际运行并记录 Critical / Warning / Recommendation。
- 无未解释 Critical blocker。

只有上述运行态验证全部完成，才允许：

```text
P0 = PASS
```

配置文件存在、测试脚本存在、scene 可编译都不等价于上述运行态 PASS。

**P0 不做硬币、不做物理、不做成就、不做 Decision Mode。**

P0 PASS 后，由 GPT 根据 §7.4 生成 P1 Coin 工程提示词。
