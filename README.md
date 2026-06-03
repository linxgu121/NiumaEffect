# NiumaEffect

## 模块定位
NiumaEffect 是效果实例与生命周期模块，负责 Buff/Debuff 创建、持续时间、叠层、过期、驱散、免疫、存档和效果 UI 数据，并通过 NiumaAttribute 添加/移除 Modifier。

## 框架设计思路
- EffectDefinition 描述效果配置，ActiveEffectRuntimeState 保存运行中实例。
- 效果不直接改最终属性，只把 Modifier 应用到 AttributeService。
- 到期、驱散、覆盖、清空来源时必须同步清理已应用 Modifier。
- 元素附着、元素反应、DOT/HOT 结算留给后续 Effect V2 / Combat。

## 核心流程
1. EffectController 注入 AttributeCommand、效果定义和免疫/应用解析器。
2. ApplyEffect 校验目标、定义、免疫和叠层规则。
3. 创建或刷新 ActiveEffectRuntimeState。
4. 将 Modifier 模板转换为 AttributeModifier 并应用。
5. Tick 扣减持续时间，到期后移除效果和 Modifier。
6. Dispel 按标签或规则移除可驱散效果。
7. SaveAdapter 保存运行中效果并在导入后重应用 Modifier。

## 模块用法
- EffectId、EffectInstanceId、OwnerActorId、SourceActorId 要区分清楚。
- 需要属性加成的效果必须配置 Modifier 模板。
- 不可驱散、免疫、标签查询通过配置和 Resolver 扩展。

## 场景使用方法
推荐放置方式：`EffectRoot` 一个效果根物体承载 Buff/Debuff 服务、UI 桥接和存档。

- `EffectRoot`：挂 `NiumaEffectController`，绑定 EffectDefinition 列表、AttributeCommand、ApplicationResolver、ImmunityResolver。
- `EffectRoot/SaveAdapter` 或全局 `SaveRoot/Providers`：挂 `NiumaEffectSaveAdapter`。
- `EffectRoot/UIBridge` 或 `UIRoot/Bridges`：挂 `EffectUIViewBridge`，绑定 Buff 图标 UI Receiver。
- `EffectRoot/Debug`：开发阶段挂 `EffectBasicTestRunner`。
- `SkillRoot`、`ItemUseRoot`、`StoryRoot`：需要施加 Buff 时调用 EffectController.ApplyEffect，不要自己维护 Buff 计时。
- `UIRoot/EffectPanel`：放状态图标、层数、剩余时间、可驱散标记。
- 场景中若有净化点、毒雾区、祝福区，建议各自挂交互/触发脚本，再调用 EffectService，而不是把区域逻辑塞到 EffectController。

## 协作边界
Effect 管“状态存在多久、叠几层、是否清理”。伤害、治疗、死亡、元素反应由 NiumaCombat 或后续效果扩展处理。


