# Toss · Meta VR Glasses 抛硬币应用 — 四方协作共享简报

> 版本：v0.25 ｜ 更新日期：2026-09-27 ｜ 维护：GPT（唯一规则写入者）
> 用途：侃哥（总协调/拍板）、Muse（前沿事实核查 + 创意提案）、GPT（架构研判 + 规则维护 + Gemini 提示词）、Gemini（工程实现）
> **本文件 `docs/PROJECT_BRIEF.md` 是项目唯一事实源（SSOT）。规则、边界、已确认事实与正式决策，以仓库版本为准。**
>
> v0.25 更新（2026-09-27，P1 relief 渲染路线冻结）：① 经 Muse 按 Unity 6 / URP 17.x / Meta 官方文档核查，Normal Map 只改变着色法线，不改变真实几何或 silhouette；URP Lit Height Map 为 parallax UV 偏移，不是真实 displacement；URP 原生无 tessellation、无 POM，Lit 也无内置 displacement；② Shader Graph 可做 vertex displacement，但受已有顶点密度约束，不能把低密 mesh 自动变成高质量雕塑；③ Toss Coin 的人物/翼徽等主要 bas-relief 改为 **真实几何 relief**，Unity 负责导入与实时 PBR，不再要求 `CoinTextureGenerator` 用 normal/AO/heightfield 承担主雕塑体积；④ 冻结管线原则：**Geometry for form, Normal/AO for micro-detail**；真实几何负责 silhouette、rim、edge、主要浮雕体积，Normal/AO 只负责发丝/刻痕/拉丝/沟槽等微细节；⑤ DCC（Blender/ZBrush 等）制作 relief → FBX 导入 Unity 为当前主路线，Shader Graph vertex displacement 仅保留为实验备选；⑥ 当前 `087cbb4` 的 Heads/Tails 可读性改进不视为最终视觉验收通过，P1.1 继续处于视觉迭代状态。
>
> v0.24 更新（2026-09-27，P1.1 candidate + Design Jam 收敛）：① Gemini 提交 `a4e1dfe` 作为 **P1.1 Coin Presence Baseline PASS candidate**；独立 Git 审计确认该提交仅新增 P1 资产/场景/脚本，未修改 P0 场景、P0_Configurator、OpenXR 设置或 SSOT；② P1.1 当前仍需人工视觉验收后才能正式 PASS，尤其确认 Heads/Tails 大形、侧面厚度、48 齿节奏、银色材质与深浅背景可读性；③ 当前 Heads/Tails“浮雕”主要由 normal/AO heightfield 表现，不得把其描述为已存在 0.35mm 的真实几何浮雕；④ P1.1 深/浅 backdrop 只作为可读性代理，不等于真实 passthrough 环境适应已验证；⑤ 采纳 Muse 复核：Summon/Dismiss 作为后续体验原则补位，但不提前冻结 Snap/Palm-up 主次；失败态坚持无 UI/无 fail buzz/自然等待或恢复；声音保持同一金属声学家族、近场低打扰，acoustic signature 长期打磨；⑥ 三个 P1 实验补验收信号：Finger Play 看无提示重复意愿，Perceived Weight 必含有声/无声 A/B 并回答“哪个更像手里有东西”，Invisible Generosity 记录辅助被察觉阈值；⑦ Micro-delight 只允许偶发、物理/手部动作驱动、不计数、不通知，**不得由 gaze 触发状态或彩蛋**。
>
> v0.23 更新（2026-09-27，P1 产品哲学冻结）：① P1 从“做一枚硬币资产”升级为 **Coin Presence / Coin Play**：设计目标是一件住在眼镜里的数字随身小物，而不只是 Coin Flip 工具；② 当前核心产品假设为 **Fidget-first**：把玩可能是留存引擎，Decision Utility 是自然分支，但该假设需由原型验证，不视为已证明事实；③ 冻结六条 P1 原则：quarter-inspired 熟悉锚点、双态性格、Invisible Generosity、Low-attention Play、Occlusion as Magic、Physical enough to believe / Designed enough to enjoy；④ 重量感不得依赖硬币 squash/stretch 或篡改用户真实手视觉，而应来自硬币自身的惯性/时序/遮挡/声音/settle；⑤ P1 设计需同时考虑视觉、材质、声音、运动、节奏、环境适应与长期关系感（patina）；⑥ P1 首轮只验证 Finger Play、Perceived Weight、Invisible Generosity 三类核心体验，Euler Disk/真实桌面交互列为后续 capability-dependent extension；⑦ P1 不进入正式 Toss 状态机、RNG、Catch/Recovery 或结果结算。
>
> v0.22 更新（2026-09-27，P0 正式通过）：① clean-session 首轮与 Stop→Play 重入均完成；② Meta XR Simulator v207 / Meta VR Glasses / Look and Pinch 下，真实 `GAZE_ENTER`、`PINCH_START`、`LOOK_AND_PINCH_TRIGGERED`、`PINCH_END`、`GAZE_EXIT` 均可重复触发；③ Console 红色 Error=0，Microsoft hand interaction `XR_ERROR_HANDLE_INVALID`、NullReferenceException、MissingReferenceException 均未复现；④ P0 target 的 Gold / Cyan 反馈稳定可重复；⑤ Windows Smart App Control 保持开启，卸载 KB5124010 并重启后此前 Code Integrity 阻止未复现，仅作为本机 A/B 相关性记录；⑥ **P0 = PASS**，允许进入 P1 Coin。
>
> v0.21 更新（2026-09-27，clean-session 首轮通过）：① 卸载 Windows 预览更新 KB5124010 并重启后，Smart App Control 保持开启，Meta XR Simulator 与 Unity/Meta SDK 不再触发此前的 Code Integrity 阻止；该 A/B 结果强烈提示 KB5124010 与阻止行为相关，但不作为微软已确认因果结论；② 全新会话中 Console 红色 Error=0；③ 真实记录到 `GAZE_ENTER`、`PINCH_START`、`LOOK_AND_PINCH_TRIGGERED`、`PINCH_END`、`GAZE_EXIT`，且目标球视觉反馈正常；④ Microsoft hand interaction `XR_ERROR_HANDLE_INVALID` 已消失；⑤ 当前仅剩一次 Stop→Play 重入复测，成功后即可正式判定 **P0 = PASS**。
>
> v0.20 更新（2026-09-27，OpenXR mutation 收窄完成）：① 提交 `7c86e6d` 仅修改 `Assets/Scripts/Editor/P0_Configurator.cs`；② `MicrosoftHandInteraction` 现在仅通过 exact type match 禁用；③ required P0 feature 仅通过 exact allowlist 启用；④ 所有其他 OpenXR feature 保持当前 enabled state，不再自动 enable/disable；⑤ `Contains("Microsoft")` / `Contains("Hand")` / `Contains("Eye")` / `Contains("Aim")` 等模糊 mutation 已移除；⑥ Interaction Rig、validation scene、LookPinchTester、OpenXR Package Settings、ProjectSettings、Packages 均未修改；⑦ 下一步仅剩 clean-session GUI 最终复测。
>
> v0.19 更新（2026-09-27，OpenXR cleanup 代码审查）：① `272928e` 已正确把 Standalone/Android 的 `MicrosoftHandInteraction` 从 enabled 改为 disabled，且未改 Interaction Rig/验证场景；② 根因确认：旧 `ConfigureBuildTargetXR()` 的 `Contains("Hand")` 模糊匹配误启用了 `MicrosoftHandInteraction`；③ 但新实现仍存在过度配置风险：对所有不在 allowlist 的 OpenXR Feature 执行 `enabled=false`，且 block 判断仍包含 `typeName.Contains("Microsoft")` / `fullName.Contains("Microsoft")`，不符合最小变更原则；④ 因此 `272928e` 作为配置资产修复是正确的，但 Configurator 逻辑仍需再收窄为“只修改明确目标 feature，其他 feature 保持原状态”；⑤ clean-session GUI 复测在该收窄完成后执行。
>
> v0.18 更新（2026-09-27，真实 Look-and-Pinch 首次成功）：① 在 Meta XR Simulator v207 / Meta VR Glasses / Look and Pinch 下，`P0_InteractionTarget` 已真实出现 gaze hover 黄色反馈，并在 index pinch 时变为青色，证明 Simulator → OpenXR → Interaction SDK → target 的核心输入链首次真实贯通；② 当前仍暂不宣布 P0 PASS，因为 clean restart 后 Console 仍有两个红错：Windows/Unity `ERROR_NO_MORE_USER_HANDLES (1158)` 弹窗句柄耗尽，以及 OVRPlugin 对 `/interaction_profiles/microsoft/hand_interaction` 调用 `xrSuggestInteractionProfileBindings` 返回 `XR_ERROR_HANDLE_INVALID`；③ Meta v207 官方 Look-and-Pinch 使用 `/interaction_profiles/ext/hand_interaction_ext`（`XR_EXT_hand_interaction`），因此 Microsoft hand profile 不属于本阶段所需输入路径；④ `P0_Configurator.ConfigureBuildTargetXR()` 当前按名称 Contains("Hand"/"Eye"/"Aim") 批量启用 OpenXR feature 的策略被判定为过宽，必须改为显式 allowlist 后再做最终 clean-session 复测。
>
> v0.17 更新（2026-09-27，P0 Rig 根因修复候选）：① 真实 NullReference 根因已定位为 `OVRComprehensiveInteractionRig` 的 `OVRCameraRigRef._ovrCameraRig/_leftHand/_rightHand` 等序列化引用缺失，导致 `TrackingToWorldTransformerOVR.get_Transform()` 在 hand interaction 预处理时解引用 null；② 提交 `17b20a1` 补齐 CameraRig/Hand/Gaze/Tracking 引用，并移除 P0 无关 TouchHandGrab/Grab/Poke/Locomotion/Controller 交互器；③ 静态/批处理审计为 0 compile / 0 NullReference / 0 MissingReference；④ 该实现虽然对齐官方 prefab/sample，但仍包含 `PrefabUtility + SerializedObject` 手工 wiring，因此不等同于“纯 Quick Action、零手工装配”；⑤ 当前状态仍为 **P0 = NOT PASS**，等待 Unity Editor + Meta XR Simulator 的真实 GUI Look-and-Pinch 复测。
>
> v0.16 更新（2026-09-26，真实 GUI 验证暴露输入架构错误）：① Unity Editor 已真实进入 Play Mode，Meta XR Simulator v207 已连接，Device=Meta VR Glasses，Left/Right input=Look and Pinch，Stop→Play 重入也已完成；② 在官方 Look and Pinch 模式下移动鼠标 gaze、左键 pinch，P0_InteractionTarget 完全无响应；③ 这否定了 v0.15 中“`OVREyeGaze` + `OVRHand` 验证架构可接受”的判断；④ Meta v207 官方说明 Look and Pinch 通过 OpenXR eye-gaze + `/interaction_profiles/ext/hand_interaction_ext` 提供输入，应用必须自行实现 gaze/hand interaction；v207 新增的 Interaction SDK Gaze Interaction 是当前标准路线；⑤ P0 验证改为 Interaction SDK Gaze Interaction / 官方 GazeExamples 路线，`OVREyeGaze` 不再作为 VR Glasses Look-and-Pinch 的 P0 主验证路径；⑥ 输入验证阶段允许不加载 Synthetic Environment，避免 `synth_env_server` 在本机异常占用约 17GB RAM。
>
> v0.15 更新（2026-09-26，P0 真实输入纠偏完成）：① 工程提交 `910f898` 已移除应用层 Mouse/Space pinch fallback、Camera.forward 假 gaze、forced PASS/forced bool；② P0 gaze 改用 `OVREyeGaze`，pinch 改用 `OVRHand.GetFingerIsPinching/GetFingerPinchStrength` 与 XR HandTracking 状态；③ Meta Project Setup 审计当前 Required/Critical 0、Warning 0、Recommendation 2；④ batchmode 审计诚实返回 `P0 = NOT PASS`，因为没有活动 OpenXR GUI session、真实 gaze/pinch transition 与 Play→Exit→Play 稳定性证据；⑤ 剩余唯一验收为 Unity Editor + Meta XR Simulator GUI 中手动驱动 Look and Pinch，记录真实 XR 事件后再判 P0 PASS。
>
> v0.14 更新（2026-09-26，P0 验收纠偏）：① GPT 独立 code review 拒绝 `654545b` 报告中的 P0=PASS 结论；② 当前 `P0_Configurator` 通过直接赋值 `editorInPlayMode/stabilityPassed/pinchObserved=true`、强制 `Camera.main.LookAt` + Physics Raycast、手工写入 `tester.isGazed/isPinched=true` 来制造验证结果；③ `LookPinchTester` 还直接读取鼠标/空格作为 pinch fallback。上述证据只能证明测试脚本可执行，不能证明 Meta XR Simulator → OpenXR/Meta Interaction SDK → gaze + hand pinch 的真实输入链路；④ P0 状态退回 **Runtime Validation Pending**；⑤ Simulator 可由鼠标/键盘驱动 Look and Pinch，但应用层不得直接读取鼠标/键盘替代 XR input。
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


### 5.6 P1 Coin Presence / Coin Play（正式设计原则）

> P1 不再把目标定义为“做一枚真实硬币模型”，而是：**设计一件用户在现实空间里会愿意反复把玩的数字随身小物。**

#### 产品假设

- **Fidget-first**：把玩本身可能是高频留存来源；Heads/Tails 决策是这件随身小物自然具备的 Utility 分支。
- 该判断当前是**产品假设**，必须通过原型验证，不作为已证实用户事实。
- Toss 不做传统游戏化留存：不依赖积分、连击、签到、等级、皮肤解锁等系统。

#### P1 核心原则

1. **Quarter-inspired, not quarter-replica**
   - quarter 只承担美国用户第一眼“coin / flip a coin”的熟悉感锚点。
   - 不 1:1 复刻真实法币，不复制真实人物头像，不放真实货币文字。
   - 视觉优先级：**可读性 ≥ 亲和感 > 写实度**。

2. **Dual-state Character，不做显式 Mode**
   - 低能量把玩时：轻松、顽皮、安静、随手。
   - 真正 Toss 时：动作和声音自然收敛，短暂获得更稳重的仪式感。
   - 用户不应看到“Fidget Mode / Decision Mode”切换；性格变化通过运动、节奏和声音连续发生。

3. **Invisible Generosity**
   - 系统应持续帮助用户得到更顺手、更成功的体验，但辅助必须不可见。
   - 允许温和修正轨迹、交互窗口、settle 与后续 catch/recovery，但禁止让用户看见“自动瞄准/磁吸拐弯”。
   - 目标是让用户觉得“我今天手感很好”，而不是“系统替我完成了”。

4. **Low-attention Play**
   - Fidget loop 应尽量允许用户把注意力留给现实环境，而不是要求持续盯住硬币。
   - “完全 eyes-free”属于实验目标，不是当前硬性能力承诺。
   - 召唤/把玩/收起均应优先考虑单手、低幅度、公共场景可接受。

5. **Occlusion as Magic**
   - 手掌、指缝、身体遮挡等不可见瞬间，可作为姿态修正、重新绑定、recover、切换 attachment 的自然窗口。
   - 原则：**Never waste an occlusion.**
   - 重新出现时必须保持视觉连续，不能暴露系统作弊。

6. **Physical enough to believe. Designed enough to enjoy.**
   - 重力、抛物线、角动量观感等核心物理直觉应可信。
   - timing、轨迹约束、settle、声音、高光、辅助量允许经过导演。
   - 金属硬币本体保持刚性：**禁止通过 squash/stretch 伪造重量。**
   - 不通过篡改用户真实手/绘制冲突虚拟手的方式制造“手被砸沉”的重量错觉。

#### Coin Presence 设计维度

P1 必须同时考虑以下元素，而不只看静态模型：

- **形**：quarter-inspired、略放大/加厚、粗齿边、强剪影、Heads/Tails 大形区分。
- **材质**：银色、缎面/拉丝、中等 roughness；不镜面、不主动发光。
- **运动人格**：按“运动中的硬币”设计，尤其关注 edge → face → edge 的翻转周期、边缘高光和 settle。
- **声音人格**：温润、近距离、低打扰；声音既提供材质感，也参与伪触觉和节奏。
- **Tempo**：重复把玩必须能形成稳定节拍；目标不是“音效丰富”，而是“能不能被用户玩成自己的节奏”。
- **环境适应**：亮暗/色温/背景可读性需要验证；实时环境光采样、桌面/Scene/Depth 等能力不得在未验证前写成依赖。
- **关系感**：允许探索无 UI 的轻微 patina / 使用痕迹，让硬币“跟用户一起变老”，但不做进度条或成长系统。

#### P1 首轮三个核心实验

1. **Finger Play**
   - 验证拇指拨、翻、捻、掌心把玩等低能量循环是否本身就足够 satisfying。
   - 重点观察：节奏、惯性、edge highlight、声音同步、可重复性。
   - **验收信号**：用户在无提示情况下是否会主动重复把玩、是否能自然形成自己的节奏；看行为，不只问主观评分。

2. **Perceived Weight**
   - 对比 rigid 跟随、spring-damper、不同 angular inertia、settle、有声/无声等方案。
   - 必须包含 **有声 / 无声 A/B**。
   - 问题不是“哪个最真实”，而是：**哪个最像手里真的有个东西**。
   - 所有 lag / damping 数值均为待实验参数，不冻结具体毫秒数。

3. **Invisible Generosity**
   - 对比纯物理、微辅助、明显辅助，寻找“系统已经帮了很多，但用户没有察觉”的边界。
   - **验收信号**：记录辅助开始被用户察觉的阈值；一旦明显感觉“系统把硬币吸过去/替我完成”，即视为越界。
   - 正式 Toss/Catch 逻辑仍属于后续 P2/P4；P1 只做原理验证，不扩张状态机。

#### Coin Relief Rendering Pipeline（P1 正式路线）

- **Geometry for form, Normal/AO for micro-detail.**
- 必须使用真实几何表达：Coin 主体厚度、rim、reeded edge、Heads/Tails 的主要 bas-relief 体积与会影响斜角观感的曲面。
- Normal Map / AO 只承担微细节：发丝、浅刻纹、细微拉丝、沟槽阴影等；不得再把它们描述成真实几何浮雕。
- URP Lit Height Map 只作为浅层 parallax 备选，不承担 Hero Coin 的主要 relief。
- Shader Graph vertex displacement 可做实验，但前提是 mesh 本身有足够顶点密度；当前不作为主资产生产线。
- 主资产路线：**DCC sculpt/model → FBX → Unity Import Normals/Tangents → URP Lit PBR**。
- Unity 的职责是实时渲染、材质、光照、动画与交互；不把 Unity/C# procedural heightfield 当成主要雕塑工具。
- 具体 triangle budget 不预设拍脑袋数字；待 Meta VR Glasses 真机/目标设备 profiling 决定。

#### Capability-dependent Extension

- **Euler Disk / 真桌面旋转 + 死亡摇摆**被保留为高潜力 Signature Extension。
- Scene/Depth/Spatial Mesh/桌面碰撞的实际稳定性必须先验证，不能成为 Competition Build 核心依赖。
- 环境光估计、真实桌面锚定、长期 Spatial Anchor 等能力在确认前均保持可选实验，不得提前写死到主流程。

#### Summon / Dismiss 体验原则（后续阶段）

- 召唤 / 收起属于用户与 Toss Coin 的核心关系设计，目标是 **<1s、单手、低幅度、低注意力、公共场景可接受、无传统 UI**。
- 当前不冻结具体主手势。Palm-up、Snap、身体“口袋位”等均为候选，必须以 Meta 实测稳定性和误触率决定。
- 识别失败时静默处理，不弹错误 UI，不播放 fail buzz，不把 tracking 失败变成用户的“错误”。
- 首次召唤可探索一次性轻仪式，但具体时长与表现必须经体验验证，不提前写死。

#### Failure / Sound / Delight 边界

- **Failure attitude**：P1/P2 不设计传统失败态。手势丢失、未识别或动作中断时，硬币应保持稳定、自然 settle / wait / recover。
- **Sound family**：声音应来自统一的温润金属声学家族，近距离、低打扰；后续允许形成极短 acoustic signature，但不以高穿透“金币叮”制造存在感。
- **Micro-delight**：允许偶发、物理或手部动作驱动的小惊喜；不计数、不通知、不形成任务/成就系统；gaze 仍只表示 attention，禁止 gaze 单独触发彩蛋或状态变化。

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
- **P1 Coin Presence / Coin Play**：按 §5.6 建立 quarter-inspired Toss Coin 的视觉/材质/声音/运动人格，并完成 Finger Play、Perceived Weight、Invisible Generosity 三类最小体验实验；P1 不进入正式 Toss 状态机、RNG、Catch/Recovery 或结果逻辑。
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
| D-020 | 2026-09-26 | P0 gaze/pinch 证据必须来自 **Meta XR Simulator → OpenXR/Meta Interaction SDK 的真实输入状态**；禁止应用层鼠标/键盘 fallback、禁止直接写测试 bool、禁止用 Camera forward Raycast 冒充 eye gaze | Active |
| D-021 | 2026-09-26 | `910f898` 的 synthetic evidence 清理被接受，但其 `OVREyeGaze` + `OVRHand` 输入架构经真实 GUI 验证失败；该架构不再视为 P0 合格实现 | Superseded |
| D-022 | 2026-09-26 | P0 的 VR Glasses Look-and-Pinch 验证改用 **Meta XR Interaction SDK v207 Gaze Interaction**（或官方 GazeExamples 等价标准路径），不再以 `OVREyeGaze` 作为主 gaze 路径 | Active |
| D-023 | 2026-09-26 | P0 输入验证不要求 Synthetic Environment；在本机 `synth_env_server` 异常占用约 17GB RAM 时保持其关闭，仅在后续确需 passthrough/Scene/Anchors/Depth 联调时再启用 | Active |
| D-024 | 2026-09-27 | `17b20a1` 被接受为 P0 Rig 修复候选：根因是 `OVRCameraRigRef`/tracking 引用缺失；修复后静态审计清零，但仍需真实 GUI 输入复测后才能判 PASS | Active |
| D-025 | 2026-09-27 | 项目不得把“使用官方 prefab/sample + 手工 SerializedObject wiring”描述为“纯官方 Quick Action / 无手工装配”；文档与报告必须区分二者 | Active |
| D-026 | 2026-09-27 | 真实 GUI 已证明 Gaze Hover + Pinch Select 核心链可用；后续 P0 blocker 从“输入链未通”收敛为 OpenXR profile 清理与 clean-session stability | Active |
| D-027 | 2026-09-27 | Meta VR Glasses Look-and-Pinch 必须使用 `XR_EXT_hand_interaction` / `/interaction_profiles/ext/hand_interaction_ext`；不得启用或依赖 Microsoft Hand Interaction Profile | Active |
| D-028 | 2026-09-27 | 禁止通过 feature 名称模糊匹配批量启用 OpenXR feature；P0 以后只允许显式 allowlist/featureId 配置 | Active |
| D-029 | 2026-09-27 | `272928e` 的 Microsoft profile 资产清理被接受，但其 Configurator 不得把“未列入 allowlist”解释为“必须禁用”；未知/无关 feature 默认保持现状，不主动改写 | Active |
| D-030 | 2026-09-27 | OpenXR feature mutation 只允许 exact type / exact featureId；禁止 `Contains("Microsoft")` 等模糊 block/allow 规则 | Active |
| D-031 | 2026-09-27 | `7c86e6d` 接受为 P0 OpenXR mutation 最小修复：未知/无关 feature 保持原状态，仅 exact block/required feature 可被修改 | Active |
| D-032 | 2026-09-27 | clean-session 首轮已通过：真实 Gaze/Pinch/Look-and-Pinch 事件、0 红色错误、Microsoft hand profile 错误消失；仅剩 Stop→Play 重入稳定性复测 | Superseded by D-033 |
| D-033 | 2026-09-27 | P0 Environment 正式 PASS：clean-session + Stop→Play 重入均通过，真实 Gaze/Pinch/Look-and-Pinch 事件可重复，Console 0 红色 Error | Active |
| D-034 | 2026-09-27 | P1 从单纯 Coin Asset 升级为 **Coin Presence / Coin Play**；Fidget-first 作为待验证产品假设，Decision Utility 作为自然分支 | Active |
| D-035 | 2026-09-27 | 冻结 P1 交互哲学：**Invisible Generosity / Low-attention Play / Occlusion as Magic / Physical enough to believe, Designed enough to enjoy** | Active |
| D-036 | 2026-09-27 | P1 重量感不得依赖硬币 squash/stretch 或篡改用户真实手视觉；优先通过硬币自身惯性、timing、遮挡、声音与 settle 建立 pseudo-haptics | Active |
| D-037 | 2026-09-27 | P1 首轮只验证 Finger Play、Perceived Weight、Invisible Generosity 三类最小实验；Euler Disk/真实桌面交互为 capability-dependent extension | Active |
| D-038 | 2026-09-27 | Summon/Dismiss 必须作为后续 Coin relationship 设计项，但当前只冻结 <1s/单手/低幅度/无 UI 等体验原则，不提前指定 Snap 或 Palm-up 为主手势 | Active |
| D-039 | 2026-09-27 | P1 失败处理坚持无传统失败态：tracking/gesture 失败不弹 UI、不 fail buzz，采用稳定等待/settle/recover | Active |
| D-040 | 2026-09-27 | `a4e1dfe` 仅作为 **P1.1 PASS candidate**；在人工视觉验收完成前不得宣布 P1.1 PASS；深浅 backdrop 不等同于真实 passthrough 验证，normal-map relief 不得描述为真实几何深度 | Active |
| D-041 | 2026-09-27 | P1 Hero Coin relief 正式采用 **Geometry for form, Normal/AO for micro-detail**；DCC 真实 bas-relief Mesh → FBX → Unity PBR 为主路线，程序化 normal/heightfield 不再承担主要雕塑体积 | Active |
| D-042 | 2026-09-27 | `087cbb4` 仅证明 Heads/Tails 大形可读性提高，不代表高级雕塑质感已达标；P1.1 视觉验收继续未通过，禁止基于当前浮雕继续进入 P1.2 | Active |

---

## 12. 当前下一步

**当前阶段：P1 Coin。**

### 12.1 P0 最终结论

```text
P0 = PASS
```

验收证据：

- Meta XR Simulator v207 正常运行。
- Device = Meta VR Glasses。
- Left / Right = Look and Pinch。
- Synthetic Environment OFF 仍可完成 P0。
- clean-session 首轮通过。
- Stop → Play 重入通过。
- Console 红色 Error = 0。
- `GAZE_ENTER` / `GAZE_EXIT` 可重复。
- `PINCH_START` / `PINCH_END` 可重复。
- `LOOK_AND_PINCH_TRIGGERED` 可重复。
- Target Gold / Cyan 反馈稳定。
- 不再出现 Microsoft hand profile `XR_ERROR_HANDLE_INVALID`。
- 不再出现 NullReferenceException / MissingReferenceException。

Windows 主机注记：

- Smart App Control 保持开启。
- 卸载 KB5124010 并重启后，之前对 `MetaXRSimulator.exe` / `ISDKEngineTelemetry.dll` 的 Code Integrity 阻止未复现。
- 该现象只记录为本机 A/B 相关性，不视为微软官方确认的 KB 因果问题。

### 12.2 P1 Coin Presence / Coin Play 允许开始

P1 的目标不再只是“把硬币放进场景”，而是按 §5.6 验证：

- 这枚硬币是否一眼像 coin，但明显属于 Toss 自己。
- 它在静止、翻转、边缘朝向、低能量把玩时是否始终有存在感。
- 没有真实触觉时，惯性、声音、时序、遮挡与 settle 能否制造“手里有个东西”的感觉。
- 用户是否愿意无目标地重复玩它，而不只是为了得到 Heads/Tails。
- 系统辅助能否做到慷慨但隐形。

P1 首轮实现 / 原型范围：

1. Coin Presence：形、材质、比例、边缘、Heads/Tails 大剪影、基础空间位置；主要 relief 必须按 §5.6 的 Coin Relief Rendering Pipeline 使用真实几何资产路线。
2. Motion Readability：静态 + 360° flip/roll/rotation 观察。
3. Finger Play 最小原型。
4. Perceived Weight A/B 原型。
5. Invisible Generosity 原理实验（不进入完整 Toss/Catch 状态机）。
6. 声音与 tempo 可作为上述实验的一部分，但不建设完整音频系统。

P1 仍明确不进入：

- 正式 Toss 状态机。
- RNG。
- Heads/Tails 结果结算。
- 完整 Catch / Recovery。
- Decision Mode。
- 游戏化进度系统。
- 依赖未验证 Scene/Depth/Spatial Mesh 的主流程。

P1 必须继续保持：

- P0 XR 输入基线冻结，不破坏已通过链路。
- hands-first / controller-free。
- Zero-UI。
- Comfort Envelope。
- 所有体验关键参数可调，不把未经实验的毫秒、角度、半径等数字写成硬规则。

