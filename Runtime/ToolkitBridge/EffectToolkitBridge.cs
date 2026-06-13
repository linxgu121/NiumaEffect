using System;
using System.Collections.Generic;
using NiumaEffect.Enum;
using NiumaEffect.ViewData;
using NiumaUI.Toolkit;
using NiumaUI.Toolkit.Common;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace NiumaEffect.Bridge
{
    public sealed class EffectToolkitReceiver : MonoBehaviour, IEffectUIReceiver
    {
        [SerializeField, Tooltip("拖核心场景 UIRoot/UIManager 上的 UIToolkitUIManager。")]
        private UIToolkitUIManager uiManager;
        [SerializeField, Tooltip("效果面板 ViewId。默认 EffectPanel，需要在 UIToolkitViewRegistrySO 注册。")]
        private string effectViewId = "EffectPanel";
        [SerializeField, Tooltip("刷新失败时是否自动打开效果面板。")]
        private bool autoOpenView = true;
        [SerializeField, Tooltip("收到 Cleared 更新时是否关闭效果面板。关闭后会立即返回，不再重新打开。")]
        private bool closeOnCleared = true;
        [SerializeField, Tooltip("缺少 UIManager 或 View 时是否输出警告。")]
        private bool logWarnings = true;

        public void ApplyEffectUpdate(EffectUIUpdate update)
        {
            if (update.UpdateType == EffectUIUpdateType.Cleared && closeOnCleared && uiManager != null)
            {
                uiManager.CloseView(effectViewId);
                return;
            }

            if (!EnsureUIManager())
                return;

            var refreshed = uiManager.RefreshView(effectViewId, update);
            if (!refreshed && autoOpenView)
                refreshed = uiManager.OpenView(effectViewId, update);

            if (!refreshed)
                Warn($"没有刷新到效果 Toolkit View：ViewId={effectViewId}。请检查 Registry 和 BindingProvider。");
        }

        private bool EnsureUIManager()
        {
            if (uiManager == null)
                uiManager = FindSceneObject<UIToolkitUIManager>();

            if (uiManager != null)
                return true;

            Warn("未绑定 UIToolkitUIManager，效果 Toolkit 面板无法刷新。");
            return false;
        }

        private void Warn(string message)
        {
            if (logWarnings && !string.IsNullOrWhiteSpace(message))
                UnityEngine.Debug.LogWarning($"[EffectToolkitReceiver] {message}", this);
        }

        private static T FindSceneObject<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<T>();
#else
            return FindObjectOfType<T>();
#endif
        }
    }

    public sealed class EffectToolkitBindingProvider : ToolkitViewBindingProviderBase
    {
        [Serializable] public sealed class EffectInstanceIdEvent : UnityEvent<string> { }

        [Header("元素名称")]
        [SerializeField, Tooltip("标题 Label 的 name。默认 TitleText。")]
        private string titleLabelName = "TitleText";
        [SerializeField, Tooltip("状态 Label 的 name。默认 StatusText。")]
        private string statusLabelName = "StatusText";
        [SerializeField, Tooltip("效果列表 ListView 的 name。默认 ListRoot。")]
        private string listViewName = "ListRoot";
        [SerializeField, Tooltip("详情 Label 的 name。用于显示当前选中效果。")]
        private string detailLabelName = "DetailText";
        [SerializeField, Tooltip("结果 Label 的 name。效果面板通常留空。")]
        private string resultLabelName = "ResultText";
        [SerializeField, Tooltip("空状态节点的 name。没有效果时显示。")]
        private string emptyRootName = "EmptyRoot";

        [Header("按钮名称")]
        [SerializeField, Tooltip("驱散按钮的 name。点击时把当前选中的 EffectInstanceId 传给 On Dispel Requested。")]
        private string dispelButtonName = "DispelButton";

        [Header("列表")]
        [SerializeField, Tooltip("最多显示多少行。")]
        private int maxRows = 60;
        [SerializeField, Tooltip("列表行 USS class。")]
        private string rowClass = "niuma-effect-row";
        [SerializeField, Tooltip("选中行 USS class。")]
        private string selectedRowClass = "is-selected";
        [SerializeField, Tooltip("禁用行 USS class。")]
        private string disabledRowClass = "is-disabled";

        [Header("交互事件")]
        [SerializeField, Tooltip("点击效果行时触发，参数为 EffectInstanceId。")]
        private EffectInstanceIdEvent onEffectSelected = new EffectInstanceIdEvent();
        [SerializeField, Tooltip("点击 DispelButton 时触发，参数为当前选中的 EffectInstanceId。请绑定到效果驱散命令入口。")]
        private EffectInstanceIdEvent onDispelRequested = new EffectInstanceIdEvent();

        protected override string DefaultProviderId => "EffectPanel";

        public override IToolkitViewBinding CreateBinding()
        {
            return new EffectToolkitBinding(
                titleLabelName,
                statusLabelName,
                listViewName,
                detailLabelName,
                resultLabelName,
                emptyRootName,
                dispelButtonName,
                maxRows,
                rowClass,
                selectedRowClass,
                disabledRowClass,
                id => onEffectSelected?.Invoke(id),
                id => onDispelRequested?.Invoke(id));
        }
    }

    public sealed class EffectToolkitViewModel : UIPanelViewModelBase
    {
        public readonly List<ToolkitTextRowData> Rows = new List<ToolkitTextRowData>();
        public EffectPanelViewData Panel { get; private set; }
        public EffectUIUpdateType UpdateType { get; private set; }
        public long Revision { get; private set; }
        public string SelectedEffectInstanceId { get; private set; }
        public EffectIconViewData SelectedEffect { get; private set; }

        public void Apply(EffectUIUpdate update, int maxRows)
        {
            Panel = update.Current;
            UpdateType = update.UpdateType;
            Revision = update.Revision;
            SetContext(Panel?.ActorId);
            SelectedEffectInstanceId = NormalizeSelection(Panel, SelectedEffectInstanceId);
            RebuildRows(maxRows);
            MarkDirty();
        }

        public void Select(string effectInstanceId)
        {
            SelectedEffectInstanceId = string.IsNullOrWhiteSpace(effectInstanceId) ? null : effectInstanceId.Trim();
            RebuildRows(int.MaxValue);
            MarkDirty();
        }

        protected override void OnClear(UIViewModelClearReason reason)
        {
            Panel = null;
            UpdateType = EffectUIUpdateType.Cleared;
            Revision = 0;
            SelectedEffectInstanceId = null;
            SelectedEffect = null;
            Rows.Clear();
        }

        private void RebuildRows(int maxRows)
        {
            Rows.Clear();
            SelectedEffect = null;
            var effects = Panel?.Effects ?? Array.Empty<EffectIconViewData>();
            var rowsLeft = Math.Max(1, maxRows);
            for (var i = 0; i < effects.Length && rowsLeft > 0; i++)
            {
                var effect = effects[i];
                if (effect == null)
                    continue;

                var id = string.IsNullOrWhiteSpace(effect.EffectInstanceId) ? effect.EffectId : effect.EffectInstanceId.Trim();
                var isSelected = string.Equals(SelectedEffectInstanceId, id, StringComparison.Ordinal);
                if (isSelected)
                    SelectedEffect = effect;

                var time = effect.IsPermanent ? "永久" : $"{effect.RemainingSeconds:0.0}/{effect.DurationSeconds:0.0}s";
                var dispel = effect.IsDispellable ? " | 可驱散" : " | 不可驱散";
                var missing = effect.IsMissingDefinition ? " | 缺失定义" : string.Empty;
                Rows.Add(new ToolkitTextRowData(id, $"{Text(effect.DisplayName, effect.EffectId)} x{effect.StackCount} | {effect.Type}/{effect.Polarity} | {time}{dispel}{missing}", isSelected, !effect.IsMissingDefinition, effect));
                rowsLeft--;
            }
        }

        private static string NormalizeSelection(EffectPanelViewData panel, string previous)
        {
            var effects = panel?.Effects ?? Array.Empty<EffectIconViewData>();
            if (!string.IsNullOrWhiteSpace(previous))
            {
                for (var i = 0; i < effects.Length; i++)
                {
                    var id = effects[i]?.EffectInstanceId;
                    if (string.Equals(id, previous, StringComparison.Ordinal))
                        return previous.Trim();
                }
            }

            return effects.Length > 0 ? effects[0]?.EffectInstanceId : null;
        }

        private static string Text(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
        }
    }

    public sealed class EffectToolkitBinding : ToolkitViewBindingBase<EffectUIUpdate, EffectToolkitViewModel>
    {
        private readonly string _titleName;
        private readonly string _statusName;
        private readonly string _listName;
        private readonly string _detailName;
        private readonly string _resultName;
        private readonly string _emptyName;
        private readonly string _dispelButtonName;
        private readonly int _maxRows;
        private readonly string _rowClass;
        private readonly string _selectedClass;
        private readonly string _disabledClass;
        private readonly Action<string> _effectSelected;
        private readonly Action<string> _dispelRequested;
        private readonly ToolkitListBinding<ToolkitTextRowData> _listBinding = new ToolkitListBinding<ToolkitTextRowData>();
        private Label _title;
        private Label _status;
        private Label _detail;
        private Label _result;

        public EffectToolkitBinding(
            string titleName,
            string statusName,
            string listName,
            string detailName,
            string resultName,
            string emptyName,
            string dispelButtonName,
            int maxRows,
            string rowClass,
            string selectedClass,
            string disabledClass,
            Action<string> effectSelected,
            Action<string> dispelRequested)
        {
            _titleName = titleName;
            _statusName = statusName;
            _listName = listName;
            _detailName = detailName;
            _resultName = resultName;
            _emptyName = emptyName;
            _dispelButtonName = dispelButtonName;
            _maxRows = Mathf.Max(1, maxRows);
            _rowClass = string.IsNullOrWhiteSpace(rowClass) ? "niuma-effect-row" : rowClass.Trim();
            _selectedClass = selectedClass;
            _disabledClass = disabledClass;
            _effectSelected = effectSelected;
            _dispelRequested = dispelRequested;
        }

        protected override void OnInitializeTyped()
        {
            _title = QLabel(_titleName);
            _status = QLabel(_statusName);
            _detail = QLabel(_detailName);
            _result = QLabel(_resultName);
            _listBinding.Bind(Root, _listName, new ToolkitTextRowItemBinder(_rowClass, _selectedClass, _disabledClass, HandleRowClicked), _emptyName);
            Callbacks.RegisterButton(Root, _dispelButtonName, () => InvokeSelected(_dispelRequested), CanDispelSelected);
            ApplyVisualState(ViewModel);
        }

        protected override void OnRefreshTyped(EffectUIUpdate viewData, EffectToolkitViewModel viewModel)
        {
            viewModel.Apply(viewData, _maxRows);
            ApplyVisualState(viewModel);
        }

        protected override void OnClearTyped(UIViewModelClearReason reason)
        {
            _listBinding.Clear();
            ApplyVisualState(ViewModel);
        }

        protected override void OnDisposeTyped()
        {
            _listBinding.Dispose();
        }

        private void HandleRowClicked(ToolkitTextRowData row)
        {
            if (row == null)
                return;

            ViewModel.Select(row.Id);
            _effectSelected?.Invoke(row.Id);
            ApplyVisualState(ViewModel);
        }

        private bool CanDispelSelected()
        {
            return ViewModel?.SelectedEffect != null && ViewModel.SelectedEffect.IsDispellable;
        }

        private void InvokeSelected(Action<string> callback)
        {
            if (callback == null || string.IsNullOrWhiteSpace(ViewModel?.SelectedEffectInstanceId))
                return;

            callback.Invoke(ViewModel.SelectedEffectInstanceId);
        }

        private void ApplyVisualState(EffectToolkitViewModel viewModel)
        {
            var panel = viewModel?.Panel;
            SetText(_title, "效果");
            SetText(_status, panel == null
                ? $"状态：{viewModel?.UpdateType ?? EffectUIUpdateType.Cleared}"
                : $"Actor {Text(panel.ActorId, "未知")} | Revision {panel.Revision} | 效果 {panel.Effects?.Length ?? 0}");
            SetText(_detail, viewModel?.SelectedEffect != null ? Detail(viewModel.SelectedEffect) : panel == null ? "暂无效果数据。" : "未选择效果。");
            SetText(_result, string.Empty);
            _listBinding.ReplaceAll(viewModel?.Rows ?? new List<ToolkitTextRowData>());
        }

        private static string Detail(EffectIconViewData effect)
        {
            if (effect == null)
                return "未选择效果。";

            var time = effect.IsPermanent ? "永久" : $"{effect.RemainingSeconds:0.0}/{effect.DurationSeconds:0.0}s";
            return $"选中：{Text(effect.DisplayName, effect.EffectId)}\n实例：{effect.EffectInstanceId}\n类型：{effect.Type}/{effect.Polarity}\n层数：{effect.StackCount}\n剩余：{time}\n说明：{effect.Description}".Trim();
        }

        private static string Text(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
        }
    }
}